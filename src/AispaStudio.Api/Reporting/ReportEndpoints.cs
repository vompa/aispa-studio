using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Online;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Stock;
using AispaStudio.Api.System;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Reporting;

/// <summary>
/// Berichte je Rolle: Tagesplan und Behandlungskarte (Therapeuten, nur eigene Termine), Tagesstart (Rezeption),
/// Management-Cockpit und Pflichtberichte (Inhaber, Standortleitung).
/// </summary>
internal static class ReportEndpoints
{
    /// <summary>
    /// Hinweise aus dem neuesten Bogen je Gast, nur ja oder nein; der Inhalt bleibt verschlüsselt im Schema health.
    /// „Anamnese prüfen“ (<see cref="TreatmentCard.CheckAnamnesis"/>) bekommt jede Karte, deren Behandlung es verlangt und deren Gast Angaben gemacht hat,
    /// so wie die Rezeption es beim Buchen sieht. Das Allergie-Kennzeichen sieht nur, wer die Person selbst behandelt (<paramref name="own"/>).
    /// </summary>
    private static async Task<DayPlanDto> WithAnamnesisHintsAsync(DayPlanDto plan, bool own, bool hints, AnamnesisStore anamnesis, AispaDbContext db,
        CancellationToken ct)
    {
        var appointmentIds = plan.Cards.Select(c => c.AppointmentId).ToList();
        var toCheck = (await db.Appointments.AsNoTracking().Where(a => appointmentIds.Contains(a.Id) && a.Treatment.CheckAnamnesis).Select(a => a.Id).ToListAsync(ct)).ToHashSet();
        var allergies = new HashSet<Guid>();
        var findings = new HashSet<Guid>();
        foreach (var customerId in plan.Cards.Where(c => own || hints || toCheck.Contains(c.AppointmentId)).Select(c => c.CustomerId).Distinct())
        {
            if (await anamnesis.LatestAsync(customerId, ct) is not { } record)
            {
                continue;
            }

            if (own && record.Answers.Any(a => a.Key == "allergies" && a.Yes == true))
            {
                allergies.Add(customerId);
            }

            if (AnamnesisStaffEndpoints.HasFindings(record))
            {
                findings.Add(customerId);
            }
        }

        return plan with
        {
            Cards = [.. plan.Cards.Select(c => c with
            {
                AllergyFlag = allergies.Contains(c.CustomerId),
                CheckAnamnesis = toCheck.Contains(c.AppointmentId) && findings.Contains(c.CustomerId),
                HasHealthHints = hints && findings.Contains(c.CustomerId),
            })],
        };
    }

    /// <summary>Hinweis unter den Gesundheitshinweisen der Karte.</summary>
    public const string HealthHintsNotice = "Angaben des Gastes aus dem Fragebogen. Ihr Abruf wird protokolliert.";

    /// <summary>
    /// Gesundheitshinweise auf der Karte (Auftraggeber, 19.11.2026): wer den Gast behandelt, die Rezeption und die Standortleitung des Standorts;
    /// Inhaber, Manager und Backoffice nicht. Die Rolle braucht dazu das Recht, Gesundheitsdaten zu lesen.
    /// </summary>
    internal static bool MaySeeHealthHints(UserAccess access, Guid employeeId, Guid locationId) =>
        (access.EmployeeId == employeeId)
        || (access.Role is StaffRole.Reception or StaffRole.Manager && access.CanSee(locationId));

    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/reports").WithTags("Berichte");

        group.MapGet("/locations/{locationId:guid}/day-plan", async Task<Results<Ok<DayPlanDto>, NotFound, ProblemHttpResult>> (
                Guid locationId, DateOnly date, Guid? employeeId, DailyReports reports, AccessService accessService, AnamnesisStore anamnesis, AispaDbContext db, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var employee = employeeId ?? access.EmployeeId;
                if (employee is null)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Person fehlt.",
                        detail: "Das Konto ist mit keiner Person im Team verknüpft; bitte eine Person wählen.");
                }

                if (!access.Has(AccessRight.Book) && employee != access.EmployeeId)
                {
                    return (ProblemHttpResult)PermissionExtensions.Forbidden("Nur der eigene Tagesplan.");
                }

                if (await reports.DayPlanAsync(locationId, employee.Value, date, ct) is not { } plan)
                {
                    return TypedResults.NotFound();
                }

                // Nur wer die Person selbst behandelt, sieht das Kennzeichen; die Rezeption sieht weiter nur den Stand des Fragebogens.
                // Ob es Gesundheitshinweise gibt (nur ja oder nein); lesen kann man sie über die Karte, mit Protokoll.
                var hints = access.Has(AccessRight.ViewHealthData) && MaySeeHealthHints(access, employee.Value, locationId);
                return TypedResults.Ok(await WithAnamnesisHintsAsync(plan, employee == access.EmployeeId, hints, anamnesis, db, ct));
            })
            .WithName("GetDayPlan")
            .WithSummary("Tagesplan mit Behandlungskarten; ohne Person der eigene. Im eigenen Plan sagt AllergyFlag nur ja oder nein, ohne Inhalt.")
            .RequirePermission(AccessRight.ViewCalendar)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapGet("/appointments/{appointmentId:guid}/card", async Task<Results<Ok<TreatmentCard>, NotFound>> (
                Guid appointmentId, AispaDbContext db, TreatmentCards cards, CancellationToken ct) =>
            {
                var appointment = await db.Appointments.AsNoTracking().Include(a => a.Treatment).Include(a => a.Customer)
                    .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
                if (appointment is null)
                {
                    return TypedResults.NotFound();
                }

                var zone = await db.Locations.Where(l => l.Id == appointment.LocationId).Select(l => l.TimeZone).FirstAsync(ct);
                return TypedResults.Ok((await cards.BuildAsync([appointment], LocalTime.Zone(zone), ct))[0]);
            })
            .WithName("GetTreatmentCard")
            .RequirePermission(AccessRight.ChangeStatus)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapGet("/appointments/{appointmentId:guid}/health", async Task<Results<Ok<TreatmentHealthHints>, NotFound, ProblemHttpResult>> (
                Guid appointmentId, ClaimsPrincipal principal, AispaDbContext db, AccessService accessService, AnamnesisStore anamnesis, HealthAccessLog log,
                CancellationToken ct) =>
            {
                var appointment = await db.Appointments.AsNoTracking().Where(a => a.Id == appointmentId)
                    .Select(a => new { a.CustomerId, a.EmployeeId, a.LocationId }).FirstOrDefaultAsync(ct);
                if (appointment is null)
                {
                    return TypedResults.NotFound();
                }

                var access = (await accessService.CurrentAsync(ct))!;
                if (!MaySeeHealthHints(access, appointment.EmployeeId, appointment.LocationId))
                {
                    return (ProblemHttpResult)PermissionExtensions.Forbidden(
                        "Gesundheitshinweise sieht nur, wer den Gast behandelt, die Rezeption und die Standortleitung.");
                }

                var record = await anamnesis.LatestAsync(appointment.CustomerId, ct);
                if (record is not null)
                {
                    await log.WriteAsync(principal, appointment.CustomerId, appointmentId, HealthAccessPurpose.TreatmentCard, ct);
                }

                return TypedResults.Ok(new TreatmentHealthHints(appointmentId, record?.SubmittedAt, record is null ? [] : AnamnesisForm.Hints(record), HealthHintsNotice));
            })
            .WithName("GetTreatmentHealthHints")
            .WithSummary("Gesundheitshinweise für die Behandlungskarte (nur „Ja“ und Sonstiges); jeder Abruf wird protokolliert.")
            .RequirePermission(AccessRight.ViewHealthData)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapPut("/appointments/{appointmentId:guid}/equipment-prepared", async Task<Results<NoContent, NotFound>> (
                Guid appointmentId, PreparationCheck input, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
            {
                if (!await db.Appointments.AnyAsync(a => a.Id == appointmentId && a.EquipmentId != null, ct))
                {
                    return TypedResults.NotFound();
                }

                var check = await db.AppointmentEquipmentChecks.FirstOrDefaultAsync(c => c.AppointmentId == appointmentId, ct);
                if (input.Done && check is null)
                {
                    db.AppointmentEquipmentChecks.Add(new AppointmentEquipmentCheck
                    {
                        AppointmentId = appointmentId,
                        CheckedAt = time.GetUtcNow(),
                        UserId = (await accessService.CurrentAsync(ct))?.UserId,
                    });
                }
                else if (!input.Done && check is not null)
                {
                    db.AppointmentEquipmentChecks.Remove(check);
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("CheckEquipmentPrepared")
            .RequirePermission(AccessRight.ChangeStatus)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapPut("/appointments/{appointmentId:guid}/preparation/{stepId:guid}", async Task<Results<NoContent, NotFound>> (
                Guid appointmentId, Guid stepId, PreparationCheck input, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
            {
                var valid = await db.Appointments.AnyAsync(a => a.Id == appointmentId
                    && db.TreatmentPreparationSteps.Any(s => s.Id == stepId && s.TreatmentId == a.TreatmentId), ct);
                if (!valid)
                {
                    return TypedResults.NotFound();
                }

                var check = await db.AppointmentPreparationChecks.FirstOrDefaultAsync(c => c.AppointmentId == appointmentId && c.StepId == stepId, ct);
                if (input.Done && check is null)
                {
                    db.AppointmentPreparationChecks.Add(new AppointmentPreparationCheck
                    {
                        AppointmentId = appointmentId,
                        StepId = stepId,
                        CheckedAt = time.GetUtcNow(),
                        UserId = (await accessService.CurrentAsync(ct))?.UserId,
                    });
                }
                else if (!input.Done && check is not null)
                {
                    db.AppointmentPreparationChecks.Remove(check);
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("CheckPreparationStep")
            .RequirePermission(AccessRight.ChangeStatus)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapPut("/appointments/{appointmentId:guid}/follow-up", async Task<Results<NoContent, NotFound, ValidationProblem>> (
                Guid appointmentId, FollowUpNote input, AispaDbContext db, AccessService accessService, TimeProvider time, CancellationToken ct) =>
            {
                if (!await db.Appointments.AnyAsync(a => a.Id == appointmentId, ct))
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check((input.Note ?? "").Length <= 2000, nameof(input.Note), "Höchstens 2000 Zeichen.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                var note = await db.AppointmentFollowUps.FirstOrDefaultAsync(f => f.AppointmentId == appointmentId, ct);
                if (string.IsNullOrWhiteSpace(input.Note))
                {
                    if (note is not null)
                    {
                        db.AppointmentFollowUps.Remove(note);
                    }
                }
                else
                {
                    if (note is null)
                    {
                        db.AppointmentFollowUps.Add(note = new AppointmentFollowUp { AppointmentId = appointmentId, Note = "" });
                    }

                    note.Note = input.Note.Trim();
                    note.UpdatedAt = time.GetUtcNow();
                    note.UserId = (await accessService.CurrentAsync(ct))?.UserId;
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("SetFollowUpNote")
            .WithSummary("Nachbereitung: Notiz für die nächste Behandlung des Gastes.")
            .RequirePermission(AccessRight.ChangeStatus)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapGet("/customers/{customerId:guid}/preferences", async (Guid customerId, AispaDbContext db, CancellationToken ct) =>
                await db.CustomerPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.CustomerId == customerId, ct) is { } p
                    ? new CustomerPreferences(p.Pressure, p.Scent, p.RoomTemperature, p.Music, p.PrefersQuiet, p.FocusAreas, p.AvoidAreas, p.Other)
                    : new CustomerPreferences(null, null, null, null, null, null, null, null))
            .WithName("GetCustomerPreferences")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPut("/customers/{customerId:guid}/preferences", async Task<Results<Ok<CustomerPreferences>, NotFound, ValidationProblem>> (
                Guid customerId, CustomerPreferences input, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                foreach (var (field, value, max) in new[]
                {
                    (nameof(input.Pressure), input.Pressure, 100), (nameof(input.Scent), input.Scent, 100),
                    (nameof(input.RoomTemperature), input.RoomTemperature, 100), (nameof(input.Music), input.Music, 100),
                    (nameof(input.FocusAreas), input.FocusAreas, 300), (nameof(input.AvoidAreas), input.AvoidAreas, 300), (nameof(input.Other), input.Other, 1000),
                })
                {
                    errors.Check((value ?? "").Length <= max, field, $"Höchstens {max} Zeichen.");
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                var p = await db.CustomerPreferences.FirstOrDefaultAsync(x => x.CustomerId == customerId, ct);
                if (p is null)
                {
                    db.CustomerPreferences.Add(p = new CustomerPreference { CustomerId = customerId });
                }

                p.Pressure = Blank(input.Pressure);
                p.Scent = Blank(input.Scent);
                p.RoomTemperature = Blank(input.RoomTemperature);
                p.Music = Blank(input.Music);
                p.PrefersQuiet = input.PrefersQuiet;
                p.FocusAreas = Blank(input.FocusAreas);
                p.AvoidAreas = Blank(input.AvoidAreas);
                p.Other = Blank(input.Other);
                p.UpdatedAt = time.GetUtcNow();
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new CustomerPreferences(p.Pressure, p.Scent, p.RoomTemperature, p.Music, p.PrefersQuiet, p.FocusAreas, p.AvoidAreas, p.Other));
            })
            .WithName("SetCustomerPreferences")
            .WithSummary("Vorlieben des Gastes (keine Gesundheitsdaten); gelten an allen Standorten.")
            .RequirePermission(AccessRight.ChangeStatus);

        group.MapGet("/treatments/{treatmentId:guid}/preparation", async (Guid treatmentId, AispaDbContext db, CancellationToken ct) =>
                await PreparationStepsAsync(db, treatmentId, ct))
            .WithName("ListPreparationSteps")
            .RequirePermission(AccessRight.ViewCalendar);

        group.MapPut("/treatments/{treatmentId:guid}/preparation", async Task<Results<Ok<List<PreparationStepDto>>, NotFound, ValidationProblem>> (
                Guid treatmentId, List<PreparationStepInput> input, AispaDbContext db, CancellationToken ct) =>
            {
                if (!await db.Treatments.AnyAsync(t => t.Id == treatmentId, ct))
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                var recipe = await StockRecipes.ForAsync(db, treatmentId, ct);
                errors.Check(input.Count <= 30, "steps", "Höchstens 30 Schritte.");
                for (var i = 0; i < input.Count; i++)
                {
                    errors.Check(!string.IsNullOrWhiteSpace(input[i].Text) && input[i].Text.Length <= 300, $"[{i}].{nameof(PreparationStepInput.Text)}", "Text angeben, höchstens 300 Zeichen.");
                    errors.Check(input[i].MinutesBefore is null or (>= 0 and <= 240), $"[{i}].{nameof(PreparationStepInput.MinutesBefore)}", "Vorlauf 0 bis 240 Minuten.");
                    errors.Check(input[i].StockItemId is not { } item || recipe.ContainsKey(item), $"[{i}].{nameof(PreparationStepInput.StockItemId)}",
                        "Das Material zuerst im Rezept der Behandlung (Kosten) oder bei „Zum Termin bereitstellen“ eintragen.");
                }

                if (errors.Any)
                {
                    return errors.Problem();
                }

                // Gleichbleibende Schritte behalten ihre Id, damit abgehakte Schritte heutiger Termine abgehakt bleiben.
                var existing = await db.TreatmentPreparationSteps.Where(s => s.TreatmentId == treatmentId).ToListAsync(ct);
                var saved = new List<TreatmentPreparationStep>();
                for (var i = 0; i < input.Count; i++)
                {
                    var text = input[i].Text.Trim();
                    var step = existing.FirstOrDefault(e => !saved.Contains(e) && e.Text == text && e.MinutesBefore == input[i].MinutesBefore
                        && e.StockItemId == input[i].StockItemId);
                    if (step is null)
                    {
                        db.TreatmentPreparationSteps.Add(step = new TreatmentPreparationStep
                        {
                            TreatmentId = treatmentId,
                            Text = text,
                            MinutesBefore = input[i].MinutesBefore,
                            StockItemId = input[i].StockItemId,
                        });
                    }

                    step.Position = i;
                    saved.Add(step);
                }

                db.TreatmentPreparationSteps.RemoveRange(existing.Except(saved));
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await PreparationStepsAsync(db, treatmentId, ct));
            })
            .WithName("SetPreparationSteps")
            .WithSummary("Behandlungsrezept: was vor jeder Behandlung vorzubereiten ist; ersetzt die bisherige Liste.")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapGet("/locations/{locationId:guid}/briefing", async Task<Results<Ok<BriefingDto>, NotFound>> (
                Guid locationId, DateOnly date, DailyReports reports, BriefingSummaries summaries, CancellationToken ct) =>
                await reports.BriefingAsync(locationId, date, ct) is { } briefing
                    ? TypedResults.Ok(await summaries.AttachAsync(briefing, ct))
                    : TypedResults.NotFound())
            .WithName("GetBriefing")
            .WithSummary("Tagesstart der Rezeption: Personal, Handlungsbedarf, Gäste, Räume, Kassa; mit KI zusätzlich eine Zusammenfassung, die später nachkommt.")
            .RequirePermission(AccessRight.Book);

        group.MapGet("/locations/{locationId:guid}/management", async Task<Results<Ok<ManagementReportDto>, NotFound, ValidationProblem>> (
                Guid locationId, DateOnly from, DateOnly until, ManagementReports reports, ManagementSummaries summaries, IManagementAnalyst analyst, AccessService accessService,
                CancellationToken ct, string compare = ManagementReports.CompareModes.Previous) =>
            {
                if (from > until || until.DayNumber - from.DayNumber > 366)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["until"] = ["Zeitraum von höchstens einem Jahr, Ende nach Beginn."] });
                }

                if (!ManagementReports.CompareModes.IsKnown(compare))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["compare"] = ["Vergleich: previous (Zeitraum davor), month (Vormonat) oder year (Vorjahr)."] });
                }

                var access = (await accessService.CurrentAsync(ct))!;
                // Gästenamen (beste und abwandernde Gäste) mit demselben Recht wie die Kundenliste.
                return await reports.ManagementAsync(locationId, from, until, access.Has(AccessRight.ViewAmountReports), ct, compare,
                        guests: access.Has(AccessRight.ViewCalendar)) is { } report
                    ? TypedResults.Ok(await summaries.AttachAsync(report, ct) with { AiAnalysisAvailable = analyst.Enabled })
                    : TypedResults.NotFound();
            })
            .WithName("GetManagementReport")
            .WithSummary("Kennzahlen eines Zeitraums mit Vergleich zum gleich langen Zeitraum davor (compare=previous, Standard), zum selben Abschnitt des Vormonats (month) oder zum Vorjahr (year); ohne das Recht „Berichte mit Beträgen“ ohne Geldbeträge. Auch in der Mitarbeiter-App für „Heute im Spa“, mit den Rechten der Rolle.")
            .RequirePermission(AccessRight.ViewFinance)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapGet("/mail-subscription", async Task<Ok<ReportMailSubscription>> (AccessService accessService, AispaDbContext db, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                var row = await db.StaffAccess.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == access.UserId, ct);
                var available = ReportMailSetup.Receives(access.Role);
                return TypedResults.Ok(new ReportMailSubscription(available && row?.ReportMails != false, available));
            })
            .WithName("GetReportMailSubscription")
            .WithSummary("Ob die angemeldete Person Wochen- und Monatsbericht per Mail bekommt (Inhaber, Manager, Standortleitung).")
            .RequirePermission(AccessRight.ViewFinance);

        group.MapPut("/mail-subscription", async Task<Results<Ok<ReportMailSubscription>, ValidationProblem>> (
                ReportMailSubscriptionInput input, AccessService accessService, AispaDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var access = (await accessService.CurrentAsync(ct))!;
                if (!ReportMailSetup.Receives(access.Role) || await db.StaffAccess.FirstOrDefaultAsync(a => a.UserId == access.UserId, ct) is not { } row)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["enabled"] = ["Wochen- und Monatsbericht per Mail gibt es für Inhaber, Manager und Standortleitung."],
                    });
                }

                row.ReportMails = input.Enabled;
                row.UpdatedAt = time.GetUtcNow();
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new ReportMailSubscription(row.ReportMails, true));
            })
            .WithName("SetReportMailSubscription")
            .WithSummary("Wochen- und Monatsbericht per Mail für die angemeldete Person ein- oder abbestellen.")
            .RequirePermission(AccessRight.ViewFinance);

        group.MapGet("/locations/{locationId:guid}/stock-usage", async Task<Results<Ok<StockUsageReportDto>, NotFound, ValidationProblem>> (
                Guid locationId, DateOnly from, DateOnly until, StockUsageReport report, AccessService accessService, CancellationToken ct) =>
            {
                if (from > until || until.DayNumber - from.DayNumber > StockUsageReport.MaxDays)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["until"] = ["Zeitraum von höchstens einem Jahr, Ende nach Beginn."] });
                }

                var amounts = (await accessService.CurrentAsync(ct))!.Has(AccessRight.ViewAmountReports);
                return await report.ReportAsync(locationId, from, until, amounts, ct) is { } result ? TypedResults.Ok(result) : TypedResults.NotFound();
            })
            .WithName("GetStockUsageReport")
            .WithSummary("Soll-Ist-Verbrauch je Artikel eines Zeitraums (Ortszeit, beide Tage eingeschlossen): Soll laut Rezept, Mehrverbrauch aus den Inventuren, Ist, "
                + "dazu die Behandlungen, die den Artikel nutzen. Der Wert in Euro nur mit dem Recht „Berichte mit Beträgen“.")
            .RequirePermission(AccessRight.ViewFinance);

        group.MapGet("/locations/{locationId:guid}/benchmark", async Task<Results<Ok<BenchmarkReport>, NotFound, ValidationProblem>> (
                Guid locationId, DateOnly? until, int? days, Benchmark benchmark, AispaDbContext db, AccessService accessService, TimeProvider time,
                CancellationToken ct) =>
            {
                var length = days ?? 30;
                if (length is < 7 or > 365)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["days"] = ["Zeitraum von 7 bis 365 Tagen."] });
                }

                // Ohne Angabe: die letzten vollen Tage bis gestern, damit ein halber Tag die Quoten nicht verzerrt.
                var zone = await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
                if (zone is null)
                {
                    return TypedResults.NotFound();
                }

                var end = until ?? LocalTime.LocalDate(time.GetUtcNow(), LocalTime.Zone(zone)).AddDays(-1);
                var amounts = (await accessService.CurrentAsync(ct))!.Has(AccessRight.ViewAmountReports);
                return await benchmark.CompareAsync(locationId, end.AddDays(1 - length), end, amounts, ct) is { } report
                    ? TypedResults.Ok(report)
                    : TypedResults.NotFound();
            })
            .RequireModule(AppModule.Benchmark)
            .WithName("GetBenchmark")
            .WithSummary("Ein-Klick-Vergleich mit anonymisierten Kennzahlen vergleichbarer Spas (Land, Größe).")
            .RequirePermission(AccessRight.ViewFinance);

        group.MapGet("/locations/{locationId:guid}/compliance", async Task<Results<Ok<ComplianceReportDto>, NotFound>> (
                Guid locationId, DateOnly date, ManagementReports reports, AccessService accessService, CancellationToken ct) =>
                await reports.ComplianceAsync(locationId, date, (await accessService.CurrentAsync(ct))!.Has(AccessRight.ViewAmountReports), ct) is { } report
                    ? TypedResults.Ok(report)
                    : TypedResults.NotFound())
            .WithName("GetComplianceReport")
            .WithSummary("Pflichtberichte: Kassen je Land, Gutscheinbestand (Summe nur mit dem Recht „Berichte mit Beträgen“), Pakete, Arbeitszeitaufzeichnungen des Monats.")
            .RequirePermission(AccessRight.ViewFinance);

        return routes;
    }

    /// <summary>Schritte der Vorbereitung mit dem Lagerartikel und der Menge je Termin laut Rezept.</summary>
    private static async Task<List<PreparationStepDto>> PreparationStepsAsync(AispaDbContext db, Guid treatmentId, CancellationToken ct)
    {
        var steps = await db.TreatmentPreparationSteps.AsNoTracking().Where(s => s.TreatmentId == treatmentId).OrderBy(s => s.Position).ToListAsync(ct);
        var itemIds = steps.Where(s => s.StockItemId != null).Select(s => s.StockItemId!.Value).ToList();
        var items = await db.StockItems.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var recipe = await StockRecipes.ForAsync(db, treatmentId, ct);
        return [.. steps.Select(s => s.StockItemId is { } id && items.TryGetValue(id, out var item)
            ? new PreparationStepDto(s.Id, s.Text, s.MinutesBefore, id, item.Name, recipe.GetValueOrDefault(id), item.Unit)
            : new PreparationStepDto(s.Id, s.Text, s.MinutesBefore))];
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
