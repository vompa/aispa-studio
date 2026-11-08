using System.Text.Json;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;
using AispaStudio.Help;

namespace AispaStudio.Desktop.Tests;

/// <summary>Slice 138 (Ä347): „KI und Hilfe“ beginnt nach dem Schließen leer, „Neu beginnen“ und „Zuletzt gefragt“ (nur heute, je Person, nur lokal).</summary>
public sealed class CompanionHistoryTests
{
    private const string Route = "POST /api/assistant";

    private static AssistantAnswer Answer(string text) =>
        new(AssistantAudience.Reception, text, [], [], "Antwort einer KI.", []);

    /// <summary>
    /// Uhr mit festem Kalendertag (UTC oder eine andere Zone), die der Test weiterstellt; ihre Zeitgeber laufen nur über <see cref="Advance"/>
    /// und feuern dabei in der Reihenfolge ihrer Fälligkeit, auch die, die ein feuernder Zeitgeber neu stellt.
    /// </summary>
    private sealed class DayClock(DateTimeOffset start, TimeZoneInfo? zone = null) : TimeProvider
    {
        private readonly List<ClockTimer> _timers = [];
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public override TimeZoneInfo LocalTimeZone => zone ?? TimeZoneInfo.Utc;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ClockTimer(callback, state, _now + dueTime);
            lock (_timers)
            {
                _timers.Add(timer);
            }

            return timer;
        }

        public void Advance(TimeSpan by)
        {
            var target = _now + by;
            while (NextDue(target) is { } timer)
            {
                _now = timer.Due;
                timer.Fire();
            }

            _now = target;
        }

        private ClockTimer? NextDue(DateTimeOffset until)
        {
            lock (_timers)
            {
                return _timers.Where(t => !t.Done && t.Due <= until).OrderBy(t => t.Due).FirstOrDefault();
            }
        }

        private sealed class ClockTimer(TimerCallback callback, object? state, DateTimeOffset due) : ITimer
        {
            public DateTimeOffset Due { get; } = due;

            public bool Done { get; private set; }

            public void Fire()
            {
                Done = true;
                callback(state);
            }

            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose() => Done = true;

            public ValueTask DisposeAsync()
            {
                Done = true;
                return ValueTask.CompletedTask;
            }
        }
    }

    private static readonly DateTimeOffset Noon = new(2027, 3, 2, 12, 0, 0, TimeSpan.Zero);

    private static CompanionViewModel Companion(StubApi stub, ISettingsStore? store = null, string owner = "person-a", TimeProvider? time = null) =>
        new(stub.Client(), store ?? new MemorySettingsStore(), true, AssistantAudience.Reception, () => Guid.NewGuid(), time: time ?? new DayClock(Noon))
        {
            HistoryOwner = owner,
        };

    private static async Task AskAsync(CompanionViewModel companion, string question)
    {
        companion.Question = question;
        await companion.AskCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task Antwort_im_Panel_dann_Schliessen_und_Oeffnen_Frage_und_Antwort_sind_weg()
    {
        var companion = Companion(new StubApi().On(Route, Answer("Heute 5 Termine.")));
        companion.IsOpen = true;
        await AskAsync(companion, "Was steht heute an?");
        companion.Question = "halb getippt";
        Assert.True(companion.HasAnswer);

        companion.CloseCommand.Execute(null);
        companion.ToggleCommand.Execute(null);

        Assert.True(companion.IsOpen);
        Assert.Equal("", companion.Question);
        Assert.False(companion.HasAnswer);
        Assert.Null(companion.Asked);
        Assert.Null(companion.Error);
        Assert.False(companion.IsBooking);
    }

    [Fact]
    public async Task Neu_beginnen_setzt_zurueck_ohne_das_Panel_zu_schliessen()
    {
        var companion = Companion(new StubApi().On(Route, Answer("Heute 5 Termine.")));
        companion.IsOpen = true;
        var restarted = 0;
        companion.Restarted += () => restarted++;
        await AskAsync(companion, "Was steht heute an?");

        companion.ResetCommand.Execute(null);

        Assert.True(companion.IsOpen);
        Assert.False(companion.HasAnswer);
        Assert.Equal("", companion.Question);
        Assert.Equal(1, restarted);
        Assert.Equal(["Was steht heute an?"], companion.RecentQuestions);
    }

    [Fact]
    public void Neu_beginnen_ist_die_Aktion_im_Kopf_der_Leiste_und_folgt_CanReset()
    {
        var companion = Companion(new StubApi().On(Route, Answer("Heute 5 Termine.")));
        ISidePanel panel = companion;
        var changed = new List<string?>();
        companion.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.False(panel.HasPanelAction);
        Assert.Equal("Neu beginnen", panel.PanelActionText);
        Assert.Equal("Frage und Antwort löschen, die Leiste bleibt offen", panel.PanelActionTip);

        companion.Question = "halb getippt";
        Assert.True(panel.HasPanelAction);
        Assert.Contains(nameof(ISidePanel.HasPanelAction), changed);

        panel.PanelAction!.Execute(null);
        Assert.False(panel.HasPanelAction);
        Assert.Equal("", companion.Question);
    }

    [Fact]
    public async Task Antwort_die_erst_nach_dem_Schliessen_eintrifft_wird_verworfen()
    {
        var gate = new TaskCompletionSource();
        var companion = Companion(new StubApi().On(Route, Answer("Zu spät.")).Hold(Route, gate.Task));
        companion.IsOpen = true;
        companion.Question = "Was steht heute an?";
        var asking = companion.AskCommand.ExecuteAsync(null);

        companion.CloseCommand.Execute(null);
        gate.SetResult();
        await asking;

        Assert.False(companion.HasAnswer);
        Assert.False(companion.IsAsking);
        Assert.Null(companion.Error);
    }

    [Fact]
    public async Task Wechsel_des_Reiters_im_Kalender_schaltet_offen_aus_und_setzt_nicht_zurueck()
    {
        var companion = Companion(new StubApi().On(Route, Answer("Heute 5 Termine.")));
        companion.IsOpen = true;
        await AskAsync(companion, "Was steht heute an?");

        // Der Kalender setzt IsOpen auf false, wenn ein anderer Reiter gewählt wird; der Assistent bleibt im Hintergrund offen.
        companion.IsOpen = false;
        companion.IsOpen = true;

        Assert.True(companion.HasAnswer);
        Assert.Equal("Was steht heute an?", companion.Asked);
    }

    [Fact]
    public async Task Neu_beginnen_waehrend_die_alte_Anfrage_laeuft_dann_ist_Fragen_sofort_wieder_moeglich()
    {
        var gate = new TaskCompletionSource();
        var companion = Companion(new StubApi().On(Route, Answer("Spät.")).Hold(Route, gate.Task));
        companion.IsOpen = true;
        companion.Question = "Erste Frage";
        var first = companion.AskCommand.ExecuteAsync(null);

        companion.ResetCommand.Execute(null);
        companion.Question = "Zweite Frage";

        Assert.True(companion.AskCommand.CanExecute(null));
        gate.SetResult();
        await first;
        Assert.False(companion.HasAnswer);
    }

    [Fact]
    public async Task Verlauf_von_gestern_wird_beim_Oeffnen_auch_vom_Geraet_genommen()
    {
        var clock = new DayClock(new DateTimeOffset(2027, 3, 2, 23, 58, 0, TimeSpan.Zero));
        var store = new MemorySettingsStore();
        var asked = Companion(new StubApi().On(Route, Answer("x")), store, time: clock);
        await AskAsync(asked, "Wer arbeitet morgen?");
        Assert.NotNull(store.Load().AssistantHistory);

        // Ohne die Leiste von gestern (sonst räumt ihr Zeitgeber um Mitternacht ab): geprüft wird der Weg beim Öffnen.
        asked.Dispose();
        clock.Advance(TimeSpan.FromMinutes(5));
        _ = Companion(new StubApi(), store, time: clock);

        Assert.Null(store.Load().AssistantHistory);
    }

    [Fact]
    public async Task Angebot_der_Antwort_stellt_seine_Frage_aber_steht_nicht_unter_Zuletzt_gefragt()
    {
        var answer = new AssistantAnswer(AssistantAudience.Reception, "Mehrere Gäste.", [], [], "Antwort einer KI.", [],
            new AssistantBlock(AssistantBlockKind.Answer, "Guests", "Welcher Gast?", null, "Daten des Spas", null, null, null,
                [new AssistantFollowUp("Eva Steiner", "wann kommt steiner", Guid.NewGuid())]));
        var companion = Companion(new StubApi().On(Route, answer));
        await AskAsync(companion, "wann kommt steiner");

        await companion.AskFollowUpCommand.ExecuteAsync(companion.FollowUps[0]);

        Assert.Equal(["wann kommt steiner"], companion.RecentQuestions);
    }

    [Fact]
    public async Task Seitenwechsel_auf_eine_Seite_mit_geschlossener_Leiste_setzt_nicht_zurueck()
    {
        var store = new MemorySettingsStore();
        store.Save(store.Load() with { CompanionPages = new Dictionary<string, bool> { ["kassa"] = false, ["kalender"] = true } });
        var companion = Companion(new StubApi().On(Route, Answer("Heute 5 Termine.")), store);
        companion.ShowPage("kalender");
        await AskAsync(companion, "Was steht heute an?");

        companion.ShowPage("kassa");
        Assert.False(companion.IsOpen);
        companion.ShowPage("kalender");

        Assert.True(companion.IsOpen);
        Assert.True(companion.HasAnswer);
    }

    [Fact]
    public async Task Gefragt_heute_dann_Panel_neu_geoeffnet_dann_Klick_auf_die_Frage_dann_geht_sie_neu_an_die_API()
    {
        var calls = 0;
        var stub = new StubApi().OnJson(Route, _ => Answer($"Antwort {++calls}"));
        var companion = Companion(stub);
        companion.IsOpen = true;
        await AskAsync(companion, "wer ist heute noch frei");
        companion.CloseCommand.Execute(null);
        companion.ToggleCommand.Execute(null);
        Assert.Equal(["wer ist heute noch frei"], companion.RecentQuestions);

        await companion.AskRecentCommand.ExecuteAsync("wer ist heute noch frei");

        Assert.Equal(2, stub.Calls.Count(c => c.Route == Route));
        Assert.Equal("Antwort 2", companion.Answer?.Text);
        Assert.Equal("wer ist heute noch frei", companion.Asked);
    }

    [Fact]
    public async Task Person_A_hat_gefragt_dann_Person_B_am_selben_Geraet_dann_ist_Zuletzt_gefragt_leer()
    {
        var store = new MemorySettingsStore();
        var a = Companion(new StubApi().On(Route, Answer("x")), store, "person-a");
        await AskAsync(a, "Wer arbeitet morgen?");
        Assert.Equal(["Wer arbeitet morgen?"], Companion(new StubApi(), store, "person-a").RecentQuestions);

        var b = Companion(new StubApi().On(Route, Answer("y")), store, "person-b");

        Assert.Empty(b.RecentQuestions);
        Assert.False(b.RecallQuestion());
        await AskAsync(b, "Umsatz heute?");
        Assert.Empty(Companion(new StubApi(), store, "person-a").RecentQuestions);
    }

    [Fact]
    public async Task Frage_von_gestern_dann_heute_geoeffnet_dann_steht_sie_nicht_unter_Zuletzt_gefragt()
    {
        var clock = new DayClock(new DateTimeOffset(2027, 3, 2, 23, 58, 0, TimeSpan.Zero));
        var store = new MemorySettingsStore();
        var companion = Companion(new StubApi().On(Route, Answer("x")), store, time: clock);
        await AskAsync(companion, "Wer arbeitet morgen?");

        clock.Advance(TimeSpan.FromMinutes(1));
        var stillToday = Companion(new StubApi(), store, time: clock);
        Assert.Equal(["Wer arbeitet morgen?"], stillToday.RecentQuestions);

        // Ohne Zeitgeber (sonst räumen sie um Mitternacht ab): geprüft wird der Weg beim Öffnen.
        companion.Dispose();
        stillToday.Dispose();
        clock.Advance(TimeSpan.FromMinutes(2));
        companion.IsOpen = true;
        Assert.Empty(companion.RecentQuestions);
        Assert.Empty(Companion(new StubApi(), store, time: clock).RecentQuestions);
        Assert.False(Companion(new StubApi(), store, time: clock).RecallQuestion());
    }

    [Fact]
    public async Task Fragefeld_leer_und_fokussiert_dann_Pfeil_nach_oben_dann_steht_die_letzte_Frage_im_Feld()
    {
        var store = new MemorySettingsStore();
        var companion = Companion(new StubApi().On(Route, Answer("x")), store);
        companion.IsOpen = true;
        await AskAsync(companion, "Erste Frage");
        await AskAsync(companion, "Letzte Frage");
        companion.CloseCommand.Execute(null);
        companion.ToggleCommand.Execute(null);

        Assert.True(companion.RecallQuestion());
        Assert.Equal("Letzte Frage", companion.Question);

        // Neues Panel am selben Tag und für dieselbe Person (App neu gestartet).
        var restarted = Companion(new StubApi(), store);
        Assert.True(restarted.RecallQuestion());
        Assert.Equal("Letzte Frage", restarted.Question);
    }

    [Fact]
    public async Task Verlauf_hat_hoechstens_fuenf_Fragen_doppelte_nur_einmal_und_nach_oben_gerueckt()
    {
        var companion = Companion(new StubApi().On(Route, Answer("x")));
        foreach (var question in new[] { "F1", "F2", "F3", "F4", "F5", "F6" })
        {
            await AskAsync(companion, question);
        }

        Assert.Equal(["F6", "F5", "F4", "F3", "F2"], companion.RecentQuestions);

        await AskAsync(companion, "f4");
        Assert.Equal(["f4", "F6", "F5", "F3", "F2"], companion.RecentQuestions);
    }

    [Fact]
    public async Task Abmelden_loescht_den_Verlauf_und_Antworten_werden_nie_gespeichert()
    {
        var store = new MemorySettingsStore();
        var user = TestData.Reception();
        var shell = new ShellViewModel(new StubApi().On(Route, Answer("Geheime Antwort 4711")).Client(), user, null, TimeProvider.System,
            new ThemeController(store), "Test", () => Task.CompletedTask);
        await AskAsync(shell.Companion, "Wer arbeitet morgen?");

        var saved = JsonSerializer.Serialize(store.Load());
        Assert.Contains("Wer arbeitet morgen?", saved);
        Assert.DoesNotContain("4711", saved);

        await shell.SignOutCommand.ExecuteAsync(null);

        Assert.Null(store.Load().AssistantHistory);
    }

    [Fact]
    public void Einstellungen_ohne_Verlauf_aus_alter_Datei_werden_gelesen_und_der_Verlauf_laeuft_rund()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aispa-history-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"theme":"Dark","companionOpen":true}""");
            var store = new FileSettingsStore(path);
            Assert.Null(store.Load().AssistantHistory);
            Assert.True(store.Load().CompanionOpen);

            store.Save(store.Load() with { AssistantHistory = new AssistantHistory("person-a", new DateOnly(2027, 3, 2), ["Frage eins", "Frage zwei"]) });

            var back = store.Load().AssistantHistory;
            Assert.Equal(new DateOnly(2027, 3, 2), back?.Day);
            Assert.Equal(["Frage eins", "Frage zwei"], back?.Questions);
            Assert.Equal(ThemeChoice.Dark, store.Load().Theme);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Auf_der_Hilfe_Seite_entfaellt_der_Teil_zur_Seite_der_Assistent_bleibt()
    {
        var page = new HelpTopic("seite-kassa", "Kassa-Seite", [new HelpParagraph([new HelpSpan("Hier wird kassiert.")])]);
        var companion = new CompanionViewModel(new StubApi().Client(), new MemorySettingsStore(), true, AssistantAudience.Reception,
            find: id => id == page.Id ? page : null);

        companion.ShowPage("kassa");
        Assert.True(companion.ShowsPageSection);

        companion.ShowPage("hilfe");
        Assert.False(companion.ShowsPageSection);
        Assert.True(companion.CanAsk);
    }

    // ---------- Mitternacht bei offener Leiste (T-029, Ä360) ----------

    private static readonly DateTimeOffset LateEvening = new(2027, 3, 2, 23, 50, 0, TimeSpan.Zero);

    [Fact]
    public async Task Leiste_bleibt_ueber_Mitternacht_offen_dann_ist_Zuletzt_gefragt_leer_auch_auf_dem_Geraet()
    {
        var clock = new DayClock(LateEvening);
        var store = new MemorySettingsStore();
        var companion = Companion(new StubApi().On(Route, Answer("x")), store, time: clock);
        companion.IsOpen = true;
        await AskAsync(companion, "Wer arbeitet morgen?");

        clock.Advance(TimeSpan.FromMinutes(15));

        Assert.Empty(companion.RecentQuestions);
        Assert.False(companion.HasRecentQuestions);
        Assert.False(companion.RecallQuestion());
        Assert.Null(store.Load().AssistantHistory);
    }

    [Fact]
    public async Task Vor_Mitternacht_bleibt_Zuletzt_gefragt_stehen()
    {
        var clock = new DayClock(LateEvening);
        var companion = Companion(new StubApi().On(Route, Answer("x")), time: clock);
        companion.IsOpen = true;
        await AskAsync(companion, "Wer arbeitet morgen?");

        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(["Wer arbeitet morgen?"], companion.RecentQuestions);
    }

    [Fact]
    public async Task Auch_die_naechste_Mitternacht_leert_Zuletzt_gefragt()
    {
        var clock = new DayClock(LateEvening);
        var companion = Companion(new StubApi().On(Route, Answer("x")), time: clock);
        companion.IsOpen = true;
        await AskAsync(companion, "Wer arbeitet morgen?");
        clock.Advance(TimeSpan.FromMinutes(15));
        await AskAsync(companion, "Umsatz heute?");
        Assert.Equal(["Umsatz heute?"], companion.RecentQuestions);

        clock.Advance(TimeSpan.FromDays(1));

        Assert.Empty(companion.RecentQuestions);
    }

    [Fact]
    public async Task Mitternacht_nimmt_den_Verlauf_einer_anderen_Person_nicht_vom_Geraet()
    {
        var clock = new DayClock(LateEvening);
        var store = new MemorySettingsStore();
        var companion = Companion(new StubApi().On(Route, Answer("x")), store, "person-a", clock);
        var other = new AssistantHistory("person-b", DateOnly.FromDateTime(LateEvening.DateTime), ["Frage von B"]);
        store.Save(store.Load() with { AssistantHistory = other });

        clock.Advance(TimeSpan.FromMinutes(15));

        Assert.Equal("person-b", store.Load().AssistantHistory?.UserId);
        Assert.Empty(companion.RecentQuestions);
    }

    [Fact]
    public async Task Nach_dem_Freigeben_raeumt_Mitternacht_nichts_mehr_ab()
    {
        var clock = new DayClock(LateEvening);
        var companion = Companion(new StubApi().On(Route, Answer("x")), time: clock);
        await AskAsync(companion, "Wer arbeitet morgen?");

        companion.Dispose();
        companion.Dispose();
        clock.Advance(TimeSpan.FromMinutes(15));

        Assert.Equal(["Wer arbeitet morgen?"], companion.RecentQuestions);
    }

    [Fact]
    public async Task Zeitumstellung_Mitternacht_in_Wien_leert_Zuletzt_gefragt_auch_in_der_Nacht_der_Umstellung()
    {
        var vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        // 27.03.2027 23:50 in Wien (MEZ, +01:00); in der Nacht auf den 28.03. wird auf MESZ umgestellt.
        var clock = new DayClock(new DateTimeOffset(2027, 3, 27, 22, 50, 0, TimeSpan.Zero), vienna);
        var companion = Companion(new StubApi().On(Route, Answer("x")), time: clock);
        companion.IsOpen = true;
        await AskAsync(companion, "Wer arbeitet morgen?");

        clock.Advance(TimeSpan.FromSeconds(10 * 60 + 30));
        Assert.Empty(companion.RecentQuestions);

        await AskAsync(companion, "Umsatz heute?");
        // Der 28.03. hat in Wien nur 23 Stunden: bis 23:59:30 bleibt die Frage, um 00:00:30 (+02:00) ist sie weg.
        clock.Advance(TimeSpan.FromHours(23) - TimeSpan.FromMinutes(1));
        Assert.Equal(["Umsatz heute?"], companion.RecentQuestions);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Empty(companion.RecentQuestions);
    }
}
