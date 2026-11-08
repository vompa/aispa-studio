using System.Globalization;
using System.Text.RegularExpressions;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Accounting;

/// <summary>Übergabe an die Buchhaltung: Einstellungen (Standard und je Land), Vorschau der Buchungen und Export für DATEV, BMD und die Schweiz (Abacus, bexio).</summary>
internal static partial class AccountingEndpoints
{
    public static IEndpointRouteBuilder MapAccountingEndpoints(this IEndpointRouteBuilder routes)
    {
        // Export und Vorschau: Inhaber und Standortleitung (nur Kassen der eigenen Standorte); Kontenzuordnung gilt für alle Standorte, daher nur Inhaber.
        var group = routes.MapGroup("/api/accounting").WithTags("Buchhaltung").RequirePermission(AccessRight.ViewAmountReports);

        group.MapGet("/settings", async (FiscalCountry? country, AispaDbContext db, CancellationToken ct) =>
                ToDto(await db.AccountingSettings.AsNoTracking().SettingsAsync(ct, country), country))
            .WithName("GetAccountingSettings")
            .WithSummary("Kontenzuordnung: ohne Land der Standard, mit Land die eigene Zuordnung des Landes oder (Inherited) der Standard, der dort gilt.");

        group.MapPut("/settings", async Task<Results<Ok<AccountingSettingsDto>, ValidationProblem>> (
                FiscalCountry? country, AccountingSettingsDto input, AispaDbContext db, CancellationToken ct) =>
            {
                var errors = new Errors();
                errors.Check(input.DatevConsultantNumber is null or "" || Digits().IsMatch(input.DatevConsultantNumber) && input.DatevConsultantNumber.Length <= 7,
                    nameof(input.DatevConsultantNumber), "Beraternummer: bis zu 7 Ziffern.");
                errors.Check(input.DatevClientNumber is null or "" || Digits().IsMatch(input.DatevClientNumber) && input.DatevClientNumber.Length <= 5,
                    nameof(input.DatevClientNumber), "Mandantennummer: bis zu 5 Ziffern.");
                errors.Check(input.AccountLength is >= 4 and <= 8, nameof(input.AccountLength), "Sachkontenlänge 4 bis 8.");
                errors.Check(ParseableTaxKeys(input.DatevTaxKeys, 4, allowEmptyKey: true), nameof(input.DatevTaxKeys),
                    "Format „19=3;7=2“: Steuersatz=BU-Schlüssel, getrennt mit Strichpunkt; „20=“ ohne Schlüssel für ein Automatikkonto.");
                errors.Check(ParseableTaxKeys(input.SwissTaxCodes, 10), nameof(input.SwissTaxCodes), "Format „8.1=UN81;2.6=UR26“: MWST-Satz=MWST-Code, getrennt mit Strichpunkt.");
                errors.Check(ParseableAccountMap(input.RevenueAccountCodes), nameof(input.RevenueAccountCodes),
                    "Format „4000=3400;4800=3200“: Erlöskonto=Konto in der Buchhaltung, Ziffern, getrennt mit Strichpunkt.");
                errors.Check(input.FiscalYearStartMonth is >= 1 and <= 12, nameof(input.FiscalYearStartMonth), "Monat 1 bis 12.");
                foreach (var (field, value) in new[]
                {
                    (nameof(input.TransitAccount), input.TransitAccount), (nameof(input.CashAccount), input.CashAccount),
                    (nameof(input.CardAccount), input.CardAccount), (nameof(input.InstantTransferAccount), input.InstantTransferAccount),
                    (nameof(input.VoucherAccount), input.VoucherAccount), (nameof(input.CashTransferAccount), input.CashTransferAccount),
                    (nameof(input.DigitalEuroAccount), input.DigitalEuroAccount), (nameof(input.TreatwellAccount), input.TreatwellAccount),
                    (nameof(input.CashShortageAccount), input.CashShortageAccount), (nameof(input.CashSurplusAccount), input.CashSurplusAccount),
                })
                {
                    errors.Check(value is not null && Digits().IsMatch(value) && value.Length <= 20, field, "Kontonummer aus Ziffern.");
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                var settings = await db.AccountingSettings.Where(s => s.Country == country).OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
                if (settings is null)
                {
                    db.AccountingSettings.Add(settings = new AccountingSettings { Country = country });
                }

                settings.DatevConsultantNumber = Blank(input.DatevConsultantNumber);
                settings.DatevClientNumber = Blank(input.DatevClientNumber);
                settings.AccountLength = input.AccountLength;
                settings.FiscalYearStartMonth = input.FiscalYearStartMonth;
                settings.TransitAccount = input.TransitAccount;
                settings.CashAccount = input.CashAccount;
                settings.CardAccount = input.CardAccount;
                settings.InstantTransferAccount = input.InstantTransferAccount;
                settings.VoucherAccount = input.VoucherAccount;
                settings.CashTransferAccount = input.CashTransferAccount;
                settings.DigitalEuroAccount = input.DigitalEuroAccount;
                settings.TreatwellAccount = input.TreatwellAccount;
                settings.CashShortageAccount = input.CashShortageAccount;
                settings.CashSurplusAccount = input.CashSurplusAccount;
                settings.DatevTaxKeys = input.DatevTaxKeys ?? "";
                settings.SwissTaxCodes = input.SwissTaxCodes?.Trim() ?? "";
                settings.RevenueAccountCodes = input.RevenueAccountCodes?.Trim() ?? "";
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(settings, country));
            })
            .WithName("SetAccountingSettings")
            .WithSummary("Speichert den Standard oder, mit Land, eine eigene Zuordnung für die Kassen dieses Landes.")
            .RequirePermission(AccessRight.ManageOrganization);

        group.MapDelete("/settings", async (FiscalCountry country, AispaDbContext db, CancellationToken ct) =>
            {
                await db.AccountingSettings.Where(s => s.Country == country).ExecuteDeleteAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("ResetAccountingSettings")
            .WithSummary("Entfernt die eigene Zuordnung eines Landes; danach gilt dort wieder der Standard.")
            .RequirePermission(AccessRight.ManageOrganization);

        group.MapGet("/postings", async Task<Results<Ok<List<AccountingPosting>>, ValidationProblem>> (
                DateOnly from, DateOnly until, Guid? registerId, AccountingExport export, AccessService access, CancellationToken ct) =>
                Range(from, until) is { } problem
                    ? problem
                    : TypedResults.Ok(await export.PostingsAsync(from, until, registerId, (await access.CurrentAsync(ct))!.CanSee, ct)))
            .WithName("ListAccountingPostings")
            .WithSummary("Vorschau der Buchungen eines Zeitraums (Ortszeit der Standorte, beide Tage eingeschlossen), alle Kassen mit ihrer Währung.");

        group.MapGet("/export", async Task<Results<FileContentHttpResult, ValidationProblem>> (
                AccountingFormat format, DateOnly from, DateOnly until, Guid? registerId, AccountingExport export, AccessService access,
                CancellationToken ct) =>
            {
                if (Range(from, until) is { } problem)
                {
                    return problem;
                }

                var (content, name, missing) = await export.FileAsync(format, from, until, registerId, (await access.CurrentAsync(ct))!.CanSee, ct);
                if (content is null)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["format"] = [missing!] });
                }

                return TypedResults.File(content, format == AccountingFormat.Swiss ? "text/csv; charset=utf-8" : "text/csv; charset=windows-1252", name);
            })
            .WithName("ExportAccounting")
            .WithSummary("CSV für DATEV (Buchungsstapel) oder BMD (Buchungsimport) mit den Euro-Kassen, oder für Abacus/bexio mit den Schweizer Kassen.");

        return routes;
    }

    private static ValidationProblem? Range(DateOnly from, DateOnly until) =>
        from > until || until.DayNumber - from.DayNumber > 366
            ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["until"] = ["Zeitraum von höchstens einem Jahr, Ende nach Beginn."] })
            : null;

    private static AccountingSettingsDto ToDto(AccountingSettings s, FiscalCountry? country) => new(s.DatevConsultantNumber, s.DatevClientNumber, s.AccountLength,
        s.FiscalYearStartMonth, s.TransitAccount, s.CashAccount, s.CardAccount, s.InstantTransferAccount, s.VoucherAccount,
        s.CashTransferAccount, s.DigitalEuroAccount, s.CashShortageAccount, s.CashSurplusAccount, s.DatevTaxKeys, s.SwissTaxCodes,
        s.RevenueAccountCodes, country, Inherited: s.Country != country, TreatwellAccount: s.TreatwellAccount);

    private static bool ParseableAccountMap(string? text) =>
        string.IsNullOrWhiteSpace(text) || text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(pair => pair.Split('=', StringSplitOptions.TrimEntries) is [var from, var to]
                && from.Length is > 0 and <= 20 && Digits().IsMatch(from) && to.Length is > 0 and <= 20 && Digits().IsMatch(to));

    private static bool ParseableTaxKeys(string? text, int maxKeyLength, bool allowEmptyKey = false) =>
        string.IsNullOrWhiteSpace(text) || text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(pair => pair.Split('=') is [var rate, var key] && decimal.TryParse(rate.Replace(',', '.'), NumberStyles.Number,
                CultureInfo.InvariantCulture, out _) && (allowEmptyKey || key.Trim().Length > 0) && key.Trim().Length <= maxKeyLength);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex Digits();
}
