using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.Pool;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic.Pool;

[TestFixture]
public class AccountSearchPoolTests
{
    private Mock<IApiCache> _cache;
    private Mock<ImmichApi> _api;
    private Mock<IAccountSettings> _settings;
    private AccountSearchPool _pool;

    [SetUp]
    public void Setup()
    {
        _cache = new Mock<IApiCache>();
        _cache.Setup(c => c.GetOrAddAsync(It.IsAny<string>(), It.IsAny<Func<Task<ICollection<TagResponseDto>>>>()))
            .Returns<string, Func<Task<ICollection<TagResponseDto>>>>((_, factory) => factory());
        _cache.Setup(c => c.GetOrAddAsync(It.IsAny<string>(), It.IsAny<Func<Task<long>>>()))
            .Returns<string, Func<Task<long>>>((_, factory) => factory());
        _api = new Mock<ImmichApi>(null, null);
        _api.Setup(a => a.SearchRandomAsync(It.IsAny<RandomSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssetResponseDto>());
        _api.Setup(a => a.SearchAssetStatisticsAsync(It.IsAny<StatisticsSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchStatisticsResponseDto { Total = 0 });
        _settings = new Mock<IAccountSettings>();
        _settings.SetupGet(s => s.ImmichServerUrl).Returns("https://photos.example");
        _settings.SetupGet(s => s.Albums).Returns(new List<Guid>());
        _settings.SetupGet(s => s.People).Returns(new List<Guid>());
        _settings.SetupGet(s => s.Tags).Returns(new List<string>());
        _settings.SetupGet(s => s.ExcludedAlbums).Returns(new List<Guid>());
        _pool = new AccountSearchPool(_cache.Object, _api.Object, _settings.Object);
    }

    [Test]
    public async Task GetAssets_WithoutSources_SearchesTheLibrary()
    {
        await _pool.GetAssets(5);

        VerifyRandom(dto =>
            dto.Size == 5 &&
            dto.WithExif == true &&
            dto.WithPeople == true &&
            dto.Filter!.Or == null &&
            dto.Filter.TrashedAt != null &&
            dto.Filter.AlbumIds == null &&
            dto.Filter.Visibility!.In.Contains(AssetVisibility.Timeline) &&
            !dto.Filter.Visibility.In.Contains(AssetVisibility.Archive) &&
            dto.Filter.Type!.In.Contains(AssetTypeEnum.IMAGE) &&
            !dto.Filter.Type.In.Contains(AssetTypeEnum.VIDEO));
    }

    [Test]
    public async Task GetAssets_ShowVideos_AddsVideoType()
    {
        _settings.SetupGet(s => s.ShowVideos).Returns(true);

        await _pool.GetAssets(1);

        VerifyRandom(dto =>
            dto.Filter!.Type!.In.Contains(AssetTypeEnum.IMAGE) &&
            dto.Filter.Type.In.Contains(AssetTypeEnum.VIDEO));
    }

    [Test]
    public async Task GetAssets_ShowArchived_AddsArchiveVisibility()
    {
        _settings.SetupGet(s => s.ShowArchived).Returns(true);

        await _pool.GetAssets(1);

        VerifyRandom(dto =>
            dto.Filter!.Visibility!.In.Contains(AssetVisibility.Timeline) &&
            dto.Filter.Visibility.In.Contains(AssetVisibility.Archive));
    }

    [Test]
    public async Task GetAssets_Rating_SetsGte()
    {
        _settings.SetupGet(s => s.Rating).Returns(3);

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.Rating!.Gte == 3);
    }

    [Test]
    public async Task GetAssets_ImagesFromDays_SetsTakenAfter()
    {
        _settings.SetupGet(s => s.ImagesFromDays).Returns(10);
        var expected = DateTime.Today.AddDays(-10);

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.TakenAt!.Gt.HasValue && dto.Filter.TakenAt.Gt.Value.Date == expected.Date);
    }

    [Test]
    public async Task GetAssets_ImagesFromDate_WinsOverFromDays()
    {
        var fromDate = new DateTime(2020, 6, 1);
        _settings.SetupGet(s => s.ImagesFromDate).Returns(fromDate);
        _settings.SetupGet(s => s.ImagesFromDays).Returns(10);

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.TakenAt!.Gt.HasValue && dto.Filter.TakenAt.Gt.Value.Date == fromDate.Date);
    }

    [Test]
    public async Task GetAssets_ImagesUntilDate_SetsTakenBefore()
    {
        var until = new DateTime(2024, 1, 1);
        _settings.SetupGet(s => s.ImagesUntilDate).Returns(until);

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.TakenAt!.Lt.HasValue && dto.Filter.TakenAt.Lt.Value.Date == until.Date);
    }

    [Test]
    public async Task GetAssets_ExcludedAlbums_SentAsNone()
    {
        var excludedAlbumId = Guid.NewGuid();
        _settings.SetupGet(s => s.ExcludedAlbums).Returns(new List<Guid> { excludedAlbumId });

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.AlbumIds!.None.Contains(excludedAlbumId));
        _api.Verify(a => a.SearchAssetsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MetadataSearchDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetAssets_EmptyExcludedAlbums_OmitsAlbumFilter()
    {
        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.AlbumIds == null);
    }

    [Test]
    public async Task GetAssets_NullExcludedAlbums_OmitsAlbumFilter()
    {
        _settings.SetupGet(s => s.ExcludedAlbums).Returns((List<Guid>)null!);

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.AlbumIds == null);
    }

    [Test]
    public async Task GetAssets_WithSources_OrsOnlyConfiguredBranches()
    {
        var albumId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var excludedAlbumId = Guid.NewGuid();
        _settings.SetupGet(s => s.ShowFavorites).Returns(true);
        _settings.SetupGet(s => s.Albums).Returns(new List<Guid> { albumId });
        _settings.SetupGet(s => s.People).Returns(new List<Guid> { personId });
        _settings.SetupGet(s => s.ExcludedAlbums).Returns(new List<Guid> { excludedAlbumId });

        await _pool.GetAssets(1);

        VerifyRandom(dto =>
            dto.Filter!.AlbumIds!.None.Contains(excludedAlbumId) &&
            dto.Filter.Or.Count == 3 &&
            dto.Filter.Or.Any(branch => branch.IsFavorite != null && branch.IsFavorite.Eq) &&
            dto.Filter.Or.Any(branch => branch.AlbumIds != null && branch.AlbumIds.Any.Contains(albumId) && branch.AlbumIds.None == null) &&
            dto.Filter.Or.Any(branch => branch.PersonIds != null && branch.PersonIds.Any.Contains(personId)));
    }

    [Test]
    public async Task GetAssets_Tags_MatchFullPathCaseSensitively()
    {
        var match = Guid.NewGuid();
        var wrongCase = Guid.NewGuid();
        var wrongParent = Guid.NewGuid();
        _settings.SetupGet(s => s.Tags).Returns(new List<string> { "Parent/Child" });
        _api.Setup(a => a.GetAllTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TagResponseDto>
            {
                new() { Id = match, Value = "Parent/Child" },
                new() { Id = wrongCase, Value = "parent/child" },
                new() { Id = wrongParent, Value = "Other/Child" }
            });

        await _pool.GetAssets(1);

        VerifyRandom(dto =>
            dto.Filter!.Or.Count == 1 &&
            dto.Filter.Or.Single().TagIds!.Any.Contains(match) &&
            !dto.Filter.Or.Single().TagIds.Any.Contains(wrongCase) &&
            !dto.Filter.Or.Single().TagIds.Any.Contains(wrongParent));
    }

    [Test]
    public async Task GetAssets_UnmatchedTagsOnly_DoesNotSearch()
    {
        _settings.SetupGet(s => s.Tags).Returns(new List<string> { "missing" });
        _api.Setup(a => a.GetAllTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TagResponseDto>());

        var result = await _pool.GetAssets(5);

        Assert.That(result, Is.Empty);
        _api.Verify(a => a.SearchRandomAsync(It.IsAny<RandomSearchDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetAssetCount_UsesTheSameFilter()
    {
        _settings.SetupGet(s => s.ShowFavorites).Returns(true);
        _api.Setup(a => a.SearchAssetStatisticsAsync(It.IsAny<StatisticsSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchStatisticsResponseDto { Total = 12 });

        var count = await _pool.GetAssetCount();

        Assert.That(count, Is.EqualTo(12));
        _api.Verify(a => a.SearchAssetStatisticsAsync(It.Is<StatisticsSearchDto>(dto =>
            dto.Filter!.Or.Any(branch => branch.IsFavorite != null && branch.IsFavorite.Eq) &&
            dto.Filter.Visibility!.In.Contains(AssetVisibility.Timeline)
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetAssetCount_UnmatchedTagsOnly_IsZeroWithoutStatisticsCall()
    {
        _settings.SetupGet(s => s.Tags).Returns(new List<string> { "missing" });
        _api.Setup(a => a.GetAllTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TagResponseDto>());

        var count = await _pool.GetAssetCount();

        Assert.That(count, Is.EqualTo(0));
        _api.Verify(a => a.SearchAssetStatisticsAsync(It.IsAny<StatisticsSearchDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetAssetCount_SameFilter_IsServedFromCache()
    {
        using var cache = new ApiCache(TimeSpan.FromHours(1));
        var pool = new AccountSearchPool(cache, _api.Object, _settings.Object);
        _api.Setup(a => a.SearchAssetStatisticsAsync(It.IsAny<StatisticsSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchStatisticsResponseDto { Total = 7 });

        Assert.That(await pool.GetAssetCount(), Is.EqualTo(7));
        Assert.That(await pool.GetAssetCount(), Is.EqualTo(7));

        _api.Verify(a => a.SearchAssetStatisticsAsync(It.IsAny<StatisticsSearchDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetAssets_ExcludedPeople_SentAsNone()
    {
        var person = Guid.NewGuid();
        _settings.SetupGet(s => s.ExcludedPeople).Returns(new List<Guid> { person });

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.PersonIds!.None.Contains(person) && dto.Filter.Or == null);
    }

    [Test]
    public async Task GetAssets_ExcludedTags_ResolvedAndSentAsNone()
    {
        var tagId = Guid.NewGuid();
        _settings.SetupGet(s => s.ExcludedTags).Returns(new List<string> { "Screenshots" });
        _api.Setup(a => a.GetAllTagsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TagResponseDto> { new() { Id = tagId, Value = "Screenshots", Name = "Screenshots" } });

        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.TagIds!.None.Contains(tagId) && dto.Filter.Or == null);
    }

    [Test]
    public async Task GetAssets_NoExclusions_OmitsPersonAndTagFilters()
    {
        await _pool.GetAssets(1);

        VerifyRandom(dto => dto.Filter!.PersonIds == null && dto.Filter.TagIds == null);
    }

    // --- Ordered slideshows: the deck ---

    private List<Guid> SetupLibrary(int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
        _api.Setup(a => a.SearchAssetsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MetadataSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, MetadataSearchDto dto, CancellationToken _) =>
            {
                // Two pages of at most 3, by cursor.
                var start = dto.Cursor == null ? 0 : int.Parse(dto.Cursor);
                var page = ids.Skip(start).Take(3).Select(id => new AssetResponseDto { Id = id }).ToList();
                return new SearchResponseDto
                {
                    Assets = new SearchAssetResponseDto
                    {
                        Items = page,
                        NextCursor = start + 3 < ids.Count ? (start + 3).ToString() : null!
                    }
                };
            });
        _api.Setup(a => a.GetAssetInfoAsync(It.IsAny<Guid>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, string _, string _, CancellationToken _) => new AssetResponseDto { Id = id });
        return ids;
    }

    private AccountSearchPool OrderedPool(string order) =>
        new(_cache.Object, _api.Object, _settings.Object, () => order, new Random(42));

    [Test]
    public async Task Shuffle_ShowsEveryPhotoOnce_ThenReshuffles()
    {
        var ids = SetupLibrary(7);
        var pool = OrderedPool(SlideshowOrders.Shuffle);

        var firstRound = new List<Guid>();
        for (var i = 0; i < 7; i++)
        {
            firstRound.AddRange((await pool.GetAssets(1)).Select(a => a.Id));
        }

        Assert.That(firstRound, Is.EquivalentTo(ids), "each photo exactly once per round");
        var secondRound = (await pool.GetAssets(7)).Select(a => a.Id).ToList();
        Assert.That(secondRound, Is.EquivalentTo(ids), "a new round deals them all again");
        _api.Verify(a => a.SearchRandomAsync(It.IsAny<RandomSearchDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // REGRESSION: Immich 400s with "Deprecated field order cannot be combined with filter/orderBy",
    // so the deck asks with OrderBy only (the Order checks below lock that in).
    [Test]
    public async Task OldestFirst_KeepsTheLibraryOrder_AndAsksAscending()
    {
        var ids = SetupLibrary(5);
        var pool = OrderedPool(SlideshowOrders.OldestFirst);

        var dealt = (await pool.GetAssets(5)).Select(a => a.Id).ToList();

        Assert.That(dealt, Is.EqualTo(ids));
        _api.Verify(a => a.SearchAssetsAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.Is<MetadataSearchDto>(d => d.Order == null && d.OrderBy.Direction == AssetOrder.Asc),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task NewestFirst_AsksDescending()
    {
        SetupLibrary(2);
        await OrderedPool(SlideshowOrders.NewestFirst).GetAssets(1);

        _api.Verify(a => a.SearchAssetsAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.Is<MetadataSearchDto>(d => d.Order == null && d.OrderBy.Direction == AssetOrder.Desc),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task Peek_DoesNotUseUpTheDeck()
    {
        var ids = SetupLibrary(5);
        var pool = OrderedPool(SlideshowOrders.OldestFirst);

        var peeked = (await pool.PeekAssets(3)).Select(a => a.Id).ToList();
        var dealt = (await pool.GetAssets(3)).Select(a => a.Id).ToList();

        Assert.That(peeked, Is.EqualTo(ids.Take(3)));
        Assert.That(dealt, Is.EqualTo(peeked), "what Up next showed is what the frame shows");
    }

    [Test]
    public async Task Deck_SkipsPhotosDeletedSinceItWasBuilt()
    {
        var ids = SetupLibrary(3);
        _api.Setup(a => a.GetAssetInfoAsync(ids[1], null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("gone", 400, null, null, null));
        var pool = OrderedPool(SlideshowOrders.OldestFirst);

        var dealt = (await pool.GetAssets(2)).Select(a => a.Id).ToList();

        Assert.That(dealt, Is.EqualTo(new[] { ids[0], ids[2] }));
    }

    [Test]
    public async Task Deck_EmptyLibrary_ReturnsNothing()
    {
        SetupLibrary(0);
        Assert.That(await OrderedPool(SlideshowOrders.Shuffle).GetAssets(5), Is.Empty);
    }

    [Test]
    public async Task Deck_ChangingTheOrder_RebuildsIt()
    {
        var ids = SetupLibrary(4);
        var order = SlideshowOrders.OldestFirst;
        var pool = new AccountSearchPool(_cache.Object, _api.Object, _settings.Object, () => order);
        await pool.GetAssets(2);

        order = SlideshowOrders.NewestFirst;
        await pool.GetAssets(1);

        _api.Verify(a => a.SearchAssetsAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.Is<MetadataSearchDto>(d => d.OrderBy.Direction == AssetOrder.Desc), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private void VerifyRandom(Func<RandomSearchDto, bool> match) =>
        _api.Verify(a => a.SearchRandomAsync(It.Is<RandomSearchDto>(dto => match(dto)), It.IsAny<CancellationToken>()), Times.Once);
}
