using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELearningPlatform.Application.Services.Payments;

/// <summary>
/// Runs a money operation as one database transaction: everything it changes is saved and committed together,
/// or nothing is. Double-spend protection has two layers:
/// <list type="bullet">
/// <item>the transaction is opened before the balance is read (on SQLite, BEGIN IMMEDIATE serialises writers);</item>
/// <item>wallets carry a concurrency token, so a write based on a stale balance fails instead of overwriting.</item>
/// </list>
/// A concurrency conflict or unique-key race re-runs the operation on fresh data, a few times at most.
/// </summary>
public sealed class FinanceTransaction(IAppDbContext db, ILogger<FinanceTransaction> logger)
{
    public const int MaxAttempts = 3;

    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                if (result.IsFailure)
                {
                    await RollbackAsync(transaction);
                    return result;
                }

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DomainException ex) when (ex.Code == DomainErrors.InsufficientFunds)
            {
                await RollbackAsync(transaction);
                return Result<T>.Conflict("Insufficient funds", ErrorCodes.InsufficientFunds);
            }
            catch (DbUpdateConcurrencyException) when (attempt >= MaxAttempts)
            {
                await RollbackAsync(transaction);
                logger.LogWarning("Money operation gave up after {Attempts} concurrency conflicts", attempt);
                return Result<T>.Conflict("The wallet was changed by another request. Please retry.", ErrorCodes.ConcurrentUpdate);
            }
            catch (DbUpdateException ex) when (attempt < MaxAttempts)
            {
                await RollbackAsync(transaction);
                logger.LogInformation("Money operation retried after {Error} (attempt {Attempt})", ex.GetType().Name, attempt);
            }
            catch
            {
                await RollbackAsync(transaction);
                throw;
            }
        }
    }

    /// <summary>Rolls back and forgets every tracked change, so a retry or a later save starts clean.</summary>
    private async Task RollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try { await transaction.RollbackAsync(CancellationToken.None); }
        catch (InvalidOperationException) { /* already completed */ }
        db.ChangeTracker.Clear();
    }
}
