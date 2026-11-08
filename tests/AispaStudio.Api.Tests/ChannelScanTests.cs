using AispaStudio.Api.Access;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Allow-Liste je Kanal (Slice 104): Was Anzeige, Mitarbeiter-App und Kunden-App erreichen, steht hier ausdrücklich. Ein neuer Endpunkt in einem dieser
/// Kanäle oder ein Endpunkt ohne Entscheidung (weder Anmeldung noch als öffentlich hier eingetragen) macht den Scan rot.
/// </summary>
public sealed class ChannelScanTests(ApiFixture fixture)
{
    /// <summary>Das Dashboard im Pausenraum: nur lesen, Gäste nur als Initialen (Slice 105); der Hub nur für das Abo der eigenen Anzeige.</summary>
    private static readonly string[] Display =
    [
        "* /hubs/live",
        "* /hubs/live/negotiate",
        "GET /api/display/board",
        "GET /api/display/session",
    ];

    /// <summary>Mitarbeiter-App: nur eigene Person und eigene Termine (Slice 106 erweitert die Liste).</summary>
    private static readonly string[] StaffApp =
    [
        "GET /api/auth/me",
        "GET /api/reports/locations/{locationId:guid}/day-plan",
        "GET /api/reports/locations/{locationId:guid}/management",
        "GET /api/reports/appointments/{appointmentId:guid}/card",
        "PUT /api/reports/appointments/{appointmentId:guid}/preparation/{stepId:guid}",
        "PUT /api/reports/appointments/{appointmentId:guid}/follow-up",
        "POST /api/appointments/{appointmentId:guid}/status",
        "POST /api/appointments/{appointmentId:guid}/begin",
        "POST /api/appointments/{appointmentId:guid}/finish",
        "PUT /api/appointments/{appointmentId:guid}/room-care/restock/{itemId:guid}",
        "GET /api/locations",
        "POST /api/auth/passkey/creation-options",
        "POST /api/auth/passkey",
        "POST /api/absence-requests/",
        "GET /api/absence-requests/mine",
        "GET /api/stock/items",
        "GET /api/stock/items/by-barcode",
        "GET /api/stock/locations/{locationId:guid}/storage-places",
        "POST /api/stock/locations/{locationId:guid}/movements",
        "POST /api/stock/locations/{locationId:guid}/storage-places/{placeId:guid}/count-sessions",
        "PUT /api/stock/locations/{locationId:guid}/count-sessions/{sessionId:guid}/lines/{itemId:guid}",
        "POST /api/stock/locations/{locationId:guid}/count-sessions/{sessionId:guid}/submit",
        "GET /api/staff-duties/reminders",
    ];

    /// <summary>Kunden-App: alles hinter dem Kunden-Cookie; jeder Endpunkt dort liest nur Termine und Daten des eigenen Kontos.</summary>
    private static readonly string[] CustomerApp =
    [
        "GET /api/online/me/",
        "GET /api/online/me/anamnesis/",
        "GET /api/online/me/appointments",
        "GET /api/online/me/appointments/{appointmentId:guid}/calendar.ics",
        "GET /api/online/me/vouchers",
        "GET /api/online/me/waitlist/",
        "POST /api/online/auth/passkey",
        "POST /api/online/auth/passkey/creation-options",
        "POST /api/online/me/anamnesis/",
        "POST /api/online/me/appointments/{appointmentId:guid}/cancel",
        "POST /api/online/me/appointments/{appointmentId:guid}/card/accept",
        "POST /api/online/me/appointments/{appointmentId:guid}/card/retry",
        "POST /api/online/me/appointments/{appointmentId:guid}/card/test",
        "POST /api/online/me/appointments/{appointmentId:guid}/confirm",
        "POST /api/online/me/appointments/{appointmentId:guid}/deposit/retry",
        "POST /api/online/me/appointments/{appointmentId:guid}/deposit/test",
        "POST /api/online/me/appointments/{appointmentId:guid}/move",
        "POST /api/online/me/waitlist/{entryId:guid}/cancel",
        "PUT /api/online/me/profile",
    ];

    /// <summary>Öffentlich ohne Anmeldung (Buchungsseite, Kopplung, Anmeldung, Kanäle von außen). Neue öffentliche Endpunkte kommen bewusst hierher.</summary>
    private static readonly string[] Public =
    [
        "* /alive",
        "* /health",
        "GET /api/auth/dev/accounts",
        "GET /api/auth/invitations/{token}",
        "GET /api/auth/quick-switch",
        "GET /api/auth/standalone/setup",
        "GET /api/online/auth/external/complete",
        "GET /api/online/auth/external/{provider}",
        "GET /api/online/auth/providers",
        "GET /api/system/modules",
        "GET /api/system/status",
        "GET /ics/{token}.ics",
        "GET /openapi/{documentName}.json",
        "POST /api/auth/dev/login",
        "POST /api/auth/dev/register",
        "POST /api/auth/device-code/redeem",
        "POST /api/auth/invitations/{token}/accept",
        "POST /api/auth/logout",
        "POST /api/auth/passkey/login",
        "POST /api/auth/passkey/request-options",
        "POST /api/auth/quick-switch",
        "POST /api/auth/standalone/login",
        "POST /api/auth/standalone/recover",
        "POST /api/auth/standalone/setup",
        "POST /api/online/auth/code",
        "POST /api/online/auth/code/verify",
        "POST /api/online/auth/logout",
        "POST /api/online/auth/passkey/login",
        "POST /api/online/auth/passkey/request-options",
        "POST /api/tenant/signup",
    ];

    [Fact]
    public void Jeder_Endpunkt_hat_eine_Kanal_Entscheidung_und_die_Kanaele_erreichen_genau_ihre_Liste()
    {
        var endpoints = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        var problems = ChannelScan.Problems(endpoints, Expected(), Public);

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Gegeben_ein_neuer_Endpunkt_ohne_Kanal_Freigabe_Wenn_der_Scan_laeuft_Dann_ist_er_rot()
    {
        RouteEndpoint[] endpoints =
        [
            Endpoint("GET", "/api/display/session", new AuthorizeAttribute(), new ChannelAllowed(AccessChannel.Display)),
            // Neu in der Gruppe der Anzeige, aber nicht in der Liste: würde dem Dashboard ungeprüft Daten öffnen.
            Endpoint("GET", "/api/display/guests", new AuthorizeAttribute(), new ChannelAllowed(AccessChannel.Display)),
            // Neu ohne Anmeldung und ohne Eintrag als öffentlich.
            Endpoint("POST", "/api/staff-app/absence"),
        ];

        var problems = ChannelScan.Problems(endpoints, [new(AccessChannel.Display, "GET /api/display/session")], []);

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.Contains("GET /api/display/guests", StringComparison.Ordinal) && p.Contains("Display", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("POST /api/staff-app/absence", StringComparison.Ordinal));
    }

    private static ChannelEndpoint[] Expected() =>
    [
        .. Display.Select(e => new ChannelEndpoint(AccessChannel.Display, e)),
        .. StaffApp.Select(e => new ChannelEndpoint(AccessChannel.StaffApp, e)),
        .. CustomerApp.Select(e => new ChannelEndpoint(AccessChannel.CustomerApp, e)),
    ];

    private static RouteEndpoint Endpoint(string method, string route, params object[] metadata) =>
        new(_ => Task.CompletedTask, RoutePatternFactory.Parse(route), 0,
            new EndpointMetadataCollection([new HttpMethodMetadata([method]), .. metadata]), route);
}
