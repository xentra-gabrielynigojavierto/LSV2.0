using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace BuildingBlocks.Tests;

/// <summary>
/// CI guard for Task #66 / Task #71.
///
/// Task #62/#65 introduced <see cref="BuildingBlocks.Diagnostics.MigrationCoverageProbe"/>,
/// the boot-time self-test that compares every EF-mapped table/column against
/// the live database schema and screams if a migration was committed without
/// its [Migration] attribute (the Task #58 regression class).
///
/// The probe is only useful if every .NET service actually wires it into
/// startup. Without this guard, a service could silently drop the
/// <c>MigrationCoverageProbe.RunAsync(...)</c> call and we'd only find out
/// in production.
///
/// Task #70 / Task #71 retired the old hand-maintained Services list and
/// replaced it with two complementary guards:
///
/// 1. <see cref="EveryDiscoveredMigrationService_WiresMigrationCoverageProbeInProgram"/>
///    (auto-discovery) — covers any service that has a <c>Migrations/</c>
///    folder under <c>apps/services/</c>. New services are enrolled
///    automatically on their first migration commit.
///
/// 2. <see cref="EveryExplicitService_WiresMigrationCoverageProbeInProgram"/>
///    (explicit list) — a small, intentionally short list of services that own
///    a DbContext but have no <c>Migrations/</c> folder (e.g. because they
///    use a shared schema or delegate migrations elsewhere). These services
///    must be listed in <see cref="ExplicitServices"/> to remain covered.
///
/// Failing either assertion fails the build.
/// </summary>
public class ServiceMigrationProbeWiringTests
{
    private readonly ITestOutputHelper _output;

    public ServiceMigrationProbeWiringTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // -----------------------------------------------------------------------
    // Explicit list — services that own a DbContext but intentionally have
    // no Migrations/ folder and are therefore not caught by auto-discovery.
    //
    // Keep this list as short as possible. Every entry here represents a
    // service that chose NOT to own its own migrations (e.g. reads a shared
    // schema, receives an already-migrated DB from another service, etc.).
    // Add a comment explaining WHY the service has no Migrations folder.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Services that own a DbContext and must wire
    /// <c>MigrationCoverageProbe.RunAsync</c>, but intentionally have no
    /// <c>Migrations/</c> directory and therefore cannot be covered by the
    /// auto-discovery test. Each entry is <c>(name, repo-relative path to
    /// Program.cs)</c>.
    /// </summary>
    public static readonly (string Name, string ProgramRelPath)[] ExplicitServices =
    {
        // notifications — uses EF Core read-only projections against a schema
        // owned and migrated by another service. No Migrations/ folder exists.
        ("notifications", "apps/services/notifications/Notifications.Api/Program.cs"),
    };

    public static IEnumerable<object[]> ExplicitServiceCases =>
        ExplicitServices.Select(s => new object[] { s.Name, s.ProgramRelPath });

    private static readonly Regex MigrationClassPattern =
        new(@":\s*Migration\b", RegexOptions.CultureInvariant);

    private static readonly Regex MigrationAttributePattern =
        new(@"\[Migration\s*\(", RegexOptions.CultureInvariant);

    private static readonly Regex ProbeCallPattern =
        new(@"MigrationCoverageProbe\s*\.\s*RunAsync\s*\(", RegexOptions.CultureInvariant);

    // -----------------------------------------------------------------------
    // Test 1 — auto-discovery (covers all services with a Migrations/ folder)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Auto-discovery guard for Task #70 / Task #71.
    ///
    /// For every service directory under <c>apps/services/</c> that contains
    /// a <c>Migrations/</c> folder, this test resolves the service root (the
    /// first path segment beneath <c>apps/services/</c>), locates the
    /// canonical API <c>Program.cs</c> via <see cref="FindApiProgramCs"/>, and
    /// asserts that it calls <c>MigrationCoverageProbe.RunAsync(</c>.
    ///
    /// Services with multiple migration directories (e.g. sharded contexts) are
    /// deduplicated by resolved <c>Program.cs</c> path so each entry point is
    /// verified exactly once. No manual update is required when a new service
    /// adds its first migration.
    ///
    /// <b>Opt-out:</b> A service that legitimately does not use
    /// <c>MigrationCoverageProbe</c> (e.g. a background worker that runs
    /// migrations but delegates boot-time validation elsewhere) may place a
    /// <c>.probe-wiring-excluded</c> file in its service root directory. The
    /// file <b>must be non-empty</b> and contain a brief explanation of why
    /// the probe is not wired; an empty marker fails the test. This ensures
    /// the exclusion is commit-reviewed and not a silent skip. Services with
    /// a valid marker file are logged via the test output helper (always
    /// visible in CI) but are not counted as failures.
    /// </summary>
    [Fact]
    public void EveryDiscoveredMigrationService_WiresMigrationCoverageProbeInProgram()
    {
        var repoRoot = FindRepoRoot();
        var servicesRoot = Path.GetFullPath(Path.Combine(repoRoot, "apps", "services"));

        var noProgramFound = new List<string>();
        var probeNotWired  = new List<string>();
        var optedOut       = new List<string>();

        // Track already-checked Program.cs paths so a service with several
        // migration folders (sharded contexts, etc.) is only verified once.
        var checkedPrograms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Track service roots that have already been opt-out-checked so that
        // a service with multiple Migrations/ directories is only recorded once.
        var optedOutServiceRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relMigrationDir in DiscoverMigrationDirs(repoRoot))
        {
            // Resolve the service root: the first directory beneath apps/services/.
            // e.g. "apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations"
            //   →  "<repoRoot>/apps/services/careconnect"
            var parts = relMigrationDir.Split('/');
            if (parts.Length < 3)
                continue; // unexpected structure — "apps/services/<name>/..."

            var serviceRoot = Path.GetFullPath(
                Path.Combine(servicesRoot, parts[2]));

            // Check for opt-out marker. A service places a .probe-wiring-excluded
            // file in its root when it legitimately has a Migrations/ folder but
            // does not wire MigrationCoverageProbe (e.g. a background worker that
            // delegates boot-time validation elsewhere). The file must be non-empty
            // and contain a brief justification so the exclusion is commit-reviewed
            // rather than a silent skip.
            var markerPath = Path.Combine(serviceRoot, ".probe-wiring-excluded");
            if (File.Exists(markerPath))
            {
                var markerContent = File.ReadAllText(markerPath).Trim();
                Assert.True(markerContent.Length > 0,
                    $"The opt-out marker '{markerPath}' exists but is empty. " +
                    "Add a brief explanation of why MigrationCoverageProbe is intentionally " +
                    "absent so that the exclusion is commit-reviewed and not a silent skip.");

                if (optedOutServiceRoots.Add(serviceRoot))
                {
                    var relServiceRoot = Path.GetRelativePath(repoRoot, serviceRoot)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    optedOut.Add(relServiceRoot);
                }
                continue;
            }

            var programPath = FindApiProgramCs(serviceRoot);

            if (programPath is null)
            {
                noProgramFound.Add(relMigrationDir);
                continue;
            }

            if (!checkedPrograms.Add(programPath))
                continue; // already verified for a sibling migration directory

            var source = File.ReadAllText(programPath);
            if (!ProbeCallPattern.IsMatch(source))
            {
                var relProgram = Path.GetRelativePath(repoRoot, programPath)
                    .Replace(Path.DirectorySeparatorChar, '/');
                probeNotWired.Add(relProgram);
            }
        }

        // Always surface opted-out services in CI output, even when the test
        // passes. This ensures exemptions are visible in every test run and
        // are not silently hidden behind a green build.
        if (optedOut.Count > 0)
        {
            _output.WriteLine(
                "[probe-wiring-excluded] The following service(s) have opted out of the " +
                "MigrationCoverageProbe wiring check via a .probe-wiring-excluded marker file. " +
                "Each exclusion must be justified inside its marker file:\n\n" +
                string.Join("\n", optedOut.Select(s => "  " + s)));
        }

        var messages = new List<string>();

        if (noProgramFound.Count > 0)
            messages.Add(
                "Could not find a Program.cs for the following auto-discovered migration " +
                "directories. FindApiProgramCs searches the service root for a directory " +
                "whose name ends with 'Api' and falls back to a root-level Program.cs. " +
                "If the service layout differs, update FindApiProgramCs:\n\n" +
                string.Join("\n", noProgramFound));

        if (probeNotWired.Count > 0)
            messages.Add(
                "The following Program.cs files were auto-discovered via their Migrations " +
                "folder but do not wire MigrationCoverageProbe.RunAsync(...) at boot. " +
                "Without this call the boot-time schema/EF-model self-test from Task #62 " +
                "is silently disabled, re-opening the Task #58 regression class. " +
                "Add `await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, " +
                "app.Logger);` to the service's Program.cs, OR add a " +
                ".probe-wiring-excluded file to the service root with a comment " +
                "explaining why the probe is intentionally absent:\n\n" +
                string.Join("\n", probeNotWired));

        // Opt-out entries are informational — only failures are fatal.
        Assert.True(noProgramFound.Count == 0 && probeNotWired.Count == 0,
            string.Join("\n\n", messages));
    }

    // -----------------------------------------------------------------------
    // Test 2 — explicit list (covers services intentionally without Migrations/)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Explicit guard for services in <see cref="ExplicitServices"/>.
    ///
    /// These services own a DbContext but have no <c>Migrations/</c> folder
    /// and are therefore not enrolled in the auto-discovery test above. This
    /// theory ensures their <c>Program.cs</c> still calls
    /// <c>MigrationCoverageProbe.RunAsync(</c> even though they are excluded
    /// from auto-discovery.
    /// </summary>
    [Theory]
    [MemberData(nameof(ExplicitServiceCases))]
    public void EveryExplicitService_WiresMigrationCoverageProbeInProgram(
        string name, string programRelPath)
    {
        var repoRoot = FindRepoRoot();
        var programPath = Path.Combine(repoRoot, programRelPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(programPath),
            $"Expected service entry point at '{programPath}'. " +
            $"If the service moved or was renamed, update ExplicitServices in {nameof(ServiceMigrationProbeWiringTests)}.");

        var source = File.ReadAllText(programPath);

        Assert.True(ProbeCallPattern.IsMatch(source),
            $"Service '{name}' ({programRelPath}) does not wire MigrationCoverageProbe.RunAsync(...) at boot. " +
            "Without this call the boot-time schema/EF-model self-test from Task #62 is silently disabled, " +
            "re-opening the Task #58 regression class (a migration committed without its [Migration] " +
            "attribute would silently leave the live schema out of sync with the EF model). " +
            "Add a `using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider." +
            "GetRequiredService<TDbContext>(); await BuildingBlocks.Diagnostics." +
            "MigrationCoverageProbe.RunAsync(db, app.Logger);` block to Program.cs.");
    }

    // -----------------------------------------------------------------------
    // Test 3 — migration attribute check (unchanged)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Static guard for Task #67.
    ///
    /// EF Core silently ignores a migration whose partial class has no
    /// <c>[Migration("...")]</c> attribute — the class is simply not
    /// discovered when <c>dotnet ef database update</c> runs.  This was
    /// the exact regression that broke a fresh-DB setup (Task #58).
    ///
    /// For every migration file (non-Designer, non-Snapshot) across the
    /// known service migration directories, this test asserts that the
    /// <c>[Migration("...")]</c> attribute is present either in the main
    /// file itself (single-file style) or in its companion
    /// <c>.Designer.cs</c>.  Failing this test means a recently added
    /// migration will be silently skipped on any clean database.
    /// </summary>
    [Fact]
    public void EveryMigrationFile_HasMigrationAttribute()
    {
        var repoRoot = FindRepoRoot();
        var failures = new List<string>();

        foreach (var relDir in DiscoverMigrationDirs(repoRoot))
        {
            var dir = Path.Combine(repoRoot, relDir.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(dir))
            {
                failures.Add($"Migration directory not found: {relDir}");
                continue;
            }

            var mainFiles = Directory.GetFiles(dir, "*.cs")
                .Where(f => !f.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
                         && !f.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f);

            foreach (var file in mainFiles)
            {
                var source = File.ReadAllText(file);

                // Only check files that actually declare a migration class.
                if (!MigrationClassPattern.IsMatch(source))
                    continue;

                // The [Migration("...")] attribute may live in the main file
                // (single-file style) or in the companion Designer.cs.
                var hasAttribute = MigrationAttributePattern.IsMatch(source);

                if (!hasAttribute)
                {
                    var designerPath = Path.ChangeExtension(file, null) + ".Designer.cs";
                    if (File.Exists(designerPath))
                        hasAttribute = MigrationAttributePattern.IsMatch(File.ReadAllText(designerPath));
                }

                if (!hasAttribute)
                {
                    var relPath = Path.GetRelativePath(repoRoot, file)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    failures.Add(relPath);
                }
            }
        }

        Assert.True(failures.Count == 0,
            "The following migration files are missing the [Migration(\"...\")] attribute. " +
            "EF Core silently skips migrations without this attribute, leaving fresh-database " +
            "schemas incomplete (Task #58 / Task #67 regression class). " +
            "Add [Migration(\"<timestamp>_<ClassName>\")] directly to the partial class " +
            "or regenerate its companion .Designer.cs file:\n\n" +
            string.Join("\n", failures));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Walks <c>apps/services/</c> under the repo root and returns the
    /// repo-relative path (sorted, forward-slash-separated) of every
    /// directory whose name is exactly <c>Migrations</c> (case-sensitive,
    /// which is the project-wide convention — no service uses a lowercase
    /// variant).  Results are sorted so that failure output is stable
    /// across environments.
    /// </summary>
    private static IEnumerable<string> DiscoverMigrationDirs(string repoRoot)
    {
        var servicesRoot = Path.Combine(repoRoot, "apps", "services");
        if (!Directory.Exists(servicesRoot))
            yield break;

        var dirs = Directory
            .EnumerateDirectories(servicesRoot, "Migrations", SearchOption.AllDirectories)
            .Select(d => Path.GetRelativePath(repoRoot, d).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(d => d, StringComparer.Ordinal);

        foreach (var rel in dirs)
            yield return rel;
    }

    /// <summary>
    /// Locates the canonical API entry-point <c>Program.cs</c> within
    /// <paramref name="serviceRoot"/>.
    ///
    /// Resolution order:
    /// <list type="number">
    ///   <item>A <c>Program.cs</c> whose immediate parent directory name ends with
    ///   <c>Api</c> (case-insensitive) — covers the typical
    ///   <c>*.Api/Program.cs</c> layout used by most services.</item>
    ///   <item>A <c>Program.cs</c> located directly in <paramref name="serviceRoot"/>
    ///   — covers flat single-project services such as <c>audit</c>.</item>
    /// </list>
    ///
    /// Returns <c>null</c> when no suitable <c>Program.cs</c> is found so the
    /// caller can report an actionable error.
    /// </summary>
    private static string? FindApiProgramCs(string serviceRoot)
    {
        if (!Directory.Exists(serviceRoot))
            return null;

        var allPrograms = Directory
            .EnumerateFiles(serviceRoot, "Program.cs", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allPrograms.Count == 0)
            return null;

        // 1. Prefer Program.cs whose parent directory name ends with "Api".
        var apiProgram = allPrograms.FirstOrDefault(p =>
            new DirectoryInfo(p).Parent?.Name
                .EndsWith("Api", StringComparison.OrdinalIgnoreCase) == true);
        if (apiProgram is not null)
            return apiProgram;

        // 2. Fall back to a Program.cs sitting directly in the service root
        //    (single-project layout, e.g. audit/).
        var rootProgram = allPrograms.FirstOrDefault(p =>
            string.Equals(
                Path.GetFullPath(Path.GetDirectoryName(p)!),
                Path.GetFullPath(serviceRoot),
                StringComparison.OrdinalIgnoreCase));

        return rootProgram; // null if nothing matched
    }

    // Walk up from the test assembly until we find the repo root marker.
    // We look for `LegalSynq.sln` so this test works whether it's run from
    // `dotnet test`, the IDE, or CI's working directory.
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LegalSynq.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate repo root (LegalSynq.sln) walking up from " + AppContext.BaseDirectory);
    }
}
