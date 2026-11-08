using System.Collections.Concurrent;
using AispaStudio.Api.CalendarSubscriptions;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Abruf der Kalender-Abos im Test: nie ins Netz. Ein Test legt für seine Adresse einen ICS-Text oder einen Fehler fest; jede andere Adresse ist „nicht erreichbar“.
/// Adressen sind je Test eindeutig (<see cref="NewUrl"/>), damit sich parallele Tests nicht stören.
/// </summary>
internal sealed class FakeCalendarFeed : ICalendarFeedClient
{
    private readonly ConcurrentDictionary<string, FeedDownload> _answers = new();
    private readonly ConcurrentQueue<string> _requests = new();

    /// <summary>Alle abgerufenen Adressen in der Reihenfolge.</summary>
    public IReadOnlyCollection<string> Requests => _requests;

    public static string NewUrl() => $"https://feeds.example.org/{Guid.NewGuid():N}/calendar.ics";

    public void Serve(string url, string ics) => _answers[url] = FeedDownload.Ok(ics);

    public void Fail(string url, string error) => _answers[url] = FeedDownload.Fail(error);

    public Task<FeedDownload> GetAsync(string url, CancellationToken ct)
    {
        _requests.Enqueue(url);
        return Task.FromResult(_answers.TryGetValue(url, out var answer) ? answer : FeedDownload.Fail("Im Test nicht erreichbar."));
    }

    /// <summary>Kleiner Kalender mit ganztägigen Einträgen (Tag, Titel, optional Beschreibung).</summary>
    public static string Ics(params (DateOnly Day, string Title, string? Description)[] events) =>
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Test//DE\r\n"
        + string.Concat(events.Select((e, i) =>
            $"BEGIN:VEVENT\r\nUID:t{i}-{e.Day:yyyyMMdd}@test\r\nDTSTART;VALUE=DATE:{e.Day:yyyyMMdd}\r\nDTEND;VALUE=DATE:{e.Day.AddDays(1):yyyyMMdd}\r\nSUMMARY:{e.Title}\r\n"
            + (e.Description is null ? "" : $"DESCRIPTION:{e.Description}\r\n") + "END:VEVENT\r\n"))
        + "END:VCALENDAR\r\n";
}
