using System.Net;
using ImmichFrame.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace ImmichFrame.Core.Tests.Services;

[TestFixture]
public class IcalCalendarServiceTests
{
    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }

    private static string Event(string uid, DateTime start) => $"""
        BEGIN:VEVENT
        UID:{uid}
        DTSTAMP:20260101T000000Z
        DTSTART:{start:yyyyMMdd}T100000
        DTEND:{start:yyyyMMdd}T110000
        SUMMARY:{uid}
        END:VEVENT
        """;

    private static async Task<List<string>> Summaries(int daysAhead)
    {
        var ics = string.Join("\n",
            "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:test",
            Event("today", DateTime.Today), Event("in-three-days", DateTime.Today.AddDays(3)),
            "END:VCALENDAR");
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new StubHandler(ics)));
        var settings = new Mock<IGeneralSettings>();
        settings.SetupGet(s => s.Webcalendars).Returns(new List<string> { "https://calendar.example/family.ics" });
        settings.SetupGet(s => s.CalendarDaysAhead).Returns(daysAhead);

        var service = new IcalCalendarService(settings.Object, NullLogger<IcalCalendarService>.Instance, factory.Object);
        return (await service.GetAppointments()).Select(a => a.Summary).ToList();
    }

    [Test]
    public async Task OneDay_ShowsTodayOnly() =>
        Assert.That(await Summaries(1), Is.EqualTo(new[] { "today" }));

    [Test]
    public async Task FiveDays_IncludesLaterEvents() =>
        Assert.That(await Summaries(5), Is.EquivalentTo(new[] { "today", "in-three-days" }));

    [Test]
    public async Task ZeroOrLess_StillShowsToday() =>
        Assert.That(await Summaries(0), Is.EqualTo(new[] { "today" }));
}
