using ImmichFrame.WebApi.Services;
using ImmichFrame.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Services
{
    [TestFixture]
    public class ScreenRegistryTests
    {
        private static readonly DateTimeOffset Start = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

        private SqliteTestDb _db;
        private ManualTimeProvider _clock;

        [SetUp]
        public void Setup()
        {
            _db = new SqliteTestDb();
            _clock = new ManualTimeProvider(Start);
        }

        [TearDown]
        public void TearDown() => _db.Dispose();

        private ScreenRegistry CreateRegistry(bool withDb = true) =>
            new(withDb ? _db : null, _clock, NullLogger<ScreenRegistry>.Instance);

        private async Task<DateTime> PersistedLastSeen(string id)
        {
            await using var db = _db.CreateDbContext();
            return (await db.Screens.AsNoTracking().SingleAsync(s => s.ClientIdentifier == id)).LastSeenUtc;
        }

        [Test]
        public async Task Record_NewScreen_IsListedAndPersisted()
        {
            var registry = CreateRegistry();

            await registry.RecordAsync("living-room", "AppleTV", "192.168.1.20");

            var screen = (await registry.GetScreensAsync()).Single();
            Assert.Multiple(async () =>
            {
                Assert.That(screen.ClientIdentifier, Is.EqualTo("living-room"));
                Assert.That(screen.UserAgent, Is.EqualTo("AppleTV"));
                Assert.That(screen.RemoteAddress, Is.EqualTo("192.168.1.20"));
                Assert.That(screen.FirstSeenUtc, Is.EqualTo(Start.UtcDateTime));
                Assert.That(await PersistedLastSeen("living-room"), Is.EqualTo(Start.UtcDateTime));
            });
        }

        [Test]
        public async Task Record_WithinInterval_UpdatesMemoryButNotTheDatabase()
        {
            var registry = CreateRegistry();
            await registry.RecordAsync("kitchen", null, null);

            _clock.Advance(TimeSpan.FromSeconds(30));
            await registry.RecordAsync("kitchen", null, null);

            Assert.Multiple(async () =>
            {
                Assert.That((await registry.GetScreensAsync()).Single().LastSeenUtc,
                    Is.EqualTo(Start.AddSeconds(30).UtcDateTime));
                Assert.That(await PersistedLastSeen("kitchen"), Is.EqualTo(Start.UtcDateTime));
            });
        }

        [Test]
        public async Task Record_AfterInterval_WritesTheDatabaseAgain()
        {
            var registry = CreateRegistry();
            await registry.RecordAsync("kitchen", null, null);

            _clock.Advance(ScreenRegistry.PersistInterval + TimeSpan.FromSeconds(1));
            await registry.RecordAsync("kitchen", null, null);

            Assert.That(await PersistedLastSeen("kitchen"),
                Is.EqualTo(Start.Add(ScreenRegistry.PersistInterval).AddSeconds(1).UtcDateTime));
        }

        [Test]
        public async Task Screens_SurviveARestart_WithTheirNames()
        {
            var registry = CreateRegistry();
            await registry.RecordAsync("ipad-kitchen", "Safari", null);
            await registry.RenameAsync("ipad-kitchen", "  Kitchen  ");

            var restarted = CreateRegistry();
            var screen = (await restarted.GetScreensAsync()).Single();

            Assert.Multiple(() =>
            {
                Assert.That(screen.ClientIdentifier, Is.EqualTo("ipad-kitchen"));
                Assert.That(screen.Name, Is.EqualTo("Kitchen"));
                Assert.That(screen.UserAgent, Is.EqualTo("Safari"));
            });
        }

        [Test]
        public async Task Rename_BlankName_ClearsIt_AndUnknownScreenReturnsFalse()
        {
            var registry = CreateRegistry();
            await registry.RecordAsync("tv", null, null);
            await registry.RenameAsync("tv", "Living room");

            Assert.Multiple(async () =>
            {
                Assert.That(await registry.RenameAsync("tv", "   "), Is.True);
                Assert.That((await registry.GetScreensAsync()).Single().Name, Is.Null);
                Assert.That(await registry.RenameAsync("nope", "x"), Is.False);
            });
        }

        [Test]
        public async Task Forget_RemovesFromMemoryAndDatabase()
        {
            var registry = CreateRegistry();
            await registry.RecordAsync("old-tablet", null, null);

            Assert.That(await registry.ForgetAsync("old-tablet"), Is.True);

            await using var db = _db.CreateDbContext();
            Assert.Multiple(async () =>
            {
                Assert.That(await registry.GetScreensAsync(), Is.Empty);
                Assert.That(await db.Screens.CountAsync(), Is.Zero);
                Assert.That(await registry.ForgetAsync("old-tablet"), Is.False);
            });
        }

        [Test]
        public async Task GetScreens_NewestFirst()
        {
            var registry = CreateRegistry();
            await registry.RecordAsync("a", null, null);
            _clock.Advance(TimeSpan.FromMinutes(5));
            await registry.RecordAsync("b", null, null);

            var ids = (await registry.GetScreensAsync()).Select(s => s.ClientIdentifier);

            Assert.That(ids, Is.EqualTo(new[] { "b", "a" }));
        }

        [Test]
        public async Task Record_BlankIdentifier_IsIgnored_AndLongOnesAreCut()
        {
            var registry = CreateRegistry();
            await registry.RecordAsync("   ", null, null);
            await registry.RecordAsync(new string('x', 500), null, null);

            var screen = (await registry.GetScreensAsync()).Single();
            Assert.That(screen.ClientIdentifier, Has.Length.EqualTo(ScreenRegistry.MaxIdentifierLength));
        }

        [Test]
        public async Task WithoutDatabase_KeepsScreensInMemory()
        {
            var registry = CreateRegistry(withDb: false);

            await registry.RecordAsync("file-mode-screen", null, null);
            await registry.RenameAsync("file-mode-screen", "Hall");

            await using var db = _db.CreateDbContext();
            Assert.Multiple(async () =>
            {
                Assert.That((await registry.GetScreensAsync()).Single().Name, Is.EqualTo("Hall"));
                Assert.That(await db.Screens.CountAsync(), Is.Zero);
            });
        }

        [Test]
        public async Task Record_StopsAddingScreensAtTheCap()
        {
            var registry = CreateRegistry(withDb: false);
            for (var i = 0; i < ScreenRegistry.MaxScreens + 5; i++)
            {
                await registry.RecordAsync($"screen-{i}", null, null);
            }

            Assert.That(await registry.GetScreensAsync(), Has.Count.EqualTo(ScreenRegistry.MaxScreens));
        }
    }
}
