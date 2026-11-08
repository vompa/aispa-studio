using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Stock;

/// <summary>
/// Lagerorte eines Standorts (ADR 0068): Hauptlager (entsteht mit dem Standort, nicht löschbar), Raumschränke, Verkaufsregal und sonstige Orte,
/// zusammen höchstens zehn je Standort, mit Mindestmengen je Material.
/// </summary>
internal sealed class StoragePlaces(AispaDbContext db)
{
    public async Task<List<StoragePlaceDto>> ListAsync(Guid locationId, CancellationToken ct)
    {
        var places = await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == locationId)
            .OrderBy(p => p.Position).ThenBy(p => p.Name).ToListAsync(ct);
        var ids = places.Select(p => p.Id).ToList();
        var minimums = (await db.StoragePlaceMinimums.AsNoTracking().Where(m => ids.Contains(m.StoragePlaceId)).ToListAsync(ct))
            .GroupBy(m => m.StoragePlaceId).ToDictionary(g => g.Key, g => g.ToList());
        return [.. places.Select(p => ToDto(p, minimums.GetValueOrDefault(p.Id) ?? []))];
    }

    public async Task<(StoragePlaceDto? Place, Errors? Errors)> CreateAsync(Guid locationId, StoragePlaceInput input, CancellationToken ct)
    {
        var errors = await ValidateAsync(locationId, input, null, ct);
        if (errors.Any)
        {
            return (null, errors);
        }

        var position = await db.StoragePlaces.Where(p => p.LocationId == locationId).MaxAsync(p => (int?)p.Position, ct) ?? -1;
        var place = new StoragePlace { LocationId = locationId, Name = input.Name.Trim(), Position = position + 1 };
        Apply(place, input);
        db.StoragePlaces.Add(place);
        AddMinimums(place.Id, input.Minimums);
        await db.SaveChangesAsync(ct);
        return ((await ListAsync(locationId, ct)).Single(p => p.Id == place.Id), null);
    }

    public async Task<(StoragePlaceDto? Place, Errors? Errors)> UpdateAsync(Guid locationId, Guid placeId, StoragePlaceInput input, CancellationToken ct)
    {
        var place = await db.StoragePlaces.FirstAsync(p => p.Id == placeId, ct);
        var errors = await ValidateAsync(locationId, input, place, ct);
        if (errors.Any)
        {
            return (null, errors);
        }

        place.Name = input.Name.Trim();
        Apply(place, input);
        if (input.Minimums is not null)
        {
            db.StoragePlaceMinimums.RemoveRange(await db.StoragePlaceMinimums.Where(m => m.StoragePlaceId == placeId).ToListAsync(ct));
            AddMinimums(placeId, input.Minimums);
        }

        await db.SaveChangesAsync(ct);
        return ((await ListAsync(locationId, ct)).Single(p => p.Id == placeId), null);
    }

    /// <summary>„Von Raum … übernehmen“: ersetzt die Mindestmengen eines Lagerorts durch die eines anderen Lagerorts desselben Standorts.</summary>
    public async Task<(StoragePlaceDto? Place, Errors? Errors)> CopyMinimumsAsync(Guid locationId, Guid placeId, Guid sourcePlaceId, CancellationToken ct)
    {
        var errors = new Errors();
        errors.Check(placeId != sourcePlaceId, "sourcePlaceId", "Anderen Lagerort als Quelle wählen.");
        var source = await db.StoragePlaceMinimums.AsNoTracking().Where(m => m.StoragePlaceId == sourcePlaceId).ToListAsync(ct);
        errors.Check(source.Count > 0, "sourcePlaceId", "Der gewählte Lagerort hat keine Mindestmengen.");
        if (errors.Any)
        {
            return (null, errors);
        }

        db.StoragePlaceMinimums.RemoveRange(await db.StoragePlaceMinimums.Where(m => m.StoragePlaceId == placeId).ToListAsync(ct));
        AddMinimums(placeId, [.. source.Select(m => new StoragePlaceMinimumDto(m.ItemId, m.MinimumQuantity, m.MaximumQuantity))]);
        await db.SaveChangesAsync(ct);
        return ((await ListAsync(locationId, ct)).Single(p => p.Id == placeId), null);
    }

    /// <summary>Löscht einen Lagerort ohne Bewegungen; das Hauptlager nie, ein Ort mit Bewegungen wird deaktiviert statt gelöscht.</summary>
    public async Task<Errors?> DeleteAsync(Guid placeId, CancellationToken ct)
    {
        var place = await db.StoragePlaces.FirstAsync(p => p.Id == placeId, ct);
        var errors = new Errors();
        errors.Check(place.Kind != StoragePlaceKind.Main, nameof(StoragePlaceInput.Name), "Das Hauptlager lässt sich nicht löschen.");
        errors.Check(!await db.StockMovements.AnyAsync(m => m.StoragePlaceId == placeId || m.TargetStoragePlaceId == placeId, ct),
            nameof(StoragePlaceInput.Name), "Der Lagerort hat Bewegungen. Deaktivieren statt löschen.");
        if (errors.Any)
        {
            return errors;
        }

        db.StoragePlaces.Remove(place);
        await db.SaveChangesAsync(ct);
        return null;
    }

    private async Task<Errors> ValidateAsync(Guid locationId, StoragePlaceInput input, StoragePlace? existing, CancellationToken ct)
    {
        var errors = new Errors();
        var name = input.Name?.Trim() ?? "";
        errors.Check(name.Length is > 0 and <= 60, nameof(input.Name), "Name mit höchstens 60 Zeichen angeben.");
        errors.Check(input.Description is null || input.Description.Trim().Length <= 160, nameof(input.Description), "Beschreibung mit höchstens 160 Zeichen.");
        errors.Check(Enum.IsDefined(input.Kind), nameof(input.Kind), "Art des Lagerorts wählen.");
        var places = await db.StoragePlaces.AsNoTracking().Where(p => p.LocationId == locationId).ToListAsync(ct);
        errors.Check(!places.Any(p => p.Id != existing?.Id && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)), nameof(input.Name),
            "Einen Lagerort mit diesem Namen gibt es schon.");
        if (existing is null)
        {
            errors.Check(places.Count < StockService.MaxPlacesPerLocation, nameof(input.Name), $"Höchstens {StockService.MaxPlacesPerLocation} Lagerorte je Standort.");
        }

        if (existing?.Kind == StoragePlaceKind.Main)
        {
            errors.Check(input.Kind == StoragePlaceKind.Main, nameof(input.Kind), "Die Art des Hauptlagers ist fest.");
            errors.Check(input.IsActive, nameof(input.IsActive), "Das Hauptlager lässt sich nicht deaktivieren.");
        }
        else
        {
            errors.Check(input.Kind != StoragePlaceKind.Main, nameof(input.Kind), "Das Hauptlager gibt es schon.");
        }

        errors.Check(input.RoomId is null || input.Kind == StoragePlaceKind.Room, nameof(input.RoomId), "Einen Raum gibt es nur bei der Art „Raum“.");
        if (input.RoomId is { } roomId)
        {
            var room = await db.Rooms.AsNoTracking().Where(r => r.Id == roomId).Select(r => new { r.LocationId }).FirstOrDefaultAsync(ct);
            errors.Check(room?.LocationId == locationId, nameof(input.RoomId), "Der Raum gehört nicht zu diesem Standort.");
            errors.Check(!places.Any(p => p.Id != existing?.Id && p.RoomId == roomId), nameof(input.RoomId), "Für diesen Raum gibt es schon einen Lagerort.");
        }

        var minimums = input.Minimums ?? [];
        var itemIds = minimums.Select(m => m.ItemId).ToList();
        errors.Check(itemIds.Distinct().Count() == itemIds.Count, nameof(input.Minimums), "Jedes Material nur einmal.");
        errors.Check(await db.StockItems.CountAsync(i => itemIds.Contains(i.Id), ct) == itemIds.Distinct().Count(), nameof(input.Minimums), "Material nicht gefunden.");
        errors.Check(minimums.All(m => m.MinimumQuantity is >= 0 and < 1_000_000 && decimal.Round(m.MinimumQuantity, 3) == m.MinimumQuantity),
            nameof(input.Minimums), "Mindestmenge ab 0 mit höchstens drei Nachkommastellen.");
        errors.Check(minimums.All(m => m.MaximumQuantity is null || (m.MaximumQuantity >= m.MinimumQuantity && m.MaximumQuantity < 1_000_000 && decimal.Round(m.MaximumQuantity.Value, 3) == m.MaximumQuantity)),
            nameof(input.Minimums), "Höchstmenge nicht unter der Mindestmenge, mit höchstens drei Nachkommastellen.");
        return errors;
    }

    private static void Apply(StoragePlace place, StoragePlaceInput input)
    {
        place.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        place.Kind = input.Kind;
        place.RoomId = input.Kind == StoragePlaceKind.Room ? input.RoomId : null;
        place.IsActive = input.IsActive;
    }

    private void AddMinimums(Guid placeId, IReadOnlyList<StoragePlaceMinimumDto>? minimums)
    {
        foreach (var minimum in minimums ?? [])
        {
            db.StoragePlaceMinimums.Add(new StoragePlaceMinimum { StoragePlaceId = placeId, ItemId = minimum.ItemId, MinimumQuantity = minimum.MinimumQuantity, MaximumQuantity = minimum.MaximumQuantity });
        }
    }

    private static StoragePlaceDto ToDto(StoragePlace p, IEnumerable<StoragePlaceMinimum> minimums) =>
        new(p.Id, p.LocationId, p.Name, p.Description, p.Kind, p.RoomId, p.IsActive, p.Position,
            [.. minimums.Select(m => new StoragePlaceMinimumDto(m.ItemId, m.MinimumQuantity, m.MaximumQuantity))]);
}
