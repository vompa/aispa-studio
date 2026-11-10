using Microsoft.AspNetCore.Http.HttpResults;

namespace AispaStudio.Api.Scheduling;

/// <summary>Sammelt Eingabefehler je Feld für eine ValidationProblem-Antwort (HTTP 400).</summary>
internal sealed class Errors
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public bool Any => _errors.Count > 0;

    /// <summary>Die erste Meldung, z. B. als Grund einer fehlerhaften Importzeile.</summary>
    public string? First => _errors.Values.Select(list => list[0]).FirstOrDefault();

    public void Add(string field, string message)
    {
        var key = char.ToLowerInvariant(field[0]) + field[1..];
        if (!_errors.TryGetValue(key, out var list))
        {
            _errors[key] = list = [];
        }

        list.Add(message);
    }

    public void Require(string field, string? value) => Check(!string.IsNullOrWhiteSpace(value), field, "Pflichtfeld.");

    public void Check(bool condition, string field, string message)
    {
        if (!condition)
        {
            Add(field, message);
        }
    }

    public ValidationProblem Problem() =>
        TypedResults.ValidationProblem(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
}
