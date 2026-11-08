using System.Globalization;
using System.Net;
using AispaStudio.Contracts;
using AispaStudio.Desktop.Services;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Schritt 16: Seitenleiste „Meldungen“ mit Gruppen, Detail und „Was tun?“, Schaltfläche, Symbole im Spaltenkopf und Live-Aktualisierung.</summary>
public sealed class MessagesPanelTests
{
    private static readonly string CalendarRoute = $"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-01";
    private static readonly string CareRoute = $"GET /api/locations/{TestData.LocationId}/room-care?date=2027-03-01";
    private static readonly string StockRoute = $"GET /api/stock/locations/{TestData.LocationId}/demand?from=2027-03-01&until=2027-03-01";
    private static readonly Guid ItemId = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid OilId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    private static RoomCareTaskDto Care(AppointmentDto appointment, RoomCareState state, DateTimeOffset from, DateTimeOffset until) => new(appointment.Id, [appointment.Id],
        TestData.Room.Id, TestData.Room.Name, "Raum Zeder pflegen bis " + until.ToOffset(TimeSpan.FromHours(1)).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
        from, until, appointment.EmployeeId, state, null, null);

    private static CalendarDay DayWith(AppointmentDto appointment, RoomCareTaskDto task, bool breakWarning = true) => TestData.Day(appointment) with
    {
        Breaks = breakWarning ? [new CalendarBreak(TestData.Ben.Id, 30, 0, "Keine Zeit mehr für die gesetzliche Pause: 30 Min.")] : [],
        RoomCare = new RoomCareDay([task], [new RoomDayStateDto(TestData.Room.Id, TestData.Room.Name, RoomStartState.Partial, 2, 1, null, null, 0, 1, null, null)], 1),
    };

    private static StockDemandDto Demand(bool warn = true) => new(TestData.LocationId, TestData.Monday, TestData.Monday, 3, [],
        warn ? [new StockDemandLine(ItemId, "Massageöl", "l", 1, 3, 0, 3, 0, StockWarningKind.Insufficient, TestData.At(14), "Massageöl: Bestand reicht nur bis 14:00")] : []);

    private static RoomStartDayDto Start() => new(TestData.LocationId, TestData.Monday,
    [
        new RoomStartRoomDto(TestData.Room.Id, TestData.Room.Name, RoomStartState.Partial,
            [new RoomStartItemStateDto(Guid.Parse("00000000-0000-0000-0000-0000000000a1"), "Temperatur prüfen", true, TestData.At(8, 30), "Rita"),
             new RoomStartItemStateDto(Guid.Parse("00000000-0000-0000-0000-0000000000a2"), "Handtücher auffüllen", false, null, null)],
            null, null, new RoomEquipmentDto(false, [new RestockLine(OilId, "Massageöl neutral", "ml", 200, 500, 300, RestockState.Open)])),
    ]);

    private static StubApi Api(CalendarDay day, bool operations = true)
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources)
            .On(CalendarRoute, day);
        if (operations)
        {
            api.On($"GET /api/locations/{TestData.LocationId}/room-start?date=2027-03-01", Start())
                .On($"GET /api/locations/{TestData.LocationId}/servings?date=2027-03-01", new ServingDayDto(TestData.LocationId, TestData.Monday, [], []))
                .On("GET /api/handbook/reminders?date=2027-03-01", new List<HandbookEntryDto>
                {
                    new(Guid.NewGuid(), "Gäste mit Hund", "Hund im Eingang anbinden.", "Empfang", true, [], true, TestData.At(8)),
                })
                .On(StockRoute, Demand());
        }

        return api;
    }

    private static async Task<CalendarViewModel> OpenAsync(StubApi api, TimeProvider? time = null, ILiveUpdates? live = null, OfflineCache? offline = null)
    {
        var vm = new CalendarViewModel(api.Client(offline), TestData.Reception(), live, time ?? new ManualTime(TestData.At(10)));
        await vm.InitializeAsync();
        await vm.LoadAsync();
        return vm;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private static (AppointmentDto Appointment, RoomCareTaskDto Task) Overdue()
    {
        var appointment = TestData.Appointment(9, TestData.Anna);
        return (appointment, Care(appointment, RoomCareState.Overdue, TestData.At(9, 45), TestData.At(9, 55)));
    }

    // ---------- (a) Quellen und Gruppen ----------

    [Fact]
    public async Task Pause_Lager_Raumpflege_Tagesstart_und_Mindestbestand_landen_in_den_richtigen_Gruppen_mit_Zaehler()
    {
        var (appointment, task) = Overdue();
        var vm = await OpenAsync(Api(DayWith(appointment, task)));

        Assert.Equal([MessageKind.Break, MessageKind.RoomCare, MessageKind.Stock, MessageKind.DayStart, MessageKind.RoomsBelowMinimum, MessageKind.Reminder],
            vm.Messages.Select(m => m.Kind));

        vm.OpenMessagesCommand.Execute(null);

        var panel = Assert.IsType<MessagesPanelViewModel>(vm.MessagesPanel);
        Assert.Equal(["Warnungen", "Hinweise"], panel.Groups.Select(g => g.Title));
        Assert.Equal([5, 1], panel.Groups.Select(g => g.Count));
        Assert.Equal("Gäste mit Hund", panel.Groups[1].Items.Single().Title);
        Assert.Equal(["Pause", "Raumpflege überfällig", "Lager", "Tagesstart offen", "Räume unter Mindestbestand"], panel.Groups[0].Items.Select(m => m.Title));
        Assert.Equal("Meldungen", panel.PanelTitle);
        Assert.Equal(["Meldungen"], vm.OpenPanels.Select(t => t.Text));
    }

    [Fact]
    public async Task Ein_Ladefehler_steht_in_der_Gruppe_Fehler()
    {
        var api = new StubApi()
            .On("GET /api/locations", new List<LocationDto> { TestData.Location })
            .On($"GET /api/locations/{TestData.LocationId}/resources", TestData.Resources);
        var vm = await OpenAsync(api);

        vm.OpenMessagesCommand.Execute(null);

        Assert.Equal(["Fehler"], vm.MessagesPanel!.Groups.Select(g => g.Title));
        Assert.True(vm.MessagesHaveError);
        Assert.False(vm.MessagesHaveWarning);
    }

    [Fact]
    public async Task Jede_Meldung_hat_ein_Handbuchthema_das_es_gibt()
    {
        var (appointment, task) = Overdue();
        var vm = await OpenAsync(Api(DayWith(appointment, task)));

        foreach (var message in vm.Messages)
        {
            var (topic, section) = message.Help!.Value;
            var found = HelpContent.Find(topic);
            Assert.NotNull(found);
            Assert.True(section is null || found.HasSection(section), $"{topic}#{section}");
        }

        Assert.True(HelpContent.Find("kalender-krankmeldung")!.HasSection("ausfall-assistent"));
        Assert.True(HelpContent.Find("kalender-meldungen") is not null);
    }

    // ---------- (b) Schaltfläche ----------

    [Fact]
    public async Task Die_Schaltflaeche_zeigt_Meldungen_2_in_der_Warnfarbe_und_ohne_Meldungen_nur_Meldungen()
    {
        var two = TestData.Day() with
        {
            Breaks =
            [
                new CalendarBreak(TestData.Anna.Id, 30, 0, "Keine Zeit mehr für die gesetzliche Pause: 30 Min."),
                new CalendarBreak(TestData.Ben.Id, 30, 0, "Keine Zeit mehr für die gesetzliche Pause: 30 Min."),
            ],
        };
        var warned = await OpenAsync(Api(two, operations: false));

        Assert.Equal("Meldungen 2", warned.MessagesButtonText);
        Assert.True(warned.MessagesHaveWarning);
        Assert.False(warned.MessagesHaveError);

        var calm = await OpenAsync(Api(TestData.Day(), operations: false));

        Assert.Equal("Meldungen", calm.MessagesButtonText);
        Assert.False(calm.MessagesHaveWarning);
        Assert.False(calm.MessagesHaveError);
        calm.OpenMessagesCommand.Execute(null);
        Assert.True(calm.MessagesPanel!.IsEmpty);
        Assert.Equal("Keine Meldungen.", calm.MessagesPanel.EmptyText);
    }

    // ---------- (c) Wegklicken ----------

    [Fact]
    public async Task Wegklicken_haelt_bis_zum_naechsten_Ausloeser_und_gilt_je_Tag()
    {
        var (appointment, task) = Overdue();
        var api = Api(DayWith(appointment, task));
        var vm = await OpenAsync(api);
        var pause = vm.Messages.Single(m => m.Kind == MessageKind.Break);

        pause.DismissCommand!.Execute(null);

        Assert.DoesNotContain(vm.Messages, m => m.Kind == MessageKind.Break);
        await vm.LoadAsync();
        Assert.DoesNotContain(vm.Messages, m => m.Kind == MessageKind.Break);

        // Anderer Tag und zurück: je Tag im Speicher, die weggeklickte Pause bleibt weg.
        api.On($"GET /api/locations/{TestData.LocationId}/calendar?date=2027-03-02", TestData.Day());
        vm.NextDayCommand.Execute(null);
        await Until(() => vm.Date == TestData.Monday.AddDays(1));
        await Until(() => vm.Messages.All(m => m.Kind != MessageKind.RoomCare));
        vm.PreviousDayCommand.Execute(null);
        await Until(() => vm.Date == TestData.Monday && vm.Messages.Any(m => m.Kind == MessageKind.Stock));
        Assert.DoesNotContain(vm.Messages, m => m.Kind == MessageKind.Break);

        // Ein neuer Auslöser (anderer Wortlaut der Prüfung) bringt sie zurück.
        api.On(CalendarRoute, DayWith(appointment, task) with
        {
            Breaks = [new CalendarBreak(TestData.Ben.Id, 45, 0, "Keine Zeit mehr für die gesetzliche Pause: 45 Min.")],
        });
        await vm.LoadAsync();
        Assert.Contains(vm.Messages, m => m.Kind == MessageKind.Break);
    }

    // ---------- (d) Aktion ----------

    [Fact]
    public async Task Die_Aktion_Pause_eintragen_oeffnet_die_Pause_der_gemeldeten_Person_mit_den_fehlenden_Minuten_in_ihrer_ersten_Luecke()
    {
        // Ben (Dienst 12:00–20:00) hat um 12:00 einen Termin; die Meldung verlangt 30 Minuten. Auftraggeber, 23.10.2026: vorher öffnete
        // das Fenster für die erste Spalte (Anna) mit fünf Minuten unter dem Mauszeiger.
        var (appointment, task) = Overdue();
        var ben = TestData.Appointment(12, TestData.Ben);
        var day = DayWith(appointment, task) with { Appointments = [appointment, ben] };
        var vm = await OpenAsync(Api(day));
        var pause = vm.Messages.Single(m => m.Kind == MessageKind.Break);

        Assert.Equal("Pause eintragen", pause.ActionLabel);
        pause.ActionCommand!.Execute(null);

        var entry = Assert.IsType<CalendarEntryViewModel>(vm.NewEntry!.Content);
        Assert.Equal(CalendarEntryKind.Break, entry.Kind);
        Assert.StartsWith("Ben", entry.Where, StringComparison.Ordinal);
        // Testzeiten liegen in UTC+1 (TestData.At): die Pause beginnt, sobald Bens Termin samt Nachbereitung vorbei ist.
        var free = ben.BlockedUntil.ToOffset(TimeSpan.FromHours(1));
        Assert.Equal(free.ToString("HH:mm", CultureInfo.InvariantCulture), entry.StartText);
        Assert.Equal(free.AddMinutes(30).ToString("HH:mm", CultureInfo.InvariantCulture), entry.EndText);
    }

    // ---------- (e) Detail und „Was tun?“ ----------

    [Fact]
    public async Task Ein_Klick_auf_die_Karte_oeffnet_den_Reiter_Meldung_und_Was_tun_zeigt_Vorschlaege_als_Knoepfe()
    {
        var (appointment, task) = Overdue();
        var answer = new AssistantAnswer(AssistantAudience.Reception, "Trage bei Ben eine Pause ein. Danach prüft der Kalender die Zeiten neu.", [],
            [new AssistantSuggestion(AssistantSuggestionKind.OpenAppointment, "Eva Muster", TestData.LocationId, null, null, null, appointment.Id, TestData.At(9))],
            "Antwort der KI.", [new AssistantHelpLink("kalender-pausen-und-notizen", "pause", "Pausen", "Pause eintragen.")]);
        var api = Api(DayWith(appointment, task)).On("POST /api/assistant", answer);
        var vm = await OpenAsync(api);
        vm.OpenMessagesCommand.Execute(null);
        var pause = vm.MessagesPanel!.Groups[0].Items.Single(m => m.Kind == MessageKind.Break);

        pause.OpenCommand!.Execute(null);

        var detail = Assert.IsType<MessageDetailViewModel>(vm.MessageDetail);
        Assert.Equal(["Meldung", "Meldungen"], vm.OpenPanels.Select(t => t.Text));
        Assert.Equal("Meldung", vm.ActivePanel!.PanelTitle);
        Assert.Equal("Pause", detail.PanelSubtitle);
        Assert.Equal(["Ben"], detail.Links.Select(l => l.Name));
        Assert.StartsWith("seit ", detail.Item.SinceText, StringComparison.Ordinal);
        Assert.True(detail.CanAct);

        await Until(() => detail.HasAnswer);
        var call = Assert.Single(api.Calls, c => c.Route == "POST /api/assistant");
        Assert.Contains("Was tun bei: Pause. Ben: Keine Zeit mehr", call.Body, StringComparison.Ordinal);
        Assert.Contains("\"page\":\"kalender\"", call.Body, StringComparison.Ordinal);
        Assert.Equal("Im Kalender zeigen", Assert.Single(detail.Suggestions).Action);
        Assert.Equal(("kalender-pausen-und-notizen", "pause"), (detail.HelpTopic, detail.HelpSection));
        Assert.Equal("Antwort der KI.", detail.Disclosure);

        // Der Vorschlag führt erst nach dem Klick aus: der Termin wird im Kalender markiert.
        Assert.Null(vm.MarkedId);
        await detail.RunSuggestionCommand.ExecuteAsync(detail.Suggestions[0]);
        Assert.Equal(appointment.Id, vm.MarkedId);
    }

    [Fact]
    public async Task Listen_Antwort_bringt_die_Ziele_ihrer_Zeilen_als_Vorschlaege_mit()
    {
        // Slice 134 (Ä316): Listen-Antworten tragen ihre Ziele in den Zeilen; der Posteingang zeigt sie weiter als Knöpfe.
        var (appointment, task) = Overdue();
        var row = new AssistantItem("Eva Muster", "Massage · Ben", "09:00",
            new AssistantSuggestion(AssistantSuggestionKind.OpenAppointment, "Eva Muster", TestData.LocationId, null, null, null, appointment.Id, TestData.At(9)), null,
            "Im Kalender zeigen");
        var answer = new AssistantAnswer(AssistantAudience.Reception, "Montag: 1 Termin.", [], [], "Antwort der KI.", [],
            new AssistantBlock(AssistantBlockKind.Answer, "DayOverview", "Montag: 1 Termin.", null, "Kalender", null, null, null, [], Items: [row]));
        var vm = await OpenAsync(Api(DayWith(appointment, task)).On("POST /api/assistant", answer));
        vm.OpenMessagesCommand.Execute(null);

        vm.MessagesPanel!.Groups[0].Items.Single(m => m.Kind == MessageKind.Break).OpenCommand!.Execute(null);
        var detail = Assert.IsType<MessageDetailViewModel>(vm.MessageDetail);
        await Until(() => detail.HasAnswer);

        Assert.Equal("Im Kalender zeigen", Assert.Single(detail.Suggestions).Action);
    }

    [Fact]
    public async Task Ohne_Antwort_des_Assistenten_steht_der_Handbuch_Abschnitt_zur_Meldungsart_da()
    {
        var (appointment, task) = Overdue();
        var api = Api(DayWith(appointment, task)).On("POST /api/assistant", null, HttpStatusCode.ServiceUnavailable);
        var vm = await OpenAsync(api);
        var care = vm.Messages.Single(m => m.Kind == MessageKind.RoomCare);

        care.OpenCommand!.Execute(null);
        var detail = vm.MessageDetail!;
        await Until(() => !detail.IsAsking);

        Assert.False(detail.HasAnswer);
        Assert.Empty(detail.Suggestions);
        Assert.Equal(("kalender-betrieb", "raumpflege"), (detail.HelpTopic, detail.HelpSection));
        Assert.True(detail.HasHelp);
    }

    [Fact]
    public async Task Das_Detail_einer_Tagesstart_Meldung_zeigt_die_Checkliste_des_Raums_und_hakt_ab()
    {
        var (appointment, task) = Overdue();
        var itemId = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
        var api = Api(DayWith(appointment, task)).On($"PUT /api/rooms/{TestData.Room.Id}/start-checks/{itemId}", null, HttpStatusCode.NoContent);
        var vm = await OpenAsync(api);
        var start = vm.Messages.Single(m => m.Kind == MessageKind.DayStart);

        Assert.Equal("Tagesstart öffnen", start.ActionLabel);
        start.ActionCommand!.Execute(null);

        var detail = vm.MessageDetail!;
        Assert.True(detail.HasStartRoom);
        Assert.Equal(["Temperatur prüfen", "Handtücher auffüllen"], detail.StartRoom!.Items.Select(i => i.Text));
        await detail.StartRoom.Items[1].ToggleCommand.ExecuteAsync(null);
        Assert.Contains("\"done\":true", Assert.Single(api.Calls, c => c.Route.StartsWith("PUT ", StringComparison.Ordinal)).Body, StringComparison.Ordinal);
    }

    // ---------- (f) Live ----------

    [Fact]
    public async Task Eine_Meldung_vom_Server_aktualisiert_Zaehler_und_Leiste_ohne_Neuladen_und_die_erledigte_steht_blass_darunter()
    {
        var (appointment, task) = Overdue();
        var api = Api(DayWith(appointment, task));
        var live = new FakeLive();
        var vm = await OpenAsync(api, live: live);
        vm.OpenMessagesCommand.Execute(null);
        Assert.Equal(6, vm.MessageCount);
        Assert.Equal(TestData.LocationId, live.Watched);

        // Ein anderer Arbeitsplatz hakt die Raumpflege ab; das Lager ist wieder in Ordnung.
        api.On(CareRoute, new RoomCareDay([task with { State = RoomCareState.Done, DoneBy = "Rita", DoneAt = TestData.At(10) }],
            [new RoomDayStateDto(TestData.Room.Id, TestData.Room.Name, RoomStartState.Partial, 2, 1, null, null, 0, 0, null, null)], 1));
        live.SendOperationsChanged(OperationsChangeKind.RoomCare);
        await Until(() => vm.Messages.All(m => m.Kind != MessageKind.RoomCare));

        Assert.Equal(5, vm.MessageCount);
        Assert.Equal("Meldungen 5", vm.MessagesButtonText);
        Assert.DoesNotContain(vm.MessagesPanel!.Groups.SelectMany(g => g.Items), m => m is { Kind: MessageKind.RoomCare, IsDone: false });
        var done = Assert.Single(vm.MessagesPanel.Groups, g => g.Title == "Erledigt");
        Assert.Equal("Raumpflege überfällig", done.Items.Single().Title);
        Assert.Equal(0.6, done.Items.Single().CardOpacity);

        var loaded = api.Calls.Count(c => c.Route == CalendarRoute);
        api.On(StockRoute, Demand(warn: false));
        live.SendOperationsChanged(OperationsChangeKind.Stock);
        await Until(() => vm.Messages.All(m => m.Kind != MessageKind.Stock));

        Assert.Equal(4, vm.MessageCount);
        Assert.Equal(loaded, api.Calls.Count(c => c.Route == CalendarRoute));
    }

    // ---------- (g) Minutentakt ----------

    [Fact]
    public async Task Der_Minutentakt_macht_eine_offene_Raumpflege_ueberfaellig_und_die_Meldung_wird_zur_Warnung()
    {
        var appointment = TestData.Appointment(9, TestData.Anna);
        var open = Care(appointment, RoomCareState.Open, TestData.At(9, 45), TestData.At(10, 5));
        var time = new ManualTime(TestData.At(10));
        var vm = await OpenAsync(Api(DayWith(appointment, open, breakWarning: false)), time);

        var before = vm.Messages.Single(m => m.Kind == MessageKind.RoomCare);
        Assert.Equal((MessageSeverity.Notice, "Raumpflege offen"), (before.Severity, before.Title));

        time.Advance(TimeSpan.FromMinutes(6));
        vm.Tick();

        var after = vm.Messages.Single(m => m.Kind == MessageKind.RoomCare);
        Assert.Equal((MessageSeverity.Warning, "Raumpflege überfällig"), (after.Severity, after.Title));
        Assert.Equal(TestData.At(10, 5), after.Since);
    }

    // ---------- (h) Spaltenkopf ----------

    [Fact]
    public async Task Der_Spaltenkopf_zeigt_ein_Symbol_mit_Hinweis_und_der_Klick_filtert_die_Meldungen()
    {
        var (appointment, task) = Overdue();
        var vm = await OpenAsync(Api(DayWith(appointment, task)));

        // Team: Ben hat die Pause-Warnung, Anna nichts.
        var ben = vm.Columns.Single(c => c.Id == TestData.Ben.Id);
        Assert.Equal(MessageSeverity.Warning, ben.Alert!.Severity);
        Assert.Equal("Pause fehlt", ben.Alert.Tip);
        Assert.Null(vm.Columns.Single(c => c.Id == TestData.Anna.Id).Alert);

        vm.ShowRoomsCommand.Execute(null);
        var room = vm.Columns.Single(c => c.Id == TestData.Room.Id);
        Assert.True(room.HasAlert);
        Assert.Equal("Raumpflege überfällig · Tagesstart offen · 1 Raum unter Mindestbestand", room.Alert!.Tip);

        vm.OpenMessagesFor(room);

        var panel = vm.MessagesPanel!;
        Assert.Equal(TestData.Room.Id, panel.Filter!.Id);
        Assert.True(panel.HasFilter);
        var shown = panel.Groups.SelectMany(g => g.Items).ToList();
        Assert.Equal([MessageKind.RoomCare, MessageKind.DayStart, MessageKind.RoomsBelowMinimum], shown.Select(m => m.Kind));

        panel.ClearFilterCommand.Execute(null);
        Assert.Contains(panel.Groups.SelectMany(g => g.Items), m => m.Kind == MessageKind.Break);
    }

    // ---------- Ohne Verbindung ----------

    [Fact]
    public async Task Ohne_Verbindung_bleibt_der_letzte_Stand_und_die_Aktionen_sind_deaktiviert()
    {
        var (appointment, task) = Overdue();
        var api = Api(DayWith(appointment, task));
        var vm = await OpenAsync(api, offline: new OfflineCache(new MemoryOfflineStore(), new ManualTime(TestData.At(10))));
        vm.OpenMessagesCommand.Execute(null);
        Assert.False(vm.MessagesPanel!.IsOffline);

        api.Down = true;
        await vm.LoadAsync();

        Assert.True(vm.MessagesPanel.IsOffline);
        Assert.Equal(6, vm.MessageCount);
        var pause = vm.Messages.Single(m => m.Kind == MessageKind.Break);
        Assert.False(pause.ActionCommand!.CanExecute(null));
        Assert.True(pause.DismissCommand!.CanExecute(null));
    }

    [Fact]
    public async Task Die_Taste_M_oeffnet_und_schliesst_die_Meldungen()
    {
        var (appointment, task) = Overdue();
        var vm = await OpenAsync(Api(DayWith(appointment, task)));

        Assert.True(vm.HandleKey(Avalonia.Input.Key.M, Avalonia.Input.KeyModifiers.None));
        Assert.NotNull(vm.MessagesPanel);

        Assert.True(vm.HandleKey(Avalonia.Input.Key.M, Avalonia.Input.KeyModifiers.None));
        Assert.Null(vm.MessagesPanel);
        Assert.False(vm.HasPanel);
    }
}
