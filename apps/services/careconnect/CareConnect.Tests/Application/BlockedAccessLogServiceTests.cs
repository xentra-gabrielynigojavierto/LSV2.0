// LSCC-01-004: BlockedAccessLogService unit tests.
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using CareConnect.Domain;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-01-004 — BlockedAccessLogService.LogAsync() tests.
///
///   1. Happy path — AddAsync is called with a correctly-shaped log entry
///   2. Best-effort — repository exception is swallowed, not re-thrown
///   3. Best-effort — warning is logged when repository throws
///   4. Null context fields — AddAsync is still called (partial context is valid)
///   5. CancellationToken is forwarded to AddAsync
/// </summary>
public class BlockedAccessLogServiceTests
{
    private readonly Mock<IBlockedAccessLogRepository>      _repoMock;
    private readonly Mock<ILogger<BlockedAccessLogService>> _loggerMock;
    private readonly BlockedAccessLogService                _sut;

    public BlockedAccessLogServiceTests()
    {
        _repoMock   = new Mock<IBlockedAccessLogRepository>();
        _loggerMock = new Mock<ILogger<BlockedAccessLogService>>();
        _sut        = new BlockedAccessLogService(_repoMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task LogAsync_HappyPath_CallsAddAsync()
    {
        var tenantId  = Guid.NewGuid();
        var userId    = Guid.NewGuid();

        await _sut.LogAsync(
            tenantId:       tenantId,
            userId:         userId,
            userEmail:      "test@example.com",
            organizationId: Guid.NewGuid(),
            providerId:     Guid.NewGuid(),
            referralId:     Guid.NewGuid(),
            failureReason:  "not_provisioned");

        _repoMock.Verify(
            r => r.AddAsync(It.Is<BlockedProviderAccessLog>(
                l => l.TenantId     == tenantId
                  && l.UserId       == userId
                  && l.FailureReason == "not_provisioned"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAsync_RepositoryThrows_DoesNotRethrow()
    {
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<BlockedProviderAccessLog>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        // Must NOT throw — best-effort contract.
        var ex = await Record.ExceptionAsync(() => _sut.LogAsync(
            tenantId:       null,
            userId:         Guid.NewGuid(),
            userEmail:      null,
            organizationId: null,
            providerId:     null,
            referralId:     null,
            failureReason:  "not_provisioned"));

        Assert.Null(ex);
    }

    [Fact]
    public async Task LogAsync_RepositoryThrows_LogsWarning()
    {
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<BlockedProviderAccessLog>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        await _sut.LogAsync(
            tenantId:       null,
            userId:         null,
            userEmail:      null,
            organizationId: null,
            providerId:     null,
            referralId:     null,
            failureReason:  "not_provisioned");

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAsync_NullContextFields_AddAsyncIsStillCalled()
    {
        await _sut.LogAsync(
            tenantId:       null,
            userId:         null,
            userEmail:      null,
            organizationId: null,
            providerId:     null,
            referralId:     null,
            failureReason:  "unknown");

        _repoMock.Verify(
            r => r.AddAsync(
                It.Is<BlockedProviderAccessLog>(
                    l => l.TenantId == null && l.UserId == null && l.FailureReason == "unknown"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAsync_CancellationToken_IsForwardedToRepository()
    {
        using var cts = new CancellationTokenSource();

        BlockedProviderAccessLog? captured = null;
        CancellationToken         capturedCt = default;

        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<BlockedProviderAccessLog>(), It.IsAny<CancellationToken>()))
            .Callback<BlockedProviderAccessLog, CancellationToken>((log, ct) =>
            {
                captured   = log;
                capturedCt = ct;
            })
            .Returns(Task.CompletedTask);

        await _sut.LogAsync(
            tenantId:       null,
            userId:         null,
            userEmail:      null,
            organizationId: null,
            providerId:     null,
            referralId:     null,
            failureReason:  "not_provisioned",
            ct:             cts.Token);

        Assert.Equal(cts.Token, capturedCt);
    }
}
