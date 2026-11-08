using System.Net;
using AispaStudio.Contracts;
using Npgsql;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Der Befehlszähler gehört dem eigenen Request (T-049): Ein <c>ActivityListener</c> gilt für den ganzen Prozess und sah vorher auch die Befehle
/// fremder Verbindungen (Handler, Poller, Wiederholungen) sowie den Verbindungsaufbau (Aktivität „CONNECT“ ohne SQL-Text, im Trace des Requests).
/// Gezählt werden nur Befehle mit SQL-Text im Trace des Requests (Kopfzeile <c>traceparent</c>).
/// </summary>
[Collection(typeof(BookingCheckAlone))]
public sealed class CommandCounterTests(ApiFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly NextMonday => TestSpa.Monday.AddDays(7);

    /// <summary>Spa mit Schließzeit, Zwischenspeicher gefüllt: Eine Buchung wird jetzt mit 6 Befehlen abgelehnt.</summary>
    private async Task<TestSpa> SpaWithWarmCacheAsync()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await ClosureTests.CreateAsync(spa, ClosureTests.Input(NextMonday, kind: ClosureKind.Outage, fromTime: new TimeOnly(13, 0)));
        await RejectedAsync(spa);
        return spa;
    }

    private static async Task RejectedAsync(TestSpa spa) =>
        Assert.Equal(HttpStatusCode.Conflict, (await spa.BookAsync(TestSpa.At(15, 0, NextMonday), Ct)).StatusCode);

    private async Task ForeignCommandAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT 1 /* fremd */", connection);
        await command.ExecuteScalarAsync(ct);
    }

    [Fact]
    public async Task Given_ein_fremder_Befehl_When_der_Zaehler_den_eigenen_Request_misst_Then_zaehlt_er_ihn_nicht()
    {
        using var counter = new CommandCounter();

        await ForeignCommandAsync(Ct);

        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public async Task Given_fremde_Befehle_im_Hintergrund_When_ein_Request_gezaehlt_wird_Then_zaehlt_er_nur_dessen_Befehle()
    {
        var spa = await SpaWithWarmCacheAsync();
        using var counter = new CommandCounter();
        spa.Client.DefaultRequestHeaders.Add("traceparent", counter.Traceparent);
        using var done = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var foreign = Task.Run(async () =>
        {
            while (!done.IsCancellationRequested)
            {
                await ForeignCommandAsync(done.Token);
            }
        }, CancellationToken.None);
        try
        {
            await RejectedAsync(spa);
        }
        finally
        {
            await done.CancelAsync();
            await foreign.ContinueWith(_ => { }, CancellationToken.None);
        }

        Assert.InRange(counter.Count, 1, 6);
    }

    [Fact]
    public async Task Given_ein_kalter_Pool_When_ein_Request_gezaehlt_wird_Then_zaehlt_der_Verbindungsaufbau_nicht_mit()
    {
        var spa = await SpaWithWarmCacheAsync();
        using var counter = new CommandCounter();
        spa.Client.DefaultRequestHeaders.Add("traceparent", counter.Traceparent);
        NpgsqlConnection.ClearAllPools();

        await RejectedAsync(spa);

        Assert.InRange(counter.Count, 1, 6);
    }
}
