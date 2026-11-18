using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ImmichFrame.WebApi.Controllers;
using ImmichFrame.WebApi.Services;
using ImmichFrame.WebApi.Tests.Mocks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Controllers
{
    /// <summary>
    /// The admin endpoints behind the guided admin page: screens, settings history, info.
    /// Through the real Program.cs pipeline with a SQLite db in a temp config dir.
    /// </summary>
    [TestFixture]
    [NonParallelizable] // manipulates process-wide environment variables
    public class AdminDashboardTests
    {
        private const string AdminPassword = "test-admin-password";

        private string _configDir;
        private WebApplicationFactory<Program> _factory;

        [SetUp]
        public void Setup()
        {
            _configDir = Path.Combine(Path.GetTempPath(), "immichframe-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_configDir);
            Environment.SetEnvironmentVariable("IMMICHFRAME_CONFIG_PATH", _configDir);
            Environment.SetEnvironmentVariable("IMMICHFRAME_ADMIN_PASSWORD", AdminPassword);
            Environment.SetEnvironmentVariable(SettingsSourceParser.EnvVar, "database");

            var versionHandler = new Mock<HttpMessageHandler>().WithServerVersion();
            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services => services.UseMockHandler(versionHandler));
                });
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(SettingsSourceParser.EnvVar, "database");
            Environment.SetEnvironmentVariable("IMMICHFRAME_ADMIN_PASSWORD", null);
            _factory.Dispose();
            Environment.SetEnvironmentVariable("IMMICHFRAME_CONFIG_PATH", null);
            if (Directory.Exists(_configDir))
            {
                Directory.Delete(_configDir, true);
            }
        }

        private HttpClient CreateAdminClient()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AdminPassword);
            return client;
        }

        private static JsonNode SettingsBody(string layout, string? secret = null)
        {
            var body = JsonNode.Parse($$"""
                {
                    "General": { "Layout": "{{layout}}" },
                    "Accounts": [ { "ImmichServerUrl": "http://mock-immich-server.com", "ApiKey": "key123" } ]
                }
                """)!;
            if (secret != null)
            {
                body["General"]!["AuthenticationSecret"] = secret;
            }

            return body;
        }

        [Test]
        public async Task ClientRequests_WithIdentifier_ShowUpAsScreens_AndCanBeRenamedAndForgotten()
        {
            var admin = CreateAdminClient();
            (await admin.PutAsJsonAsync("/api/Admin/Settings", SettingsBody("single"))).EnsureSuccessStatusCode();

            var screen = _factory.CreateClient();
            screen.DefaultRequestHeaders.UserAgent.ParseAdd("ImmichFrameTest/1.0");
            (await screen.GetAsync("/api/Config?clientIdentifier=living-room")).EnsureSuccessStatusCode();

            var screens = JsonNode.Parse(await admin.GetStringAsync("/api/Admin/Screens"))!.AsArray();
            Assert.Multiple(() =>
            {
                Assert.That(screens, Has.Count.EqualTo(1));
                Assert.That((string)screens[0]!["clientIdentifier"]!, Is.EqualTo("living-room"));
                Assert.That((string)screens[0]!["userAgent"]!, Is.EqualTo("ImmichFrameTest/1.0"));
            });

            var rename = await admin.PutAsJsonAsync("/api/Admin/Screens/living-room/Name", new { Name = "Living room" });
            Assert.That(rename.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            var renamed = JsonNode.Parse(await admin.GetStringAsync("/api/Admin/Screens"))!.AsArray();
            Assert.That((string)renamed[0]!["name"]!, Is.EqualTo("Living room"));

            Assert.That((await admin.DeleteAsync("/api/Admin/Screens/living-room")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That((await admin.DeleteAsync("/api/Admin/Screens/living-room")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task ClientRequests_WithoutTheSecret_AreNotRecorded()
        {
            var admin = CreateAdminClient();
            (await admin.PutAsJsonAsync("/api/Admin/Settings", SettingsBody("single", secret: "frame-secret"))).EnsureSuccessStatusCode();

            // Config is anonymous, so this succeeds, but it must not add a screen.
            await _factory.CreateClient().GetAsync("/api/Config?clientIdentifier=intruder");
            var screen = _factory.CreateClient();
            screen.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "frame-secret");
            await screen.GetAsync("/api/Config?clientIdentifier=kitchen");

            var screens = JsonNode.Parse(await admin.GetStringAsync("/api/Admin/Screens"))!.AsArray();
            Assert.That(screens.Select(s => (string)s!["clientIdentifier"]!), Is.EqualTo(new[] { "kitchen" }));
        }

        [Test]
        public async Task Screens_RequireTheAdminPassword()
        {
            var response = await _factory.CreateClient().GetAsync("/api/Admin/Screens");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task History_ListsSaves_AndRestoreUndoesOne()
        {
            var admin = CreateAdminClient();
            (await admin.PutAsJsonAsync("/api/Admin/Settings", SettingsBody("single"))).EnsureSuccessStatusCode();
            (await admin.PutAsJsonAsync("/api/Admin/Settings", SettingsBody("splitview"))).EnsureSuccessStatusCode();

            var history = JsonNode.Parse(await admin.GetStringAsync("/api/Admin/Settings/History"))!.AsArray();
            var newest = history[0]!;
            Assert.That(newest["changes"]!.AsArray().Select(c => (string)c!), Does.Contain("Layout: single → splitview"));

            var previousVersion = (long)history[1]!["version"]!;
            var restore = await admin.PostAsync($"/api/Admin/Settings/History/{previousVersion}/Restore", null);
            restore.EnsureSuccessStatusCode();

            var settings = JsonNode.Parse(await admin.GetStringAsync("/api/Admin/Settings"))!;
            Assert.That((string)settings["General"]!["layout"]!, Is.EqualTo("single"));
        }

        [Test]
        public async Task Restore_UnknownVersion_Returns404()
        {
            var admin = CreateAdminClient();
            (await admin.PutAsJsonAsync("/api/Admin/Settings", SettingsBody("single"))).EnsureSuccessStatusCode();

            var response = await admin.PostAsync("/api/Admin/Settings/History/999/Restore", null);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task Info_ReportsTheVersion()
        {
            var info = JsonNode.Parse(await CreateAdminClient().GetStringAsync("/api/Admin/Info"))!;

            Assert.Multiple(() =>
            {
                Assert.That((string)info["version"]!, Does.Match(@"^\d+\.\d+"));
                // The fixture sets IMMICHFRAME_ADMIN_PASSWORD, which the admin page must know about.
                Assert.That((bool)info["passwordFromEnvironment"]!, Is.True);
            });
        }

        [Test]
        public async Task Library_WorksForAnUnsavedAccount_AndReportsValidationErrors()
        {
            var admin = CreateAdminClient();

            var response = await admin.PostAsJsonAsync("/api/Admin/Library",
                new { ImmichServerUrl = "http://mock-immich-server.com" }); // no key

            response.EnsureSuccessStatusCode();
            var library = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Assert.Multiple(() =>
            {
                Assert.That((bool)library["success"]!, Is.False);
                Assert.That((string)library["message"]!, Does.Contain("ApiKey"));
            });
        }

        [TestCase(0, 1)]
        [TestCase(4, 4)]
        [TestCase(500, AdminController.MaxPreviewCount)]
        public void PreviewCount_IsClamped(int requested, int expected)
        {
            Assert.That(AdminController.ClampPreviewCount(requested), Is.EqualTo(expected));
        }
    }
}
