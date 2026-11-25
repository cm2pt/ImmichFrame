using Moq;
using NUnit.Framework;
using ImmichFrame.Core.Api;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic.AccountSelection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImmichFrame.Core.Tests.Logic.AccountSelection
{
    [TestFixture]
    public class TotalAccountImagesSelectionStrategyTests
    {
        private Mock<IAssetAccountTracker> _mockTracker;

        [SetUp]
        public void Setup()
        {
            _mockTracker = new Mock<IAssetAccountTracker>();
            _mockTracker
                .Setup(t => t.RecordAssetLocation(It.IsAny<IAccountImmichFrameLogic>(), It.IsAny<Guid>()))
                .ReturnsAsync(true);
        }

        private TotalAccountImagesSelectionStrategy CreateStrategy(params IAccountImmichFrameLogic[] accounts)
            => new(NullLogger<TotalAccountImagesSelectionStrategy>.Instance, _mockTracker.Object, accounts.ToList());

        private static Mock<IAccountImmichFrameLogic> CreateAccount(long totalAssets, IEnumerable<AssetResponseDto> assets)
        {
            var mock = new Mock<IAccountImmichFrameLogic>();
            mock.Setup(a => a.GetTotalAssets()).ReturnsAsync(totalAssets);
            mock.Setup(a => a.GetAssets()).ReturnsAsync(assets);
            return mock;
        }

        private static AssetResponseDto CreateAsset() => new()
        {
            Id = Guid.NewGuid(),
            OriginalPath = "/path/asset.jpg",
            Type = AssetTypeEnum.IMAGE,
        };

        [Test]
        public async Task GetAssets_NoAccounts_ReturnsEmpty()
        {
            var strategy = CreateStrategy();

            var assets = await strategy.GetAssets();

            Assert.That(assets, Is.Empty);
        }

        [Test]
        public async Task GetAssets_AllAccountsEmpty_ReturnsEmptyWithoutNaN()
        {
            var account1 = CreateAccount(0, Enumerable.Empty<AssetResponseDto>());
            var account2 = CreateAccount(0, Enumerable.Empty<AssetResponseDto>());
            var strategy = CreateStrategy(account1.Object, account2.Object);

            var assets = await strategy.GetAssets();

            Assert.That(assets, Is.Empty);
        }

        [Test]
        public async Task GetAssets_UnevenAccounts_LargestAccountKeepsAllAssets()
        {
            var largeAssets = Enumerable.Range(0, 10).Select(_ => CreateAsset()).ToList();
            var smallAssets = Enumerable.Range(0, 10).Select(_ => CreateAsset()).ToList();
            var largeAccount = CreateAccount(1000, largeAssets);
            var smallAccount = CreateAccount(100, smallAssets);
            var strategy = CreateStrategy(largeAccount.Object, smallAccount.Object);

            var result = (await strategy.GetAssets()).ToList();

            var fromLarge = result.Count(t => ReferenceEquals(t.Item1, largeAccount.Object));
            var fromSmall = result.Count(t => ReferenceEquals(t.Item1, smallAccount.Object));
            Assert.Multiple(() =>
            {
                // The account with the most assets keeps its full batch, smaller ones are scaled.
                Assert.That(fromLarge, Is.EqualTo(10));
                Assert.That(fromSmall, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task GetAssets_RecordsEveryReturnedAssetWithTracker()
        {
            var assets = Enumerable.Range(0, 5).Select(_ => CreateAsset()).ToList();
            var account = CreateAccount(5, assets);
            var strategy = CreateStrategy(account.Object);

            var result = (await strategy.GetAssets()).ToList();

            Assert.That(result, Has.Count.EqualTo(5));
            _mockTracker.Verify(
                t => t.RecordAssetLocation(account.Object, It.IsAny<Guid>()),
                Times.Exactly(5));
        }

        [Test]
        public async Task GetNextAsset_ReturnsAssetAndRecordsLocation()
        {
            var asset = CreateAsset();
            var account = CreateAccount(1, new[] { asset });
            account.Setup(a => a.GetNextAsset()).ReturnsAsync(asset);
            var strategy = CreateStrategy(account.Object);

            var result = await strategy.GetNextAsset();

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Value.Item2.Id, Is.EqualTo(asset.Id));
            _mockTracker.Verify(t => t.RecordAssetLocation(account.Object, asset.Id), Times.Once);
        }
    
        [Test]
        public async Task GetAssets_OrderedAccounts_DealTheirShare_WithoutDroppingOrShuffling()
        {
            var big = Enumerable.Range(0, 20).Select(_ => CreateAsset()).ToList();
            var small = Enumerable.Range(0, 5).Select(_ => CreateAsset()).ToList();
            var a = new Mock<IAccountImmichFrameLogic>();
            a.Setup(x => x.GetTotalAssets()).ReturnsAsync(800);
            a.SetupGet(x => x.KeepsOrder).Returns(true);
            a.Setup(x => x.GetAssets(It.IsAny<int>())).ReturnsAsync((int n) => big.Take(n));
            var b = new Mock<IAccountImmichFrameLogic>();
            b.Setup(x => x.GetTotalAssets()).ReturnsAsync(200);
            b.SetupGet(x => x.KeepsOrder).Returns(true);
            b.Setup(x => x.GetAssets(It.IsAny<int>())).ReturnsAsync((int n) => small.Take(n));

            var assets = (await CreateStrategy(a.Object, b.Object).GetAssets()).Select(t => t.Item2).ToList();

            a.Verify(x => x.GetAssets(20), Times.Once);
            b.Verify(x => x.GetAssets(5), Times.Once);
            Assert.That(assets, Has.Count.EqualTo(25), "nothing dealt is dropped");
            Assert.That(assets.Where(big.Contains), Is.EqualTo(big), "each account keeps its order");
            Assert.That(assets.Where(small.Contains), Is.EqualTo(small));
        }

        // REGRESSION: every account got at least one photo per 25-photo batch, so a tiny account
        // cycled its few photos every batch, far above its share.
        [Test]
        public async Task GetAssets_OrderedAccounts_TinyAccountGetsItsShareAcrossBatches()
        {
            var a = new Mock<IAccountImmichFrameLogic>();
            a.Setup(x => x.GetTotalAssets()).ReturnsAsync(9900);
            a.SetupGet(x => x.KeepsOrder).Returns(true);
            a.Setup(x => x.GetAssets(It.IsAny<int>())).ReturnsAsync((int n) => Enumerable.Range(0, n).Select(_ => CreateAsset()));
            var b = new Mock<IAccountImmichFrameLogic>();
            b.Setup(x => x.GetTotalAssets()).ReturnsAsync(100);
            b.SetupGet(x => x.KeepsOrder).Returns(true);
            b.Setup(x => x.GetAssets(It.IsAny<int>())).ReturnsAsync((int n) => Enumerable.Range(0, n).Select(_ => CreateAsset()));
            var strategy = CreateStrategy(a.Object, b.Object);

            var total = 0;
            for (var batch = 0; batch < 8; batch++)
            {
                total += (await strategy.GetAssets()).Count();
            }

            // 1% of 8 × 25 = 2 photos, not 8.
            b.Verify(x => x.GetAssets(1), Times.Exactly(2));
            Assert.That(total, Is.EqualTo(200), "the carried fractions add up to whole batches");
        }

        [Test]
        public async Task PeekAssets_OrderedAccounts_PeekInsteadOfDealing()
        {
            var asset = CreateAsset();
            var a = new Mock<IAccountImmichFrameLogic>();
            a.Setup(x => x.GetTotalAssets()).ReturnsAsync(10);
            a.SetupGet(x => x.KeepsOrder).Returns(true);
            a.Setup(x => x.PeekAssets(It.IsAny<int>())).ReturnsAsync(new[] { asset });

            var peeked = await CreateStrategy(a.Object).PeekAssets();

            Assert.That(peeked.Single().Item2, Is.SameAs(asset));
            a.Verify(x => x.GetAssets(It.IsAny<int>()), Times.Never);
            a.Verify(x => x.GetAssets(), Times.Never);
        }
}
}
