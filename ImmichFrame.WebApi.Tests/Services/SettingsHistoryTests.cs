using ImmichFrame.WebApi.Helpers.Config;
using ImmichFrame.WebApi.Models;
using ImmichFrame.WebApi.Services;
using ImmichFrame.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Services
{
    [TestFixture]
    public class SettingsDiffTests
    {
        [Test]
        public void Describe_ScalarChange_ShowsOldAndNew()
        {
            var changes = SettingsDiff.Describe(
                """{"General":{"Layout":"single","Interval":45},"Accounts":[]}""",
                """{"General":{"Layout":"splitview","Interval":45},"Accounts":[]}""");

            Assert.That(changes, Is.EqualTo(new[] { "Layout: single → splitview" }));
        }

        [Test]
        public void Describe_Secrets_NeverShowTheirValues()
        {
            var changes = SettingsDiff.Describe(
                """{"General":{"AuthenticationSecret":"old-secret","WeatherApiKey":""},"Accounts":[{"ApiKey":"k1"}]}""",
                """{"General":{"AuthenticationSecret":"new-secret","WeatherApiKey":"w"},"Accounts":[{"ApiKey":"k2"}]}""");

            Assert.Multiple(() =>
            {
                Assert.That(changes, Is.EquivalentTo(new[]
                {
                    "AuthenticationSecret changed", "WeatherApiKey set", "Account 1: ApiKey changed"
                }));
                Assert.That(string.Join(" ", changes), Does.Not.Contain("secret").And.Not.Contain("k2"));
            });
        }

        [Test]
        public void Describe_Accounts_AddedRemovedAndListsChanged()
        {
            var changes = SettingsDiff.Describe(
                """{"General":{},"Accounts":[{"ExcludedAlbums":[]},{"ShowFavorites":true}]}""",
                """{"General":{},"Accounts":[{"ExcludedAlbums":["354315e3-0000-0000-0000-000000000000"]}]}""");

            Assert.That(changes, Is.EquivalentTo(new[] { "Account 1: ExcludedAlbums changed", "Account 2 removed" }));
        }

        [Test]
        public void Describe_EmptyStringAndNull_CountAsTheSame()
        {
            var changes = SettingsDiff.Describe(
                """{"General":{"Webhook":null},"Accounts":[]}""",
                """{"General":{"Webhook":""},"Accounts":[{}]}""");

            Assert.That(changes, Is.EqualTo(new[] { "Account 1 added" }));
        }
    }

    [TestFixture]
    public class SettingsHistoryTests
    {
        private SqliteTestDb _db;

        [SetUp]
        public void Setup() => _db = new SqliteTestDb();

        [TearDown]
        public void TearDown() => _db.Dispose();

        private SettingsService CreateService() => new(
            _db,
            new ConfigLoader(NullLogger<ConfigLoader>.Instance),
            NullLogger<SettingsService>.Instance,
            new SettingsServiceOptions(_db.Directory));

        private static ServerSettings Settings(string layout, string adminPassword = "admin-1", string? secret = null) => new()
        {
            GeneralSettingsImpl = new GeneralSettings
            {
                Layout = layout,
                AdminPassword = adminPassword,
                AuthenticationSecret = secret
            },
            AccountsImpl = new List<ServerAccountSettings>
            {
                new() { ImmichServerUrl = "http://immich.local", ApiKey = "key123" }
            }
        };

        [Test]
        public async Task Update_KeepsThePreviousVersion_AndSummarisesEachSave()
        {
            var service = CreateService();
            await service.InitializeAsync();
            await service.UpdateAsync(Settings("single"));
            await service.UpdateAsync(Settings("splitview", secret: "s3cret"));

            var history = await service.GetHistoryAsync();

            Assert.Multiple(() =>
            {
                Assert.That(history.Select(h => h.Version), Is.EqualTo(new long[] { 2, 1 }));
                Assert.That(history[0].Changes, Is.EquivalentTo(new[]
                {
                    "Layout: single → splitview", "AuthenticationSecret set"
                }));
                Assert.That(history[1].Changes, Is.EqualTo(new[] { "First settings" }));
            });
        }

        [Test]
        public async Task Restore_MakesAnOldVersionCurrent_AsANewVersion_KeepingTheAdminPassword()
        {
            var service = CreateService();
            await service.InitializeAsync();
            await service.UpdateAsync(Settings("single", adminPassword: "first"));
            await service.UpdateAsync(Settings("splitview", adminPassword: "second"));

            var restored = await service.RestoreAsync(1);

            var history = await service.GetHistoryAsync();
            Assert.Multiple(() =>
            {
                Assert.That(restored, Is.Not.Null);
                Assert.That(service.Current.GeneralSettings.Layout, Is.EqualTo("single"));
                Assert.That(service.GetRawSettings().GeneralSettingsImpl!.AdminPassword, Is.EqualTo("second"));
                Assert.That(history.First().Version, Is.EqualTo(3));
                Assert.That(history.First().Changes, Is.EqualTo(new[] { "Restored version 1", "Layout: splitview → single" }));
            });
        }

        [Test]
        public async Task Restore_StaysLabelledAfterTheNextSave()
        {
            var service = CreateService();
            await service.InitializeAsync();
            await service.UpdateAsync(Settings("single"));
            await service.UpdateAsync(Settings("splitview"));
            await service.RestoreAsync(1);
            var next = Settings("single");
            next.GeneralSettingsImpl!.Interval = 99;
            await service.UpdateAsync(next);

            var history = await service.GetHistoryAsync();

            Assert.Multiple(() =>
            {
                Assert.That(history[0].Changes, Does.Not.Contain("Restored version 1"));
                Assert.That(history[1].Version, Is.EqualTo(3));
                Assert.That(history[1].Changes.First(), Is.EqualTo("Restored version 1"));
            });
        }

        [Test]
        public async Task Restore_UnknownVersion_ReturnsNull()
        {
            var service = CreateService();
            await service.InitializeAsync();
            await service.UpdateAsync(Settings("single"));

            Assert.That(await service.RestoreAsync(42), Is.Null);
        }

        [Test]
        public async Task History_KeepsOnlyTheNewestVersions()
        {
            var service = CreateService();
            await service.InitializeAsync();
            for (var i = 0; i < SettingsService.HistoryLimit + 5; i++)
            {
                var settings = Settings("single");
                settings.GeneralSettingsImpl!.Interval = 10 + i;
                await service.UpdateAsync(settings);
            }

            await using var db = _db.CreateDbContext();
            var history = await service.GetHistoryAsync();
            Assert.Multiple(async () =>
            {
                Assert.That(await db.SettingsHistory.CountAsync(), Is.EqualTo(SettingsService.HistoryLimit));
                Assert.That(history, Has.Count.EqualTo(SettingsService.HistoryLimit + 1));
                Assert.That(history.Last().Changes, Is.EqualTo(new[] { "Oldest kept version" }));
            });
        }
    }
}
