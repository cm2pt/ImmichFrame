using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ImmichFrame.Core.Api;
using ImmichFrame.WebApi.Helpers;
using ImmichFrame.WebApi.Models;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Helpers
{
    [TestFixture]
    public class ImmichAccountLibraryTests
    {
        private static readonly Guid AlbumA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid AlbumB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid Mia = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid Hidden = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid Unnamed = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid Leo = Guid.Parse("66666666-6666-6666-6666-666666666666");
        private static readonly Guid TagHolidays = Guid.Parse("77777777-7777-7777-7777-777777777777");

        /// <summary>Answers Immich API requests by path; records the statistics request bodies.</summary>
        private sealed class FakeImmich : HttpMessageHandler
        {
            public bool FailTags { get; init; }
            public bool DenyUserRead { get; init; }
            public List<string> StatisticsBodies { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var path = request.RequestUri!.AbsolutePath;
                string body;
                if (path.EndsWith("/users/me") && DenyUserRead)
                {
                    return new HttpResponseMessage(HttpStatusCode.Forbidden)
                    {
                        Content = new StringContent("""{"message":"Missing required permission: user.read"}""", Encoding.UTF8, "application/json")
                    };
                }
                if (path.EndsWith("/users/me"))
                {
                    body = Json(new UserAdminResponseDto
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "Alex", Email = "alex@example.com",
                        ClusterGroupId = Guid.Empty, OauthId = "", ProfileImagePath = "", Status = UserStatus.Active
                    });
                }
                else if (path.EndsWith("/albums"))
                {
                    body = Json(new[] { Album(AlbumB, "Summer 2025", 1284, true), Album(AlbumA, "Hide from frame", 18, false) });
                }
                else if (path.EndsWith("/people"))
                {
                    var page = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["page"];
                    body = page == "1"
                        ? Json(new PeopleResponseDto
                        {
                            Total = 4, Hidden = 1, HasNextPage = true,
                            People = new[] { Person(Mia, "Mia", false), Person(Hidden, "Old friend", true), Person(Unnamed, "", false) }
                        })
                        : Json(new PeopleResponseDto
                        {
                            Total = 4, Hidden = 1, HasNextPage = false,
                            People = new[] { Person(Leo, "Leo", false) }
                        });
                }
                else if (path.EndsWith("/tags"))
                {
                    if (FailTags)
                    {
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") };
                    }

                    body = Json(new[] { new TagResponseDto { Id = TagHolidays, Name = "2025", Value = "Holidays/2025" } });
                }
                else if (path.EndsWith("/search/statistics"))
                {
                    var json = await request.Content!.ReadAsStringAsync(ct);
                    lock (StatisticsBodies) StatisticsBodies.Add(json);
                    var filter = JsonNode.Parse(json)!["filter"]!;
                    var total = filter["isFavorite"] != null ? 412 : filter["or"] != null ? 1302 : 7176;
                    body = $$"""{"total":{{total}}}""";
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
            }
        }

        private static string Json(object value) => Newtonsoft.Json.JsonConvert.SerializeObject(value);

        private static AlbumResponseDto Album(Guid id, string name, long count, bool shared) => new()
        {
            Id = id, AlbumName = name, AssetCount = count, Shared = shared, Description = ""
        };

        private static PersonResponseDto Person(Guid id, string name, bool hidden) => new()
        {
            Id = id, Name = name, IsHidden = hidden, ThumbnailPath = ""
        };

        private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
        }

        private static ServerAccountSettings Account() => new()
        {
            ImmichServerUrl = "http://immich.local",
            ApiKey = "key",
            ShowFavorites = true,
            Albums = new List<Guid> { AlbumB }
        };

        [Test]
        public async Task Load_MapsAlbumsPeopleTagsAndCounts()
        {
            var immich = new FakeImmich();

            var library = await ImmichAccountLibrary.Load(Account(), new Factory(immich));

            Assert.Multiple(() =>
            {
                Assert.That(library.Success, Is.True, library.Message);
                Assert.That(library.UserName, Is.EqualTo("Alex"));
                Assert.That(library.Albums.Select(a => (a.Name, a.AssetCount, a.Shared)).ToArray(), Is.EqualTo(new[]
                {
                    ("Hide from frame", 18, false), ("Summer 2025", 1284, true)
                }));
                // Named, visible people from every page, sorted by name
                Assert.That(library.People.Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "Leo", "Mia" }));
                Assert.That(library.Tags.Single().Name, Is.EqualTo("Holidays/2025"));
                Assert.That(library.TotalAssets, Is.EqualTo(7176));
                Assert.That(library.FavoriteCount, Is.EqualTo(412));
                Assert.That(library.MatchingAssets, Is.EqualTo(1302));
            });
        }

        /// <summary>
        /// REGRESSION: a read-only key without user.read (the common frame key) made the whole
        /// library report a failure, although only the label was missing.
        /// </summary>
        [Test]
        public async Task Load_KeyWithoutUserRead_StillSucceedsWithoutAName()
        {
            var library = await ImmichAccountLibrary.Load(Account(), new Factory(new FakeImmich { DenyUserRead = true }));

            Assert.Multiple(() =>
            {
                Assert.That(library.Success, Is.True, library.Message);
                Assert.That(library.UserName, Is.Null);
                Assert.That(library.Albums, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task Load_OneFailingPart_KeepsTheRest()
        {
            var immich = new FakeImmich { FailTags = true };

            var library = await ImmichAccountLibrary.Load(Account(), new Factory(immich));

            Assert.Multiple(() =>
            {
                Assert.That(library.Success, Is.False);
                Assert.That(library.Message, Does.Contain("tags"));
                Assert.That(library.Albums, Has.Count.EqualTo(2));
                Assert.That(library.People, Has.Count.EqualTo(2));
                Assert.That(library.TotalAssets, Is.EqualTo(7176));
            });
        }

        [Test]
        public async Task Load_AccountWithTags_SkipsTheMatchingCountWhenTagsFail()
        {
            var account = Account();
            account.Tags = new List<string> { "Holidays/2025" };

            var library = await ImmichAccountLibrary.Load(account, new Factory(new FakeImmich { FailTags = true }));

            Assert.That(library.MatchingAssets, Is.Null);
        }
        [Test]
        public async Task CountMatching_CountsWithTheAccountsFilter()
        {
            var immich = new FakeImmich();

            var result = await ImmichAccountLibrary.CountMatching(Account(), new Factory(immich));

            Assert.Multiple(() =>
            {
                Assert.That(result.Count, Is.EqualTo(1302), result.Message);
                Assert.That(immich.StatisticsBodies, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task CountMatching_ExcludedTagIsResolvedToItsId()
        {
            var immich = new FakeImmich();
            var account = Account();
            account.ExcludedTags = new List<string> { "Holidays/2025" };

            await ImmichAccountLibrary.CountMatching(account, new Factory(immich));

            Assert.That(immich.StatisticsBodies.Single(), Does.Contain(TagHolidays.ToString()));
        }

        [Test]
        public async Task CountMatching_OnlyAnUnknownTag_IsZeroWithoutSearching()
        {
            var immich = new FakeImmich();
            var account = new ServerAccountSettings
            {
                ImmichServerUrl = "http://immich.local", ApiKey = "key", ShowFavorites = false,
                Tags = new List<string> { "No/Such/Tag" }
            };

            var result = await ImmichAccountLibrary.CountMatching(account, new Factory(immich));

            Assert.Multiple(() =>
            {
                Assert.That(result.Count, Is.EqualTo(0));
                Assert.That(immich.StatisticsBodies, Is.Empty);
            });
        }

        [Test]
        public async Task CountMatching_FailingTags_ReportsInsteadOfCounting()
        {
            var account = Account();
            account.Tags = new List<string> { "Holidays/2025" };

            var result = await ImmichAccountLibrary.CountMatching(account, new Factory(new FakeImmich { FailTags = true }));

            Assert.Multiple(() =>
            {
                Assert.That(result.Count, Is.Null);
                Assert.That(result.Message, Does.Contain("Could not count"));
            });
        }
    }
}
