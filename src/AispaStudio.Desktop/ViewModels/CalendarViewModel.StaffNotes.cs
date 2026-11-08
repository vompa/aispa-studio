using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

// ---------- Hinweise zu Personen (R05 Slice 111): Symbol mit Tooltip im Kopf der Spalte ----------
public partial class CalendarViewModel
{
    private Dictionary<Guid, string> _noteSymbols = [];

    private async Task LoadNoteSymbolsAsync()
    {
        // Welche Hinweise jemand sehen darf, entscheidet der Server; ohne Antwort bleibt der letzte Stand.
        var result = await _api.GetAsync<List<StaffNoteSymbolDto>>($"api/employee-notes/symbols?on={Date:yyyy-MM-dd}").ConfigureAwait(true);
        if (result.Value is { } symbols)
        {
            _noteSymbols = symbols.GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => string.Join("\n", g.Select(s => s.Text)));
            ApplyNoteSymbols(Columns);
        }
    }

    private void ApplyNoteSymbols(IEnumerable<CalendarColumn> columns)
    {
        foreach (var column in columns)
        {
            column.NoteText = column.IsRoom ? null : _noteSymbols.GetValueOrDefault(column.Id);
        }
    }
}
