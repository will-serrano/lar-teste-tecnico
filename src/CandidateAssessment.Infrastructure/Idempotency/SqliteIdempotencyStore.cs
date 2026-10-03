using System.Data;
using CandidateAssessment.Application.Abstractions.Idempotency;
using CandidateAssessment.Application.Abstractions.Time;
using CandidateAssessment.Infrastructure.Caching;
using CandidateAssessment.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace CandidateAssessment.Infrastructure.Idempotency;

public sealed class SqliteIdempotencyStore : IIdempotencyStore
{
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly TransactionalCacheState _cacheState;
    private readonly MemoryCacheService _cache;
    private readonly ILogger<SqliteIdempotencyStore> _logger;

    public SqliteIdempotencyStore(
        ApplicationDbContext db, IDateTimeProvider clock, TransactionalCacheState cacheState,
        MemoryCacheService cache, ILogger<SqliteIdempotencyStore> logger)
    {
        _db = db;
        _clock = clock;
        _cacheState = cacheState;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IdempotencyEntry?> FindAsync(string scopeHash, string keyHash, int lockTimeoutSeconds, CancellationToken cancellationToken)
    {
        var previousTimeout = _db.Database.GetCommandTimeout();
        _db.Database.SetCommandTimeout(lockTimeoutSeconds);
        try
        {
            var now = _clock.UtcNow;
            var record = await _db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
                x => x.ScopeHash == scopeHash && x.KeyHash == keyHash && x.StatusCode != 0 && x.ExpiresAtUtc > now,
                cancellationToken);
            return record is null ? null : ToEntry(record);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
        {
            throw new IdempotencyBusyException(ex);
        }
        finally
        {
            _db.Database.SetCommandTimeout(previousTimeout);
        }
    }

    public async Task<IIdempotencySession> BeginAsync(
        string scopeHash, string keyHash, string fingerprint, int lockTimeoutSeconds, CancellationToken cancellationToken)
    {
        var transaction = await BeginTransactionAsync(lockTimeoutSeconds, cancellationToken);
        try
        {
            var record = await _db.IdempotencyRecords.SingleOrDefaultAsync(
                x => x.ScopeHash == scopeHash && x.KeyHash == keyHash, cancellationToken);
            if (record is not null && record.StatusCode != 0 && record.ExpiresAtUtc > _clock.UtcNow)
            {
                return new Session(this, transaction, record, ToEntry(record));
            }

            if (record is null)
            {
                record = new IdempotencyRecord { ScopeHash = scopeHash, KeyHash = keyHash };
                _db.IdempotencyRecords.Add(record);
            }

            record.Fingerprint = fingerprint;
            record.StatusCode = 0;
            record.Body = Array.Empty<byte>();
            record.ContentType = null;
            record.Location = null;
            record.CreatedAtUtc = _clock.UtcNow;
            record.ExpiresAtUtc = DateTime.MaxValue;
            await _db.SaveChangesAsync(cancellationToken);
            _cacheState.Begin();
            return new Session(this, transaction, record, null);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task<int> DeleteExpiredAsync(int batchSize, int lockTimeoutSeconds, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(lockTimeoutSeconds, cancellationToken);
        var now = _clock.UtcNow;
        var expired = await _db.IdempotencyRecords.Where(x => x.ExpiresAtUtc <= now)
            .OrderBy(x => x.ExpiresAtUtc).Take(batchSize).ToListAsync(cancellationToken);
        _db.IdempotencyRecords.RemoveRange(expired);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.EfTransaction.CommitAsync(cancellationToken);
        return expired.Count;
    }

    private async Task<Transaction> BeginTransactionAsync(int timeout, CancellationToken cancellationToken)
    {
        await _db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (SqliteConnection)_db.Database.GetDbConnection();
        var previousTimeout = connection.DefaultTimeout;
        connection.DefaultTimeout = timeout;
        SqliteTransaction? sqliteTransaction = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Non-deferred acquisition avoids upgrading a read transaction after another writer commits.
            sqliteTransaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
            var efTransaction = await _db.Database.UseTransactionAsync(sqliteTransaction, cancellationToken)
                ?? throw new InvalidOperationException("Unable to enlist the SQLite transaction.");
            return new Transaction(connection, sqliteTransaction, efTransaction, previousTimeout);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
        {
            sqliteTransaction?.Dispose();
            connection.DefaultTimeout = previousTimeout;
            throw new IdempotencyBusyException(ex);
        }
        catch
        {
            sqliteTransaction?.Dispose();
            connection.DefaultTimeout = previousTimeout;
            throw;
        }
    }

    private static IdempotencyEntry ToEntry(IdempotencyRecord record)
        => new(record.Fingerprint, new IdempotencyResponse(record.StatusCode, record.Body, record.ContentType, record.Location));

    private sealed class Transaction : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SqliteTransaction _sqliteTransaction;
        private readonly int _previousTimeout;

        public Transaction(SqliteConnection connection, SqliteTransaction sqliteTransaction,
            IDbContextTransaction efTransaction, int previousTimeout)
        {
            _connection = connection;
            _sqliteTransaction = sqliteTransaction;
            EfTransaction = efTransaction;
            _previousTimeout = previousTimeout;
        }

        public IDbContextTransaction EfTransaction { get; }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await EfTransaction.DisposeAsync();
                await _sqliteTransaction.DisposeAsync();
            }
            finally
            {
                _connection.DefaultTimeout = _previousTimeout;
            }
        }
    }

    private sealed class Session : IIdempotencySession
    {
        private readonly SqliteIdempotencyStore _owner;
        private readonly Transaction _transaction;
        private readonly IdempotencyRecord _record;
        private bool _completed;
        private bool _disposed;

        public Session(SqliteIdempotencyStore owner, Transaction transaction, IdempotencyRecord record, IdempotencyEntry? existing)
        {
            _owner = owner;
            _transaction = transaction;
            _record = record;
            Existing = existing;
        }

        public IdempotencyEntry? Existing { get; }

        public async Task CompleteAsync(IdempotencyResponse response, TimeSpan lifetime, CancellationToken cancellationToken)
        {
            if (Existing is not null || _completed)
            {
                throw new InvalidOperationException("The idempotency session cannot be completed again.");
            }

            _record.StatusCode = response.StatusCode;
            _record.Body = response.Body;
            _record.ContentType = response.ContentType;
            _record.Location = response.Location;
            _record.ExpiresAtUtc = _owner._clock.UtcNow.Add(lifetime);
            await _owner._db.SaveChangesAsync(cancellationToken);
            await _transaction.EfTransaction.CommitAsync(cancellationToken);
            _completed = true;
            try
            {
                await _owner._cacheState.CommitAsync(_owner._cache);
            }
            catch (Exception ex)
            {
                _owner._logger.LogError(ex, "Cache finalization failed after committed idempotent operation.");
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                await _transaction.DisposeAsync();
            }
            finally
            {
                _owner._cacheState.Reset();
            }
        }
    }
}
