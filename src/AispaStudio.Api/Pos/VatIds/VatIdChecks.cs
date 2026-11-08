using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AispaStudio.Api.Data;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Pos.VatIds;

/// <summary>
/// Abfrage einer UID-Nummer im MwSt-Informationsaustauschsystem der EU (VIES). Mit der UID des eigenen Unternehmens als Anfragende
/// ist es eine qualifizierte Abfrage („Stufe 2“): VIES liefert dann Name, Anschrift und eine Abfragenummer als Nachweis.
/// Nur angefügt; das Ergebnis wird einmal eingetragen.
/// </summary>
public sealed class VatIdCheck
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string VatId { get; init; }
    public string? RequesterVatId { get; init; }
    public VatIdCheckStatus Status { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? ConsultationNumber { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset? CheckedAt { get; set; }
    public string? UserId { get; init; }
}

/// <summary>Queue: eine angelegte Abfrage an VIES schicken.</summary>
public sealed record SendVatIdCheck(Guid CheckId);

/// <summary>Ergebnis von VIES; <c>Message</c> ist bei einem Fehler des Dienstes gesetzt (dann ist <c>Valid</c> bedeutungslos).</summary>
public sealed record VatIdResult(bool Valid, string? Name, string? Address, string? ConsultationNumber, string? Message = null);

public interface IVatIdValidator
{
    Task<VatIdResult> CheckAsync(string vatId, string? requesterVatId, CancellationToken ct);
}

/// <summary>Ohne Netz für Entwicklung und Tests: UID-Nummern mit „000“ am Ende sind ungültig, mit „999“ antwortet der Dienst nicht.</summary>
internal sealed class TestVatIdValidator : IVatIdValidator
{
    public Task<VatIdResult> CheckAsync(string vatId, string? requesterVatId, CancellationToken ct) =>
        Task.FromResult(vatId.EndsWith("999", StringComparison.Ordinal)
            ? new VatIdResult(false, null, null, null, "Der Dienst des Mitgliedstaats antwortet gerade nicht (Test).")
            : vatId.EndsWith("000", StringComparison.Ordinal)
                ? new VatIdResult(false, null, null, null)
                : new VatIdResult(true, "Testfirma GmbH", "Teststraße 1\n1010 Wien", requesterVatId is null ? null : $"TEST{vatId[2..]}"));
}

/// <summary>VIES über die REST-Schnittstelle der Europäischen Kommission.</summary>
internal sealed class ViesVatIdValidator(IHttpClientFactory http) : IVatIdValidator
{
    public const string HttpClientName = "vies";
    private const string Url = "https://ec.europa.eu/taxation_customs/vies/rest-api/check-vat-number";

    public async Task<VatIdResult> CheckAsync(string vatId, string? requesterVatId, CancellationToken ct)
    {
        try
        {
            using var response = await http.CreateClient(HttpClientName).PostAsJsonAsync(Url, new ViesRequest(vatId[..2], vatId[2..],
                requesterVatId?[..2], requesterVatId?[2..]), ct);
            var body = await response.Content.ReadFromJsonAsync<ViesResponse>(ct);
            if (!response.IsSuccessStatusCode || body is null || body.ErrorWrappers is { Count: > 0 })
            {
                var code = body?.ErrorWrappers?.FirstOrDefault()?.Error ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
                return new VatIdResult(false, null, null, null, $"VIES hat die Abfrage nicht beantwortet ({code}). Später nochmals prüfen.");
            }

            return new VatIdResult(body.Valid, Clean(body.Name), Clean(body.Address), Clean(body.RequestIdentifier));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new VatIdResult(false, null, null, null, "VIES ist gerade nicht erreichbar. Später nochmals prüfen.");
        }
    }

    // VIES schreibt „---“, wenn ein Mitgliedstaat Name oder Anschrift nicht herausgibt.
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) || value.Trim() == "---" ? null : value.Trim();

    private sealed record ViesRequest(string CountryCode, string VatNumber, string? RequesterMemberStateCode, string? RequesterNumber);

    private sealed record ViesResponse(
        bool Valid,
        string? Name,
        string? Address,
        string? RequestIdentifier,
        [property: JsonPropertyName("errorWrappers")] List<ViesError>? ErrorWrappers);

    private sealed record ViesError(string? Error, string? Message);
}

public static class SendVatIdCheckHandler
{
    public static async Task Handle(SendVatIdCheck message, IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AispaDbContext>();
        if (await TenantModel.TenantOfAsync(db.VatIdChecks.Where(c => c.Id == message.CheckId), ct) is not { } tenantId)
        {
            return;
        }

        services.GetRequiredService<TenantContext>().Use(tenantId);
        await services.GetRequiredService<VatIdChecks>().SendAsync(message.CheckId, ct);
    }
}

internal sealed partial class VatIdChecks(AispaDbContext db, IVatIdValidator validator, IMessageBus bus, TimeProvider time)
{
    /// <summary>Mitgliedstaaten in VIES; Griechenland als „EL“, Nordirland als „XI“.</summary>
    private static readonly HashSet<string> Members =
    [
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "EL", "ES", "FI", "FR", "HR", "HU", "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL",
        "PT", "RO", "SE", "SI", "SK", "XI",
    ];

    /// <summary>„ATU 123 456 78“ → „ATU12345678“; null, wenn es keine EU-UID-Nummer sein kann.</summary>
    public static string? Normalize(string? vatId)
    {
        var compact = Separators().Replace(vatId ?? "", "").ToUpperInvariant();
        return compact.Length is >= 4 and <= 14 && Members.Contains(compact[..2]) && Shape().IsMatch(compact) ? compact : null;
    }

    /// <summary>Legt eine Abfrage an und schickt sie über die Queue an VIES; anfragend ist die UID des Standorts der Kasse.</summary>
    public async Task<PosResult<VatIdCheckDto>> StartAsync(VatIdCheckInput input, string? userId, CancellationToken ct)
    {
        if (Normalize(input.VatId) is not { } vatId)
        {
            return new PosError(PosErrorKind.Invalid, nameof(input.VatId),
                "UID-Nummer mit Länderkennzeichen eines EU-Staats, z. B. ATU12345678. Schweizer UID-Nummern prüft VIES nicht.");
        }

        var requester = await db.CashRegisters.Where(r => r.Id == input.RegisterId)
            .Join(db.Locations, r => r.LocationId, l => l.Id, (_, l) => new { l.VatId }).FirstOrDefaultAsync(ct);
        if (requester is null)
        {
            return new PosError(PosErrorKind.NotFound, nameof(input.RegisterId), "Kasse nicht gefunden.");
        }

        var check = new VatIdCheck
        {
            VatId = vatId,
            RequesterVatId = Normalize(requester.VatId),
            Status = VatIdCheckStatus.Pending,
            RequestedAt = time.GetUtcNow(),
            UserId = userId,
        };
        db.VatIdChecks.Add(check);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new SendVatIdCheck(check.Id));
        return new PosResult<VatIdCheckDto>(ToDto(check), null);
    }

    public async Task SendAsync(Guid checkId, CancellationToken ct)
    {
        var check = await db.VatIdChecks.FirstOrDefaultAsync(c => c.Id == checkId, ct);
        if (check is not { Status: VatIdCheckStatus.Pending })
        {
            return;
        }

        var result = await validator.CheckAsync(check.VatId, check.RequesterVatId, ct);
        check.Status = result.Message is not null ? VatIdCheckStatus.Failed : result.Valid ? VatIdCheckStatus.Valid : VatIdCheckStatus.Invalid;
        check.Name = Limit(result.Name, 300);
        check.Address = Limit(result.Address, 500);
        check.ConsultationNumber = Limit(result.ConsultationNumber, 50);
        check.Message = Limit(result.Message, 300);
        check.CheckedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<VatIdCheckDto?> GetAsync(Guid id, CancellationToken ct) =>
        await db.VatIdChecks.AsNoTracking().Where(c => c.Id == id).Select(c => ToDto(c)).FirstOrDefaultAsync(ct);

    /// <summary>Die letzten Abfragen zu einer UID-Nummer, neueste zuerst (Nachweis für die Prüfung).</summary>
    public async Task<List<VatIdCheckDto>> HistoryAsync(string vatId, CancellationToken ct)
    {
        var normalized = Normalize(vatId) ?? vatId.Trim().ToUpperInvariant();
        return await db.VatIdChecks.AsNoTracking().Where(c => c.VatId == normalized).OrderByDescending(c => c.RequestedAt).Take(20)
            .Select(c => ToDto(c)).ToListAsync(ct);
    }

    public static VatIdCheckDto ToDto(VatIdCheck c) =>
        new(c.Id, c.VatId, c.RequesterVatId, c.Status, c.Name, c.Address, c.ConsultationNumber, c.Message, c.RequestedAt, c.CheckedAt);

    private static string? Limit(string? value, int length) => value is null ? null : value.Length <= length ? value : value[..length];

    [GeneratedRegex(@"[\s.\-]")]
    private static partial Regex Separators();

    [GeneratedRegex("^[A-Z]{2}[0-9A-Z+*]{2,12}$")]
    private static partial Regex Shape();
}

internal static class VatIdModel
{
    public static void Configure(ModelBuilder builder) =>
        builder.Entity<VatIdCheck>(e =>
        {
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.VatId).HasMaxLength(20);
            e.Property(c => c.RequesterVatId).HasMaxLength(20);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(c => c.Name).HasMaxLength(300);
            e.Property(c => c.Address).HasMaxLength(500);
            e.Property(c => c.ConsultationNumber).HasMaxLength(50);
            e.Property(c => c.Message).HasMaxLength(300);
            e.Property(c => c.UserId).HasMaxLength(450);
            e.HasIndex(c => new { c.VatId, c.RequestedAt });
        });
}
