using AispaStudio.Api.Data;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Access;

internal static class PermissionExtensions
{
    /// <summary>
    /// Verlangt eine Berechtigung und Zugang zum betroffenen Standort. Der Standort ergibt sich aus der Route
    /// (locationId, appointmentId, roomId, equipmentId, employeeId, blockId, registerId, billId, receiptId, terminalId, cardPaymentId, mailId, feedId, purchaseOrderId, displayId) oder aus dem
    /// Anfragekörper (Sperrzeit, oder eine Eigenschaft LocationId bzw. RegisterId).
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, AccessRight permission)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization();
        builder.AddEndpointFilter(new PermissionFilter(permission));
        builder.WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status403Forbidden, typeof(ProblemDetails), ["application/problem+json"]));
        return builder;
    }

    /// <summary>
    /// Lässt zusätzlich zu <paramref name="insteadOf"/> auch das Recht <paramref name="alternative"/> zu, nur für diesen einen Endpunkt.
    /// So lesen Rollen wie das Backoffice einzelne Abfragen einer Gruppe, ohne deren Schreib-Endpunkte zu erreichen.
    /// </summary>
    public static TBuilder AlsoAllow<TBuilder>(this TBuilder builder, AccessRight alternative, AccessRight insteadOf)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new AlsoAllowed(insteadOf, alternative));

    public static IResult Forbidden(string detail) =>
        TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Keine Berechtigung.", detail: detail);
}

/// <summary>Am Endpunkt: wer <paramref name="Alternative"/> hat, besteht die Prüfung auf <paramref name="Instead"/> ebenfalls.</summary>
internal sealed record AlsoAllowed(AccessRight Instead, AccessRight Alternative);

internal sealed class PermissionFilter(AccessRight permission) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var ct = http.RequestAborted;
        var access = await http.RequestServices.GetRequiredService<AccessService>().CurrentAsync(ct);
        if (access is null)
        {
            return PermissionExtensions.Forbidden("Für dieses Konto ist kein Zugang eingerichtet oder er ist gesperrt.");
        }

        var alternatives = http.GetEndpoint()?.Metadata.GetOrderedMetadata<AlsoAllowed>() ?? [];
        if (!access.Has(permission) && !alternatives.Any(a => a.Instead == permission && access.Has(a.Alternative)))
        {
            return PermissionExtensions.Forbidden("Das ist für deine Rolle nicht freigegeben.");
        }

        var db = http.RequestServices.GetRequiredService<AispaDbContext>();
        var target = await ResolveAsync(context, db, ct);
        if (target.Locations is { Count: > 0 } locations && !locations.Any(access.CanSee))
        {
            return PermissionExtensions.Forbidden("Kein Zugang zu diesem Standort.");
        }

        // Therapeuten dürfen nur den Status ihrer eigenen Termine setzen.
        if (permission == AccessRight.ChangeStatus && !access.Has(AccessRight.Book) && target.EmployeeId is { } employeeId
            && employeeId != access.EmployeeId)
        {
            return PermissionExtensions.Forbidden("Nur eigene Termine.");
        }

        return await next(context);
    }

    /// <summary>Standorte, die der Aufruf betrifft (einer genügt bei Personen, die an mehreren Standorten arbeiten); leer, wenn unbekannt oder standortunabhängig.</summary>
    private sealed record Target(IReadOnlyList<Guid> Locations, Guid? EmployeeId = null);

    private static async Task<Target> ResolveAsync(EndpointFilterInvocationContext context, AispaDbContext db, CancellationToken ct)
    {
        var request = context.HttpContext.Request;

        // Aus der Route, sonst aus der Abfrage (z. B. ?locationId=… beim Auflisten der Kassen).
        Guid? Id(string name) =>
            (request.RouteValues.TryGetValue(name, out var v) ? v?.ToString() : request.Query[name].FirstOrDefault()) is { } text
            && Guid.TryParse(text, out var id) ? id : null;

        if (Id("locationId") is { } locationId)
        {
            return new([locationId]);
        }

        if (Id("appointmentId") is { } appointmentId)
        {
            var a = await db.Appointments.Where(x => x.Id == appointmentId)
                .Select(x => new { x.LocationId, x.EmployeeId }).FirstOrDefaultAsync(ct);
            return a is null ? new([]) : new([a.LocationId], a.EmployeeId);
        }

        if (Id("roomId") is { } roomId)
        {
            return new(await db.Rooms.Where(r => r.Id == roomId).Select(r => r.LocationId).ToListAsync(ct));
        }

        if (Id("equipmentId") is { } equipmentId)
        {
            return new(await db.Equipment.Where(e => e.Id == equipmentId).Select(e => e.LocationId).ToListAsync(ct));
        }

        if (Id("employeeId") is { } employeeId)
        {
            return new(await EmployeeLocations(db, employeeId, ct));
        }

        if (Id("blockId") is { } blockId)
        {
            var block = await db.ResourceBookings.Where(b => b.Id == blockId).Select(b => new { b.Kind, b.ResourceId }).FirstOrDefaultAsync(ct);
            return block is null ? new([]) : new(await ResourceLocations(db, block.Kind, block.ResourceId, ct));
        }

        if (Id("registerId") is { } registerId)
        {
            return new(await RegisterLocations(db, registerId, ct));
        }

        if (Id("billId") is { } billId)
        {
            var register = await db.Bills.Where(b => b.Id == billId).Select(b => b.RegisterId).FirstOrDefaultAsync(ct);
            return new(await RegisterLocations(db, register, ct));
        }

        if (Id("receiptId") is { } receiptId)
        {
            var register = await db.Receipts.Where(r => r.Id == receiptId).Select(r => r.RegisterId).FirstOrDefaultAsync(ct);
            return new(await RegisterLocations(db, register, ct));
        }

        if (Id("terminalId") is { } terminalId)
        {
            return new(await db.PaymentTerminals.Where(t => t.Id == terminalId).Select(t => t.LocationId).ToListAsync(ct));
        }

        if (Id("cardPaymentId") is { } cardPaymentId)
        {
            return new(await db.CardPayments.Where(c => c.Id == cardPaymentId)
                .Join(db.Bills, c => c.BillId, b => b.Id, (_, b) => b.RegisterId)
                .Join(db.CashRegisters, r => r, c => c.Id, (_, c) => c.LocationId).ToListAsync(ct));
        }

        if (Id("mailId") is { } mailId)
        {
            // Mail ohne erkannten Standort: für alle mit dem Recht bearbeitbar.
            var location = await db.InboundMails.Where(m => m.Id == mailId).Select(m => m.LocationId).FirstOrDefaultAsync(ct);
            return new(location is { } l ? [l] : []);
        }

        if (Id("purchaseOrderId") is { } purchaseOrderId)
        {
            return new(await db.PurchaseOrders.Where(o => o.Id == purchaseOrderId).Select(o => o.LocationId).ToListAsync(ct));
        }

        if (Id("displayId") is { } displayId)
        {
            return new(await db.Set<DisplayDevice>().Where(d => d.Id == displayId).Select(d => d.LocationId).ToListAsync(ct));
        }

        if (Id("feedId") is { } feedId)
        {
            return new(await db.CalendarFeeds.Where(f => f.Id == feedId).Select(f => f.LocationId).ToListAsync(ct));
        }

        foreach (var argument in context.Arguments)
        {
            switch (argument)
            {
                case BlockInput block:
                    return new(await ResourceLocations(db, block.Kind, block.ResourceId, ct));
                case not null when argument.GetType().GetProperty("LocationId")?.GetValue(argument) is Guid locationOfBody:
                    // Anfragekörper mit Standort (Termin, Kasse anlegen …).
                    return new([locationOfBody]);
                case not null when argument.GetType().GetProperty("RegisterId")?.GetValue(argument) is Guid registerOfBody:
                    return new(await RegisterLocations(db, registerOfBody, ct));
            }
        }

        return new([]);
    }

    private static async Task<IReadOnlyList<Guid>> ResourceLocations(AispaDbContext db, ResourceKind kind, Guid id, CancellationToken ct) =>
        kind switch
        {
            ResourceKind.Room => await db.Rooms.Where(r => r.Id == id).Select(r => r.LocationId).ToListAsync(ct),
            ResourceKind.Equipment => await db.Equipment.Where(e => e.Id == id).Select(e => e.LocationId).ToListAsync(ct),
            _ => await EmployeeLocations(db, id, ct),
        };

    private static async Task<IReadOnlyList<Guid>> RegisterLocations(AispaDbContext db, Guid registerId, CancellationToken ct) =>
        await db.CashRegisters.Where(r => r.Id == registerId).Select(r => r.LocationId).ToListAsync(ct);

    /// <summary>Standorte aus Wochenarbeitszeit und Dienstplan (auch wer nur aushilft, gehört zum Standort).</summary>
    private static async Task<IReadOnlyList<Guid>> EmployeeLocations(AispaDbContext db, Guid employeeId, CancellationToken ct) =>
        await db.Employees.Where(e => e.Id == employeeId).SelectMany(e => e.WorkingHours).Select(w => w.LocationId)
            .Union(db.RosterDays.Where(d => d.EmployeeId == employeeId).SelectMany(d => d.Entries)
                .Where(x => x.LocationId != null).Select(x => x.LocationId!.Value))
            .ToListAsync(ct);
}
