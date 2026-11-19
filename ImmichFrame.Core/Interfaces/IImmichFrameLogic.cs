using ImmichFrame.Core.Api;
using ImmichFrame.Core.Models;


namespace ImmichFrame.Core.Interfaces
{
    public interface IImmichFrameLogic
    {
        public Task<AssetResponseDto?> GetNextAsset();
        public Task<IEnumerable<AssetResponseDto>> GetAssets();
        /// <summary>The assets that would come next, without using them up.</summary>
        public Task<IEnumerable<AssetResponseDto>> PeekAssets() => GetAssets();
        public Task<AssetResponseDto> GetAssetInfoById(Guid assetId);
        public Task<IEnumerable<AssetFaceResponseDto>> GetAssetFacesById(Guid assetId);
        public Task<IEnumerable<AlbumResponseDto>> GetAlbumInfoById(Guid assetId);
        public Task<AssetResponse> GetAsset(Guid id, AssetTypeEnum? assetType = null, string? rangeHeader = null);
        public Task<long> GetTotalAssets();
        public Task SendWebhookNotification(IWebhookNotification notification);
    }

    public interface IAccountImmichFrameLogic : IImmichFrameLogic
    {
        public IAccountSettings AccountSettings { get; }

        /// <summary>True when the slideshow deals in an order that a shuffle would break.</summary>
        public bool KeepsOrder => false;

        public Task<IEnumerable<AssetResponseDto>> GetAssets(int count) => GetAssets();

        public Task<IEnumerable<AssetResponseDto>> PeekAssets(int count) => GetAssets(count);

    }

    public interface IAccountSelectionStrategy
    {
        Task<(IAccountImmichFrameLogic, AssetResponseDto)?> GetNextAsset();
        Task<IEnumerable<(IAccountImmichFrameLogic, AssetResponseDto)>> GetAssets();
        Task<IEnumerable<(IAccountImmichFrameLogic, AssetResponseDto)>> PeekAssets() => GetAssets();
        T ForAsset<T>(Guid assetId, Func<IAccountImmichFrameLogic, T> f);
    }
}
