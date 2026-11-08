using AispaStudio.Desktop.ViewModels;
using AispaStudio.Desktop.Views;
using Avalonia.Logging;

namespace AispaStudio.Desktop.Tests;

/// <summary>
/// Ä68 (Runde 6, Auftraggeber): das Ausgabefenster von Visual Studio blieb beim Öffnen des Kalenders nicht leer, sondern zeigte hunderte Zeilen
/// „An error occurred binding …“ für Eigenschaften, die absichtlich <c>null</c> sein dürfen. Die Tests lauschen auf das Binding-Protokoll von Avalonia
/// und verlangen, dass beim Start, beim Öffnen des Kalenders, jeder Seite der Seitenleiste, des Assistenten, der Kunden und der Kassa nichts gemeldet wird.
/// </summary>
public sealed partial class ViewTests
{
    /// <summary>Sammelt alle Meldungen des Bereichs „Binding“ ab Warnung, so wie Visual Studio sie im Ausgabefenster zeigt.</summary>
    private sealed class BindingLog : ILogSink
    {
        private readonly List<string> _messages = [];
        private readonly List<string> _report = [];

        public bool IsEnabled(LogEventLevel level, string area) => area == LogArea.Binding && level >= LogEventLevel.Warning;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        {
            if (IsEnabled(level, area))
            {
                Total++;
                _messages.Add(messageTemplate);
            }
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            if (IsEnabled(level, area))
            {
                Total++;
                _messages.Add(Format(messageTemplate, propertyValues));
            }
        }

        /// <summary>Setzt die Werte in die benannten Platzhalter ein (<c>{Property}</c>), in der Reihenfolge der Angabe.</summary>
        private static string Format(string template, object?[] values)
        {
            var index = 0;
            return System.Text.RegularExpressions.Regex.Replace(template, @"\{[^}]*\}", _ => index < values.Length ? $"{values[index++]}" : "");
        }

        /// <summary>Hält die Meldungen eines Schritts fest; <see cref="AssertClean"/> prüft am Ende alle Schritte zugleich, damit ein Lauf die ganze Liste zeigt.</summary>
        public void Record(string step)
        {
            var found = Take();
            if (found.Count > 0)
            {
                _report.Add($"{step}: {found.Count} Meldungen{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", found)}");
            }
        }

        public void AssertClean() => Assert.True(_report.Count == 0, $"{Total} Meldungen insgesamt{Environment.NewLine}{string.Join(Environment.NewLine, _report)}");

        /// <summary>Zahl aller Meldungen seit Beginn, auch der wiederholten.</summary>
        public int Total { get; private set; }

        /// <summary>Alle Meldungen seit dem letzten Aufruf, ohne Wiederholungen derselben Zeile (eine Eigenschaft meldet bei jeder Zeile der Liste erneut).</summary>
        public List<string> Take()
        {
            var distinct = _messages.Distinct(StringComparer.Ordinal).ToList();
            _messages.Clear();
            return distinct;
        }
    }

    /// <summary>Führt <paramref name="run"/> mit einem Binding-Protokoll aus und stellt das bisherige zurück.</summary>
    private static async Task<T> WithBindingLog<T>(Func<BindingLog, Task<T>> run)
    {
        var previous = Logger.Sink;
        var log = new BindingLog();
        Logger.Sink = log;
        try
        {
            return await run(log);
        }
        finally
        {
            Logger.Sink = previous;
        }
    }

    private static async Task AssertNoBindingErrors(BindingLog log, string step, Func<Task> act)
    {
        await act();
        await Pump(Settling);
        log.Record(step);
    }

    [Fact]
    public Task Start_Kalender_und_Seitenleiste_melden_keine_Bindungsfehler() => Render("hell", "bindungen-kalender", async (window, main) =>
    {
        var calendar = await WithBindingLog(async log =>
        {
            var shell = default(ShellViewModel);
            CalendarViewModel? calendar = null;
            await AssertNoBindingErrors(log, "Start und Kalender", async () =>
            {
                calendar = await SignedInCalendar(main);
                shell = (ShellViewModel)main.Content!;
            });

            // Jede Seite der Seitenleiste, je ein Reiter.
            await AssertNoBindingErrors(log, "Termin", () => { calendar!.OpenAppointment(SpaSample.Selected); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Warteliste", () => { calendar!.OpenWaitlistCommand.Execute(null); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Freie Zeit", () => { calendar!.OpenFreeTimeCommand.Execute(null); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Wunsch eintippen", () => { calendar!.OpenRequestCommand.Execute(null); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Meldungen", () => { calendar!.OpenMessagesCommand.Execute(null); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Neuer Termin", () => { calendar!.NewAppointmentCommand.Execute(null); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Paar oder Spa-Tag", () => { calendar!.OpenGroupBookingCommand.Execute(null); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Sauna", () => { calendar!.OpenAreasCommand.Execute(null); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Ausfall-Assistent", () => { calendar!.OpenAbsence(calendar.Columns.Single(c => c.IsSick)); return Task.CompletedTask; });

            // Anderer Tag (ohne Linie „jetzt“), Raumansicht und zurück.
            await AssertNoBindingErrors(log, "Anderer Tag", async () =>
            {
                calendar!.Date = new DateOnly(2026, 10, 26);
                await calendar.LoadAsync();
            });
            await AssertNoBindingErrors(log, "Raumansicht", () => { calendar!.ByRoom = true; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Wieder Team", () => { calendar!.ByRoom = false; return Task.CompletedTask; });

            // Der Assistent als Reiter, leer und mit Antwort.
            await AssertNoBindingErrors(log, "Assistent", () => { shell!.Companion.IsOpen = true; return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Assistent mit Antwort", () => shell!.Companion.AskExampleCommand.ExecuteAsync("Wie kassiere ich einen Termin?"));
            await AssertNoBindingErrors(log, "Seitenleiste zu", () =>
            {
                shell!.Companion.IsOpen = false;
                calendar!.CloseAllPanels();
                return Task.CompletedTask;
            });
            log.AssertClean();
            return calendar!;
        });
        return calendar;
    }, HeaderApi()
        .On($"GET /api/locations/{SpaSample.LocationId}/resources", SpaSample.ResourcesWithAbsent)
        .On($"GET /api/locations/{SpaSample.LocationId}/calendar?date=2026-10-19", SpaSample.BusyDay())
        .On($"POST /api/locations/{SpaSample.LocationId}/absence/proposal", SpaSample.AbsenceProposal())
        .On("POST /api/assistant", AssistantSample));

    /// <summary>
    /// Ä72 (Runde 6, Auftraggeber): beim Aushängen einer Ansicht (Seitenwechsel, Tageswechsel, Beenden) steht der <c>DataContext</c> ihres Wurzelelements kurz auf <c>null</c>;
    /// Bindungen der Form <c>#Root.DataContext.X</c> meldeten dann hunderte Zeilen „Value is null“. Der Test geht über alle Seiten, wechselt Tag und Ansicht
    /// des Kalenders mehrfach, kehrt zurück und schließt zum Schluss das Fenster, alles mit angehängtem Protokoll.
    /// </summary>
    [Fact]
    public Task Seitenwechsel_Tageswechsel_und_Beenden_melden_keine_Bindungsfehler() => Session.Dispatch(async () =>
    {
        var previous = Logger.Sink;
        var log = new BindingLog();
        Logger.Sink = log;
        try
        {
            var main = new MainViewModel(HeaderApi().On("POST /api/assistant", AssistantSample).Client(null), time: new SampleTime());
            var window = new MainWindow { DataContext = main, Width = 1440, Height = 900 };
            window.Show();
            await AssertNoBindingErrors(log, "Start und Kalender", async () => await SignedInCalendar(main));
            var shell = (ShellViewModel)main.Content!;

            // Jede Seite der Navigation, dazwischen immer zurück zum Kalender: dabei wird die Kalenderansicht aus- und wieder eingehängt.
            foreach (var page in shell.Pages.ToList())
            {
                await AssertNoBindingErrors(log, $"Seite {page.Title}", () => { shell.SelectedPage = page; return Task.CompletedTask; });
                await AssertNoBindingErrors(log, $"Zurück zum Kalender von {page.Title}", () =>
                {
                    shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Calendar);
                    return Task.CompletedTask;
                });
            }

            await AssertNoBindingErrors(log, "Anderer Tag", async () =>
            {
                shell.Calendar.Date = new DateOnly(2026, 10, 26);
                await shell.Calendar.LoadAsync();
            });
            await AssertNoBindingErrors(log, "Termin offen, Seite gewechselt", () =>
            {
                shell.Calendar.OpenAppointment(SpaSample.Selected);
                shell.Calendar.OpenWaitlistCommand.Execute(null);
                shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Customers);
                return Task.CompletedTask;
            });
            await AssertNoBindingErrors(log, "Wieder Kalender", () => { shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Calendar); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Fenster schließen", () => { window.Close(); return Task.CompletedTask; });
            log.AssertClean();
        }
        finally
        {
            Logger.Sink = previous;
        }

        return true;
    }, CancellationToken.None);

    [Fact]
    public Task Kunden_und_Kassa_melden_keine_Bindungsfehler() => Render("hell", "bindungen-seiten", async (window, main) =>
    {
        var calendar = await WithBindingLog(async log =>
        {
            var calendar = await SignedInCalendar(main);
            var shell = (ShellViewModel)main.Content!;
            await AssertNoBindingErrors(log, "Kunden", () =>
            {
                shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Customers);
                return Task.CompletedTask;
            });
            await AssertNoBindingErrors(log, "Kunde gewählt", () => shell.ShowCustomerAsync(SpaSample.Customers[0].Id));
            await AssertNoBindingErrors(log, "Kunde bearbeiten", () => { shell.Customers.EditContactCommand.Execute(null); return Task.CompletedTask; });
            await AssertNoBindingErrors(log, "Kassa", async () =>
            {
                await shell.CheckoutAsync(SpaSample.Paying);
                await shell.Pos.AddArticleCommand.ExecuteAsync(SpaSample.Articles[0]);
            });
            await AssertNoBindingErrors(log, "Hilfe", () =>
            {
                shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Help);
                return Task.CompletedTask;
            });
            await AssertNoBindingErrors(log, "Posteingang", () =>
            {
                shell.SelectedPage = shell.Pages.First(p => p.Page == shell.Inbox);
                return Task.CompletedTask;
            });
            await AssertNoBindingErrors(log, "Assistent neben der Kassa", () => { shell.Companion.IsOpen = true; return Task.CompletedTask; });
            log.AssertClean();
            return calendar;
        });
        return calendar;
    }, Api().On("POST /api/assistant", AssistantSample));
}
