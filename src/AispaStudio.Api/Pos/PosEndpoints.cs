using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Documents;
using AispaStudio.Api.Pos.Fiscal;
using AispaStudio.Api.Pos.VatIds;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Staff;
using AispaStudio.Api.Stock;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Pos;

/// <summary>Ereignis: ein Beleg wurde storniert (nach dem Commit des Stornobelegs).</summary>
public sealed record ReceiptVoided(Guid ReceiptId, Guid VoidReceiptId, string Reason, string? UserId);

/// <summary>Kassa: Stammdaten, Kassen, offene Rechnungen, Bezahlen, Belege, Gutscheine und Tagesabschluss.</summary>
internal static class PosEndpoints
{
    public static IEndpointRouteBuilder MapPosEndpoints(this IEndpointRouteBuilder routes)
    {
        // Kassieren darf, wer buchen darf (Rezeption aufwärts); Konten, Artikel, Preise und Kassen anlegen nur Inhaber und Standortleitung.
        var group = routes.MapGroup("/api/pos").WithTags("Kassa").RequirePermission(AccessRight.Book);

        // ---------- Stammdaten ----------

        group.MapGet("/revenue-accounts", async (AispaDbContext db, CancellationToken ct) =>
                (await db.RevenueAccounts.AsNoTracking().OrderBy(a => a.Code).ToListAsync(ct))
                .Select(a => new RevenueAccountDto(a.Id, a.Code, a.Name, a.CostCenter)).ToList())
            .WithName("ListRevenueAccounts")
            .AlsoAllow(AccessRight.ViewAmountReports, insteadOf: AccessRight.Book);

        group.MapPost("/revenue-accounts", async Task<Results<Created<RevenueAccountDto>, ValidationProblem>> (
                RevenueAccountInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var code = input.Code?.Trim() ?? "";
                var errors = new Errors();
                errors.Require(nameof(input.Code), code);
                errors.Require(nameof(input.Name), input.Name);
                errors.Check(!await db.RevenueAccounts.AnyAsync(a => a.Code == code, ct), nameof(input.Code), "Dieses Konto gibt es schon.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var account = new RevenueAccount { Code = code, Name = input.Name.Trim(), CostCenter = Blank(input.CostCenter) };
                db.RevenueAccounts.Add(account);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/pos/revenue-accounts/{account.Id}",
                    new RevenueAccountDto(account.Id, account.Code, account.Name, account.CostCenter));
            })
            .WithName("CreateRevenueAccount")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Erlöskonto anlegen, z. B. für einen eigenen Geschäftsbereich mit Kostenstelle. Umsätze darauf werden normal versteuert und signiert.");

        group.MapGet("/articles", async (AispaDbContext db, CancellationToken ct) =>
                (await db.Articles.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync(ct)).Select(ToDto).ToList())
            .WithName("ListArticles");

        group.MapGet("/articles/by-barcode", async Task<Results<Ok<ArticleDto>, NotFound>> (string code, AispaDbContext db, CancellationToken ct) =>
                await StockBarcodes.FindArticleAsync(db, code, ct) is { } article ? TypedResults.Ok(ToDto(article)) : TypedResults.NotFound())
            .WithName("FindArticleByBarcode")
            .WithSummary("Der Kassa-Artikel des Materials mit diesem Code (Handscanner an der Kassa); 404, wenn der Code unbekannt ist oder das Material nicht verkauft wird.");

        group.MapPost("/articles", async Task<Results<Created<ArticleDto>, ValidationProblem>> (
                ArticleInput input, AispaDbContext db, CancellationToken ct) =>
            {
                var errors = new Errors();
                errors.Require(nameof(input.Name), input.Name);
                errors.Check(input.Price > 0 && decimal.Round(input.Price, 2) == input.Price, nameof(input.Price), "Preis größer als 0 mit höchstens zwei Nachkommastellen.");
                errors.Check(await db.RevenueAccounts.AnyAsync(a => a.Id == input.RevenueAccountId, ct), nameof(input.RevenueAccountId), "Unbekanntes Erlöskonto.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var article = new Article { Name = input.Name.Trim(), Price = input.Price, TaxCategory = input.TaxCategory, RevenueAccountId = input.RevenueAccountId };
                db.Articles.Add(article);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/pos/articles/{article.Id}", ToDto(article));
            })
            .WithName("CreateArticle")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapGet("/treatments/{treatmentId:guid}/settings", async (Guid treatmentId, AispaDbContext db, CancellationToken ct) =>
                await db.TreatmentPosSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TreatmentId == treatmentId, ct) is { } s
                    ? new TreatmentPosSettings(s.TaxCategory, s.RevenueAccountId)
                    : new TreatmentPosSettings(TaxCategory.Normal, PosModel.Treatments.Id))
            .WithName("GetTreatmentPosSettings");

        group.MapPut("/treatments/{treatmentId:guid}/settings", async Task<Results<Ok<TreatmentPosSettings>, NotFound, ValidationProblem>> (
                Guid treatmentId, TreatmentPosSettings input, AispaDbContext db, CancellationToken ct) =>
            {
                if (!await db.Treatments.AnyAsync(t => t.Id == treatmentId, ct))
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check(await db.RevenueAccounts.AnyAsync(a => a.Id == input.RevenueAccountId, ct), nameof(input.RevenueAccountId), "Unbekanntes Erlöskonto.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var setting = await db.TreatmentPosSettings.FirstOrDefaultAsync(s => s.TreatmentId == treatmentId, ct);
                if (setting is null)
                {
                    db.TreatmentPosSettings.Add(setting = new TreatmentPosSetting { TreatmentId = treatmentId });
                }

                setting.TaxCategory = input.TaxCategory;
                setting.RevenueAccountId = input.RevenueAccountId;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(input);
            })
            .WithName("SetTreatmentPosSettings")
            .RequirePermission(AccessRight.ManageCatalog)
            .WithSummary("Steuersatz und Erlöskonto einer Behandlung an der Kassa; gilt für neue Rechnungen.");

        // ---------- Kassen ----------

        group.MapGet("/registers", async (Guid? locationId, AispaDbContext db, PosService pos, CancellationToken ct) =>
                (await db.CashRegisters.AsNoTracking().Where(r => locationId == null || r.LocationId == locationId)
                    .OrderBy(r => r.Code).ToListAsync(ct)).Select(pos.ToDto).ToList())
            .WithName("ListCashRegisters")
            .AlsoAllow(AccessRight.ViewReceipts, insteadOf: AccessRight.Book);

        group.MapGet("/registers/{registerId:guid}", async Task<Results<Ok<CashRegisterDto>, NotFound>> (
                Guid registerId, AispaDbContext db, PosService pos, CancellationToken ct) =>
                await db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == registerId, ct) is { } r
                    ? TypedResults.Ok(pos.ToDto(r))
                    : TypedResults.NotFound())
            .WithName("GetCashRegister")
            .AlsoAllow(AccessRight.ViewReceipts, insteadOf: AccessRight.Book);

        group.MapPost("/registers", async Task<Results<Created<CashRegisterDto>, ValidationProblem, ProblemHttpResult>> (
                CashRegisterInput input, PosService pos, CancellationToken ct) =>
            {
                var result = await pos.CreateRegisterAsync(input, ct);
                if (result.Error is { } error)
                {
                    return error.Kind == PosErrorKind.Invalid ? Validation(error) : Problem(error);
                }

                return TypedResults.Created($"/api/pos/registers/{result.Value!.Id}", result.Value);
            })
            .WithName("CreateCashRegister")
            .RequirePermission(AccessRight.ManageLocation)
            .WithSummary("Neue Kasse. Österreich: erzeugt Schlüssel für den Umsatzzähler und den signierten Startbeleg.");

        group.MapPost("/registers/{registerId:guid}/null-receipt", async Task<Results<Ok<ReceiptDto>, ValidationProblem, ProblemHttpResult>> (
                Guid registerId, PosService pos, CancellationToken ct) => Respond(await pos.NullReceiptAsync(registerId, ct)))
            .WithName("CreateNullReceipt")
            .WithSummary("Nullbeleg, z. B. zur Prüfung mit der BMF-Belegcheck-App.");

        group.MapPost("/registers/{registerId:guid}/decommission", async Task<Results<Ok<ReceiptDto>, ValidationProblem, ProblemHttpResult>> (
                Guid registerId, PosService pos, CancellationToken ct) => Respond(await pos.DecommissionAsync(registerId, ct)))
            .WithName("DecommissionCashRegister")
            .RequirePermission(AccessRight.ManageLocation)
            .WithSummary("Kasse außer Betrieb nehmen (Schlussbeleg). Danach in FinanzOnline abmelden.");

        group.MapGet("/registers/{registerId:guid}/receipts", async Task<Results<Ok<List<ReceiptDto>>, NotFound>> (
                Guid registerId, DateOnly date, AispaDbContext db, PosService pos, CancellationToken ct) =>
            {
                var register = await db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == registerId, ct);
                if (register is null)
                {
                    return TypedResults.NotFound();
                }

                var zone = LocalTime.Zone(await db.Locations.Where(l => l.Id == register.LocationId).Select(l => l.TimeZone).FirstAsync(ct));
                var (from, until) = LocalTime.Day(date, zone);
                var ids = await db.Receipts.AsNoTracking()
                    .Where(r => r.RegisterId == registerId && r.IssuedAt >= from && r.IssuedAt < until)
                    .OrderByDescending(r => r.Number).Select(r => r.Id).ToListAsync(ct);
                return TypedResults.Ok(await pos.GetReceiptsAsync(ids, ct));
            })
            .WithName("ListReceipts")
            .AlsoAllow(AccessRight.ViewReceipts, insteadOf: AccessRight.Book)
            .WithSummary("Belege einer Kasse an einem Tag (Ortszeit), neueste zuerst.");

        group.MapGet("/registers/{registerId:guid}/bills", async (Guid registerId, AispaDbContext db, PosService pos, CancellationToken ct) =>
            {
                var ids = await db.Bills.AsNoTracking().Where(b => b.RegisterId == registerId && b.Status == BillStatus.Open)
                    .OrderBy(b => b.CreatedAt).Select(b => b.Id).ToListAsync(ct);
                var bills = new List<BillDto>();
                foreach (var id in ids)
                {
                    bills.Add((await pos.GetBillAsync(id, ct))!);
                }

                return bills;
            })
            .WithName("ListOpenBills");

        group.MapGet("/registers/{registerId:guid}/dep", async Task<Results<Ok<DepExport>, NotFound, ProblemHttpResult>> (
                Guid registerId, AispaDbContext db, FiscalizationResolver fiscal, CancellationToken ct) =>
            {
                var register = await db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == registerId, ct);
                if (register is null)
                {
                    return TypedResults.NotFound();
                }

                if (register.Country != FiscalCountry.AT)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Das DEP gibt es nur für österreichische Kassen.");
                }

                var certificate = await fiscal.For(register.Country).GetCertificateAsync(register, ct);
                var receipts = await db.Receipts.AsNoTracking().Where(r => r.RegisterId == registerId)
                    .OrderBy(r => r.Number).Select(r => r.FiscalSignature!).ToListAsync(ct);
                return TypedResults.Ok(new DepExport([new DepGroup(Convert.ToBase64String(certificate ?? []), [], receipts)]));
            })
            .WithName("ExportDep")
            .RequirePermission(AccessRight.ViewAmountReports).AlsoAllow(AccessRight.ViewAmountReports, insteadOf: AccessRight.Book)
            .WithSummary("Datenerfassungsprotokoll (RKSV) für die Betriebsprüfung.");

        group.MapGet("/registers/{registerId:guid}/dsfinvk", async Task<Results<FileContentHttpResult, NotFound, ProblemHttpResult>> (
                Guid registerId, DateOnly from, DateOnly until, AispaDbContext db, DsfinvkExport export, CancellationToken ct) =>
            {
                var register = await db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == registerId, ct);
                if (register is null)
                {
                    return TypedResults.NotFound();
                }

                if (register.Country != FiscalCountry.DE || until < from || until.DayNumber - from.DayNumber > 366)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                        title: "Die DSFinV-K gibt es nur für deutsche Kassen, für höchstens ein Jahr.");
                }

                var (content, name) = (await export.ZipAsync(registerId, from, until, ct))!.Value;
                return TypedResults.File(content, "application/zip", name);
            })
            .WithName("ExportDsfinvk")
            .RequirePermission(AccessRight.ViewAmountReports).AlsoAllow(AccessRight.ViewAmountReports, insteadOf: AccessRight.Book)
            .WithSummary("DSFinV-K für die Kassen-Nachschau und Außenprüfung (Deutschland): je Tagesabschluss, als ZIP mit index.xml.");

        group.MapPost("/registers/{registerId:guid}/tse-exports", async Task<Results<Ok<TseExportDto>, ValidationProblem, ProblemHttpResult>> (
                Guid registerId, TseExportInput input, ClaimsPrincipal user, TseExports exports, CancellationToken ct) =>
                Respond(await exports.RequestAsync(registerId, input, UserId(user), ct)))
            .WithName("RequestTseExport")
            .RequirePermission(AccessRight.ViewAmountReports).AlsoAllow(AccessRight.ViewAmountReports, insteadOf: AccessRight.Book)
            .WithSummary("TSE-Export (TAR) anfordern (Deutschland); er läuft im Hintergrund, die Datei steht danach in der Liste.");

        group.MapGet("/registers/{registerId:guid}/tse-exports", async (Guid registerId, TseExports exports, CancellationToken ct) =>
                await exports.ListAsync(registerId, ct))
            .WithName("ListTseExports")
            .RequirePermission(AccessRight.ViewAmountReports).AlsoAllow(AccessRight.ViewAmountReports, insteadOf: AccessRight.Book)
            .WithSummary("Die letzten zehn TSE-Exporte einer Kasse mit Stand.");

        group.MapGet("/tse-exports/{exportId:guid}/file", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid exportId, TseExports exports, CancellationToken ct) =>
                await exports.FileAsync(exportId, ct) is { } file ? TypedResults.File(file.Content, "application/x-tar", file.FileName) : TypedResults.NotFound())
            .WithName("DownloadTseExport")
            .RequirePermission(AccessRight.ViewAmountReports).AlsoAllow(AccessRight.ViewAmountReports, insteadOf: AccessRight.Book);

        group.MapPost("/registers/{registerId:guid}/cash-movements", async Task<Results<Ok<CashMovementDto>, ValidationProblem, ProblemHttpResult>> (
                Guid registerId, CashMovementInput input, ClaimsPrincipal user, PosService pos, AccessService access, CancellationToken ct) =>
                Respond(await pos.AddCashMovementAsync(registerId, input, UserId(user), await SupervisorAsync(access, ct), ct)))
            .WithName("AddCashMovement")
            .WithSummary("Einlage oder Entnahme in der Kassenlade (kein Umsatz). Entnehmen nur mit Kassenaufsicht.");

        group.MapGet("/registers/{registerId:guid}/day-report", async Task<Results<Ok<DayClosingDto>, ValidationProblem, ProblemHttpResult>> (
                Guid registerId, PosService pos, CancellationToken ct) => Respond(await pos.DayReportAsync(registerId, null, null, ct)))
            .WithName("GetDayReport")
            .WithSummary("Zwischenbericht (X-Bericht) seit dem letzten Tagesabschluss.");

        group.MapPost("/registers/{registerId:guid}/day-closings", async Task<Results<Ok<DayClosingDto>, ValidationProblem, ProblemHttpResult>> (
                Guid registerId, DayClosingInput input, ClaimsPrincipal user, PosService pos, CancellationToken ct) =>
                Respond(await pos.DayReportAsync(registerId, input, UserId(user), ct)))
            .WithName("CloseDay")
            .WithSummary("Tagesabschluss (Z-Bericht) mit gezähltem Kassenbestand; wird unveränderlich gespeichert.");

        group.MapGet("/registers/{registerId:guid}/day-closings", async (Guid registerId, AispaDbContext db, CancellationToken ct) =>
                (await db.DayClosings.AsNoTracking().Where(c => c.RegisterId == registerId).OrderByDescending(c => c.Until).Take(60)
                    .Select(c => c.Report).ToListAsync(ct))
                .Select(r => JsonSerializer.Deserialize<DayClosingDto>(r, PosJson.Options)!).ToList())
            .WithName("ListDayClosings")
            .RequirePermission(AccessRight.ViewAmountReports)
            .AlsoAllow(AccessRight.ViewAmountReports, insteadOf: AccessRight.Book)
            .WithSummary("Die letzten 60 Tagesabschlüsse einer Kasse mit allen Zahlen; nur mit dem Recht „Berichte mit Beträgen“.");

        // PDF-Ausgaben (ADR 0092): der gespeicherte Tagesabschluss als PDF zum Abheften; Recht wie das Lesen der Abschlüsse, kein Versand.
        group.MapGet("/registers/{registerId:guid}/day-closings/{closingId:guid}/pdf", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid registerId, Guid closingId, ClaimsPrincipal user, AispaDbContext db, DocumentHeaders headers, CancellationToken ct) =>
            {
                var stored = await db.DayClosings.AsNoTracking().Where(c => c.Id == closingId && c.RegisterId == registerId).Select(c => c.Report).FirstOrDefaultAsync(ct);
                if (stored is null)
                {
                    return TypedResults.NotFound();
                }

                var closing = JsonSerializer.Deserialize<DayClosingDto>(stored, PosJson.Options)!;
                var code = await db.CashRegisters.Where(r => r.Id == registerId).Select(r => r.Code).FirstAsync(ct);
                var location = await RegisterLocationAsync(db, registerId, ct);
                var zone = LocalTime.Zone(location.TimeZone);
                var numbers = await db.Receipts.AsNoTracking()
                    .Where(r => r.RegisterId == registerId && r.IssuedAt >= closing.From && r.IssuedAt < closing.Until && (r.Kind == ReceiptKind.Sale || r.Kind == ReceiptKind.Void))
                    .GroupBy(r => 1).Select(g => new DayClosingDocument.ReceiptRange(g.Min(r => r.Number), g.Max(r => r.Number))).FirstOrDefaultAsync(ct);
                var movements = await db.CashMovements.AsNoTracking().Where(m => m.RegisterId == registerId && m.At >= closing.From && m.At < closing.Until).ToListAsync(ct);
                var pdf = DayClosingDocument.Build(closing, code, numbers, movements, await headers.ForAsync(location.Id, user, ct), zone, location.Currency);
                return DocumentFiles.Pdf(pdf, DayClosingDocument.FileName(code, closing, zone));
            })
            .WithName("DayClosingPdf")
            .RequirePermission(AccessRight.ViewAmountReports)
            .AlsoAllow(AccessRight.ViewAmountReports, insteadOf: AccessRight.Book)
            .WithSummary("Ein gespeicherter Tagesabschluss als PDF (A4) mit Summen, Differenz, Einlagen und Entnahmen und Unterschriftenzeilen; nur mit dem Recht „Berichte mit Beträgen“.");

        // ---------- Termine abrechnen ----------

        group.MapGet("/locations/{locationId:guid}/unbilled-appointments", async (
                Guid locationId, DateOnly date, AispaDbContext db, CancellationToken ct) =>
            {
                var zoneId = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
                if (zoneId is null)
                {
                    return [];
                }

                var (from, until) = LocalTime.Day(date, LocalTime.Zone(zoneId));
                return (await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Treatment)
                        .Where(a => a.LocationId == locationId && a.Start >= from && a.Start < until && a.Status != AppointmentStatus.Cancelled)
                        .Where(a => !db.BillLines.Any(l => l.AppointmentId == a.Id))
                        .OrderBy(a => a.Start)
                        .ToListAsync(ct))
                    .Select(a => BookingService.ToDto(a, a.Treatment.Name)).ToList();
            })
            .WithName("ListUnbilledAppointments")
            .WithSummary("Termine eines Tages, die noch auf keiner Rechnung stehen.");

        group.MapGet("/locations/{locationId:guid}/unbilled-visits", async (
                Guid locationId, DateOnly date, AispaDbContext db, CancellationToken ct) =>
            {
                var zoneId = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
                if (zoneId is null)
                {
                    return [];
                }

                var (from, until) = LocalTime.Day(date, LocalTime.Zone(zoneId));
                return (await db.AreaVisits.AsNoTracking().Include(v => v.Customer)
                        .Where(v => v.LocationId == locationId && v.From >= from && v.From < until && !v.IsCancelled)
                        .Where(v => !db.BillLines.Any(l => l.AreaVisitId == v.Id))
                        .OrderBy(v => v.From)
                        .ToListAsync(ct))
                    .Select(AreaVisitService.ToDto).ToList();
            })
            .WithName("ListUnbilledVisits")
            .WithSummary("Eintritte in Bereiche eines Tages, die noch auf keiner Rechnung stehen.");

        group.MapGet("/locations/{locationId:guid}/day-list", (Guid locationId, DateOnly date, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
                PosDayList.LoadAsync(db, locationId, date, time.GetUtcNow(), ct))
            .WithName("ListPosDay")
            .WithSummary("Liste „Heute abzurechnen“ eines Tages: wartet auf Abrechnung (fertig, noch auf keiner Rechnung), später heute und erledigt (bezahlt); "
                + "geparkte Rechnungen fehlen, eine Anzahlung zählt nicht als abgerechnet.");

        // ---------- Rechnungen ----------

        group.MapPost("/bills", async Task<Results<Ok<BillDto>, ValidationProblem, ProblemHttpResult>> (
                BillInput input, PosService pos, CancellationToken ct) => Respond(await pos.CreateBillAsync(input, ct)))
            .WithName("CreateBill")
            .WithSummary("Offene Rechnung, optional gleich mit Terminen aus dem Kalender.");

        group.MapGet("/bills/{billId:guid}", async Task<Results<Ok<BillDto>, NotFound>> (Guid billId, PosService pos, CancellationToken ct) =>
                await pos.GetBillAsync(billId, ct) is { } bill ? TypedResults.Ok(bill) : TypedResults.NotFound())
            .WithName("GetBill");

        group.MapDelete("/bills/{billId:guid}", async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> (
                Guid billId, PosService pos, CancellationToken ct) =>
            {
                if (await pos.DeleteBillAsync(billId, ct) is { } error)
                {
                    return error.Kind == PosErrorKind.Invalid ? Validation(error) : Problem(error);
                }

                return TypedResults.NoContent();
            })
            .WithName("DeleteBill");

        group.MapPost("/bills/{billId:guid}/lines", async Task<Results<Ok<BillDto>, ValidationProblem, ProblemHttpResult>> (
                Guid billId, BillLineInput input, PosService pos, AccessService access, CancellationToken ct) =>
                Respond(await pos.AddLineAsync(billId, input, await SupervisorAsync(access, ct), ct)))
            .WithName("AddBillLine");

        group.MapDelete("/bills/{billId:guid}/lines/{lineId:guid}", async Task<Results<Ok<BillDto>, ValidationProblem, ProblemHttpResult>> (
                Guid billId, Guid lineId, PosService pos, CancellationToken ct) => Respond(await pos.RemoveLineAsync(billId, lineId, ct)))
            .WithName("RemoveBillLine");

        group.MapPut("/bills/{billId:guid}/lines/{lineId:guid}/discount", async Task<Results<Ok<BillDto>, ValidationProblem, ProblemHttpResult>> (
                Guid billId, Guid lineId, LineDiscountInput input, PosService pos, AccessService access, AispaDbContext db,
                UserManager<AispaUser> users, TimeProvider time, CancellationToken ct) =>
            {
                if (input.Approval is not { } approval)
                {
                    return Respond(await pos.SetDiscountAsync(billId, lineId, input, await SupervisorAsync(access, ct), null, ct));
                }

                // Freigabe am Rezeptionsplatz: Inhaber oder Standortleitung bestätigen mit ihrer PIN.
                var location = await db.Bills.Where(b => b.Id == billId)
                    .Join(db.CashRegisters, b => b.RegisterId, r => r.Id, (b, r) => (Guid?)r.LocationId).FirstOrDefaultAsync(ct);
                var (approver, error) = await QuickSwitch.ApproveAsync(approval, location, db, users, time.GetUtcNow(), ct);
                return approver is null
                    ? TypedResults.Problem(error, statusCode: StatusCodes.Status403Forbidden)
                    : Respond(await pos.SetDiscountAsync(billId, lineId, input with { Approval = null }, true, approver, ct));
            })
            .WithName("SetBillLineDiscount")
            .WithSummary("Rabatt einer noch offenen Position in Prozent oder als Betrag; 0 nimmt ihn zurück. Über der Rabattgrenze nur mit Kassenaufsicht.");

        group.MapGet("/bills/{billId:guid}/promotions", async Task<Results<Ok<List<PromotionOfferDto>>, NotFound>> (Guid billId, PosService pos, CancellationToken ct) =>
                await pos.PromotionOffersAsync(billId, ct) is { } offers ? TypedResults.Ok(offers) : TypedResults.NotFound())
            .WithName("ListBillPromotions")
            .WithSummary("Vorschläge: laufende Aktionen des Standorts, die zu Positionen der offenen Rechnung passen, mit dem Rabatt, den die Kassa dafür rechnet. Ändert nichts.");

        group.MapPost("/bills/{billId:guid}/promotions/{promotionId:guid}/apply", async Task<Results<Ok<BillDto>, ValidationProblem, ProblemHttpResult>> (
                Guid billId, Guid promotionId, PosService pos, CancellationToken ct) => Respond(await pos.ApplyPromotionAsync(billId, promotionId, ct)))
            .WithName("ApplyBillPromotion")
            .WithSummary("Wendet eine laufende Aktion an: die Kassa setzt den Rabatt der passenden offenen Positionen und nennt die Aktion auf dem Beleg. Nur auf Klick, nie automatisch.");

        group.MapGet("/approvers", async (Guid? locationId, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
                await QuickSwitch.ApproversAsync(locationId, db, time.GetUtcNow(), ct))
            .WithName("ListDiscountApprovers")
            .WithSummary("Wer am Standort einen Rabatt über der Grenze per PIN freigeben kann (Inhaber, Standortleitung mit PIN).");

        group.MapGet("/settings", async (AispaDbContext db, TenantContext tenants, AccessService access, CancellationToken ct) =>
                await db.Tenants.Where(t => t.Id == tenants.TenantId).FirstOrDefaultAsync(ct) is { } tenant
                    ? new PosSettingsDto(tenant.DiscountLimitPercent, await SupervisorAsync(access, ct), tenant.CashDiscountPercent)
                    : new PosSettingsDto(20m, false))
            .WithName("GetPosSettings")
            .WithSummary("Kassa-Einstellungen des Spas, z. B. bis wie viel Prozent jede Person Rabatt geben darf.");

        group.MapPut("/settings", async Task<Results<Ok<PosSettingsDto>, ValidationProblem, NotFound>> (
                PosSettingsInput input, AispaDbContext db, TenantContext tenants, AccessService access, CancellationToken ct) =>
            {
                var errors = new Errors();
                errors.Check(input.DiscountLimitPercent is >= 0 and <= 100 && decimal.Round(input.DiscountLimitPercent, 2) == input.DiscountLimitPercent,
                    nameof(input.DiscountLimitPercent), "Rabattgrenze in Prozent zwischen 0 und 100.");
                errors.Check(input.CashDiscountPercent is null or (>= 0 and <= 10) && (input.CashDiscountPercent is not { } cash || decimal.Round(cash, 2) == cash),
                    nameof(input.CashDiscountPercent), "Barzahlungsrabatt in Prozent zwischen 0 (aus) und 10.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                if (await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenants.TenantId, ct) is not { } tenant)
                {
                    return TypedResults.NotFound();
                }

                tenant.DiscountLimitPercent = input.DiscountLimitPercent;
                tenant.CashDiscountPercent = input.CashDiscountPercent ?? tenant.CashDiscountPercent;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new PosSettingsDto(tenant.DiscountLimitPercent, await SupervisorAsync(access, ct), tenant.CashDiscountPercent));
            })
            .WithName("SetPosSettings")
            .WithSummary("Rabattgrenze des Spas in Prozent: bis dahin darf jede Person Rabatt geben, darüber nur die Kassenaufsicht.")
            .RequirePermission(AccessRight.ManageOrganization);

        group.MapPost("/bills/{billId:guid}/checkout", async Task<Results<Ok<ReceiptDto>, ValidationProblem, ProblemHttpResult>> (
                Guid billId, CheckoutRequest request, ClaimsPrincipal user, PosService pos, IMessageBus bus, CancellationToken ct) =>
                Respond(await StockAsync(bus, await pos.CheckoutAsync(billId, request, UserId(user), ct))))
            .WithName("Checkout")
            .WithSummary("Bezahlen und signierten Beleg erstellen. Mit Positionsbeträgen nur einen Teil (Rechnungssplitting).");

        group.MapGet("/bills/{billId:guid}/instant-transfer", async Task<Results<Ok<InstantTransferQr>, NotFound, ProblemHttpResult>> (
                Guid billId, decimal? amount, AispaDbContext db, PosService pos, CancellationToken ct) =>
            {
                var bill = await pos.GetBillAsync(billId, ct);
                if (bill is null)
                {
                    return TypedResults.NotFound();
                }

                var register = await db.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == bill.RegisterId, ct);
                var location = await db.Locations.AsNoTracking().FirstAsync(l => l.Id == register.LocationId, ct);
                var (iban, bic, payee) = (register.Iban, register.Bic, register.PayeeName);
                if (iban is null && !CompanyProfiles.HasOwnCompany(location)
                    && await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == db.CurrentTenantId, ct) is { Iban: not null } tenant)
                {
                    // Kasse ohne eigenes Konto: das Konto aus den Firmendaten des Spas (ADR 0052).
                    (iban, bic, payee) = (tenant.Iban, tenant.Bic, tenant.AccountHolder ?? tenant.LegalName ?? tenant.Name);
                }

                if (iban is null || payee is null || location.Currency != "EUR")
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                        title: "Für Sofortüberweisung braucht es eine IBAN in den Firmendaten oder an der Kasse (nur in Euro).");
                }

                var due = amount is > 0 && amount <= bill.Open ? amount.Value : bill.Open;
                var payload = string.Join('\n', "BCD", "002", "1", "SCT", bic ?? "", payee, iban,
                    $"EUR{due.ToString("0.00", CultureInfo.InvariantCulture)}", "", "", $"Rechnung {bill.Id.ToString()[^8..].ToUpperInvariant()}");
                return TypedResults.Ok(new InstantTransferQr(payload, due, iban, payee));
            })
            .WithName("GetInstantTransferQr")
            .WithSummary("EPC-QR-Code für eine Sofortüberweisung des offenen Betrags.");

        // ---------- Belege und Gutscheine ----------

        group.MapGet("/receipts/{receiptId:guid}", async Task<Results<Ok<ReceiptDto>, NotFound>> (Guid receiptId, PosService pos, CancellationToken ct) =>
                await pos.GetReceiptAsync(receiptId, ct) is { } receipt ? TypedResults.Ok(receipt) : TypedResults.NotFound())
            .WithName("GetReceipt")
            .AlsoAllow(AccessRight.ViewReceipts, insteadOf: AccessRight.Book);

        group.MapGet("/receipts/{receiptId:guid}/print", async Task<Results<FileContentHttpResult, NotFound, ValidationProblem>> (
                Guid receiptId, ReceiptPrintFormat? format, int? width, PosService pos, AispaDbContext db, CancellationToken ct) =>
            {
                var columns = width ?? 42;
                if (columns is < 32 or > 64)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["width"] = ["Zeichen je Zeile: 32 bis 64."] });
                }

                if (await pos.GetReceiptAsync(receiptId, ct) is not { } receipt)
                {
                    return TypedResults.NotFound();
                }

                var location = await db.CashRegisters.Where(r => r.Id == receipt.RegisterId)
                    .Join(db.Locations, r => r.LocationId, l => l.Id, (_, l) => new { l.TimeZone, l.Currency }).FirstAsync(ct);
                var zone = LocalTime.Zone(location.TimeZone);
                return format == ReceiptPrintFormat.EscPos
                    ? TypedResults.File(ReceiptPrinter.EscPos(receipt, zone, location.Currency, columns), "application/octet-stream", $"Beleg-{receipt.RegisterCode}-{receipt.Number}.bin")
                    : TypedResults.File(Encoding.UTF8.GetBytes(ReceiptPrinter.Text(receipt, zone, location.Currency, columns)), "text/plain; charset=utf-8",
                        $"Beleg-{receipt.RegisterCode}-{receipt.Number}.txt");
            })
            .WithName("PrintReceipt")
            .AlsoAllow(AccessRight.ViewReceipts, insteadOf: AccessRight.Book)
            .WithSummary("Beleg für den Bondrucker: als Text in fester Breite oder als ESC/POS-Befehle mit QR-Code und Schnitt (z. B. für die Windows-App).");

        // PDF-Ausgaben (ADR 0092): Beleg oder Rechnung als PDF, Vorbelegung und Versand per Mail mit Vermerk am Beleg.
        group.MapGet("/receipts/{receiptId:guid}/pdf", async Task<Results<FileContentHttpResult, NotFound>> (
                Guid receiptId, DocumentPageFormat? format, bool? copy, ClaimsPrincipal user, PosService pos, AispaDbContext db, DocumentHeaders headers, CancellationToken ct) =>
            {
                if (await pos.GetReceiptAsync(receiptId, ct) is not { } receipt)
                {
                    return TypedResults.NotFound();
                }

                var location = await RegisterLocationAsync(db, receipt.RegisterId, ct);
                var header = ReceiptDocument.Header(await headers.ForAsync(location.Id, user, ct), receipt);
                var pdf = ReceiptDocument.Build(receipt, header, LocalTime.Zone(location.TimeZone), location.Currency, format ?? DocumentPageFormat.A4, copy == true);
                return DocumentFiles.Pdf(pdf, ReceiptDocument.FileName(receipt));
            })
            .WithName("ReceiptPdf")
            .AlsoAllow(AccessRight.ViewReceipts, insteadOf: AccessRight.Book)
            .WithSummary("Beleg oder Rechnung als PDF: A4 (Standard) oder Bon-Format 80 mm (format=Receipt); copy=true kennzeichnet den Nachdruck als Kopie.");

        group.MapGet("/receipts/{receiptId:guid}/send", async Task<Results<Ok<DocumentMailDraft>, NotFound>> (
                Guid receiptId, ClaimsPrincipal user, PosService pos, AispaDbContext db, DocumentHeaders headers, CancellationToken ct) =>
                await PrepareReceiptMailAsync(receiptId, user, pos, db, headers, ct) is { } prepared ? TypedResults.Ok(prepared.Draft) : TypedResults.NotFound())
            .WithName("ReceiptMailDraft")
            .AlsoAllow(AccessRight.ViewReceipts, insteadOf: AccessRight.Book)
            .WithSummary("Vorbelegung für „Per Mail senden“: Adresse des Gastes aus der Kundenkarte, Betreff und Text.");

        group.MapPost("/receipts/{receiptId:guid}/send", async Task<Results<Accepted, NotFound, ValidationProblem>> (
                Guid receiptId, SendDocumentRequest request, ClaimsPrincipal user, PosService pos, AispaDbContext db, DocumentHeaders headers, SentDocuments sent,
                TenantContext tenants, IMessageBus bus, CancellationToken ct) =>
            {
                if (DocumentMails.Validate(request) is { } errors)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                if (await PrepareReceiptMailAsync(receiptId, user, pos, db, headers, ct) is not { } prepared)
                {
                    return TypedResults.NotFound();
                }

                var (subject, text) = DocumentMails.Resolve(prepared.Draft, request);
                var pdf = ReceiptDocument.Build(prepared.Receipt, ReceiptDocument.Header(prepared.Header, prepared.Receipt), prepared.Zone, prepared.Currency, DocumentPageFormat.A4, false);
                await bus.PublishAsync(DocumentMails.Message(tenants.TenantId!.Value, request.To, subject, text, ReceiptDocument.FileName(prepared.Receipt), pdf));
                await sent.RecordAsync(SentDocument.ReceiptKind, receiptId, request.To, subject, user, ct);
                return TypedResults.Accepted($"/api/pos/receipts/{receiptId}");
            })
            .WithName("SendReceipt")
            .AlsoAllow(AccessRight.ViewReceipts, insteadOf: AccessRight.Book)
            .WithSummary("Beleg oder Rechnung als PDF per Mail an den Gast (über die Queue), mit Vermerk am Beleg; ohne Betreff und Text gilt die Vorbelegung.");

        group.MapPost("/receipts/{receiptId:guid}/recipient", async Task<Results<Ok<ReceiptDto>, ValidationProblem, ProblemHttpResult>> (
                Guid receiptId, InvoiceRecipient input, ClaimsPrincipal user, PosService pos, CancellationToken ct) =>
                Respond(await pos.AddRecipientAsync(receiptId, input, UserId(user), ct)))
            .WithName("AddReceiptRecipient")
            .WithSummary("Rechnungsempfänger zu einem Verkaufsbeleg ergänzen; danach druckt der Beleg als Rechnung. Einmal je Beleg, nicht änderbar.");

        group.MapPost("/vat-checks", async Task<Results<Ok<VatIdCheckDto>, ValidationProblem, ProblemHttpResult>> (
                VatIdCheckInput input, ClaimsPrincipal user, VatIdChecks checks, CancellationToken ct) =>
                Respond(await checks.StartAsync(input, UserId(user), ct)))
            .WithName("StartVatIdCheck")
            .WithSummary("UID-Nummer über VIES prüfen (über die Queue); das Ergebnis mit GET /vat-checks/{id} abfragen.");

        group.MapGet("/vat-checks/{vatIdCheckId:guid}", async Task<Results<Ok<VatIdCheckDto>, NotFound>> (
                Guid vatIdCheckId, VatIdChecks checks, CancellationToken ct) =>
                await checks.GetAsync(vatIdCheckId, ct) is { } check ? TypedResults.Ok(check) : TypedResults.NotFound())
            .WithName("GetVatIdCheck");

        group.MapGet("/vat-checks", async (string vatId, VatIdChecks checks, CancellationToken ct) => await checks.HistoryAsync(vatId, ct))
            .WithName("ListVatIdChecks")
            .WithSummary("Die letzten 20 Abfragen zu einer UID-Nummer, neueste zuerst.");

        group.MapPost("/receipts/{receiptId:guid}/void", async Task<Results<Ok<ReceiptDto>, ValidationProblem, ProblemHttpResult>> (
                Guid receiptId, VoidRequest request, ClaimsPrincipal user, PosService pos, AccessService access, IMessageBus bus,
                AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                // Vollmacht zusätzlich zur Rolle (R05 Slice 111, E11).
                if (await StaffPowerCheck.DeniedAsync(db, access, StaffPowerKind.CashVoid, time, ct) is { } denied)
                {
                    return Problem(new PosError(PosErrorKind.Forbidden, "", denied));
                }

                // Ein Anzahlungsbeleg bewegt Geld ohne Behandlung: den hebt nur die Leitung auf (Ä51), die Standortleitung und die Rezeption nicht. Das System retourniert über DepositReturns.
                if (await pos.IsDepositReceiptAsync(receiptId, ct) && (await access.CurrentAsync(ct))?.IsOwnerLevel != true)
                {
                    return Problem(new PosError(PosErrorKind.Forbidden, "", DepositReceiptVoidMessage));
                }

                var result = await StockAsync(bus, await pos.VoidAsync(receiptId, request.Reason, UserId(user), await SupervisorAsync(access, ct), ct));
                if (result.Value is { } voided)
                {
                    // Z. B. erstattet der Kundenbereich daraufhin eine Online-Zahlung.
                    await bus.PublishAsync(new ReceiptVoided(receiptId, voided.Id, request.Reason!, UserId(user)));
                }

                return Respond(result);
            })
            .WithName("VoidReceipt")
            .WithSummary("Storniert einen Beleg mit Begründung (Stornobeleg). Mit Kassenaufsicht jeden, sonst nur eigene der letzten 15 Minuten; "
                + "einen Anzahlungsbeleg nur Inhaber und Manager (403 sonst).");

        group.MapGet("/vouchers/{code}", async Task<Results<Ok<VoucherDto>, NotFound>> (string code, AispaDbContext db, CancellationToken ct) =>
            {
                var normalized = PosService.NormalizeCode(code);
                return await db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Code == normalized, ct) is { } v
                    ? TypedResults.Ok(PosService.ToDto(v))
                    : TypedResults.NotFound();
            })
            .WithName("GetVoucher")
            .WithSummary("Gutschein mit Restwert, z. B. vor dem Einlösen.");

        group.MapGet("/customers/{customerId:guid}/packages", async (Guid customerId, AispaDbContext db, CancellationToken ct) =>
                (await db.Vouchers.AsNoTracking()
                    .Where(v => v.CustomerId == customerId && v.Units != null && v.Status == VoucherStatus.Active)
                    .OrderBy(v => v.CreatedAt)
                    .ToListAsync(ct))
                .Select(PosService.ToDto).ToList())
            .WithName("GetCustomerPackages")
            .WithSummary("Gekaufte Pakete eines Gastes mit offenen Behandlungen, z. B. um an der Kassa eines abzuziehen.");

        return routes;
    }

    /// <summary>Meldung, wenn jemand ohne die Rolle Inhaber oder Manager einen Anzahlungsbeleg stornieren will (die Oberflächen zeigen sie als Hinweis am gesperrten Knopf).</summary>
    internal const string DepositReceiptVoidMessage = "Anzahlungsbelege storniert nur die Leitung.";

    private static ArticleDto ToDto(Article a) => new(a.Id, a.Name, a.Price, a.TaxCategory, a.RevenueAccountId);

    private static async Task<bool> SupervisorAsync(AccessService access, CancellationToken ct) =>
        (await access.CurrentAsync(ct))?.Has(AccessRight.SuperviseCash) == true;

    private static string? UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>Standort der Kasse eines Belegs: Zeitzone und Währung für Bon und PDF.</summary>
    private static Task<RegisterLocation> RegisterLocationAsync(AispaDbContext db, Guid registerId, CancellationToken ct) =>
        db.CashRegisters.Where(r => r.Id == registerId)
            .Join(db.Locations, r => r.LocationId, l => l.Id, (_, l) => new RegisterLocation(l.Id, l.TimeZone, l.Currency))
            .FirstAsync(ct);

    /// <summary>Beleg, Kopf, Zeitzone, Währung und die Vorbelegung der Mail (Adresse des Gastes aus der Kundenkarte); null, wenn es den Beleg nicht gibt.</summary>
    private static async Task<PreparedReceiptMail?> PrepareReceiptMailAsync(Guid receiptId, ClaimsPrincipal user, PosService pos, AispaDbContext db, DocumentHeaders headers, CancellationToken ct)
    {
        if (await pos.GetReceiptAsync(receiptId, ct) is not { } receipt)
        {
            return null;
        }

        var location = await RegisterLocationAsync(db, receipt.RegisterId, ct);
        var header = await headers.ForAsync(location.Id, user, ct);
        var zone = LocalTime.Zone(location.TimeZone);
        var email = receipt.CustomerId is { } customerId ? await db.Customers.Where(c => c.Id == customerId).Select(c => c.Email).FirstOrDefaultAsync(ct) : null;
        return new PreparedReceiptMail(receipt, header, zone, location.Currency, ReceiptDocument.Draft(receipt, header.SpaName, string.IsNullOrWhiteSpace(email) ? null : email, zone));
    }

    private sealed record RegisterLocation(Guid Id, string TimeZone, string Currency);

    private sealed record PreparedReceiptMail(ReceiptDto Receipt, DocumentHeader Header, TimeZoneInfo Zone, string Currency, DocumentMailDraft Draft);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Verkaufte oder stornierte Artikel bucht das Lager danach über die Queue.</summary>
    private static async Task<PosResult<ReceiptDto>> StockAsync(IMessageBus bus, PosResult<ReceiptDto> result)
    {
        if (result.Value is { } receipt)
        {
            await bus.PublishAsync(new BookReceiptStock(receipt.Id));
        }

        return result;
    }

    internal static Results<Ok<T>, ValidationProblem, ProblemHttpResult> Respond<T>(PosResult<T> result) =>
        result.Error is not { } error ? TypedResults.Ok(result.Value!)
        : error.Kind == PosErrorKind.Invalid ? Validation(error)
        : Problem(error);

    private static ValidationProblem Validation(PosError error) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [string.IsNullOrEmpty(error.Field) ? "" : char.ToLowerInvariant(error.Field[0]) + error.Field[1..]] = [error.Message],
        });

    private static ProblemHttpResult Problem(PosError error) => TypedResults.Problem(
        statusCode: error.Kind switch
        {
            PosErrorKind.NotFound => StatusCodes.Status404NotFound,
            PosErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status409Conflict,
        },
        title: error.Message);
}
