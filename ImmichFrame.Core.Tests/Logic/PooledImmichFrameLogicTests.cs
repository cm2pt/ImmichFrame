using System.Net;
using ImmichFrame.Core.Helpers;
using ImmichFrame.Core.Interfaces;
using ImmichFrame.Core.Logic;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Logic;

[TestFixture]
public class PooledImmichFrameLogicTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public readonly List<Uri> Requests = new();
        /// <summary>Answer full-size requests with Immich's redirect to the preview.</summary>
        public bool RedirectFullSize;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            if (RedirectFullSize && request.RequestUri!.Query.Contains("size=fullsize"))
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found) { Content = new StringContent("") };
                redirect.Headers.Location = new Uri("/api/assets/x/thumbnail?size=preview", UriKind.Relative);
                return Task.FromResult(redirect);
            }
            var content = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF });
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private static (PooledImmichFrameLogic Logic, RecordingHandler Handler) Create(bool fullResolution)
    {
        var handler = new RecordingHandler();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(ImmichApiHttpClientExtensions.ImmichApiAccountClient))
            .Returns(() => new HttpClient(handler));
        var account = new Mock<IAccountSettings>();
        account.SetupGet(a => a.ImmichServerUrl).Returns("https://photos.example");
        account.SetupGet(a => a.ApiKey).Returns("key");
        var general = new Mock<IGeneralSettings>();
        general.SetupGet(g => g.UseFullResolution).Returns(fullResolution);
        return (new PooledImmichFrameLogic(account.Object, general.Object, factory.Object), handler);
    }

    [TestCase(false, "size=preview")]
    [TestCase(true, "size=fullsize")]
    public async Task GetAsset_AsksForTheConfiguredSize(bool fullResolution, string expected)
    {
        var (logic, handler) = Create(fullResolution);

        var asset = await logic.GetAsset(Guid.NewGuid(), Api.AssetTypeEnum.IMAGE);

        Assert.That(handler.Requests.Single().Query, Does.Contain(expected));
        Assert.That(asset.FileName, fullResolution ? Does.Contain("-full.") : Does.Not.Contain("-full"));
    }

    // REGRESSION: Immich redirects full-size requests to the preview when it has no full-size copy.
    [Test]
    public async Task GetAsset_FullSizeRedirect_FallsBackToThePreview()
    {
        var (logic, handler) = Create(fullResolution: true);
        handler.RedirectFullSize = true;

        var asset = await logic.GetAsset(Guid.NewGuid(), Api.AssetTypeEnum.IMAGE);

        Assert.That(handler.Requests.Select(r => r.Query.Contains("size=preview")), Is.EqualTo(new[] { false, true }));
        Assert.That(asset.FileStream, Is.Not.Null);
    }

    [Test]
    public void CacheName_KeepsFullSizeCopiesApart()
    {
        var id = Guid.NewGuid();
        Assert.That(PooledImmichFrameLogic.CacheName(id, true), Is.Not.EqualTo(PooledImmichFrameLogic.CacheName(id, false)));
    }
}
