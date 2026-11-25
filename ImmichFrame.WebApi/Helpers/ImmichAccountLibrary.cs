using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.WebApi.Helpers
{
    public class LibraryAlbumDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int AssetCount { get; set; }
        public bool Shared { get; set; }

        /// <summary>The album's cover photo, for <c>Admin/Accounts/{index}/Assets/{id}/Thumbnail</c>.</summary>
        public Guid? ThumbnailAssetId { get; set; }
    }

    public class LibraryPersonDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class LibraryTagDto
    {
        public Guid Id { get; set; }

        /// <summary>The full tag path (Immich's <c>value</c>), e.g. <c>Holidays/2025</c>, as used in the Tags setting.</summary>
        public string Name { get; set; } = string.Empty;
    }

    public class AccountLibraryDto
    {
        /// <summary>False when at least one part could not be read; the rest is still filled in.</summary>
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>The Immich user the API key belongs to (<c>GET /users/me</c>), to label the account.</summary>
        public string? UserName { get; set; }

        /// <summary>Images on the account's timeline (archived and hidden excluded), from Immich's search statistics.</summary>
        public int? TotalAssets { get; set; }

        /// <summary>Favourite images on the timeline, from Immich's search statistics.</summary>
        public int? FavoriteCount { get; set; }

        /// <summary>
        /// What the frame's library search draws from with exactly these settings (favourites,
        /// albums, people, tags, date range, rating, videos, archive, excluded albums), counted
        /// the way the slideshow counts it. "On this day" and birthday memories are not included.
        /// </summary>
        public int? MatchingAssets { get; set; }

        public List<LibraryAlbumDto> Albums { get; set; } = new();
        public List<LibraryPersonDto> People { get; set; } = new();
        public List<LibraryTagDto> Tags { get; set; } = new();
    }

    public class MatchingCountDto
    {
        /// <summary>Photos the library search draws from with these settings; null when it couldn't be counted.</summary>
        public int? Count { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Reads what an account can pick from (albums, named people, tags) and two counts, so the
    /// admin UI can offer names instead of ids. Every part is best-effort: one failing Immich
    /// call leaves the others intact and is reported in <see cref="AccountLibraryDto.Message"/>.
    /// </summary>
    public static class ImmichAccountLibrary
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
        private const int PeoplePageSize = 1000;

        public static async Task<AccountLibraryDto> Load(IAccountSettings account, IHttpClientFactory httpClientFactory)
        {
            var httpClient = httpClientFactory.CreateClient(ImmichApiHttpClientExtensions.ImmichApiAccountClient);
            httpClient.UseApiKey(account.ApiKey);
            var immichApi = new ImmichApi(account.ImmichServerUrl, httpClient);

            using var cts = new CancellationTokenSource(RequestTimeout);
            var token = cts.Token;
            var result = new AccountLibraryDto();
            var errors = new List<string>();

            async Task Try(string part, Func<Task> action)
            {
                try
                {
                    await action();
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add($"{part}: {ex.Message}");
                    }
                }
            }

            await Task.WhenAll(
                // Optional: only a label. A key without user.read (the usual read-only key) still
                // gets everything else, so this part never counts as a failure.
                Task.Run(async () =>
                {
                    try
                    {
                        var me = await immichApi.GetMyUserAsync(token);
                        result.UserName = string.IsNullOrWhiteSpace(me.Name) ? me.Email : me.Name;
                    }
                    catch (Exception)
                    {
                        result.UserName = null;
                    }
                }),
                Try("albums", async () =>
                {
                    var albums = await immichApi.GetAllAlbumsAsync(null, null, null, null, null, token);
                    result.Albums = albums
                        .Select(a => new LibraryAlbumDto
                        {
                            Id = a.Id,
                            Name = a.AlbumName ?? string.Empty,
                            AssetCount = (int)Math.Min(a.AssetCount, int.MaxValue),
                            Shared = a.Shared,
                            ThumbnailAssetId = a.AlbumThumbnailAssetId
                        })
                        .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                }),
                Try("people", async () =>
                {
                    var people = new List<PersonResponseDto>();
                    for (long page = 1; ; page++)
                    {
                        var response = await immichApi.GetAllPeopleAsync(null, null, null, null, null, page, null, null,
                            PeoplePageSize, false, token);
                        people.AddRange(response.People);
                        if (response.HasNextPage != true || response.People.Count == 0) break;
                    }

                    result.People = people
                        .Where(p => !p.IsHidden && !string.IsNullOrWhiteSpace(p.Name))
                        .Select(p => new LibraryPersonDto { Id = p.Id, Name = p.Name })
                        .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                }),
                Try("tags", async () =>
                {
                    var tags = await immichApi.GetAllTagsAsync(token);
                    result.Tags = tags
                        .Select(t => new LibraryTagDto { Id = t.Id, Name = t.Value ?? t.Name ?? string.Empty })
                        .Where(t => t.Name.Length > 0)
                        .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                }),
                Try("photo count", async () =>
                {
                    var stats = await immichApi.SearchAssetStatisticsAsync(
                        new StatisticsSearchDto { Filter = TimelineImages() }, token);
                    result.TotalAssets = ToInt(stats.Total);
                }),
                Try("favourite count", async () =>
                {
                    var filter = TimelineImages();
                    filter.IsFavorite = new BoolFilter { Eq = true };
                    var stats = await immichApi.SearchAssetStatisticsAsync(new StatisticsSearchDto { Filter = filter }, token);
                    result.FavoriteCount = ToInt(stats.Total);
                }));

            // After the tags: the Tags setting holds tag values, the search needs their ids.
            var tagsMissing = (account.Tags is { Count: > 0 } || account.ExcludedTags is { Count: > 0 })
                && errors.Any(e => e.StartsWith("tags:"));
            if (!tagsMissing)
            {
                await Try("matching photos", async () =>
                {
                    var tagIds = result.Tags
                        .Where(t => account.Tags.Contains(t.Name))
                        .Select(t => t.Id)
                        .ToList();
                    var excludedTagIds = result.Tags
                        .Where(t => (account.ExcludedTags ?? []).Contains(t.Name))
                        .Select(t => t.Id)
                        .ToList();
                    if (SearchFilters.HasConfiguredSource(account) && !SearchFilters.HasMatchingBranch(account, tagIds))
                    {
                        result.MatchingAssets = 0;
                        return;
                    }

                    var stats = await immichApi.SearchAssetStatisticsAsync(new StatisticsSearchDto
                    {
                        Filter = SearchFilters.ForAccount(account, tagIds, excludedTagIds)
                    }, token);
                    result.MatchingAssets = ToInt(stats.Total);
                });
            }

            result.Success = errors.Count == 0;
            result.Message = result.Success
                ? $"Read {result.Albums.Count} albums, {result.People.Count} named people and {result.Tags.Count} tags."
                : $"Could not read everything from {account.ImmichServerUrl}: {Summarize(errors)}";
            return result;
        }

        /// <summary>
        /// How many photos the frame's library search draws from with exactly these settings
        /// (sources, Never show, date range, rating, videos, archive), counted the way the
        /// slideshow counts it, for the admin page's live count. Memories are not included.
        /// </summary>
        public static async Task<MatchingCountDto> CountMatching(IAccountSettings account, IHttpClientFactory httpClientFactory)
        {
            var httpClient = httpClientFactory.CreateClient(ImmichApiHttpClientExtensions.ImmichApiAccountClient);
            httpClient.UseApiKey(account.ApiKey);
            var immichApi = new ImmichApi(account.ImmichServerUrl, httpClient);
            using var cts = new CancellationTokenSource(RequestTimeout);
            var token = cts.Token;

            try
            {
                List<Guid> tagIds = [], excludedTagIds = [];
                var wanted = account.Tags ?? [];
                var excluded = account.ExcludedTags ?? [];
                if (wanted.Count > 0 || excluded.Count > 0)
                {
                    // The settings hold tag values; the search needs their ids.
                    var byValue = (await immichApi.GetAllTagsAsync(token))
                        .Where(t => !string.IsNullOrEmpty(t.Value))
                        .GroupBy(t => t.Value)
                        .ToDictionary(g => g.Key, g => g.First().Id);
                    tagIds = wanted.Where(byValue.ContainsKey).Select(v => byValue[v]).ToList();
                    excludedTagIds = excluded.Where(byValue.ContainsKey).Select(v => byValue[v]).ToList();
                }

                if (SearchFilters.HasConfiguredSource(account) && !SearchFilters.HasMatchingBranch(account, tagIds))
                {
                    return new MatchingCountDto { Count = 0 };
                }

                var stats = await immichApi.SearchAssetStatisticsAsync(new StatisticsSearchDto
                {
                    Filter = SearchFilters.ForAccount(account, tagIds, excludedTagIds)
                }, token);
                return new MatchingCountDto { Count = ToInt(stats.Total) };
            }
            catch (Exception ex)
            {
                return new MatchingCountDto { Message = $"Could not count from {account.ImmichServerUrl}: {ex.Message}" };
            }
        }

        private static SearchFilter TimelineImages() => new()
        {
            TrashedAt = SearchFilters.NotTrashed(),
            Visibility = new EnumFilterAssetVisibility { In = new List<AssetVisibility> { AssetVisibility.Timeline } },
            Type = new EnumFilterAssetType { In = new List<AssetTypeEnum> { AssetTypeEnum.IMAGE } }
        };

        /// <summary>One line per distinct error, so an unreachable server isn't reported once per part.</summary>
        private static string Summarize(List<string> errors) => string.Join("; ", errors
            .Select(e => e.Split(": ", 2))
            .GroupBy(parts => parts.Length > 1 ? parts[1] : parts[0])
            .Select(g => $"{string.Join(", ", g.Select(parts => parts[0]))}: {g.Key}"));

        private static int ToInt(long value) => (int)Math.Min(value, int.MaxValue);
    }
}
