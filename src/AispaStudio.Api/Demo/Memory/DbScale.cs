using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AispaStudio.Api.Demo.Memory;

/// <summary>
/// Bringt Beträge im Speicher auf die Nachkommastellen ihrer Spalte (<c>numeric(10,2)</c>), so wie sie nach einem Weg durch die Datenbank
/// aussehen: 20 wird 20,00. Berichte schreiben ihre Zahlen als Text fest; nur so steht dort in beiden Wegen dasselbe.
/// </summary>
internal sealed class DbScale(IModel model)
{
    private readonly Dictionary<Type, List<(PropertyInfo Property, int Scale)>> _cache = [];

    public void Normalize(object entity)
    {
        var type = entity.GetType();
        if (!_cache.TryGetValue(type, out var properties))
        {
            _cache[type] = properties = [.. (model.FindEntityType(type)?.GetProperties() ?? [])
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?))
                .Where(p => p.PropertyInfo is not null && p.GetScale() is not null)
                .Select(p => (p.PropertyInfo!, p.GetScale()!.Value))];
        }

        foreach (var (property, scale) in properties)
        {
            if (property.GetValue(entity) is decimal value)
            {
                property.SetValue(entity, decimal.Parse(decimal.Round(value, scale, MidpointRounding.AwayFromZero).ToString("F" + scale, CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture));
            }
        }
    }
}
