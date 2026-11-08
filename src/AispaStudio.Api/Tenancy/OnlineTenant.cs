using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Tenancy;

/// <summary>
/// Mandant für den Kundenbereich (<c>/api/online</c>) und die Webhooks (<c>/api/hooks</c>), die ohne Anmeldung des Spa-Teams laufen. Reihenfolge:
/// <list type="number">
/// <item>ausdrücklich: Abfrage <c>?spa=</c> oder Kopfzeile <c>X-Aispa-Spa</c> (die Buchungsseite eines Spas setzt sie);</item>
/// <item>Standort in der Route;</item>
/// <item>Link aus einer Mail (Termin verwalten, Anamnese, Warteliste) oder zu einer Gutscheinbestellung über die Kundin des Links;</item>
/// <item>angemeldetes Kundenkonto über die verknüpfte Kundin;</item>
/// <item>gibt es nur ein Spa, dieses (lokale Entwicklung).</item>
/// </list>
/// Ohne Treffer bleibt der Mandant leer: Abfragen liefern nichts, Speichern schlägt fehl.
/// </summary>
internal static class OnlineTenant
{
    public const string Header = "X-Aispa-Spa";
    public const string Query = "spa";

    public static IApplicationBuilder UseOnlineTenant(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            // Webhooks der Anbieter (SMS, WhatsApp, Anrufe): nur ausdrücklich über ?spa= in der eingetragenen Webhook-Adresse.
            var hook = context.Request.Path.StartsWithSegments("/api/hooks");
            if (hook || context.Request.Path.StartsWithSegments("/api/online"))
            {
                var tenants = context.RequestServices.GetRequiredService<TenantContext>();
                if (await ResolveAsync(context, explicitOnly: hook) is { } tenantId)
                {
                    tenants.Use(tenantId);
                }
            }

            await next(context);
        });

    private static async Task<Guid?> ResolveAsync(HttpContext context, bool explicitOnly)
    {
        var db = context.RequestServices.GetRequiredService<AispaDbContext>();
        var ct = context.RequestAborted;

        var explicitSpa = context.Request.Query[Query].FirstOrDefault() ?? context.Request.Headers[Header].FirstOrDefault();
        if (Guid.TryParse(explicitSpa, out var spa))
        {
            return await db.Tenants.AnyAsync(t => t.Id == spa, ct) ? spa : null;
        }

        if (explicitOnly)
        {
            return null;
        }

        if (Guid.TryParse(context.Request.RouteValues["locationId"]?.ToString(), out var locationId))
        {
            return await TenantModel.TenantOfAsync(db.Locations.Where(l => l.Id == locationId), ct);
        }

        var online = context.RequestServices.GetRequiredService<OnlineDbContext>();
        Guid? customerId = null;
        if (context.Request.RouteValues["token"]?.ToString() is { Length: >= 20 and <= 100 } token)
        {
            var hash = AccessLinks.Hash(token);
            customerId = await online.AccessLinks.Where(l => l.TokenHash == hash).Select(l => (Guid?)l.CustomerId).FirstOrDefaultAsync(ct);
            // Link zu einer Gutscheinbestellung (Seite nach dem Bezahlen).
            customerId ??= await online.VoucherOrders.Where(o => o.TokenHash == hash).Select(o => (Guid?)o.CustomerId).FirstOrDefaultAsync(ct);
        }

        if (customerId is null && await CustomerAuth.TryGetAccountIdAsync(context) is { } accountId)
        {
            customerId = await online.Users.Where(a => a.Id == accountId).Select(a => a.CustomerId).FirstOrDefaultAsync(ct);
        }

        if (customerId is { } id)
        {
            return await TenantModel.TenantOfAsync(db.Customers.Where(c => c.Id == id), ct);
        }

        var single = await db.Tenants.Select(t => t.Id).Take(2).ToListAsync(ct);
        return single.Count == 1 ? single[0] : null;
    }
}
