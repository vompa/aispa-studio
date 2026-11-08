using System.Security.Claims;
using AispaStudio.Api.Access;
using AispaStudio.Api.Data;
using AispaStudio.Api.Mail;
using AispaStudio.Api.MailIntake;
using AispaStudio.Api.Operations;
using AispaStudio.Api.Scheduling;
using AispaStudio.Api.Staff;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.Stock;

/// <summary>Lager: Materialien und Verkaufsprodukte, Bestand je Standort mit Bestellvorschlag, Bestellungen, Wareneingang, Inventur und Umbuchung.</summary>
internal static class StockEndpoints
{
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/stock").WithTags("Lager");

        group.MapGet("/items", async (AispaDbContext db, CancellationToken ct) =>
                (await db.StockItems.AsNoTracking().Include(i => i.Supplier).OrderBy(i => i.Name).ToListAsync(ct)).Select(ToDto).ToList())
            .WithName("ListStockItems")
            .RequirePermission(AccessRight.ViewCalendar)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapGet("/items/by-barcode", async Task<Results<Ok<StockItemDto>, NotFound>> (string code, AispaDbContext db, CancellationToken ct) =>
                await StockBarcodes.FindAsync(db, code, ct) is { } item ? TypedResults.Ok(ToDto(item)) : TypedResults.NotFound())
            .WithName("FindStockItemByBarcode")
            .WithSummary("Das aktive Material mit diesem Code (Handscanner im Wareneingang und in der Inventur); 404, wenn der Code unbekannt ist.")
            .RequirePermission(AccessRight.ViewCalendar)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapPost("/items", async Task<Results<Created<StockItemDto>, ValidationProblem>> (
                StockItemInput input, AispaDbContext db, StockSuppliers suppliers, CancellationToken ct) =>
            {
                if (await ValidateAsync(input, null, db, ct) is { } problem)
                {
                    return problem;
                }

                var item = new StockItem { Name = input.Name.Trim(), Unit = input.Unit.Trim() };
                await ApplyAsync(item, input, db, suppliers, ct);
                db.StockItems.Add(item);
                if (await StockBarcodes.SaveAsync(db, ct) is { } conflict)
                {
                    return conflict;
                }

                return TypedResults.Created($"/api/stock/items/{item.Id}", ToDto(item));
            })
            .WithName("CreateStockItem")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapPut("/items/{stockItemId:guid}", async Task<Results<Ok<StockItemDto>, NotFound, ValidationProblem>> (
                Guid stockItemId, StockItemInput input, AispaDbContext db, StockSuppliers suppliers, CancellationToken ct) =>
            {
                var item = await db.StockItems.FirstOrDefaultAsync(i => i.Id == stockItemId, ct);
                if (item is null)
                {
                    return TypedResults.NotFound();
                }

                if (await ValidateAsync(input, stockItemId, db, ct) is { } problem)
                {
                    return problem;
                }

                item.Name = input.Name.Trim();
                item.Unit = input.Unit.Trim();
                await ApplyAsync(item, input, db, suppliers, ct);
                if (await StockBarcodes.SaveAsync(db, ct) is { } conflict)
                {
                    return conflict;
                }

                return TypedResults.Ok(ToDto(item));
            })
            .WithName("UpdateStockItem")
            .WithSummary("Material ändern; der Einstandspreis gilt ab jetzt für neue Bewegungen und die Vollkosten. Deaktivieren statt löschen.")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapGet("/locations/{locationId:guid}", async Task<Results<Ok<StockOverviewDto>, NotFound>> (
                Guid locationId, AispaDbContext db, StockService stock, CancellationToken ct) =>
                await db.Locations.AnyAsync(l => l.Id == locationId, ct) ? TypedResults.Ok(await stock.OverviewAsync(locationId, ct)) : TypedResults.NotFound())
            .WithName("GetStock")
            .WithSummary("Bestand je Material mit Wert, Verbrauch laut Rezeptur für die gebuchten Termine der nächsten 14 Tage und Bestellvorschlag.")
            .RequirePermission(AccessRight.Book);

        group.MapGet("/locations/{locationId:guid}/demand", async Task<Results<Ok<StockDemandDto>, NotFound, ValidationProblem>> (
                Guid locationId, DateOnly from, DateOnly until, StockDemandService demand, CancellationToken ct) =>
            {
                if (from > until || until.DayNumber - from.DayNumber >= StockDemandService.MaxDays)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["until"] = [$"Zeitraum von höchstens {StockDemandService.MaxDays} Tagen, Ende nicht vor Beginn."],
                    });
                }

                return await demand.DemandAsync(locationId, from, until, ct) is { } result ? TypedResults.Ok(result) : TypedResults.NotFound();
            })
            .WithName("GetStockDemand")
            .WithSummary("Bedarfsprüfung: Verbrauch der gebuchten, offenen Termine eines Zeitraums (Ortszeit, beide Tage eingeschlossen) laut Rezept und Extras gegen den Bestand; "
                + "Meldungen, wo der Bestand nicht reicht, die Mindestmenge unterschritten wird oder nur noch für wenige Behandlungen bleibt. Bucht nichts.")
            .RequirePermission(AccessRight.Book);

        group.MapGet("/locations/{locationId:guid}/demand/booking", async Task<Results<Ok<List<StockDemandLine>>, NotFound>> (
                Guid locationId, Guid treatmentId, DateTimeOffset start, AispaDbContext db, StockDemandService demand, CancellationToken ct) =>
                await db.Locations.AnyAsync(l => l.Id == locationId, ct) && await db.Treatments.AnyAsync(t => t.Id == treatmentId, ct)
                    ? TypedResults.Ok(await demand.BookingCheckAsync(locationId, treatmentId, start, null, ct))
                    : TypedResults.NotFound())
            .WithName("PreviewBookingStockDemand")
            .WithSummary("Buchungsvorschau: Material, das mit einem neuen Termin dieser Behandlung zu dieser Zeit knapp wird (nur Hinweis, die Buchung bleibt erlaubt). Leer jenseits von 14 Tagen.")
            .RequirePermission(AccessRight.Book);

        group.MapGet("/locations/{locationId:guid}/batches", async Task<Results<Ok<StockBatchesDto>, NotFound>> (
                Guid locationId, AispaDbContext db, StockService stock, CancellationToken ct) =>
                await db.Locations.AnyAsync(l => l.Id == locationId, ct) ? TypedResults.Ok(await stock.BatchesAsync(locationId, ct)) : TypedResults.NotFound())
            .WithName("ListStockBatches")
            .WithSummary("Vorhandene Chargen mit Ablaufdatum, zuerst ablaufend zuerst; abgelaufene und bald ablaufende sind markiert.")
            .RequirePermission(AccessRight.Book);

        group.MapGet("/locations/{locationId:guid}/expiry-tasks", async Task<Results<Ok<StockExpiryTasksDto>, NotFound>> (
                Guid locationId, AispaDbContext db, StockService stock, CancellationToken ct) =>
                await db.Locations.AnyAsync(l => l.Id == locationId, ct) ? TypedResults.Ok(await stock.ExpiryTasksAsync(locationId, ct)) : TypedResults.NotFound())
            .WithName("GetStockExpiryTasks")
            .WithSummary("Aufgaben der Leitung: bald ablaufende Chargen je nach Frist des Materials, die Abverkaufsliste daraus und Abgelaufenes zum Ausbuchen.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/batch-usage", async Task<Results<Ok<StockBatchUsageDto>, NotFound>> (
                Guid itemId, string batch, StockService stock, CancellationToken ct) =>
                await stock.BatchUsageAsync(itemId, batch, ct) is { } usage ? TypedResults.Ok(usage) : TypedResults.NotFound())
            .WithName("GetStockBatchUsage")
            .WithSummary("Rückverfolgung einer Charge über alle Standorte: Eingänge, Verbrauch je Termin mit Kunde, Verkäufe je Beleg (Rückruf).")
            .RequirePermission(AccessRight.Book);

        group.MapGet("/locations/{locationId:guid}/movements", async Task<Results<Ok<List<StockMovementDto>>, NotFound, ValidationProblem>> (
                Guid locationId, DateOnly from, DateOnly until, AispaDbContext db, StockService stock, CancellationToken ct) =>
            {
                if (from > until || until.DayNumber - from.DayNumber > 366)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["until"] = ["Zeitraum von höchstens einem Jahr, Ende nach Beginn."] });
                }

                if (await db.Locations.Where(l => l.Id == locationId).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) is not { } timeZone)
                {
                    return TypedResults.NotFound();
                }

                var zone = LocalTime.Zone(timeZone);
                var (start, _) = LocalTime.Day(from, zone);
                var (_, end) = LocalTime.Day(until, zone);
                return TypedResults.Ok(await stock.MovementsAsync(locationId, start, end, ct));
            })
            .WithName("ListStockMovements")
            .WithSummary("Lagerbewegungen eines Zeitraums (Ortszeit, beide Tage eingeschlossen), neueste zuerst, höchstens 1000.")
            .RequirePermission(AccessRight.Book);

        group.MapPost("/locations/{locationId:guid}/movements", async Task<Results<Ok<List<StockMovementDto>>, ValidationProblem, ForbidHttpResult>> (
                Guid locationId, StockMovementInput input, ClaimsPrincipal user, AispaDbContext db, StockService stock, AccessService access,
                IMessageBus bus, TenantContext tenants, CancellationToken ct) =>
            {
                var current = (await access.CurrentAsync(ct))!;
                if (input.TargetLocationId is { } target && !current.CanSee(target))
                {
                    return TypedResults.Forbid();
                }

                // Die Inventur bucht nur die Leitung; Wareneingang, Nachfüllen, Umbuchen und Korrektur auch die Rezeption.
                if (input.Kind == StockMovementKind.Count && !current.Has(AccessRight.ManageLocation))
                {
                    return TypedResults.Forbid();
                }

                var (movements, errors) = await stock.AddAsync(locationId, input, user.FindFirstValue(ClaimTypes.NameIdentifier), ct);
                if (errors is not null)
                {
                    return errors.Problem();
                }

                await bus.PublishOperationsChangedAsync(tenants, locationId, OperationsChangeKind.Stock);
                if (input.TargetLocationId is { } targetLocation && targetLocation != locationId)
                {
                    await bus.PublishOperationsChangedAsync(tenants, targetLocation, OperationsChangeKind.Stock);
                }

                var name = await db.StockItems.Where(i => i.Id == input.ItemId).Select(i => i.Name).FirstAsync(ct);
                var placeIds = movements!.Select(m => m.StoragePlaceId).Distinct().ToList();
                var placeNames = await db.StoragePlaces.Where(p => placeIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
                return TypedResults.Ok(movements!.Select(m => StockService.ToDto(m, name, placeNames[m.StoragePlaceId])).ToList());
            })
            .WithName("AddStockMovement")
            .WithSummary("Wareneingang (ins Hauptlager), Inventur (gezählter Bestand eines Lagerorts), Korrektur mit Grund, Nachfüllen oder Umbuchen "
                + "an einen anderen Lagerort oder Standort. Inventur nur mit dem Recht für den Standort, die übrigen mit Buchen.")
            .RequirePermission(AccessRight.Book)
            .AllowChannel(AccessChannel.StaffApp);

        MapStoragePlaces(group);
        MapCountSessions(group);
        MapPurchaseOrders(group);
        MapSuppliers(group);
        return routes;
    }

    /// <summary>
    /// Zählung eines Lagerorts vom Handy: zählen und abschicken darf jede Person mit Kalenderzugang (sie bucht dabei nichts), ansehen, abschließen
    /// und verwerfen die Leitung des Standorts.
    /// </summary>
    private static void MapCountSessions(RouteGroupBuilder group)
    {
        group.MapPost("/locations/{locationId:guid}/storage-places/{placeId:guid}/count-sessions", async Task<Results<Ok<StockCountSessionDto>, ValidationProblem>> (
                Guid locationId, Guid placeId, ClaimsPrincipal user, StockCountSessions sessions, CancellationToken ct) =>
            {
                var (session, errors) = await sessions.StartAsync(locationId, placeId, user.FindFirstValue(ClaimTypes.NameIdentifier), ct);
                return errors is not null ? errors.Problem() : TypedResults.Ok(await sessions.ToDtoAsync(session!, withExpected: false, ct));
            })
            .WithName("StartStockCountSession")
            .WithSummary("Zählung des Lagerorts starten oder die laufende (offen oder abgeschickt) zurückgeben, damit zwei Handys nicht gegeneinander zählen. Ohne Soll: das Handy zählt blind.")
            .RequirePermission(AccessRight.ViewCalendar)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapPut("/locations/{locationId:guid}/count-sessions/{sessionId:guid}/lines/{itemId:guid}",
                async Task<Results<Ok<StockCountSessionDto>, NotFound, ValidationProblem>> (
                    Guid locationId, Guid sessionId, Guid itemId, StockCountLineInput input, StockCountSessions sessions, IMessageBus bus, TenantContext tenants,
                    CancellationToken ct) =>
                {
                    if (await sessions.FindAsync(locationId, sessionId, ct) is not { } session)
                    {
                        return TypedResults.NotFound();
                    }

                    if (await sessions.SetLineAsync(session, itemId, input.Counted, ct) is { } errors)
                    {
                        return errors.Problem();
                    }

                    return TypedResults.Ok(await sessions.ToDtoAsync(session, withExpected: false, ct));
                })
            .WithName("SetStockCountLine")
            .WithSummary("Zwischenstand einer Zeile (gezählte Menge ab 0, höchstens drei Nachkommastellen); ohne Menge wird die Zeile gelöscht. Nur solange die Zählung offen ist.")
            .RequirePermission(AccessRight.ViewCalendar)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapPost("/locations/{locationId:guid}/count-sessions/{sessionId:guid}/submit", async Task<Results<Ok<StockCountSessionDto>, NotFound, ValidationProblem>> (
                Guid locationId, Guid sessionId, StockCountSessions sessions, IMessageBus bus, TenantContext tenants, CancellationToken ct) =>
            {
                if (await sessions.FindAsync(locationId, sessionId, ct) is not { } session)
                {
                    return TypedResults.NotFound();
                }

                if (await sessions.SubmitAsync(session, ct) is { } errors)
                {
                    return errors.Problem();
                }

                await bus.PublishOperationsChangedAsync(tenants, locationId, OperationsChangeKind.Stock);
                return TypedResults.Ok(await sessions.ToDtoAsync(session, withExpected: false, ct));
            })
            .WithName("SubmitStockCountSession")
            .WithSummary("Zählung abschicken: sie wartet dann auf den Abschluss durch die Leitung, das Handy ändert nichts mehr. Ohne gezählte Zeile abgelehnt.")
            .RequirePermission(AccessRight.ViewCalendar)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapGet("/locations/{locationId:guid}/count-sessions", async (Guid locationId, StockCountSessionStatus? status, StockCountSessions sessions, CancellationToken ct) =>
                await sessions.ListAsync(locationId, status, ct))
            .WithName("ListStockCountSessions")
            .WithSummary("Zählungen vom Handy, neueste zuerst, mit gezählter Menge und Bestand des Lagerorts; mit status=Submitted die, die auf den Abschluss warten.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/locations/{locationId:guid}/count-sessions/{sessionId:guid}", async Task<Results<Ok<StockCountSessionDto>, NotFound>> (
                Guid locationId, Guid sessionId, StockCountSessions sessions, CancellationToken ct) =>
                await sessions.FindAsync(locationId, sessionId, ct) is { } session
                    ? TypedResults.Ok(await sessions.ToDtoAsync(session, withExpected: true, ct))
                    : TypedResults.NotFound())
            .WithName("GetStockCountSession")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/locations/{locationId:guid}/count-sessions/{sessionId:guid}/close", async Task<Results<Ok<StockCountSessionDto>, NotFound, ValidationProblem>> (
                Guid locationId, Guid sessionId, ClaimsPrincipal user, AispaDbContext db, StockCountSessions sessions, IMessageBus bus, TenantContext tenants,
                CancellationToken ct) =>
            {
                if (await sessions.FindAsync(locationId, sessionId, ct) is not { } session)
                {
                    return TypedResults.NotFound();
                }

                if (await sessions.CloseAsync(session, user.FindFirstValue(ClaimTypes.NameIdentifier), ct) is { } errors)
                {
                    return errors.Problem();
                }

                await bus.PublishOperationsChangedAsync(tenants, locationId, OperationsChangeKind.Stock);
                // Der Abschluss setzt den Status an der Datenbank vorbei: die Antwort aus frisch geladenen Daten.
                db.ChangeTracker.Clear();
                return TypedResults.Ok(await sessions.ToDtoAsync((await sessions.FindAsync(locationId, sessionId, ct))!, withExpected: true, ct));
            })
            .WithName("CloseStockCountSession")
            .WithSummary("Zählung abschließen: bucht jede abweichende Zeile als Inventur am Lagerort (derselbe Weg wie die Inventur in der Windows-App). Nur eine abgeschickte Zählung, nur einmal.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/locations/{locationId:guid}/count-sessions/{sessionId:guid}/discard", async Task<Results<Ok<StockCountSessionDto>, NotFound, ValidationProblem>> (
                Guid locationId, Guid sessionId, StockCountSessions sessions, IMessageBus bus, TenantContext tenants, CancellationToken ct) =>
            {
                if (await sessions.FindAsync(locationId, sessionId, ct) is not { } session)
                {
                    return TypedResults.NotFound();
                }

                if (await sessions.DiscardAsync(session, ct) is { } errors)
                {
                    return errors.Problem();
                }

                await bus.PublishOperationsChangedAsync(tenants, locationId, OperationsChangeKind.Stock);
                return TypedResults.Ok(await sessions.ToDtoAsync(session, withExpected: true, ct));
            })
            .WithName("DiscardStockCountSession")
            .WithSummary("Zählung verwerfen (offen oder abgeschickt); gebucht wird nichts.")
            .RequirePermission(AccessRight.ManageLocation);
    }

    /// <summary>Lieferantenstamm: sehen und pflegen mit dem Recht auf den Katalog; Löschen gibt es nicht, nur Deaktivieren.</summary>
    private static void MapSuppliers(RouteGroupBuilder group)
    {
        group.MapGet("/suppliers", (StockSuppliers suppliers, CancellationToken ct) => suppliers.ListAsync(ct))
            .WithName("ListStockSuppliers")
            .WithSummary("Lieferanten mit Kontakt, Lieferzeit, Mindestbestellwert und Bestellweg, nach Namen sortiert.")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapPost("/suppliers", async Task<Results<Created<StockSupplierDto>, ValidationProblem>> (
                StockSupplierInput input, StockSuppliers suppliers, CancellationToken ct) =>
            {
                var (supplier, errors) = await suppliers.CreateAsync(input, ct);
                return errors is not null ? errors.Problem() : TypedResults.Created($"/api/stock/suppliers/{supplier!.Id}", supplier);
            })
            .WithName("CreateStockSupplier")
            .WithSummary("Lieferant anlegen; der Name ist eindeutig, Groß- und Kleinschreibung zählen nicht.")
            .RequirePermission(AccessRight.ManageCatalog);

        group.MapPut("/suppliers/{supplierId:guid}", async Task<Results<Ok<StockSupplierDto>, NotFound, ValidationProblem>> (
                Guid supplierId, StockSupplierInput input, AispaDbContext db, StockSuppliers suppliers, CancellationToken ct) =>
            {
                if (await db.StockSuppliers.FirstOrDefaultAsync(s => s.Id == supplierId, ct) is not { } known)
                {
                    return TypedResults.NotFound();
                }

                var (supplier, errors) = await suppliers.UpdateAsync(known, input, ct);
                return errors is not null ? errors.Problem() : TypedResults.Ok(supplier!);
            })
            .WithName("UpdateStockSupplier")
            .WithSummary("Lieferant ändern oder deaktivieren. Bestellungen behalten den Namen zum Zeitpunkt der Bestellung.")
            .RequirePermission(AccessRight.ManageCatalog);
    }

    /// <summary>Lagerorte eines Standorts (ADR 0068): sehen mit Kalender-Recht, anlegen, ändern und löschen mit Standortrecht.</summary>
    private static void MapStoragePlaces(RouteGroupBuilder group)
    {
        group.MapGet("/locations/{locationId:guid}/storage-places", async Task<Results<Ok<List<StoragePlaceDto>>, NotFound>> (
                Guid locationId, AispaDbContext db, StoragePlaces places, CancellationToken ct) =>
                await db.Locations.AnyAsync(l => l.Id == locationId, ct) ? TypedResults.Ok(await places.ListAsync(locationId, ct)) : TypedResults.NotFound())
            .WithName("ListStoragePlaces")
            .WithSummary("Lagerorte des Standorts mit ihren Mindestmengen, in der Reihenfolge der Anzeige; das Hauptlager steht an erster Stelle.")
            .RequirePermission(AccessRight.ViewCalendar)
            .AllowChannel(AccessChannel.StaffApp);

        group.MapPost("/locations/{locationId:guid}/storage-places", async Task<Results<Created<StoragePlaceDto>, NotFound, ValidationProblem>> (
                Guid locationId, StoragePlaceInput input, AispaDbContext db, StoragePlaces places, CancellationToken ct) =>
            {
                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                var (place, errors) = await places.CreateAsync(locationId, input, ct);
                return errors is not null ? errors.Problem() : TypedResults.Created($"/api/stock/locations/{locationId}/storage-places/{place!.Id}", place);
            })
            .WithName("CreateStoragePlace")
            .WithSummary("Lagerort anlegen (höchstens 10 je Standort, das Hauptlager eingeschlossen).")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPut("/locations/{locationId:guid}/storage-places/{placeId:guid}", async Task<Results<Ok<StoragePlaceDto>, NotFound, ValidationProblem>> (
                Guid locationId, Guid placeId, StoragePlaceInput input, AispaDbContext db, StoragePlaces places, CancellationToken ct) =>
            {
                if (await db.StoragePlaces.Where(p => p.Id == placeId).Select(p => (Guid?)p.LocationId).FirstOrDefaultAsync(ct) is not { } placeLocation)
                {
                    return TypedResults.NotFound();
                }

                if (placeLocation != locationId)
                {
                    return WrongLocation();
                }

                var (place, errors) = await places.UpdateAsync(locationId, placeId, input, ct);
                return errors is not null ? errors.Problem() : TypedResults.Ok(place!);
            })
            .WithName("UpdateStoragePlace")
            .WithSummary("Lagerort ändern. Das Hauptlager lässt sich umbenennen und beschreiben, nicht deaktivieren. Mindestmengen: Liste ersetzt die bisherige, ohne Liste bleiben sie.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/locations/{locationId:guid}/storage-places/{placeId:guid}/minimums/copy-from/{sourcePlaceId:guid}",
                async Task<Results<Ok<StoragePlaceDto>, NotFound, ValidationProblem>> (
                    Guid locationId, Guid placeId, Guid sourcePlaceId, AispaDbContext db, StoragePlaces places, CancellationToken ct) =>
                {
                    var locations = await db.StoragePlaces.Where(p => p.Id == placeId || p.Id == sourcePlaceId).Select(p => new { p.Id, p.LocationId }).ToListAsync(ct);
                    if (!locations.Any(p => p.Id == placeId) || !locations.Any(p => p.Id == sourcePlaceId))
                    {
                        return TypedResults.NotFound();
                    }

                    if (locations.Any(p => p.LocationId != locationId))
                    {
                        return WrongLocation();
                    }

                    var (place, errors) = await places.CopyMinimumsAsync(locationId, placeId, sourcePlaceId, ct);
                    return errors is not null ? errors.Problem() : TypedResults.Ok(place!);
                })
            .WithName("CopyStoragePlaceMinimums")
            .WithSummary("Von Raum … übernehmen: ersetzt die Mindestmengen (Raumausstattung) des Lagerorts durch die des Quell-Lagerorts desselben Standorts.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapDelete("/locations/{locationId:guid}/storage-places/{placeId:guid}", async Task<Results<NoContent, NotFound, ValidationProblem>> (
                Guid locationId, Guid placeId, AispaDbContext db, StoragePlaces places, CancellationToken ct) =>
            {
                if (await db.StoragePlaces.Where(p => p.Id == placeId).Select(p => (Guid?)p.LocationId).FirstOrDefaultAsync(ct) is not { } placeLocation)
                {
                    return TypedResults.NotFound();
                }

                if (placeLocation != locationId)
                {
                    return WrongLocation();
                }

                return await places.DeleteAsync(placeId, ct) is { } errors ? errors.Problem() : TypedResults.NoContent();
            })
            .WithName("DeleteStoragePlace")
            .WithSummary("Lagerort löschen, solange er keine Bewegungen hat; das Hauptlager nie.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/locations/{locationId:guid}/storage-places/{placeId:guid}/levels", async Task<Results<Ok<StoragePlaceLevelsDto>, NotFound, ValidationProblem>> (
                Guid locationId, Guid placeId, AispaDbContext db, StockService stock, CancellationToken ct) =>
            {
                if (await db.StoragePlaces.Where(p => p.Id == placeId).Select(p => (Guid?)p.LocationId).FirstOrDefaultAsync(ct) is not { } placeLocation)
                {
                    return TypedResults.NotFound();
                }

                return placeLocation != locationId ? WrongLocation() : TypedResults.Ok(await stock.LevelsAsync(locationId, placeId, ct));
            })
            .WithName("GetStoragePlaceLevels")
            .WithSummary("Bestandsliste eines Lagerorts: Menge je Material, Mindestmenge des Orts (falls eingetragen) und Chargen.")
            .RequirePermission(AccessRight.ViewCalendar);
    }

    private static ValidationProblem WrongLocation() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["storagePlaceId"] = ["Der Lagerort gehört nicht zu diesem Standort."] });

    /// <summary>Bestellungen: anlegen (auch aus dem Vorschlag), als bestellt markieren, Lieferungen buchen; Standortleitung und Inhaber.</summary>
    private static void MapPurchaseOrders(RouteGroupBuilder group)
    {
        group.MapGet("/locations/{locationId:guid}/orders", async (Guid locationId, PurchaseOrders orders, CancellationToken ct) =>
                await orders.ListAsync(locationId, ct))
            .WithName("ListPurchaseOrders")
            .WithSummary("Offene Bestellungen des Standorts und die letzten 30 abgeschlossenen, neueste zuerst.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/locations/{locationId:guid}/orders", async Task<Results<Created<PurchaseOrderDto>, NotFound, ValidationProblem>> (
                Guid locationId, PurchaseOrderInput input, ClaimsPrincipal user, AispaDbContext db, PurchaseOrders orders, CancellationToken ct) =>
            {
                if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
                {
                    return TypedResults.NotFound();
                }

                var (order, errors) = await orders.CreateAsync(locationId, input, user.FindFirstValue(ClaimTypes.NameIdentifier), ct);
                return errors is not null ? errors.Problem() : TypedResults.Created($"/api/stock/orders/{order!.Id}", order);
            })
            .WithName("CreatePurchaseOrder")
            .WithSummary("Bestellung als Entwurf anlegen; ohne Preis gilt der aktuelle Einstandspreis.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/locations/{locationId:guid}/orders/from-suggestions", async Task<Results<Ok<List<PurchaseOrderDto>>, NotFound>> (
                Guid locationId, ClaimsPrincipal user, AispaDbContext db, PurchaseOrders orders, CancellationToken ct) =>
                await db.Locations.AnyAsync(l => l.Id == locationId, ct)
                    ? TypedResults.Ok(await orders.FromSuggestionsAsync(locationId, user.FindFirstValue(ClaimTypes.NameIdentifier), ct))
                    : TypedResults.NotFound())
            .WithName("CreatePurchaseOrdersFromSuggestions")
            .WithSummary("Je Lieferant ein Entwurf mit den vorgeschlagenen Mengen; Materialien ohne Lieferant bleiben draußen, offene Bestellungen sind abgezogen.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/orders/{purchaseOrderId:guid}", async Task<Results<Ok<PurchaseOrderDto>, NotFound>> (
                Guid purchaseOrderId, PurchaseOrders orders, CancellationToken ct) =>
                await orders.GetAsync(purchaseOrderId, ct) is { } order ? TypedResults.Ok(order) : TypedResults.NotFound())
            .WithName("GetPurchaseOrder")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPut("/orders/{purchaseOrderId:guid}", async Task<Results<Ok<PurchaseOrderDto>, NotFound, ValidationProblem>> (
                Guid purchaseOrderId, PurchaseOrderInput input, AispaDbContext db, PurchaseOrders orders, CancellationToken ct) =>
            {
                if (!await db.PurchaseOrders.AnyAsync(o => o.Id == purchaseOrderId, ct))
                {
                    return TypedResults.NotFound();
                }

                var (order, errors) = await orders.UpdateAsync(purchaseOrderId, input, ct);
                return errors is not null ? errors.Problem() : TypedResults.Ok(order!);
            })
            .WithName("UpdatePurchaseOrder")
            .WithSummary("Entwurf ändern: Lieferant, Positionen, Anmerkung.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/orders/{purchaseOrderId:guid}/ordered", async Task<Results<Ok<PurchaseOrderDto>, NotFound, ValidationProblem, ProblemHttpResult>> (
                Guid purchaseOrderId, AispaDbContext db, PurchaseOrders orders, AccessService access, TimeProvider time, CancellationToken ct) =>
            {
                if (await StaffPowerCheck.DeniedAsync(db, access, StaffPowerKind.TriggerOrders, time, ct) is { } denied)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Keine Berechtigung.", detail: denied);
                }

                if (!await db.PurchaseOrders.AnyAsync(o => o.Id == purchaseOrderId, ct))
                {
                    return TypedResults.NotFound();
                }

                var (order, errors) = await orders.MarkOrderedAsync(purchaseOrderId, ct);
                return errors is not null ? errors.Problem() : TypedResults.Ok(order!);
            })
            .WithName("MarkPurchaseOrderOrdered")
            .WithSummary("Entwurf als beim Lieferanten bestellt markieren; das Spa schickt die Bestellung selbst.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/locations/{locationId:guid}/reorder", async Task<Results<Ok<List<ReorderSuggestionDto>>, NotFound>> (
                Guid locationId, AispaDbContext db, StockService stock, CancellationToken ct) =>
                await db.Locations.AnyAsync(l => l.Id == locationId, ct)
                    ? TypedResults.Ok(await stock.ReorderSuggestionsAsync(locationId, ct))
                    : TypedResults.NotFound())
            .WithName("ListReorderSuggestions")
            .WithSummary("Bestellvorschlag mit Rechenweg: Tagesverbrauch der letzten 90 Tage, gebuchte Termine bis zur Lieferung, Lieferzeit, Bestellziel.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/orders/{purchaseOrderId:guid}/send", async Task<Results<Ok<PurchaseOrderDto>, NotFound, ValidationProblem, ProblemHttpResult>> (
                Guid purchaseOrderId, AispaDbContext db, PurchaseOrders orders, IMessageBus bus, TenantContext tenants, IMailSender shared, AccessService access, TimeProvider time,
                CancellationToken ct) =>
            {
                if (await StaffPowerCheck.DeniedAsync(db, access, StaffPowerKind.TriggerOrders, time, ct) is { } denied)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Keine Berechtigung.", detail: denied);
                }

                if (!await db.PurchaseOrders.AnyAsync(o => o.Id == purchaseOrderId, ct))
                {
                    return TypedResults.NotFound();
                }

                if (!SpaMailSender.CanDeliver(shared, await db.SpaMailboxes.AsNoTracking().ToListAsync(ct)))
                {
                    var noRoute = new Errors();
                    noRoute.Check(false, "mail", "Es ist kein Mailversand eingerichtet, die Bestellung bleibt Entwurf. Richte unter „Postfach & Nummern“ ein Postfach mit Versand ein.");
                    return noRoute.Problem();
                }

                var (order, mail, errors) = await orders.SendAsync(purchaseOrderId, ct);
                if (errors is not null)
                {
                    return errors.Problem();
                }

                await bus.PublishGuestMailAsync(tenants, mail!);
                return TypedResults.Ok(order!);
            })
            .WithName("SendPurchaseOrder")
            .WithSummary("Entwurf nach der Freigabe in der Maske per Mail an den Lieferanten schicken und auf „bestellt“ setzen; die Mail geht über die Queue.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/orders/{purchaseOrderId:guid}/close", async Task<Results<Ok<PurchaseOrderDto>, NotFound, ValidationProblem>> (
                Guid purchaseOrderId, AispaDbContext db, PurchaseOrders orders, CancellationToken ct) =>
            {
                if (!await db.PurchaseOrders.AnyAsync(o => o.Id == purchaseOrderId, ct))
                {
                    return TypedResults.NotFound();
                }

                var (order, errors) = await orders.CloseAsync(purchaseOrderId, ct);
                return errors is not null ? errors.Problem() : TypedResults.Ok(order!);
            })
            .WithName("ClosePurchaseOrder")
            .WithSummary("Ohne Lieferung stornieren, nach einer Teillieferung abschließen (der Rest kommt nicht mehr).")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapPost("/orders/{purchaseOrderId:guid}/deliveries", async Task<Results<Ok<PurchaseOrderDto>, NotFound, ValidationProblem>> (
                Guid purchaseOrderId, PurchaseOrderDeliveryInput input, ClaimsPrincipal user, AispaDbContext db, PurchaseOrders orders,
                IMessageBus bus, TenantContext tenants, CancellationToken ct) =>
            {
                if (!await db.PurchaseOrders.AnyAsync(o => o.Id == purchaseOrderId, ct))
                {
                    return TypedResults.NotFound();
                }

                var (order, errors) = await orders.DeliverAsync(purchaseOrderId, input, user.FindFirstValue(ClaimTypes.NameIdentifier), ct);
                if (errors is not null)
                {
                    return errors.Problem();
                }

                await bus.PublishOperationsChangedAsync(tenants, order!.LocationId, OperationsChangeKind.Stock);
                return TypedResults.Ok(order!);
            })
            .WithName("DeliverPurchaseOrder")
            .WithSummary("Lieferung buchen (auch in Teilen): je Position ein Wareneingang zum Bestell- oder Lieferscheinpreis.")
            .RequirePermission(AccessRight.ManageLocation);

        group.MapGet("/orders/{purchaseOrderId:guid}/text", async Task<Results<ContentHttpResult, NotFound>> (
                Guid purchaseOrderId, AispaDbContext db, PurchaseOrders orders, CancellationToken ct) =>
                await db.PurchaseOrders.AnyAsync(o => o.Id == purchaseOrderId, ct)
                    ? TypedResults.Text(await orders.TextAsync(purchaseOrderId, ct), "text/plain; charset=utf-8")
                    : TypedResults.NotFound())
            .WithName("GetPurchaseOrderText")
            .Produces<string>(StatusCodes.Status200OK, "text/plain")
            .WithSummary("Bestellung als Text zum Kopieren in eine Mail oder zum Ausdrucken.")
            .RequirePermission(AccessRight.ManageLocation);
    }

    private static async Task<ValidationProblem?> ValidateAsync(StockItemInput input, Guid? id, AispaDbContext db, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Check(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= 200, nameof(input.Name), "Bezeichnung angeben.");
        errors.Check(!string.IsNullOrWhiteSpace(input.Unit) && input.Unit.Length <= 20, nameof(input.Unit), "Einheit angeben, z. B. ml oder Stück.");
        errors.Check(input.UnitCost >= 0 && input.UnitCost < 1_000_000 && decimal.Round(input.UnitCost, 4) == input.UnitCost,
            nameof(input.UnitCost), "Einstandspreis ab 0 mit höchstens vier Nachkommastellen.");
        errors.Check(input.Supplier is null || input.Supplier.Trim().Length <= 200, nameof(input.Supplier), "Lieferant mit höchstens 200 Zeichen.");
        errors.Check(input.SupplierArticleNumber is null || input.SupplierArticleNumber.Trim().Length <= 50, nameof(input.SupplierArticleNumber),
            "Artikelnummer mit höchstens 50 Zeichen.");
        errors.Check(input.MinimumQuantity >= 0 && input.MinimumQuantity < 1_000_000, nameof(input.MinimumQuantity), "Mindestbestand nicht negativ.");
        StockPackaging.Check(errors, input);
        await StockBarcodes.CheckAsync(errors, input.Barcode, id, db, ct);
        if (input.SupplierId is { } supplierId)
        {
            // Ein deaktivierter Lieferant bleibt am Material, das ihn schon hat; neu zuordnen lässt er sich nicht.
            errors.Check(await db.StockSuppliers.AnyAsync(s => s.Id == supplierId && (s.IsActive || db.StockItems.Any(i => i.Id == id && i.SupplierId == supplierId)), ct),
                nameof(input.SupplierId), "Lieferant nicht gefunden oder deaktiviert.");
        }

        if (input.ArticleId is { } articleId)
        {
            errors.Check(await db.Articles.AnyAsync(a => a.Id == articleId, ct), nameof(input.ArticleId), "Artikel der Kassa nicht gefunden.");
            errors.Check(!await db.StockItems.AnyAsync(i => i.ArticleId == articleId && i.Id != id, ct), nameof(input.ArticleId),
                "Der Artikel ist schon mit einem anderen Material verknüpft.");
        }

        return errors.Any ? errors.Problem() : null;
    }

    private static async Task ApplyAsync(StockItem item, StockItemInput input, AispaDbContext db, StockSuppliers suppliers, CancellationToken ct)
    {
        item.UnitCost = input.UnitCost;
        item.MinimumQuantity = input.MinimumQuantity;
        item.ArticleId = input.ArticleId;
        item.IsActive = input.IsActive;
        var supplier = input.SupplierId is { } supplierId
            ? await db.StockSuppliers.FirstAsync(s => s.Id == supplierId, ct)
            : string.IsNullOrWhiteSpace(input.Supplier) ? null : await suppliers.FindOrAddAsync(input.Supplier, ct);
        // Die Navigation ist beim Ändern nicht geladen: null darauf allein ließe den alten Fremdschlüssel stehen.
        item.Supplier = supplier;
        item.SupplierId = supplier?.Id;
        item.Barcode = StockBarcodes.Normalize(input.Barcode);
        item.SupplierArticleNumber = string.IsNullOrWhiteSpace(input.SupplierArticleNumber) ? null : input.SupplierArticleNumber.Trim();
        StockPackaging.Apply(item, input);
    }

    private static StockItemDto ToDto(StockItem i) =>
        new(i.Id, i.Name, i.Unit, i.UnitCost, i.MinimumQuantity, i.ArticleId, i.IsActive, i.Supplier?.Name, i.SupplierArticleNumber, i.SupplierId, i.PackUnit, i.PackSize,
            i.CaseUnit, i.PacksPerCase, i.TreatmentDose, i.ExpiryWarningDays, StockPackaging.Packages(i), i.Barcode);
}
