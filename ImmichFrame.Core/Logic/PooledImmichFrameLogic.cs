using ImmichFrame.Core.Api;
using ImmichFrame.Core.Exceptions;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.Pool;
using ImmichFrame.Core.Models;

namespace ImmichFrame.Core.Logic;

public class PooledImmichFrameLogic : IAccountImmichFrameLogic, IDisposable
{
    private readonly IGeneralSettings _generalSettings;
    private readonly IApiCache _apiCache;
    private readonly IAssetPool _pool;
    private readonly ImmichApi _immichApi;
    private readonly string _downloadLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ImageCache");

    public PooledImmichFrameLogic(IAccountSettings accountSettings, IGeneralSettings generalSettings, IHttpClientFactory httpClientFactory)
    {
        _generalSettings = generalSettings;

        var httpClient = httpClientFactory.CreateClient(ImmichApiHttpClientExtensions.ImmichApiAccountClient);
        AccountSettings = accountSettings;

        httpClient.UseApiKey(accountSettings.ApiKey);
        _immichApi = new ImmichApi(accountSettings.ImmichServerUrl, httpClient);

        _apiCache = new ApiCache(RefreshInterval(generalSettings.RefreshAlbumPeopleInterval));
        _pool = BuildPool(accountSettings);
    }

    private static TimeSpan RefreshInterval(int hours)
        => hours > 0 ? TimeSpan.FromHours(hours) : TimeSpan.FromMilliseconds(1);

    public IAccountSettings AccountSettings { get; }

    private IAssetPool BuildPool(IAccountSettings accountSettings)
    {
        var hasSource = accountSettings.ShowFavorites
            || accountSettings.ShowAssetsInAlbums
            || (accountSettings.Albums?.Any() ?? false)
            || (accountSettings.People?.Any() ?? false)
            || (accountSettings.Tags?.Any() ?? false);

        // With every memory type switched off, treat memories as off rather than as an empty source.
        var showMemories = accountSettings.ShowMemories
            && (accountSettings.ShowOnThisDayMemories || accountSettings.ShowBirthdayMemories);

        var pools = new List<IAssetPool>();

        // Memories-only skips the library search. Every other case needs it.
        if (!showMemories || hasSource)
        {
            pools.Add(new AccountSearchPool(_apiCache, _immichApi, accountSettings, () => _generalSettings.SlideshowOrder));
        }

        if (showMemories)
        {
            pools.Add(new MemoryAssetsPool(_immichApi, accountSettings));
        }

        return pools.Count == 1 ? pools[0] : new MultiAssetPool(pools);
    }

    public async Task<AssetResponseDto?> GetNextAsset()
    {
        return (await _pool.GetAssets(1)).FirstOrDefault();
    }

    public const int BatchSize = 25;

    public async Task<IEnumerable<AssetResponseDto>> GetAssets()
    {
        return await _pool.GetAssets(BatchSize);
    }

    public bool KeepsOrder => SlideshowOrders.Normalize(_generalSettings.SlideshowOrder) != SlideshowOrders.Random;

    public Task<IEnumerable<AssetResponseDto>> GetAssets(int count) => _pool.GetAssets(count);

    public Task<IEnumerable<AssetResponseDto>> PeekAssets() => _pool.PeekAssets(BatchSize);

    public Task<IEnumerable<AssetResponseDto>> PeekAssets(int count) => _pool.PeekAssets(count);

    public async Task<AssetResponseDto> GetAssetInfoById(Guid assetId) => await _immichApi.GetAssetInfoAsync(assetId, null, null);

    public async Task<IEnumerable<AssetFaceResponseDto>> GetAssetFacesById(Guid assetId) => await _immichApi.GetFacesAsync(assetId);

    public async Task<IEnumerable<AlbumResponseDto>> GetAlbumInfoById(Guid assetId) => await _immichApi.GetAllAlbumsAsync(assetId, null, null, null, null);

    public async Task<long> GetTotalAssets() => await _pool.GetAssetCount();

    public async Task<AssetResponse> GetAsset(Guid id, AssetTypeEnum? assetType = null, string? rangeHeader = null)
    {
        if (!assetType.HasValue)
        {
            var assetInfo = await _immichApi.GetAssetInfoAsync(id, null, null);
            if (assetInfo == null)
                throw new AssetNotFoundException($"Assetinfo for asset '{id}' was not found!");
            assetType = assetInfo.Type;
        }

        if (assetType == AssetTypeEnum.IMAGE)
        {
            var (fileName, contentType, fileStream) = await GetImageAsset(id);
            return new AssetResponse
            {
                FileName = fileName,
                ContentType = contentType,
                FileStream = fileStream,
                ContentRange = null,
                IsPartial = false,
                Owner = null,
                ContentLength = null
            };
        }

        if (assetType == AssetTypeEnum.VIDEO)
        {
            return await GetVideoAsset(id, rangeHeader);
        }

        throw new AssetNotFoundException($"Asset {id} is not a supported media type ({assetType}).");
    }
    /// <summary>The cache name of an image: full-size copies get their own, so the two never mix.</summary>
    public static string CacheName(Guid id, bool fullResolution) => fullResolution ? $"{id}-full" : id.ToString();

    private async Task<(string fileName, string ContentType, Stream fileStream)> GetImageAsset(Guid id)
    {
        var fullResolution = _generalSettings.UseFullResolution;
        var cacheName = CacheName(id, fullResolution);
        if (_generalSettings.DownloadImages)
        {
            if (!Directory.Exists(_downloadLocation))
            {
                Directory.CreateDirectory(_downloadLocation);
            }

            var file = Directory.GetFiles(_downloadLocation)
                .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == cacheName);

            if (!string.IsNullOrWhiteSpace(file))
            {
                if (_generalSettings.RenewImagesDuration > (DateTime.UtcNow - File.GetCreationTimeUtc(file)).Days)
                {
                    var fs = File.OpenRead(file);

                    var ex = Path.GetExtension(file).TrimStart('.');

                    return (Path.GetFileName(file), $"image/{ex}", fs);
                }

                File.Delete(file);
            }
        }

        // edited: true returns the version with Immich's crop or rotation applied (#680). Immich
        // falls back to the original when an asset has no edits. Full size (#614) is Immich's
        // full-size image: the original for formats browsers show, a converted copy otherwise.
        var size = fullResolution ? AssetMediaSize.Fullsize : AssetMediaSize.Preview;
        FileResponse data;
        try
        {
            data = await _immichApi.ViewAssetAsync(true, id, string.Empty, size, null);
        }
        catch (ApiException ex) when (fullResolution && ex.StatusCode is >= 300 and < 400)
        {
            // REGRESSION: with full-size generation off, Immich answers a full-size request with a
            // redirect to the preview, which the generated client treats as an error.
            data = await _immichApi.ViewAssetAsync(true, id, string.Empty, AssetMediaSize.Preview, null);
        }

        if (data == null)
            throw new AssetNotFoundException($"Asset {id} was not found!");

        var contentType = "";
        if (data.Headers.ContainsKey("Content-Type"))
        {
            contentType = data.Headers["Content-Type"].FirstOrDefault() ?? "";
        }

        var ext = contentType.ToLower() switch
        {
            "image/webp" => "webp",
            "image/png" => "png",
            _ => "jpeg"
        };
        var fileName = $"{cacheName}.{ext}";

        if (_generalSettings.DownloadImages)
        {
            var stream = data.Stream;

            var filePath = Path.Combine(_downloadLocation, fileName);

            // save to folder
            var fs = File.Create(filePath);
            await stream.CopyToAsync(fs);
            fs.Position = 0;
            return (Path.GetFileName(filePath), contentType, fs);
        }

        return (fileName, contentType, data.Stream);
    }

    private async Task<AssetResponse> GetVideoAsset(Guid id, string? rangeHeader = null)
    {
        var videoResponse = string.IsNullOrEmpty(rangeHeader)
            ? await _immichApi.PlayAssetVideoAsync(id, null, null)
            : await _immichApi.PlayAssetVideoWithRangeAsync(id, rangeHeader);

        var contentType = videoResponse.Headers.TryGetValue("Content-Type", out var ct)
            ? ct.FirstOrDefault() ?? "video/mp4"
            : "video/mp4";

        var contentRange = videoResponse.Headers.TryGetValue("Content-Range", out var cr)
            ? cr.FirstOrDefault()
            : null;

        long? contentLength = videoResponse.Headers.TryGetValue("Content-Length", out var cl)
            && long.TryParse(cl.FirstOrDefault(), out var clValue) ? clValue : null;

        return new AssetResponse
        {
            FileName = $"{id}.mp4",
            ContentType = contentType,
            FileStream = videoResponse.Stream,
            ContentRange = contentRange,
            IsPartial = videoResponse.StatusCode == 206,
            Owner = videoResponse,
            ContentLength = contentLength
        };
    }
    public async Task SendWebhookNotification(IWebhookNotification notification) =>
        await WebhookHelper.SendWebhookNotification(notification, _generalSettings.Webhook);

    public override string ToString() => $"Account Pool [{_immichApi.BaseUrl}]";

    public void Dispose() => (_apiCache as IDisposable)?.Dispose();
}
