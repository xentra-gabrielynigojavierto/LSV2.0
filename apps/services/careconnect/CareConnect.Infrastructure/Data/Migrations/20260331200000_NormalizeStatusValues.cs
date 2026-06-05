using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// Canonical status normalization for CareConnect referrals and appointments.
///
/// Referrals:
///   - "Received"  → "Accepted"  (receiver acknowledged the referral)
///   - "Contacted" → "Accepted"  (intermediate step absorbed into Accepted lifecycle)
///
/// Appointments:
///   - "Scheduled" → "Pending"   (pre-confirmation state renamed to canonical Pending)
///
/// Down: reverses the referral data migration only. Appointment data is NOT reversed
/// since "Pending" is the canonical value going forward (Scheduled is a legacy alias).
/// </summary>
public partial class NormalizeStatusValues : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Referrals: Received → Accepted ───────────────────────────────────
        migrationBuilder.Sql(
            "UPDATE `Referrals` SET `Status` = 'Accepted' WHERE `Status` = 'Received';");

        // ── Referrals: Contacted → Accepted ──────────────────────────────────
        migrationBuilder.Sql(
            "UPDATE `Referrals` SET `Status` = 'Accepted' WHERE `Status` = 'Contacted';");

        // ── ReferralStatusHistories: normalize historical snapshots ───────────
        migrationBuilder.Sql(
            "UPDATE `ReferralStatusHistories` SET `OldStatus` = 'Accepted' WHERE `OldStatus` IN ('Received', 'Contacted');");
        migrationBuilder.Sql(
            "UPDATE `ReferralStatusHistories` SET `NewStatus` = 'Accepted' WHERE `NewStatus` IN ('Received', 'Contacted');");

        // ── Appointments: Scheduled → Pending ────────────────────────────────
        migrationBuilder.Sql(
            "UPDATE `Appointments` SET `Status` = 'Pending' WHERE `Status` = 'Scheduled';");

        // ── AppointmentStatusHistories: normalize historical snapshots ─────────
        migrationBuilder.Sql(
            "UPDATE `AppointmentStatusHistories` SET `OldStatus` = 'Pending' WHERE `OldStatus` = 'Scheduled';");
        migrationBuilder.Sql(
            "UPDATE `AppointmentStatusHistories` SET `NewStatus` = 'Pending' WHERE `NewStatus` = 'Scheduled';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverse referral status normalization only.
        // Appointment Pending → Scheduled reversal is omitted: Pending is canonical going forward.
        migrationBuilder.Sql(
            "UPDATE `Referrals` SET `Status` = 'Received' WHERE `Status` = 'Accepted' AND `CreatedAtUtc` < NOW();");
        migrationBuilder.Sql(
            "UPDATE `ReferralStatusHistories` SET `OldStatus` = 'Received' WHERE `OldStatus` = 'Accepted';");
        migrationBuilder.Sql(
            "UPDATE `ReferralStatusHistories` SET `NewStatus` = 'Received' WHERE `NewStatus` = 'Accepted';");
    }
}
