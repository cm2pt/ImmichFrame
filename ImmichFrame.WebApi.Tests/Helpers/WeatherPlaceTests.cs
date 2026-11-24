using ImmichFrame.WebApi.Helpers;
using NUnit.Framework;

namespace ImmichFrame.WebApi.Tests.Helpers
{
    [TestFixture]
    public class WeatherPlaceTests
    {
        [TestCase("38.763,-9.174", 38.763, -9.174)]
        [TestCase(" 38.763 , -9.174 ", 38.763, -9.174)]
        [TestCase("-33.9,151.2", -33.9, 151.2)]
        public void TryParseLatLong_ReadsDotDecimals(string value, double lat, double lon)
        {
            Assert.That(WeatherPlace.TryParseLatLong(value, out var la, out var lo), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(la, Is.EqualTo(lat).Within(1e-9));
                Assert.That(lo, Is.EqualTo(lon).Within(1e-9));
            });
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("38.7")]
        [TestCase("38,7,-9")]
        [TestCase("abc,def")]
        [TestCase("91,0")]
        [TestCase("0,181")]
        public void TryParseLatLong_RejectsBadInput(string? value)
        {
            Assert.That(WeatherPlace.TryParseLatLong(value, out _, out _), Is.False);
        }

        [Test]
        public async Task Lookup_WithoutKey_DoesNotCallOut()
        {
            var result = await WeatherPlace.Lookup(new WeatherPlaceRequestDto { LatLong = "38.7,-9.1" });
            Assert.Multiple(() =>
            {
                Assert.That(result.Name, Is.Null);
                Assert.That(result.Message, Does.Contain("API key"));
            });
        }

        [Test]
        public async Task Lookup_BadCoordinates_ExplainsTheFormat()
        {
            var result = await WeatherPlace.Lookup(new WeatherPlaceRequestDto { ApiKey = "k", LatLong = "Lisbon" });
            Assert.That(result.Message, Does.Contain("latitude and longitude"));
        }
    }
}
