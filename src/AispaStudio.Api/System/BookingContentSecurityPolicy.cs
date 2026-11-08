namespace AispaStudio.Api.System;

/// <summary>
/// Content-Security-Policy des Kundenbereichs <c>/buchen/</c> (ADR 0080), zuerst nur als Bericht. Dieselbe Richtlinie steht in
/// <c>docker/nginx.conf</c>; ein Test hält beide gleich. Zum Durchsetzen wird in beiden der Header-Name auf
/// <c>Content-Security-Policy</c> umgestellt.
/// </summary>
internal static class BookingContentSecurityPolicy
{
    public const string HeaderName = "Content-Security-Policy-Report-Only";

    // Bezahlung läuft per Weiterleitung zu Stripe Checkout (Seitennavigation), deshalb braucht es keine Stripe-Quelle.
    public const string Value = "default-src 'self'; base-uri 'self'; object-src 'none'; form-action 'self'";
}
