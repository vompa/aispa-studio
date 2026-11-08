using System.Globalization;
using System.Linq.Expressions;
using AispaStudio.Api.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql;
using NpgsqlTypes;

namespace AispaStudio.Api.Demo.Memory;

/// <summary>
/// Schreibt Zeilen mit dem binären <c>COPY</c> von Npgsql: eine Anweisung je Tabelle, Spalten, Typen, Wertumwandlungen und der Mandant (Schattenspalte
/// <c>TenantId</c>) kommen aus dem EF-Modell, damit neue Spalten ohne Zutun mitgeschrieben werden. Zeilen, die schon eine Navigation tragen, werden
/// nur über ihre Spalten geschrieben. Die Abgleich-Trigger feuern bei <c>COPY</c> wie bei jedem <c>INSERT</c>.
/// </summary>
internal sealed class BulkWriter(NpgsqlConnection connection, Guid tenantId)
{
    private static readonly Dictionary<(Type Entity, string Property), Func<object, object?>> Getters = [];

    /// <summary>Anzahl der geschriebenen Zeilen je Tabelle (für das Protokoll).</summary>
    public List<(string Table, int Rows)> Written { get; } = [];

    public async Task WriteAsync<T>(IModel model, IEnumerable<T> rows, CancellationToken ct)
        where T : class
    {
        var list = rows as IReadOnlyCollection<T> ?? [.. rows];
        if (list.Count == 0)
        {
            return;
        }

        var entity = model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException($"{typeof(T).Name} ist nicht im Modell.");
        var table = StoreObjectIdentifier.Create(entity, StoreObjectType.Table) ?? throw new InvalidOperationException($"{typeof(T).Name} hat keine Tabelle.");
        var columns = entity.GetProperties().Where(p => IsWritable(p, table)).Select(p => new Column(p, table, Getter(p))).ToList();
        var sql = $"COPY \"{table.Schema}\".\"{table.Name}\" ({string.Join(", ", columns.Select(c => $"\"{c.Name}\""))}) FROM STDIN (FORMAT BINARY)";
        await using var writer = await connection.BeginBinaryImportAsync(sql, ct);
        foreach (var row in list)
        {
            await writer.StartRowAsync(ct);
            foreach (var column in columns)
            {
                await column.WriteAsync(writer, column.Value(row, tenantId), ct);
            }
        }

        await writer.CompleteAsync(ct);
        Written.Add(($"{table.Schema}.{table.Name}", list.Count));
    }

    /// <summary>Spalten, die die Datenbank selbst füllt (Zeilenversion <c>xmin</c>, berechnete und erzeugte Werte), schreibt <c>COPY</c> nicht.</summary>
    private static bool IsWritable(IProperty property, StoreObjectIdentifier table) =>
        property.GetColumnName(table) is { } name && name != "xmin" && property.GetComputedColumnSql() is null && !property.IsConcurrencyToken;

    private static Func<object, object?>? Getter(IProperty property)
    {
        if (property.IsShadowProperty() || property.PropertyInfo is null)
        {
            return null;
        }

        lock (Getters)
        {
            var key = (property.DeclaringType.ClrType, property.Name);
            if (!Getters.TryGetValue(key, out var getter))
            {
                var entity = Expression.Parameter(typeof(object));
                var access = Expression.Convert(Expression.Property(Expression.Convert(entity, property.DeclaringType.ClrType), property.PropertyInfo), typeof(object));
                Getters[key] = getter = Expression.Lambda<Func<object, object?>>(access, entity).Compile();
            }

            return getter;
        }
    }

    private sealed class Column
    {
        private readonly Func<object, object?>? _getter;
        private readonly ValueConverter? _converter;
        private readonly NpgsqlDbType _type;
        private readonly bool _isTenant;

        public Column(IProperty property, StoreObjectIdentifier table, Func<object, object?>? getter)
        {
            Name = property.GetColumnName(table)!;
            _getter = getter;
            _isTenant = property.Name == TenantModel.Property && property.IsShadowProperty();
            var mapping = property.GetRelationalTypeMapping();
            _converter = mapping.Converter;
            _type = TypeOf(mapping);
        }

        public string Name { get; }

        public object? Value(object row, Guid tenantId) => _isTenant ? tenantId : _getter?.Invoke(row);

        public async Task WriteAsync(NpgsqlBinaryImporter writer, object? value, CancellationToken ct)
        {
            value = _converter is null || value is null ? value : _converter.ConvertToProvider(value);
            switch (value)
            {
                case null:
                    await writer.WriteNullAsync(ct);
                    return;
                case Enum e:
                    await writer.WriteAsync(Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType()), CultureInfo.InvariantCulture), _type, ct);
                    return;
                case DateTimeOffset at:
                    await writer.WriteAsync(at.ToUniversalTime(), _type, ct);
                    return;
                case uint u:
                    await writer.WriteAsync((long)u, _type, ct);
                    return;
                default:
                    await writer.WriteAsync(value, _type, ct);
                    return;
            }
        }

        private static NpgsqlDbType TypeOf(RelationalTypeMapping mapping)
        {
            var name = mapping.StoreType;
            var open = name.IndexOf('(', StringComparison.Ordinal);
            name = (open < 0 ? name : name[..open]).Trim().ToLowerInvariant();
            return name switch
            {
                "uuid" => NpgsqlDbType.Uuid,
                "text" => NpgsqlDbType.Text,
                "character varying" or "varchar" => NpgsqlDbType.Varchar,
                "character" or "bpchar" => NpgsqlDbType.Char,
                "integer" or "int" or "int4" => NpgsqlDbType.Integer,
                "bigint" or "int8" => NpgsqlDbType.Bigint,
                "smallint" or "int2" => NpgsqlDbType.Smallint,
                "boolean" or "bool" => NpgsqlDbType.Boolean,
                "numeric" or "decimal" => NpgsqlDbType.Numeric,
                "double precision" or "float8" => NpgsqlDbType.Double,
                "real" or "float4" => NpgsqlDbType.Real,
                "timestamp with time zone" or "timestamptz" => NpgsqlDbType.TimestampTz,
                "timestamp without time zone" or "timestamp" => NpgsqlDbType.Timestamp,
                "date" => NpgsqlDbType.Date,
                "time without time zone" or "time" => NpgsqlDbType.Time,
                "jsonb" => NpgsqlDbType.Jsonb,
                "bytea" => NpgsqlDbType.Bytea,
                "text[]" => NpgsqlDbType.Array | NpgsqlDbType.Text,
                _ => throw new InvalidOperationException($"Spaltentyp {mapping.StoreType} ist für COPY nicht bekannt."),
            };
        }
    }
}
