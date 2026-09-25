using ContainerControl.Modules.Delivery.Persistence;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ContainerControl.Modules.Delivery.Runs;

/// <summary>
/// One active deploy per application and environment. A held lease is refused
/// immediately; it expires after <see cref="HoldFor"/> if the holder never releases it.
/// </summary>
public sealed class DeployLease
{
    public const string ContendedMessage = "A deployment is already running for this application and environment.";

    public static readonly TimeSpan HoldFor = TimeSpan.FromMinutes(5);

    private readonly DeliveryDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<DeployLease> _logger;

    public DeployLease(DeliveryDbContext db, IClock clock, ILogger<DeployLease> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Guid?> TryAcquireAsync(Guid applicationId, string environment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(environment))
        {
            throw new ArgumentException("An environment is required.", nameof(environment));
        }

        var ownerId = Guid.NewGuid();
        var expiresAt = _clock.UtcNow.Add(HoldFor);
        var lease = await _db.Leases.SingleOrDefaultAsync(
            item => item.ApplicationId == applicationId && item.Environment == environment,
            cancellationToken);

        if (lease is null)
        {
            lease = new WorkerLease
            {
                ApplicationId = applicationId,
                Environment = environment,
                OwnerId = ownerId,
                ExpiresAtUtc = expiresAt
            };
            _db.Leases.Add(lease);
        }
        else if (IsHeld(lease))
        {
            return null;
        }
        else
        {
            lease.OwnerId = ownerId;
            lease.ExpiresAtUtc = expiresAt;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return ownerId;
        }
        catch (DbUpdateConcurrencyException)
        {
            Detach(lease);
            return null;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            Detach(lease);
            return null;
        }
        catch (OperationCanceledException)
        {
            await ReleaseAfterInterruptedAcquireAsync(applicationId, environment, ownerId);
            throw;
        }
    }

    public async Task<bool> IsContendedAsync(Guid applicationId, string environment, CancellationToken cancellationToken)
    {
        var lease = await _db.Leases.AsNoTracking().SingleOrDefaultAsync(
            item => item.ApplicationId == applicationId && item.Environment == environment,
            cancellationToken);
        return lease is not null && IsHeld(lease);
    }

    public async Task ReleaseAsync(Guid applicationId, string environment, Guid ownerId, CancellationToken cancellationToken)
    {
        var lease = await _db.Leases.SingleOrDefaultAsync(
            item => item.ApplicationId == applicationId && item.Environment == environment,
            cancellationToken);
        if (lease is null || lease.OwnerId != ownerId)
        {
            return;
        }

        lease.OwnerId = null;
        lease.ExpiresAtUtc = null;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "Deploy lease for {ApplicationId} in {Environment} changed before it was released.",
                applicationId,
                environment);
        }
    }

    private async Task ReleaseAfterInterruptedAcquireAsync(Guid applicationId, string environment, Guid ownerId)
    {
        try
        {
            _db.ChangeTracker.Clear();
            await ReleaseAsync(applicationId, environment, ownerId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Deploy lease for {ApplicationId} in {Environment} could not be released after a cancelled acquire. {ExceptionType}",
                applicationId,
                environment,
                exception.GetType().Name);
        }
    }

    private bool IsHeld(WorkerLease lease) =>
        lease.OwnerId is not null && lease.ExpiresAtUtc is not null && lease.ExpiresAtUtc > _clock.UtcNow;

    private void Detach(WorkerLease lease)
    {
        var entry = _db.Entry(lease);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return true;
            }
        }

        return false;
    }
}
