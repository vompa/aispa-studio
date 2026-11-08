using AispaStudio.Api.Data;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Stock;

/// <summary>Lieferant im Stamm, gilt für alle Standorte; Material verweist auf ihn, Bestellungen halten den Namen zum Zeitpunkt fest.</summary>
public sealed class StockSupplier
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int? LeadTimeDays { get; set; }
    public decimal? MinimumOrderValue { get; set; }
    public SupplierOrderChannel OrderChannel { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Lieferantenstamm: anlegen, ändern, deaktivieren. Der Name ist eindeutig, Groß- und Kleinschreibung zählen nicht.</summary>
internal sealed class StockSuppliers(AispaDbContext db)
{
    public async Task<List<StockSupplierDto>> ListAsync(CancellationToken ct) =>
        [.. (await db.StockSuppliers.AsNoTracking().ToListAsync(ct)).OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).Select(ToDto)];

    public async Task<(StockSupplierDto? Supplier, Errors? Errors)> CreateAsync(StockSupplierInput input, CancellationToken ct)
    {
        if (await ValidateAsync(input, null, ct) is { } errors)
        {
            return (null, errors);
        }

        var supplier = new StockSupplier { Name = input.Name.Trim() };
        Apply(supplier, input);
        db.StockSuppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return (ToDto(supplier), null);
    }

    public async Task<(StockSupplierDto? Supplier, Errors? Errors)> UpdateAsync(StockSupplier supplier, StockSupplierInput input, CancellationToken ct)
    {
        if (await ValidateAsync(input, supplier.Id, ct) is { } errors)
        {
            return (null, errors);
        }

        supplier.Name = input.Name.Trim();
        Apply(supplier, input);
        await db.SaveChangesAsync(ct);
        return (ToDto(supplier), null);
    }

    /// <summary>Der Lieferant zu einem Namen aus dem Material; gibt es ihn noch nicht, wird er angelegt (noch nicht gespeichert).</summary>
    public async Task<StockSupplier> FindOrAddAsync(string name, CancellationToken ct)
    {
        var trimmed = name.Trim();
        var known = (await db.StockSuppliers.ToListAsync(ct)).FirstOrDefault(s => string.Equals(s.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        if (known is not null)
        {
            return known;
        }

        var added = new StockSupplier { Name = trimmed };
        db.StockSuppliers.Add(added);
        return added;
    }

    private async Task<Errors?> ValidateAsync(StockSupplierInput input, Guid? id, CancellationToken ct)
    {
        var errors = new Errors();
        var name = input.Name?.Trim() ?? "";
        errors.Check(name.Length is > 0 and <= 200, nameof(input.Name), "Den Namen des Lieferanten eintragen, höchstens 200 Zeichen.");
        errors.Check(input.ContactPerson is null || input.ContactPerson.Trim().Length <= 200, nameof(input.ContactPerson), "Ansprechperson mit höchstens 200 Zeichen.");
        errors.Check(input.Email is null || input.Email.Trim().Length == 0 || (input.Email.Trim().Length <= 254 && input.Email.Contains('@', StringComparison.Ordinal)),
            nameof(input.Email), "Die Mailadresse ist nicht gültig. Trage sie z. B. als bestellung@lieferant.at ein.");
        errors.Check(input.Phone is null || input.Phone.Trim().Length <= 40, nameof(input.Phone), "Telefon mit höchstens 40 Zeichen.");
        errors.Check(input.LeadTimeDays is null or (>= 0 and <= 365), nameof(input.LeadTimeDays), "Lieferzeit von 0 bis 365 Tagen.");
        errors.Check(input.MinimumOrderValue is null || (input.MinimumOrderValue is >= 0 and < 1_000_000 && decimal.Round(input.MinimumOrderValue.Value, 2) == input.MinimumOrderValue),
            nameof(input.MinimumOrderValue), "Mindestbestellwert ab 0 mit höchstens zwei Nachkommastellen.");
        errors.Check(Enum.IsDefined(input.OrderChannel), nameof(input.OrderChannel), "Bestellweg wählen.");
        var others = await db.StockSuppliers.AsNoTracking().Where(s => s.Id != id).Select(s => s.Name).ToListAsync(ct);
        errors.Check(!others.Contains(name, StringComparer.OrdinalIgnoreCase), nameof(input.Name), "Diesen Lieferanten gibt es schon.");
        return errors.Any ? errors : null;
    }

    private static void Apply(StockSupplier supplier, StockSupplierInput input)
    {
        supplier.ContactPerson = Blank(input.ContactPerson);
        supplier.Email = Blank(input.Email);
        supplier.Phone = Blank(input.Phone);
        supplier.LeadTimeDays = input.LeadTimeDays;
        supplier.MinimumOrderValue = input.MinimumOrderValue;
        supplier.OrderChannel = input.OrderChannel;
        supplier.IsActive = input.IsActive;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StockSupplierDto ToDto(StockSupplier s) =>
        new(s.Id, s.Name, s.ContactPerson, s.Email, s.Phone, s.LeadTimeDays, s.MinimumOrderValue, s.OrderChannel, s.IsActive);
}
