using System.Text.RegularExpressions;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.MailIntake;

/// <summary>
/// Absender-Regel eines Spas (ADR 0062): welche Absender Buchungsplattformen sind und was mit ihren Mails geschieht.
/// <see cref="Pattern"/> ist klein geschrieben und entweder eine genaue Mailadresse (mit „@“) oder eine Domain, die auch für Unterdomains gilt.
/// </summary>
public sealed class SenderRule
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Pattern { get; set; }
    public SenderPlatform Platform { get; set; }
    public SenderHandling Handling { get; set; }
    public bool Active { get; set; } = true;
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Muster, Abgleich mit dem Absender und die vorbelegten Regeln.</summary>
internal static partial class SenderRules
{
    /// <summary>Domains, die jedes Spa anfangs für Treatwell eingetragen hat (die Unterdomain <c>partner.treatwell.com</c> ist mit <c>treatwell.com</c> gemeint).</summary>
    public static readonly string[] DefaultTreatwellDomains = ["treatwell.de", "treatwell.at", "treatwell.ch", "treatwell.com", "treatwell.co.uk"];

    /// <summary>Genaue Adresse oder Domain in einheitlicher Schreibweise; <c>null</c>, wenn es keines von beiden ist.</summary>
    public static string? NormalizePattern(string? input)
    {
        var text = input?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        text = text.TrimStart('@');
        if (text.StartsWith("*.", StringComparison.Ordinal))
        {
            text = text[2..];
        }

        return text.Contains('@', StringComparison.Ordinal)
            ? AddressPattern().IsMatch(text) ? text : null
            : DomainPattern().IsMatch(text) ? text : null;
    }

    /// <summary>Die Mailadresse aus einem Absender wie <c>Treatwell &lt;bookings@partner.treatwell.com&gt;</c>, klein geschrieben.</summary>
    public static string AddressOf(string from)
    {
        var open = from.LastIndexOf('<');
        var close = from.LastIndexOf('>');
        var address = open >= 0 && close > open ? from[(open + 1)..close] : from;
        return address.Trim().ToLowerInvariant();
    }

    /// <summary>Wie genau das Muster auf den Absender passt: 0 = gar nicht, sonst größer bei genauerem Treffer (Adresse vor Domain, längere Domain vor kürzerer).</summary>
    public static int Specificity(string pattern, string from)
    {
        var address = AddressOf(from);
        if (pattern.Contains('@', StringComparison.Ordinal))
        {
            return address == pattern ? int.MaxValue : 0;
        }

        var at = address.LastIndexOf('@');
        var domain = at < 0 ? "" : address[(at + 1)..];
        return domain == pattern || domain.EndsWith($".{pattern}", StringComparison.Ordinal) ? pattern.Length : 0;
    }

    /// <summary>Die genaueste aktive Regel, die zum Absender passt; <c>null</c>, wenn keine passt.</summary>
    public static SenderRule? Best(IEnumerable<SenderRule> rules, string from) =>
        rules.Where(r => r.Active).Select(r => (Rule: r, Score: Specificity(r.Pattern, from))).Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).Select(x => x.Rule).FirstOrDefault();

    [GeneratedRegex(@"^[a-z0-9._%+\-]+@[a-z0-9\-]+(\.[a-z0-9\-]+)+$")]
    private static partial Regex AddressPattern();

    [GeneratedRegex(@"^[a-z0-9\-]+(\.[a-z0-9\-]+)+$")]
    private static partial Regex DomainPattern();
}

/// <summary>
/// Absender-Regeln des laufenden Spas. Beim ersten Zugriff legt der Dienst die Standardregeln für Treatwell an (jedes Spa, auch ältere
/// und per Demo oder Registrierung neu angelegte); danach gehören die Regeln dem Spa und werden nie wieder ergänzt oder überschrieben.
/// </summary>
internal sealed class SenderRuleStore(AispaDbContext db, TenantContext tenants, TimeProvider time)
{
    public async Task<List<SenderRule>> ListAsync(CancellationToken ct)
    {
        await EnsureDefaultsAsync(ct);
        return await db.SenderRules.AsNoTracking().OrderBy(r => r.Platform).ThenBy(r => r.Pattern).ToListAsync(ct);
    }

    public async Task<SenderRule?> FindAsync(string from, CancellationToken ct) => SenderRules.Best(await ListAsync(ct), from);

    private async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        var tenantId = tenants.TenantId ?? throw new InvalidOperationException("Ohne Mandant gibt es keine Absender-Regeln.");
        if (await db.Tenants.AnyAsync(t => t.Id == tenantId && t.SenderRulesSeeded, ct))
        {
            return;
        }

        var now = time.GetUtcNow();
        foreach (var domain in SenderRules.DefaultTreatwellDomains)
        {
            // Unabhängig vom Change-Tracker und gegen gleichzeitige Aufrufe sicher: gibt es das Muster schon, passiert nichts.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO aispa."SenderRules" ("Id","TenantId","Pattern","Platform","Handling","Active","Note","CreatedAt")
                VALUES ({Guid.CreateVersion7()}, {tenantId}, {domain}, {nameof(SenderPlatform.Treatwell)}, {nameof(SenderHandling.Automatic)},
                        TRUE, {"Voreingestellt"}, {now})
                ON CONFLICT ("TenantId","Pattern") DO NOTHING
                """, ct);
        }

        await db.Tenants.Where(t => t.Id == tenantId).ExecuteUpdateAsync(s => s.SetProperty(t => t.SenderRulesSeeded, true), ct);
    }
}

/// <summary>Absender-Regeln verwalten (Verwaltung › Postfach &amp; Nummern).</summary>
internal static class SenderRuleEndpoints
{
    public static IEndpointRouteBuilder MapSenderRuleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/intake/sender-rules").WithTags("Terminannahme").RequirePermission(AccessRight.ManageOrganization);

        group.MapGet("/", async (SenderRuleStore store, CancellationToken ct) => (await store.ListAsync(ct)).Select(ToDto).ToList())
            .WithName("ListSenderRules")
            .WithSummary("Absender-Regeln des Spas: welche Absender Buchungsplattformen sind und was mit ihren Mails geschieht. Beim ersten Aufruf mit den Standardregeln für Treatwell.");

        group.MapPost("/", async Task<Results<Created<SenderRuleDto>, ValidationProblem>> (
                SenderRuleInput input, SenderRuleStore store, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var rules = await store.ListAsync(ct);
                var (pattern, errors) = Validate(input, null, rules);
                if (errors.Any || pattern is null)
                {
                    return errors.Problem();
                }

                var rule = new SenderRule
                {
                    Pattern = pattern,
                    Platform = input.Platform,
                    Handling = input.Handling,
                    Active = input.Active,
                    Note = Note(input.Note),
                    CreatedAt = time.GetUtcNow(),
                };
                db.SenderRules.Add(rule);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/intake/sender-rules/{rule.Id}", ToDto(rule));
            })
            .WithName("CreateSenderRule")
            .WithSummary("Neue Absender-Regel für eine Mailadresse oder Domain (gilt auch für Unterdomains).");

        group.MapPut("/{ruleId:guid}", async Task<Results<Ok<SenderRuleDto>, NotFound, ValidationProblem>> (
                Guid ruleId, SenderRuleInput input, SenderRuleStore store, AispaDbContext db, CancellationToken ct) =>
            {
                var rules = await store.ListAsync(ct);
                if (await db.SenderRules.FirstOrDefaultAsync(r => r.Id == ruleId, ct) is not { } rule)
                {
                    return TypedResults.NotFound();
                }

                var (pattern, errors) = Validate(input, ruleId, rules);
                if (errors.Any || pattern is null)
                {
                    return errors.Problem();
                }

                rule.Pattern = pattern;
                rule.Platform = input.Platform;
                rule.Handling = input.Handling;
                rule.Active = input.Active;
                rule.Note = Note(input.Note);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(rule));
            })
            .WithName("UpdateSenderRule")
            .WithSummary("Ändert eine Absender-Regel.");

        group.MapDelete("/{ruleId:guid}", async Task<Results<NoContent, NotFound>> (Guid ruleId, AispaDbContext db, CancellationToken ct) =>
                await db.SenderRules.Where(r => r.Id == ruleId).ExecuteDeleteAsync(ct) > 0 ? TypedResults.NoContent() : TypedResults.NotFound())
            .WithName("DeleteSenderRule")
            .WithSummary("Löscht eine Absender-Regel; Mails dieses Absenders bearbeitet die Terminannahme danach wie jede andere Anfrage.");

        return routes;
    }

    private static (string? Pattern, Errors Errors) Validate(SenderRuleInput input, Guid? ownId, IReadOnlyList<SenderRule> rules)
    {
        var errors = new Errors();
        var pattern = SenderRules.NormalizePattern(input.Pattern);
        errors.Check(pattern is not null, nameof(input.Pattern), "Bitte eine Mailadresse (name@domain.at) oder eine Domain (domain.at) angeben.");
        errors.Check(pattern is null || !rules.Any(r => r.Pattern == pattern && r.Id != ownId), nameof(input.Pattern), "Für dieses Muster gibt es schon eine Regel.");
        errors.Check((input.Note?.Length ?? 0) <= 200, nameof(input.Note), "Höchstens 200 Zeichen.");
        return (pattern, errors);
    }

    private static string? Note(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private static SenderRuleDto ToDto(SenderRule r) => new(r.Id, r.Pattern, r.Platform, r.Handling, r.Active, r.Note);
}

internal static class SenderRuleModel
{
    public static void Configure(ModelBuilder builder) =>
        builder.Entity<SenderRule>(e =>
        {
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.Pattern).HasMaxLength(320);
            e.Property(r => r.Platform).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Handling).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Note).HasMaxLength(200);
            e.HasIndex(r => r.Pattern).IsUnique();
        });
}
