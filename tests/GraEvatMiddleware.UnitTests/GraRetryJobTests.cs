using GraEvatMiddleware.Domain.Retry;

namespace GraEvatMiddleware.UnitTests;

public sealed class GraRetryJobTests
{
    [Fact]
    public void DueJob_CanBeClaimedOnlyOnceDuringLease()
    {
        var now = DateTimeOffset.Parse("2026-11-03T12:00:00Z");
        var job = GraRetryJob.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), "correlation", "idempotency", "{}",
            RetryFailureClassification.Temporary, "GRA_SERVICE_UNAVAILABLE", 3, now, now);

        var firstClaim = job.TryClaim("worker-a", now, TimeSpan.FromMinutes(1));
        var secondClaim = job.TryClaim("worker-b", now.AddSeconds(1), TimeSpan.FromMinutes(1));

        Assert.True(firstClaim);
        Assert.False(secondClaim);
        Assert.Equal(1, job.AttemptNumber);
        Assert.Equal(RetryJobStatus.Processing, job.Status);
        Assert.Equal("worker-a", job.LeaseOwner);
    }

    [Fact]
    public void AmbiguousJob_ReturnsToReconciliationWithoutBecomingAutomaticSubmission()
    {
        var now = DateTimeOffset.Parse("2026-11-03T12:00:00Z");
        var job = GraRetryJob.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), "correlation", "idempotency", "{}",
            RetryFailureClassification.Ambiguous, "GRA_TIMEOUT_STATUS_UNKNOWN", 3, now, now);
        Assert.True(job.TryClaim("worker-a", now, TimeSpan.FromMinutes(1)));

        job.ScheduleReconciliation("E701", now.AddMinutes(1), now.AddSeconds(2));

        Assert.Equal(RetryJobStatus.ReconciliationPending, job.Status);
        Assert.Equal(RetryFailureClassification.Ambiguous, job.FailureClassification);
        Assert.Equal(now.AddMinutes(1), job.NextAttemptAt);
        Assert.Null(job.LeaseOwner);
    }

    [Fact]
    public void Job_IsExhaustedAtConfiguredAttemptLimit()
    {
        var now = DateTimeOffset.Parse("2026-11-03T12:00:00Z");
        var job = GraRetryJob.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), "correlation", "idempotency", "{}",
            RetryFailureClassification.Temporary, "E990", 1, now, now);
        Assert.True(job.TryClaim("worker-a", now, TimeSpan.FromMinutes(1)));

        job.Exhaust("E990", now.AddSeconds(1));

        Assert.Equal(RetryJobStatus.Exhausted, job.Status);
        Assert.Equal(1, job.AttemptNumber);
        Assert.Null(job.LeaseOwner);
    }
}
