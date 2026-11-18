using Microsoft.EntityFrameworkCore;

namespace ImmichFrame.WebApi.Database;

public class SettingsDbContext : DbContext
{
    public SettingsDbContext(DbContextOptions<SettingsDbContext> options) : base(options) { }

    public DbSet<SettingsDocument> SettingsDocuments => Set<SettingsDocument>();
    public DbSet<SettingsHistoryEntry> SettingsHistory => Set<SettingsHistoryEntry>();
    public DbSet<ScreenRecord> Screens => Set<ScreenRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SettingsDocument>()
            .Property(d => d.Version)
            .IsConcurrencyToken();

        modelBuilder.Entity<SettingsHistoryEntry>()
            .HasIndex(h => h.Version);

        modelBuilder.Entity<ScreenRecord>()
            .HasKey(s => s.ClientIdentifier);
    }
}

public class SettingsDocument
{
    public int Id { get; set; }

    // Raw ServerSettings V2 JSON, pre-Validate (ApiKeyFile stays unresolved)
    public string Json { get; set; } = "{}";

    public int SchemaVersion { get; set; } = 2;

    public DateTime UpdatedAtUtc { get; set; }

    // "Settings.json" | "Settings.yml" | "env" | null when created via admin UI
    public string? ImportedFrom { get; set; }

    public long Version { get; set; }

    /// <summary>Set when this version was made by restoring an older one (shown in the history).</summary>
    public long? RestoredFromVersion { get; set; }
}

/// <summary>A previous settings document, kept so a save can be undone.</summary>
public class SettingsHistoryEntry
{
    public int Id { get; set; }

    /// <summary>The <see cref="SettingsDocument.Version"/> this JSON had while it was current.</summary>
    public long Version { get; set; }

    // Raw ServerSettings V2 JSON, same form as SettingsDocument.Json
    /// <summary>Copied from the document: the version this one restored, if any.</summary>
    public long? RestoredFromVersion { get; set; }

    public string Json { get; set; } = "{}";

    /// <summary>When this version was saved (became current).</summary>
    public DateTime SavedAtUtc { get; set; }

    /// <summary>When it was replaced and moved into the history.</summary>
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>A device that showed the frame, recognised by its client identifier.</summary>
public class ScreenRecord
{
    public string ClientIdentifier { get; set; } = string.Empty;

    public string? Name { get; set; }

    public DateTime FirstSeenUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public string? UserAgent { get; set; }

    public string? RemoteAddress { get; set; }
}
