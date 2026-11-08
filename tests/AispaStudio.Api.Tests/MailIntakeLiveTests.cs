using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>Posteingang live: jede neue oder verarbeitete Nachricht wird per SignalR an die Rezeption des Spas gemeldet.</summary>
public sealed partial class MailIntakeTests
{
    [Fact]
    public async Task Neue_Mail_und_ihr_Ergebnis_kommen_live_im_Posteingang_an()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var changes = new List<Guid>();
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = Live(spa.Spa.Cookie);
        connection.On<InboxChangedEvent>(LiveHubContract.InboxChanged, change =>
        {
            lock (changes)
            {
                changes.Add(change.MailId);
                // Eingang und Ergebnis der Queue: mindestens zwei Meldungen für dieselbe Mail.
                if (changes.Count(id => id == change.MailId) >= 2)
                {
                    processed.TrySetResult();
                }
            }
        });
        await connection.StartAsync(Ct);
        await connection.InvokeAsync(LiveHubContract.WatchInbox, Ct);

        var id = await spa.DeliverAsync(spa.Guest, "Terminanfrage",
            $"Hallo, bitte eine {spa.Treatment.Name} am 01.03.2027 um 10 Uhr in {spa.Location.Name}. Eva Sommer", Ct);

        await processed.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        lock (changes)
        {
            Assert.All(changes, change => Assert.Equal(id, change));
        }
    }

    [Fact]
    public async Task Posteingang_live_nur_mit_Buchungsrecht_und_nur_fuers_eigene_Spa()
    {
        var spa = await MailSpa.CreateAsync(fixture, Ct);
        var therapist = await TestUsers.RegisterAsync(fixture, Ct, StaffRole.Therapist, [spa.Location.Id], spa.Employee.Id);
        await using var connection = Live(therapist);
        await connection.StartAsync(Ct);
        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync(LiveHubContract.WatchInbox, Ct));

        // Jedes Testkonto als Inhaber ist ein eigenes Spa: das andere hört nichts von dieser Mail.
        var other = await TestSpa.CreateAsync(fixture, Ct);
        var heard = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var stranger = Live(other.Cookie);
        stranger.On<InboxChangedEvent>(LiveHubContract.InboxChanged, change => heard.TrySetResult(change.MailId));
        await stranger.StartAsync(Ct);
        await stranger.InvokeAsync(LiveHubContract.WatchInbox, Ct);

        var mail = await spa.ReceiveAsync(spa.Guest, "Frage", "Haben Sie Parkplätze?", Ct);
        Assert.NotEqual(InboundMailStatus.Received, mail.Status);
        await Task.Delay(500, Ct);
        Assert.False(heard.Task.IsCompleted);
    }

    private HubConnection Live(string cookie) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.Factory.Server.BaseAddress, LiveHubContract.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = cookie;
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions = TestSpa.Json)
            .Build();
}
