using ImmichFrame.WebApi.Database;
using Microsoft.EntityFrameworkCore;

namespace ImmichFrame.WebApi.Tests.Helpers
{
    /// <summary>A migrated SQLite settings database in its own temp directory.</summary>
    public sealed class SqliteTestDb : IDbContextFactory<SettingsDbContext>, IDisposable
    {
        private readonly DbContextOptions<SettingsDbContext> _options;

        public string Directory { get; }

        public SqliteTestDb()
        {
            Directory = Path.Combine(Path.GetTempPath(), "immichframe-tests", Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(Directory);
            _options = new DbContextOptionsBuilder<SettingsDbContext>()
                .UseSqlite($"Data Source={Path.Combine(Directory, "immichframe.db")};Pooling=False")
                .Options;
            using var db = CreateDbContext();
            db.Database.Migrate();
        }

        public SettingsDbContext CreateDbContext() => new(_options);

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, true);
            }
        }
    }

    /// <summary>A clock the test moves by hand.</summary>
    public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
