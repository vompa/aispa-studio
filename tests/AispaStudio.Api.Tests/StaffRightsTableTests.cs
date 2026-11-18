using AispaStudio.Contracts;

namespace AispaStudio.Api.Tests;

/// <summary>
/// Masken-Überarbeitung P11 (ADR 0098, Nachtrag): die Tabelle „Rechte und Zuständigkeiten“ bekommt von den beiden vorhandenen Übersichten (<c>staff-regulations/powers</c>,
/// <c>staff-duties/overview</c>) optionale Felder: Gruppe, Hinweis, Wirkung, „Unbesetzt“ und den Eintrag je Person; <c>locationId</c> begrenzt auf die Personen eines Standorts.
/// Erwartete Texte stehen von Hand in den Tests.
/// </summary>
public sealed class StaffRightsTableTests(ApiFixture fixture)
{
    private static readonly DateOnly NewYearsEve = new(2027, 12, 31);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")).DateTime);

    private static string Regulations(Guid employeeId) => $"/api/employees/{employeeId}/regulations";

    private static async Task<Guid> TypeIdAsync(TestSpa spa, DutyTypeKey key) =>
        (await spa.GetAsync<List<DutyTypeDto>>("/api/staff-duties/types", Ct)).Single(t => t.Key == key).Id;

    [Fact]
    public async Task Given_die_Vollmachten_When_die_Uebersicht_geladen_wird_Then_hat_jede_Zeile_Gruppe_Hinweis_Wirkung_und_Unbesetzt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var storno = await spa.PostAsync<StaffPowerDto>($"{Regulations(spa.Ben.Id)}/powers", new StaffPowerInput(StaffPowerKind.CashVoid, Today.AddDays(-3), NewYearsEve), Ct);
        await spa.PostAsync<StaffPowerDto>($"{Regulations(spa.Anna.Id)}/powers", new StaffPowerInput(StaffPowerKind.Safe, Today.AddDays(-9), Today.AddDays(-1)), Ct);

        var rows = await spa.GetAsync<List<PowerOverviewRowDto>>("/api/staff-regulations/powers", Ct);

        Assert.Equal(
            [
                ("Kassa-Storno", "Kassa und Geld", 1, "Belege stornieren", "Schaltet frei", false),
                ("Kassa-Abschluss", "Kassa und Geld", 1, "Tagesabschluss", "Nur Aushang", true),
                ("Tresor", "Kassa und Geld", 1, "Tresor öffnen und befüllen", "Nur Aushang", true),
                ("Bankeinzahlung", "Kassa und Geld", 1, "Geld zur Bank bringen", "Nur Aushang", true),
                ("Schlüssel", "Sicherheit und Vertretung", 3, "Schlüssel zum Haus", "Nur Aushang", true),
                ("Lieferungen annehmen", "Lager", 2, "Lieferungen entgegennehmen", "Nur Aushang", true),
                ("Bestellungen auslösen", "Lager", 2, "Bestellungen an Lieferanten senden", "Schaltet frei", true),
                ("Vertretung der Leitung", "Sicherheit und Vertretung", 3, "Vertritt die Leitung", "Nur Aushang", true),
            ],
            rows.Select(r => (r.Label, r.Group, r.GroupOrder, r.Hint, r.Effect, r.IsUnstaffed ?? throw new InvalidOperationException($"{r.Label}: IsUnstaffed fehlt"))));
        var holder = Assert.Single(rows[0].Holders);
        Assert.Equal(storno.Id, holder.EntryId);
    }

    [Fact]
    public async Task Given_eine_nur_abgelaufene_Vollmacht_When_die_Uebersicht_geladen_wird_Then_bleibt_die_Zeile_unbesetzt_und_zeigt_die_Person_trotzdem()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<StaffPowerDto>($"{Regulations(spa.Anna.Id)}/powers", new StaffPowerInput(StaffPowerKind.Safe, Today.AddDays(-9), Today.AddDays(-1)), Ct);
        await spa.PostAsync<StaffPowerDto>($"{Regulations(spa.Ben.Id)}/powers", new StaffPowerInput(StaffPowerKind.Keys, Today.AddDays(1)), Ct);

        var rows = await spa.GetAsync<List<PowerOverviewRowDto>>("/api/staff-regulations/powers", Ct);

        var safe = rows.Single(r => r.Kind == StaffPowerKind.Safe);
        Assert.True(safe.IsUnstaffed);
        Assert.True(Assert.Single(safe.Holders).IsExpired);
        Assert.True(rows.Single(r => r.Kind == StaffPowerKind.Keys).IsUnstaffed);
    }

    [Fact]
    public async Task Given_ein_zweiter_Standort_When_die_Uebersicht_mit_locationId_geladen_wird_Then_zaehlen_nur_Personen_dieses_Standorts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        var lena = await spa.CreateEmployeeAsync("Lena", [spa.Massage.Id], graz.Id, Ct);
        await spa.PostAsync<StaffPowerDto>($"{Regulations(spa.Ben.Id)}/powers", new StaffPowerInput(StaffPowerKind.Safe, Today), Ct);
        await spa.PostAsync<StaffPowerDto>($"{Regulations(lena.Id)}/powers", new StaffPowerInput(StaffPowerKind.Keys, Today), Ct);

        var vienna = await spa.GetAsync<List<PowerOverviewRowDto>>($"/api/staff-regulations/powers?locationId={spa.Location.Id}", Ct);
        var styria = await spa.GetAsync<List<PowerOverviewRowDto>>($"/api/staff-regulations/powers?locationId={graz.Id}", Ct);
        var all = await spa.GetAsync<List<PowerOverviewRowDto>>("/api/staff-regulations/powers", Ct);

        Assert.Equal((false, true), (vienna.Single(r => r.Kind == StaffPowerKind.Safe).IsUnstaffed, vienna.Single(r => r.Kind == StaffPowerKind.Keys).IsUnstaffed));
        Assert.Equal((true, false), (styria.Single(r => r.Kind == StaffPowerKind.Safe).IsUnstaffed, styria.Single(r => r.Kind == StaffPowerKind.Keys).IsUnstaffed));
        Assert.Empty(styria.Single(r => r.Kind == StaffPowerKind.Safe).Holders);
        Assert.Equal((false, false), (all.Single(r => r.Kind == StaffPowerKind.Safe).IsUnstaffed, all.Single(r => r.Kind == StaffPowerKind.Keys).IsUnstaffed));
    }

    [Fact]
    public async Task Given_die_Zustaendigkeiten_When_die_Uebersicht_geladen_wird_Then_haben_sie_Gruppe_Hinweis_Wirkung_und_Unbesetzt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<StaffDutyDto>($"/api/employees/{spa.Ben.Id}/duties", new StaffDutyInput(await TypeIdAsync(spa, DutyTypeKey.FirstAid), Today.AddDays(-5)), Ct);
        await spa.PostAsync<StaffDutyDto>($"/api/employees/{spa.Anna.Id}/duties", new StaffDutyInput(await TypeIdAsync(spa, DutyTypeKey.Hygiene), Today.AddDays(-9), Today.AddDays(-1)), Ct);

        var rows = await spa.GetAsync<List<DutyOverviewRowDto>>("/api/staff-duties/overview", Ct);

        Assert.Equal(
            [
                ("Ersthelfer", "Sicherheit und Vertretung", 3, "Verlangt: Erste-Hilfe-Kurs", "Warnt im Dienstplan", false),
                ("Sicherheitsvertrauensperson", "Sicherheit und Vertretung", 3, null, "Nur Aushang", true),
                ("Lager", "Lager", 2, null, "Erinnert an Bestellungen und Ablauf", true),
                ("Brandschutz", "Sicherheit und Vertretung", 3, null, "Nur Aushang", true),
                ("Hygiene", "Sicherheit und Vertretung", 3, null, "Nur Aushang", true),
                ("Kassa-Verantwortung", "Kassa und Geld", 1, null, "Nur Aushang", true),
                ("Ausbildung", "Weiteres", 4, null, "Nur Aushang", true),
            ],
            rows.Select(r => (r.Name, r.Group, r.GroupOrder, r.Hint, r.Effect, r.IsUnstaffed ?? throw new InvalidOperationException($"{r.Name}: IsUnstaffed fehlt"))));
    }

    [Fact]
    public async Task Given_eine_eigene_Zustaendigkeit_When_die_Uebersicht_geladen_wird_Then_steht_sie_in_der_Gruppe_Weiteres()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        await spa.PostAsync<DutyTypeDto>("/api/staff-duties/types", new DutyTypeInput("Schlüsseldienst am Abend"), Ct);

        var rows = await spa.GetAsync<List<DutyOverviewRowDto>>("/api/staff-duties/overview", Ct);

        var own = rows.Single(r => r.Key is null);
        Assert.Equal(("Schlüsseldienst am Abend", "Weiteres", 4, "Nur Aushang", true), (own.Name, own.Group, own.GroupOrder, own.Effect, own.IsUnstaffed));
    }

    [Fact]
    public async Task Given_ein_Standort_When_die_Zustaendigkeiten_mit_locationId_geladen_werden_Then_zaehlen_nur_Personen_dieses_Standorts()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var graz = await spa.CreateLocationAsync("Graz", [DayOfWeek.Monday], Ct);
        await spa.PostAsync<StaffDutyDto>($"/api/employees/{spa.Ben.Id}/duties", new StaffDutyInput(await TypeIdAsync(spa, DutyTypeKey.FirstAid), Today), Ct);

        var vienna = await spa.GetAsync<List<DutyOverviewRowDto>>($"/api/staff-duties/overview?locationId={spa.Location.Id}", Ct);
        var styria = await spa.GetAsync<List<DutyOverviewRowDto>>($"/api/staff-duties/overview?locationId={graz.Id}", Ct);

        Assert.Equal((false, true), (vienna.Single(r => r.Key == DutyTypeKey.FirstAid).IsUnstaffed, styria.Single(r => r.Key == DutyTypeKey.FirstAid).IsUnstaffed));
        Assert.Empty(styria.Single(r => r.Key == DutyTypeKey.FirstAid).Holders);
    }
}
