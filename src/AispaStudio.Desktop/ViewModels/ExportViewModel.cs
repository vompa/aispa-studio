using AispaStudio.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>Ein Eintrag im Menü „Exportieren“: Beschriftung, der API-Pfad mit den aktuellen Filtern der Liste (beim Klick ausgewertet) und der Dateityp des Speicherdialogs.</summary>
public sealed record ExportOption(string Label, Func<string> Path, string TypeName, IReadOnlyList<string> Patterns)
{
    /// <summary>
    /// Der Standard-Eintrag eines Formats: „Excel“, „CSV“, „Portal-CSV“, „vCard“, „Kalenderdatei (iCal)“. „Portal-CSV“ ohne Markennamen, weil keine Import-Vorlage eines
    /// Portals öffentlich belegt ist (PR-Protokoll, Aufgabe B).
    /// </summary>
    public static ExportOption For(ExportFormat format, Func<string> path, string? label = null) => format switch
    {
        ExportFormat.Xlsx => new(label ?? "Excel", path, "Excel-Arbeitsmappe", ["*.xlsx"]),
        ExportFormat.Portal => new(label ?? "Portal-CSV", path, "CSV-Datei", ["*.csv"]),
        ExportFormat.Vcf => new(label ?? "vCard", path, "vCard-Datei", ["*.vcf"]),
        ExportFormat.Ics => new(label ?? "Kalenderdatei (iCal)", path, "Kalenderdatei", ["*.ics"]),
        _ => new(label ?? "CSV", path, "CSV-Datei", ["*.csv"]),
    };

    public override string ToString() => Label;
}

/// <summary>
/// Der Knopf „Exportieren“ (Export/Import, Aufgabe A): holt die Datei vom Server (der rechnet Inhalt, Format und Dateinamen) und speichert sie Byte für Byte über den Dialog
/// des Fensters; danach „Gespeichert: Kunden_2026-11-09.xlsx“ als Meldung unten. Jede Liste gibt ihre Einträge mit den eigenen Filtern mit; ohne Recht legt die Seite kein Modell an.
/// </summary>
public sealed partial class ExportViewModel(IAispaApi api, IReadOnlyList<ExportOption> options) : ObservableObject, ISavesFiles
{
    public const string NotCreated = "Der Export konnte nicht erstellt werden.";
    public const string NotSaved = "Die Datei konnte nicht gespeichert werden. Wählen Sie einen anderen Ort.";

    public IReadOnlyList<ExportOption> Options { get; } = options;

    public IFileSaver? Files { get; set; }

    /// <summary>Rückmeldung der Seite als Toast („Gespeichert: …“).</summary>
    public Action<string>? Saved { get; init; }

    /// <summary>Fehler als Hinweis der Seite (bleibt stehen, nennt Problem und Weg).</summary>
    public Action<string>? Failed { get; init; }

    /// <summary>Solange die Datei entsteht, ist der Knopf gesperrt und zeigt „Wird erstellt …“.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial bool IsBusy { get; private set; }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync(ExportOption option)
    {
        IsBusy = true;
        try
        {
            var result = await api.GetFileAsync(option.Path()).ConfigureAwait(true);
            if (result.Value is not { } file)
            {
                Failed?.Invoke(result.Error ?? NotCreated);
                return;
            }

            var name = file.FileName ?? $"Export{option.Patterns[0][1..]}";
            if (Files is null)
            {
                Failed?.Invoke(NotSaved);
                return;
            }

            if (await Files.SaveAsync(name, option.TypeName, option.Patterns, file.Content).ConfigureAwait(true))
            {
                Saved?.Invoke($"Gespeichert: {name}");
            }
        }
        catch (IOException)
        {
            Failed?.Invoke(NotSaved);
        }
        catch (UnauthorizedAccessException)
        {
            Failed?.Invoke(NotSaved);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExport(ExportOption? option) => !IsBusy && option is not null;
}
