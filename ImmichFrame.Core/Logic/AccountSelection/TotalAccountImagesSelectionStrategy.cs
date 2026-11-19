using ImmichFrame.Core.Api;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ImmichFrame.Core.Logic.AccountSelection;

public class TotalAccountImagesSelectionStrategy(
    ILogger<TotalAccountImagesSelectionStrategy> _logger,
    IAssetAccountTracker _tracker,
    IList<IAccountImmichFrameLogic> _accounts) : IAccountSelectionStrategy
{
    public async Task<(IAccountImmichFrameLogic, AssetResponseDto)?> GetNextAsset()
    {
        if (_accounts.Count == 0)
        {
            _logger.LogDebug("No accounts configured, returning no asset");
            return null;
        }

        var chosen = await _accounts.ChooseOne(logic => logic.GetTotalAssets());

        var asset = await chosen.GetNextAsset();
        if (asset != null)
        {
            await _tracker.RecordAssetLocation(chosen, asset.Id);
            return (chosen, asset);
        }

        _logger.LogDebug("No next asset found");
        return null;
    }

    private async Task<(IList<long>, long)> GetWeights(IList<IAccountImmichFrameLogic> accounts)
    {
        var weights = await Task.WhenAll(accounts.Select(GetTotalForAccount));
        return (weights, weights.Sum());
    }

    private async Task<IList<double>> GetProportions(IList<IAccountImmichFrameLogic> accounts)
    {
        var (totals, sum) = await GetWeights(accounts);
        if (sum == 0)
        {
            // No account reported any assets — avoid 0/0 NaN proportions; uniform weights
            // keep the (empty) batches intact.
            return accounts.Select(_ => 1d).ToList();
        }

        return totals.Select(t => (double)t / sum).ToList();
    }

    private Task<long> GetTotalForAccount(IImmichFrameLogic account)
    {
        return account.GetTotalAssets();
    }

    public async Task<IEnumerable<(IAccountImmichFrameLogic, AssetResponseDto)>> PeekAssets()
    {
        if (_accounts.Count == 0)
        {
            return [];
        }

        return _accounts.Any(a => a.KeepsOrder) ? await GetOrderedAssets(peek: true) : await GetAssets();
    }

    /// <summary>
    /// For the ordered slideshows: each account deals its share of a batch, by its photo count,
    /// and the shares are interleaved without shuffling. Nothing dealt is dropped, so "every
    /// photo once" holds across accounts too.
    /// </summary>
    private async Task<IEnumerable<(IAccountImmichFrameLogic, AssetResponseDto)>> GetOrderedAssets(bool peek)
    {
        var proportions = await GetProportions(_accounts);
        var sum = proportions.Sum();
        var lists = await Task.WhenAll(_accounts.Select(async (account, i) =>
        {
            var share = sum > 0 ? proportions[i] / sum : 0;
            var count = share > 0 ? Math.Max(1, (int)Math.Round(PooledImmichFrameLogic.BatchSize * share)) : 0;
            var assets = count == 0
                ? []
                : (peek ? await account.PeekAssets(count) : await account.GetAssets(count)).ToList();
            foreach (var asset in assets)
            {
                await _tracker.RecordAssetLocation(account, asset.Id);
            }

            return assets.Select(asset => (account, asset)).ToList();
        }));

        return lists.Interleave().ToList();
    }

    public async Task<IEnumerable<(IAccountImmichFrameLogic, AssetResponseDto)>> GetAssets()
    {
        if (_accounts.Count == 0)
        {
            _logger.LogDebug("No accounts configured, returning no assets");
            return Enumerable.Empty<(IAccountImmichFrameLogic, AssetResponseDto)>();
        }

        if (_accounts.Any(a => a.KeepsOrder))
        {
            return await GetOrderedAssets(peek: false);
        }

        var proportions = await GetProportions(_accounts);
        var maxAccount = proportions.Max();
        var adjustedProportions = proportions.Select(x => x / maxAccount).ToList();
        var assetLists = _accounts.Select(account => account.GetAssets()).ToList();

        var taskList = assetLists
            .Zip(_accounts, adjustedProportions)
            .Select(async tuple =>
            {
                var (task, account, proportion) = tuple;
                var assets = (await task).ToList();
                _logger.LogDebug("Retrieved {total} asset(s) for account [{account}], will take {proportion}%", assets.Count(), account, proportion * 100);
                return (account, assets.Shuffle().TakeProportional(proportion));
            });

        var accountAssetTupleList = await Task.WhenAll(taskList);

        _logger.LogDebug("Processing {} list(s) of asset(s) of length {}", accountAssetTupleList.Length, string.Join(",", accountAssetTupleList.Select(a => a.Item2.Count())));

        foreach (var accountAssetTuple in accountAssetTupleList)
        {
            foreach (var asset in accountAssetTuple.Item2)
            {
                await _tracker.RecordAssetLocation(accountAssetTuple.account, asset.Id);
            }
        }

        var assets = accountAssetTupleList.SelectMany(tuple => tuple.Item2.Select(asset => (tuple.account, asset))).ToList();

        _logger.LogDebug("Returning {count} asset(s)", assets.Count);

        return assets;
    }

    public T ForAsset<T>(Guid assetId, Func<IAccountImmichFrameLogic, T> f)
        => _tracker.ForAsset(assetId, f);
}