using AispaStudio.Api.System;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Bricht der Aufrufer ab (Windows-App nach 20 s, geschlossener Browser), endet die Anfrage still mit 499; jede andere Ausnahme läuft
/// weiter zur Fehlerbehandlung. So hält der Debugger nicht mehr bei jedem Abbruch an.
/// </summary>
public sealed class ClientAbortTests
{
    private static DefaultHttpContext Context(bool aborted) =>
        new() { RequestAborted = new CancellationToken(canceled: aborted) };

    [Fact]
    public async Task Abgebrochene_Anfrage_endet_still_mit_499()
    {
        var context = Context(aborted: true);

        await ClientAbort.HandleAsync(context, c => Task.FromCanceled(c.RequestAborted));

        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Abbruch_ohne_abgebrochene_Anfrage_laeuft_weiter()
    {
        var context = Context(aborted: false);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => ClientAbort.HandleAsync(context, _ => throw new OperationCanceledException()));
    }

    [Fact]
    public async Task Andere_Ausnahme_bei_abgebrochener_Anfrage_laeuft_weiter()
    {
        var context = Context(aborted: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ClientAbort.HandleAsync(context, _ => throw new InvalidOperationException()));
    }
}
