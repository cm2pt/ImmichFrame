using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;

namespace ImmichFrame.Core.Logic.Pool;

/// <summary>
/// The account's library search. In the default random order each request asks Immich for a
/// random sample. In the other orders (<see cref="SlideshowOrders"/>) it deals from a deck: the
/// ids of every matching photo, shuffled or by date taken, walked once and then rebuilt, so
/// photos added in the meantime join the next round.
/// </summary>
public class AccountSearchPool(
    IApiCache apiCache,
    ImmichApi immichApi,
    IAccountSettings accountSettings,
    Func<string>? order = null,
    Random? random = null) : IAssetPool
{
    /// <summary>Immich's largest page for a metadata search.</summary>
    internal const int DeckPageSize = 1000;

    /// <summary>A cap on the deck, so a huge library can't hold the server's memory.</summary>
    internal const int MaxDeckSize = 250_000;

    private readonly Random _random = random ?? Random.Shared;
    private readonly SemaphoreSlim _deckLock = new(1, 1);
    private List<Guid>? _deck;
    private string? _deckOrder;
    private int _position;

    private string Order => SlideshowOrders.Normalize(order?.Invoke());

    public async Task<long> GetAssetCount(CancellationToken ct = default)
    {
        var (tagIds, excludedTagIds) = await ResolveTags(ct);
        if (SearchFilters.HasConfiguredSource(accountSettings) && !SearchFilters.HasMatchingBranch(accountSettings, tagIds))
        {
            return 0;
        }

        var filter = SearchFilters.ForAccount(accountSettings, tagIds, excludedTagIds);
        return await apiCache.GetOrAddAsync("stats", async () =>
        {
            var stats = await immichApi.SearchAssetStatisticsAsync(new StatisticsSearchDto
            {
                Filter = filter
            }, ct);
            return stats.Total;
        });
    }

    public Task<IEnumerable<AssetResponseDto>> GetAssets(int requested, CancellationToken ct = default) =>
        Take(requested, advance: true, ct);

    /// <summary>The photos that come next, without using them up (the admin page's "Up next").</summary>
    public Task<IEnumerable<AssetResponseDto>> PeekAssets(int requested, CancellationToken ct = default) =>
        Take(requested, advance: false, ct);

    private async Task<IEnumerable<AssetResponseDto>> Take(int requested, bool advance, CancellationToken ct)
    {
        var (tagIds, excludedTagIds) = await ResolveTags(ct);
        if (SearchFilters.HasConfiguredSource(accountSettings) && !SearchFilters.HasMatchingBranch(accountSettings, tagIds))
        {
            return [];
        }

        var filter = SearchFilters.ForAccount(accountSettings, tagIds, excludedTagIds);
        var currentOrder = Order;
        if (currentOrder == SlideshowOrders.Random)
        {
            return await immichApi.SearchRandomAsync(new RandomSearchDto
            {
                Size = requested,
                WithExif = true,
                WithPeople = true,
                Filter = filter
            }, ct);
        }

        return await Deal(requested, advance, currentOrder, filter, ct);
    }

    private async Task<IEnumerable<AssetResponseDto>> Deal(
        int requested, bool advance, string currentOrder, SearchFilter filter, CancellationToken ct)
    {
        var dealt = new List<AssetResponseDto>(Math.Max(requested, 0));
        await _deckLock.WaitAsync(ct);
        try
        {
            var position = _position;
            // A deck that was emptied once, or a library with no matches, gives up after one rebuild.
            var rebuilt = false;
            while (dealt.Count < requested)
            {
                if (_deck == null || _deckOrder != currentOrder || position >= _deck.Count)
                {
                    if (rebuilt && position >= (_deck?.Count ?? 0))
                    {
                        break;
                    }

                    _deck = await BuildDeck(currentOrder, filter, ct);
                    _deckOrder = currentOrder;
                    _position = position = 0;
                    rebuilt = true;
                    if (_deck.Count == 0)
                    {
                        break;
                    }
                }

                var batch = _deck.Skip(position).Take(requested - dealt.Count).ToList();
                position += batch.Count;
                var assets = await Task.WhenAll(batch.Select(id => FetchAsset(id, ct)));
                // Photos deleted since the deck was built are skipped.
                dealt.AddRange(assets.OfType<AssetResponseDto>());
            }

            if (advance)
            {
                _position = position;
            }
        }
        finally
        {
            _deckLock.Release();
        }

        return dealt;
    }

    private async Task<AssetResponseDto?> FetchAsset(Guid id, CancellationToken ct)
    {
        try
        {
            return await immichApi.GetAssetInfoAsync(id, null, null, ct);
        }
        catch (ApiException)
        {
            return null;
        }
    }

    private async Task<List<Guid>> BuildDeck(string currentOrder, SearchFilter filter, CancellationToken ct)
    {
        var direction = currentOrder == SlideshowOrders.NewestFirst ? AssetOrder.Desc : AssetOrder.Asc;
        var ids = new List<Guid>();
        string? cursor = null;
        long? page = null;
        do
        {
            var response = await immichApi.SearchAssetsAsync(null, null, new MetadataSearchDto
            {
                Filter = filter,
                Size = DeckPageSize,
                // Not the deprecated Order field: Immich rejects it next to Filter/OrderBy.
                OrderBy = new SearchOrder { Field = SearchOrderField.FileCreatedAt, Direction = direction },
                Cursor = cursor,
                Page = page
            }, ct);

            var items = response?.Assets?.Items ?? [];
            ids.AddRange(items.Select(a => a.Id));

            cursor = response?.Assets?.NextCursor;
            page = cursor == null && long.TryParse(response?.Assets?.NextPage, out var next) ? next : null;
            if (items.Count == 0)
            {
                break;
            }
        } while ((cursor != null || page != null) && ids.Count < MaxDeckSize);

        if (ids.Count > MaxDeckSize)
        {
            ids.RemoveRange(MaxDeckSize, ids.Count - MaxDeckSize);
        }

        if (currentOrder == SlideshowOrders.Shuffle)
        {
            // Fisher–Yates: every order equally likely.
            for (var i = ids.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }
        }

        return ids;
    }

    private async Task<(IReadOnlyList<Guid> TagIds, IReadOnlyList<Guid> ExcludedTagIds)> ResolveTags(CancellationToken ct)
    {
        var wanted = accountSettings.Tags ?? [];
        var excluded = accountSettings.ExcludedTags ?? [];
        if (wanted.Count == 0 && excluded.Count == 0)
        {
            return ([], []);
        }

        var allTags = await apiCache.GetOrAddAsync($"allTags_{accountSettings.ImmichServerUrl}",
            () => immichApi.GetAllTagsAsync(ct));
        var tagValueToTag = allTags.ToDictionary(t => t.Value);
        List<Guid> Resolve(IEnumerable<string> values) => values
            .Where(tagValueToTag.ContainsKey)
            .Select(value => tagValueToTag[value].Id)
            .ToList();

        return (Resolve(wanted), Resolve(excluded));
    }
}
