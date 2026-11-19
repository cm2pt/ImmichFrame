using System.Collections.Concurrent;
using ImmichFrame.WebApi.Database;
using Microsoft.EntityFrameworkCore;

namespace ImmichFrame.WebApi.Services;

public class ScreenDto
{
    public string ClientIdentifier { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public string? UserAgent { get; set; }
    public string? RemoteAddress { get; set; }
}

/// <summary>A screen the frame turned away because it sent a wrong secret, or none.</summary>
public class RejectedScreenDto
{
    public string ClientIdentifier { get; set; } = string.Empty;
    /// <summary>The name given to it, when it was a known screen before its secret went stale.</summary>
    public string? Name { get; set; }
    public DateTime FirstRejectedUtc { get; set; }
    public DateTime LastRejectedUtc { get; set; }
    public int Attempts { get; set; }
    /// <summary>False when it sent no secret at all; true when it sent one that didn't match.</summary>
    public bool SentSecret { get; set; }
    public string? UserAgent { get; set; }
    public string? RemoteAddress { get; set; }
}

/// <summary>
/// Remembers the devices showing the frame, keyed by the <c>clientIdentifier</c> they send.
/// Every request updates memory; the database (when there is one) is written at most once per
/// <see cref="PersistInterval"/> per screen, so photo requests stay cheap. In file mode there
/// is no database and screens are kept in memory only, until the next restart.
/// </summary>
public class ScreenRegistry
{
    public static readonly TimeSpan PersistInterval = TimeSpan.FromMinutes(1);

    /// <summary>A cap on remembered screens, so a misbehaving client cannot fill the database.</summary>
    public const int MaxScreens = 200;

    /// <summary>A cap on remembered rejections, which are kept in memory only.</summary>
    public const int MaxRejected = 50;

    /// <summary>Rejections older than this are dropped: the screen has given up or been fixed.</summary>
    public static readonly TimeSpan RejectedRetention = TimeSpan.FromDays(7);

    public const int MaxIdentifierLength = 100;
    private const int MaxUserAgentLength = 300;
    private const int MaxNameLength = 100;

    private readonly IDbContextFactory<SettingsDbContext>? _dbFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<ScreenRegistry> _logger;
    private readonly ConcurrentDictionary<string, Entry> _screens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RejectedScreenDto> _rejected = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile bool _loaded;

    private sealed class Entry
    {
        public required ScreenRecord Record { get; init; }
        public DateTime? PersistedAtUtc { get; set; }
    }

    public ScreenRegistry(IDbContextFactory<SettingsDbContext>? dbFactory, TimeProvider time, ILogger<ScreenRegistry> logger)
    {
        _dbFactory = dbFactory;
        _time = time;
        _logger = logger;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <summary>Records a request from a screen. Persists when the screen is new or its last write is older than <see cref="PersistInterval"/>.</summary>
    public async Task RecordAsync(string clientIdentifier, string? userAgent, string? remoteAddress)
    {
        var id = Normalize(clientIdentifier);
        if (id == null)
        {
            return;
        }

        await EnsureLoadedAsync();
        var now = Now;
        userAgent = Truncate(userAgent, MaxUserAgentLength);

        // It has the right secret now.
        _rejected.TryRemove(id, out _);

        if (!_screens.TryGetValue(id, out var entry))
        {
            if (_screens.Count >= MaxScreens)
            {
                return;
            }

            entry = _screens.GetOrAdd(id, _ => new Entry
            {
                Record = new ScreenRecord { ClientIdentifier = id, FirstSeenUtc = now, LastSeenUtc = now }
            });
        }

        bool persist;
        ScreenRecord snapshot;
        lock (entry)
        {
            entry.Record.LastSeenUtc = now;
            entry.Record.UserAgent = userAgent ?? entry.Record.UserAgent;
            entry.Record.RemoteAddress = remoteAddress ?? entry.Record.RemoteAddress;
            persist = entry.PersistedAtUtc == null || now - entry.PersistedAtUtc.Value >= PersistInterval;
            if (persist)
            {
                entry.PersistedAtUtc = now;
            }

            snapshot = Copy(entry.Record);
        }

        if (persist)
        {
            await WriteAsync(snapshot);
        }
    }

    /// <summary>
    /// Records a request turned away for a wrong or missing secret. Kept in memory only, capped
    /// at <see cref="MaxRejected"/>: it's a hint for the admin, not an audit log.
    /// </summary>
    public void RecordRejected(string clientIdentifier, string? userAgent, string? remoteAddress, bool sentSecret)
    {
        var id = Normalize(clientIdentifier);
        if (id == null)
        {
            return;
        }

        var now = Now;
        Prune(now);
        if (!_rejected.ContainsKey(id) && _rejected.Count >= MaxRejected)
        {
            return;
        }

        var entry = _rejected.GetOrAdd(id, _ => new RejectedScreenDto
        {
            ClientIdentifier = id,
            FirstRejectedUtc = now
        });
        lock (entry)
        {
            entry.LastRejectedUtc = now;
            entry.Attempts++;
            entry.SentSecret = sentSecret;
            entry.UserAgent = Truncate(userAgent, MaxUserAgentLength) ?? entry.UserAgent;
            entry.RemoteAddress = remoteAddress ?? entry.RemoteAddress;
        }
    }

    public async Task<IReadOnlyList<RejectedScreenDto>> GetRejectedAsync()
    {
        await EnsureLoadedAsync();
        Prune(Now);
        return _rejected.Values
            .Select(r =>
            {
                lock (r)
                {
                    string? name = null;
                    if (_screens.TryGetValue(r.ClientIdentifier, out var known))
                    {
                        lock (known)
                        {
                            name = known.Record.Name;
                        }
                    }

                    return new RejectedScreenDto
                    {
                        ClientIdentifier = r.ClientIdentifier,
                        Name = name,
                        FirstRejectedUtc = DateTime.SpecifyKind(r.FirstRejectedUtc, DateTimeKind.Utc),
                        LastRejectedUtc = DateTime.SpecifyKind(r.LastRejectedUtc, DateTimeKind.Utc),
                        Attempts = r.Attempts,
                        SentSecret = r.SentSecret,
                        UserAgent = r.UserAgent,
                        RemoteAddress = r.RemoteAddress
                    };
                }
            })
            .OrderByDescending(r => r.LastRejectedUtc)
            .ToList();
    }

    /// <summary>Clears a rejection. It comes back if the screen tries again with a wrong secret.</summary>
    public bool DismissRejected(string clientIdentifier) => _rejected.TryRemove(clientIdentifier, out _);

    private void Prune(DateTime now)
    {
        foreach (var (id, r) in _rejected)
        {
            if (now - r.LastRejectedUtc > RejectedRetention)
            {
                _rejected.TryRemove(id, out _);
            }
        }
    }

    public async Task<IReadOnlyList<ScreenDto>> GetScreensAsync()
    {
        await EnsureLoadedAsync();
        return _screens.Values
            .Select(e =>
            {
                lock (e)
                {
                    return ToDto(e.Record);
                }
            })
            .OrderByDescending(s => s.LastSeenUtc)
            .ToList();
    }

    /// <summary>Names a screen. A blank name clears it. Returns false for an unknown screen.</summary>
    public async Task<bool> RenameAsync(string clientIdentifier, string? name)
    {
        await EnsureLoadedAsync();
        if (!_screens.TryGetValue(clientIdentifier, out var entry))
        {
            return false;
        }

        ScreenRecord snapshot;
        lock (entry)
        {
            entry.Record.Name = string.IsNullOrWhiteSpace(name) ? null : Truncate(name.Trim(), MaxNameLength);
            snapshot = Copy(entry.Record);
        }

        await WriteAsync(snapshot);
        return true;
    }

    /// <summary>Forgets a screen. It reappears, unnamed, the next time it asks for photos.</summary>
    public async Task<bool> ForgetAsync(string clientIdentifier)
    {
        await EnsureLoadedAsync();
        if (!_screens.TryRemove(clientIdentifier, out _))
        {
            return false;
        }

        if (_dbFactory == null)
        {
            return true;
        }

        await _writeLock.WaitAsync();
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            await db.Screens.Where(s => s.ClientIdentifier == clientIdentifier).ExecuteDeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not delete screen '{id}' from the database: {message}", clientIdentifier, ex.Message);
        }
        finally
        {
            _writeLock.Release();
        }

        return true;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        await _loadLock.WaitAsync();
        try
        {
            if (_loaded)
            {
                return;
            }

            if (_dbFactory != null)
            {
                try
                {
                    await using var db = await _dbFactory.CreateDbContextAsync();
                    foreach (var record in await db.Screens.AsNoTracking().ToListAsync())
                    {
                        _screens.TryAdd(record.ClientIdentifier, new Entry { Record = record, PersistedAtUtc = record.LastSeenUtc });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not load screens from the database, keeping them in memory only: {message}", ex.Message);
                }
            }

            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task WriteAsync(ScreenRecord snapshot)
    {
        if (_dbFactory == null)
        {
            return;
        }

        await _writeLock.WaitAsync();
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var row = await db.Screens.FindAsync(snapshot.ClientIdentifier);
            if (row == null)
            {
                db.Screens.Add(snapshot);
            }
            else
            {
                row.Name = snapshot.Name;
                row.LastSeenUtc = snapshot.LastSeenUtc;
                row.UserAgent = snapshot.UserAgent;
                row.RemoteAddress = snapshot.RemoteAddress;
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not save screen '{id}' to the database: {message}", snapshot.ClientIdentifier, ex.Message);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static string? Normalize(string? clientIdentifier)
    {
        if (string.IsNullOrWhiteSpace(clientIdentifier))
        {
            return null;
        }

        // Kept as sent (dashes and all), so the admin sees what the screen calls itself;
        // control characters are dropped and the length is capped.
        var cleaned = new string(clientIdentifier.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            return null;
        }

        return cleaned.Length > MaxIdentifierLength ? cleaned[..MaxIdentifierLength] : cleaned;
    }

    private static string? Truncate(string? value, int max) =>
        value == null || value.Length <= max ? value : value[..max];

    private static ScreenRecord Copy(ScreenRecord r) => new()
    {
        ClientIdentifier = r.ClientIdentifier,
        Name = r.Name,
        FirstSeenUtc = r.FirstSeenUtc,
        LastSeenUtc = r.LastSeenUtc,
        UserAgent = r.UserAgent,
        RemoteAddress = r.RemoteAddress
    };

    private static ScreenDto ToDto(ScreenRecord r) => new()
    {
        ClientIdentifier = r.ClientIdentifier,
        Name = r.Name,
        FirstSeenUtc = DateTime.SpecifyKind(r.FirstSeenUtc, DateTimeKind.Utc),
        LastSeenUtc = DateTime.SpecifyKind(r.LastSeenUtc, DateTimeKind.Utc),
        UserAgent = r.UserAgent,
        RemoteAddress = r.RemoteAddress
    };
}
