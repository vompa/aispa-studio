using AispaStudio.Contracts;
using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Tests;

/// <summary>Seite „Verlauf“: Zeitraum in Ortszeit des Standorts, Chips, Suche mit Wartezeit, Nachladen nach Tagen, Rechte der Chips und Leerzustände.</summary>
public sealed class ActivityTests
{
    private const string Today = "2027-03-01";

    private static readonly string HistoryRoute = $"GET /api/locations/{TestData.LocationId}/history";

    private static readonly TimeZoneInfo Vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");

    /// <summary>Uhr mit Zeitgebern, die der Test auslöst; die Ortszeit ist Wien.</summary>
    internal sealed class TimerTime(DateTimeOffset now) : TimeProvider
    {
        private readonly List<FakeTimer> _timers = [];
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public override TimeZoneInfo LocalTimeZone => Vienna;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new FakeTimer(callback, state, _now + dueTime);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
            foreach (var timer in _timers.Where(t => !t.Fired && t.Due <= _now).ToList())
            {
                timer.Fire();
            }
        }

        private sealed class FakeTimer(TimerCallback callback, object? state, DateTimeOffset due) : ITimer
        {
            public DateTimeOffset Due { get; } = due;

            public bool Fired { get; private set; }

            public void Fire()
            {
                Fired = true;
                callback(state);
            }

            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private static ActivityEntry Entry(long seq, DateTimeOffset at, ActivityKind kind = ActivityKind.Booked, string text = "Gebucht von Rita", string? customer = "Anna Huber",
        ActivityChannel channel = ActivityChannel.Reception, bool? late = null) =>
        new(seq, at, kind, text, "Rita", channel, Guid.NewGuid(), customer, Guid.NewGuid(), null, null, [], late);

    /// <summary>Zeitpunkt am 1. März 2027 in Wiener Ortszeit (Winterzeit, UTC+1).</summary>
    private static DateTimeOffset Local(int hour, int minute = 0, int day = 1) => new(2027, 3, day, hour, minute, 0, TimeSpan.FromHours(1));

    private static ActivityViewModel Open(StubApi api, CurrentUser? user = null, TimeProvider? time = null) =>
        new(api.Client(), user ?? TestData.Owner(), time ?? new TimerTime(Local(10)))
        {
            LocationSource = () => TestData.Location,
        };

    private static StubApi Api(string query, params ActivityEntry[] items) => new StubApi().On($"{HistoryRoute}?{query}", new ActivityPage(items, null));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task Heute_ruft_den_Endpunkt_mit_den_Tagesgrenzen_des_Standorts_und_gruppiert_nach_Tag()
    {
        var api = Api($"from={Today}&until={Today}", Entry(2, Local(10, 5)), Entry(1, Local(9, 30), ActivityKind.Cancelled, "Storniert von Rita", late: true));
        var activity = Open(api);

        await activity.RefreshAsync();

        Assert.Equal([$"{HistoryRoute}?from={Today}&until={Today}"], api.Calls.Where(c => c.Route.StartsWith(HistoryRoute, StringComparison.Ordinal)).Select(c => c.Route));
        Assert.Equal(["Heute, Montag, 1. März", "Gebucht von Rita", "Storniert von Rita"], activity.Rows.Select(r => r is ActivityDayHeader h ? h.Title : r.AccessibleName));
        var row = Assert.IsType<ActivityRow>(activity.Rows[1]);
        Assert.Equal(("10:05", "Anna Huber · von Rita"), (row.Time, row.Detail));
        Assert.True(((ActivityRow)activity.Rows[2]).IsLate);
        Assert.True(((ActivityRow)activity.Rows[2]).IsWarn);
    }

    [Fact]
    public async Task Kurz_nach_Mitternacht_in_Wien_gilt_schon_der_naechste_Tag()
    {
        // 23:30 UTC am 1. März ist 00:30 am 2. März in Wien: „Heute“ ist der 2. März.
        var api = Api("from=2027-03-02&until=2027-03-02");
        var activity = Open(api, time: new TimerTime(new DateTimeOffset(2027, 3, 1, 23, 30, 0, TimeSpan.Zero)));

        await activity.RefreshAsync();

        Assert.Contains(api.Calls, c => c.Route == $"{HistoryRoute}?from=2027-03-02&until=2027-03-02");
    }

    [Theory]
    [InlineData(ActivityPeriod.Yesterday, "2027-02-28", "2027-02-28")]
    [InlineData(ActivityPeriod.Week, "2027-02-23", "2027-03-01")]
    [InlineData(ActivityPeriod.Month, "2027-01-31", "2027-03-01")]
    public async Task Zeitraum_Chips_setzen_von_und_bis_in_Ortszeit(ActivityPeriod period, string from, string until)
    {
        var api = Api($"from={Today}&until={Today}").On($"{HistoryRoute}?from={from}&until={until}", new ActivityPage([], null));
        var activity = Open(api);
        await activity.RefreshAsync();

        activity.SetPeriodCommand.Execute(period);
        await WaitUntilAsync(() => api.Calls.Any(c => c.Route == $"{HistoryRoute}?from={from}&until={until}"));
    }

    [Fact]
    public async Task Art_Chips_setzen_kinds_und_Stornos_umfassen_Storno_und_Ruecknahme()
    {
        var api = Api($"from={Today}&until={Today}")
            .On($"{HistoryRoute}?from={Today}&until={Today}&kinds=Cancelled&kinds=Restored", new ActivityPage([Entry(1, Local(9), ActivityKind.Cancelled, "Storniert")], null));
        var activity = Open(api);
        await activity.RefreshAsync();

        activity.KindChips.Single(c => c.Label == "Stornos").ToggleCommand.Execute(null);

        await WaitUntilAsync(() => activity.Rows.Count == 2);
        Assert.True(activity.HasFilter);
        Assert.True(activity.IsModified);
        Assert.Contains(api.Calls, c => c.Route == $"{HistoryRoute}?from={Today}&until={Today}&kinds=Cancelled&kinds=Restored");
    }

    [Fact]
    public async Task Suche_wartet_300_Millisekunden_und_fragt_dann_einmal()
    {
        var time = new TimerTime(Local(10));
        var api = Api($"from={Today}&until={Today}").On($"{HistoryRoute}?from={Today}&until={Today}&search=Huber", new ActivityPage([Entry(1, Local(9))], null));
        var activity = Open(api, time: time);
        await activity.RefreshAsync();
        var before = api.Calls.Count;

        activity.Query = "H";
        activity.Query = "Hu";
        activity.Query = "Huber";
        time.Advance(TimeSpan.FromMilliseconds(299));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(before, api.Calls.Count);

        time.Advance(TimeSpan.FromMilliseconds(1));
        await WaitUntilAsync(() => activity.Rows.Count == 2);

        Assert.Equal([$"{HistoryRoute}?from={Today}&until={Today}&search=Huber"], api.Calls.Skip(before).Select(c => c.Route));
    }

    [Fact]
    public async Task Nachladen_haengt_die_zweite_Seite_an_und_haelt_den_Tag_zusammen()
    {
        var first = new ActivityPage([Entry(4, Local(10)), Entry(3, Local(9))], 3);
        var second = new ActivityPage([Entry(2, Local(8)), Entry(1, new DateTimeOffset(2027, 2, 28, 18, 0, 0, TimeSpan.FromHours(1)))], null);
        var api = new StubApi()
            .On($"{HistoryRoute}?from=2027-02-23&until={Today}", first)
            .On($"{HistoryRoute}?from=2027-02-23&until={Today}&before=3", second)
            .On($"{HistoryRoute}?from={Today}&until={Today}", new ActivityPage([], null));
        var activity = Open(api);
        await activity.RefreshAsync();
        activity.SetPeriodCommand.Execute(ActivityPeriod.Week);
        await WaitUntilAsync(() => activity.Rows.Count == 3);
        Assert.True(activity.HasMore);

        await activity.LoadMoreAsync();

        // Ein Kopf für den 1. März (zwei Einträge der ersten und einer der zweiten Seite) und einer für den Vortag.
        Assert.Equal(["H", "E", "E", "E", "H", "E"], activity.Rows.Select(r => r is ActivityDayHeader ? "H" : "E"));
        Assert.Equal(["Heute, Montag, 1. März", "Gestern, Sonntag, 28. Februar"], activity.Rows.OfType<ActivityDayHeader>().Select(h => h.Title));
        Assert.False(activity.HasMore);
    }

    [Fact]
    public void Rezeption_sieht_keine_Chips_fuer_Dienstplan_und_Zugaenge()
    {
        var reception = Open(Api($"from={Today}&until={Today}"), TestData.Reception());
        var owner = Open(Api($"from={Today}&until={Today}"), TestData.Owner());
        var manager = Open(Api($"from={Today}&until={Today}"), TestData.Owner(StaffRole.GeneralManager));
        var calendarOnly = Open(Api($"from={Today}&until={Today}"), new CurrentUser("u9", "k@aispa.test", "Kim", 1, TestData.Reception().Access, null, [AccessRight.ViewCalendar]));

        Assert.Equal(["Buchungen", "Verschoben", "Stornos", "Status", "Zahlungen", "Neue Kunden", "Kundendaten", "Behandlungen"], reception.KindChips.Select(c => c.Label));
        Assert.Equal(["Buchungen", "Verschoben", "Stornos", "Status", "Zahlungen", "Neue Kunden", "Kundendaten", "Behandlungen", "Dienstplan", "Zugänge"], owner.KindChips.Select(c => c.Label));
        Assert.Equal(owner.KindChips.Select(c => c.Label), manager.KindChips.Select(c => c.Label));
        Assert.DoesNotContain(calendarOnly.KindChips, c => c.Label == "Zahlungen");
    }

    [Fact]
    public async Task Leerzustand_ohne_Filter_und_mit_Filter()
    {
        var api = Api($"from={Today}&until={Today}").On($"{HistoryRoute}?from={Today}&until={Today}&kinds=Booked", new ActivityPage([], null));
        var activity = Open(api);
        await activity.RefreshAsync();

        Assert.True(activity.ShowEmpty);
        Assert.Equal("Im gewählten Zeitraum ist nichts passiert.", activity.EmptyText);

        activity.KindChips.Single(c => c.Label == "Buchungen").ToggleCommand.Execute(null);
        await WaitUntilAsync(() => api.Calls.Any(c => c.Route.EndsWith("kinds=Booked", StringComparison.Ordinal)));
        await WaitUntilAsync(() => !activity.IsLoading);

        Assert.True(activity.ShowEmpty);
        Assert.Equal("Nichts gefunden. Filter zurücksetzen?", activity.EmptyText);
    }

    [Fact]
    public async Task Zuruecksetzen_leert_alle_Filter_und_laedt_einmal_neu()
    {
        var api = Api($"from={Today}&until={Today}", Entry(1, Local(9)))
            .On($"{HistoryRoute}?from={Today}&until={Today}&kinds=Paid&kinds=Deposit&kinds=ReceiptVoided&search=bar", new ActivityPage([], null));
        var activity = Open(api);
        await activity.RefreshAsync();
        activity.KindChips.Single(c => c.Label == "Zahlungen").ToggleCommand.Execute(null);
        activity.Query = "bar";
        await WaitUntilAsync(() => !activity.IsLoading);
        var before = api.Calls.Count;

        await activity.ResetAsync();

        Assert.False(activity.IsModified);
        Assert.Equal("", activity.Query);
        Assert.Equal([$"{HistoryRoute}?from={Today}&until={Today}"], api.Calls.Skip(before).Select(c => c.Route));
        Assert.Equal(2, activity.Rows.Count);
    }

    [Fact]
    public async Task Ohne_Verbindung_zeigt_die_Seite_die_Meldung_und_keine_Rueckfrage()
    {
        var api = Api($"from={Today}&until={Today}");
        api.Down = true;
        var activity = Open(api);

        await activity.RefreshAsync();

        Assert.NotNull(activity.Error);
        Assert.True(activity.HasError);
        Assert.False(activity.ShowEmpty);
    }

    [Fact]
    public async Task Enter_meldet_den_gewaehlten_Eintrag_und_Tageskoepfe_sind_nicht_waehlbar()
    {
        var api = Api($"from={Today}&until={Today}", Entry(1, Local(9)));
        var activity = Open(api);
        await activity.RefreshAsync();
        ActivityEntry? opened = null;
        activity.EntryOpened += entry => opened = entry;

        activity.SelectedItem = activity.Rows[0];
        Assert.Null(activity.Selected);
        activity.SelectedItem = activity.Rows[1];
        activity.OpenSelectedCommand.Execute(null);

        Assert.Equal(1, opened?.Seq);
    }

    // ---------- Feld „Person“ aus den Personen der Antwort (Aufgabe E) ----------

    private static readonly ActivityActor[] Actors = [new("u-lena", "Lena"), new("u-rita", "Rita")];

    private static StubApi ApiWithActors(IReadOnlyList<ActivityActor>? actors, string query = "") => new StubApi()
        .On($"{HistoryRoute}?from={Today}&until={Today}{query}", new ActivityPage([Entry(1, Local(9))], null, actors));

    private static bool CalledStaff(StubApi api) => api.Calls.Any(c => c.Route.StartsWith("GET /api/staff", StringComparison.Ordinal));

    [Fact]
    public async Task Die_Rezeption_sieht_das_Feld_Person_mit_genau_den_Personen_der_Antwort_und_ruft_api_staff_nicht_auf()
    {
        var api = ApiWithActors(Actors);
        var activity = Open(api, TestData.Reception());

        await activity.RefreshAsync();

        Assert.True(activity.HasPersonChoice);
        Assert.Equal(["Alle Personen", "Lena", "Rita"], activity.People.Select(p => p.Name));
        Assert.Equal([null, "u-lena", "u-rita"], activity.People.Select(p => p.UserId));
        Assert.False(CalledStaff(api));
    }

    [Fact]
    public async Task Auch_die_Inhaberin_bekommt_die_Personen_aus_der_Antwort_und_nicht_aus_api_staff()
    {
        var api = ApiWithActors(Actors);
        var activity = Open(api);

        await activity.RefreshAsync();

        Assert.Equal(["Alle Personen", "Lena", "Rita"], activity.People.Select(p => p.Name));
        Assert.False(CalledStaff(api));
    }

    [Fact]
    public async Task Ohne_Personen_in_der_Antwort_bleibt_das_Feld_Person_weg()
    {
        var activity = Open(ApiWithActors(null), TestData.Reception());

        await activity.RefreshAsync();

        Assert.False(activity.HasPersonChoice);
        Assert.Equal(["Alle Personen"], activity.People.Select(p => p.Name));
    }

    [Fact]
    public async Task Beim_Neuladen_bleibt_die_gewaehlte_Person_und_neue_Personen_der_Antwort_kommen_dazu()
    {
        var api = ApiWithActors(Actors)
            .On($"{HistoryRoute}?from={Today}&until={Today}&actor=u-rita", new ActivityPage([Entry(2, Local(9))], null, [.. Actors, new ActivityActor("u-tom", "Tom")]));
        var activity = Open(api, TestData.Reception());
        await activity.RefreshAsync();

        activity.Person = activity.People[2];
        await WaitUntilAsync(() => activity.People.Count == 4);

        Assert.Equal("Rita", activity.Person.Name);
        Assert.Equal(["Alle Personen", "Lena", "Rita", "Tom"], activity.People.Select(p => p.Name));
        Assert.Same(activity.People[2], activity.Person);
    }

    [Fact]
    public async Task Eine_gewaehlte_Person_bleibt_in_der_Liste_auch_wenn_die_Antwort_sie_nicht_mehr_nennt()
    {
        var api = ApiWithActors(Actors)
            .On($"{HistoryRoute}?from={Today}&until={Today}&actor=u-rita", new ActivityPage([], null, [Actors[0]]));
        var activity = Open(api, TestData.Reception());
        await activity.RefreshAsync();

        activity.Person = activity.People[2];
        await WaitUntilAsync(() => activity.People.Count == 3 && !activity.IsLoading);

        Assert.Equal("Rita", activity.Person.Name);
        Assert.Equal(["Alle Personen", "Lena", "Rita"], activity.People.Select(p => p.Name));
    }
}
