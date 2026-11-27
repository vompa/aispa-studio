using System.Net;
using System.Net.Http.Json;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http;

namespace AispaStudio.Api.Tests;

/// <summary>Startkatalog nach Art des Spas (ADR 0104, Entscheidungen 4 und 5): Pakete listen, anwenden ohne Doppelte, ablehnen ohne Teilergebnis.</summary>
public sealed class SetupCatalogTests(ApiFixture fixture)
{
    private const string CatalogPath = "/api/setup/catalog";

    private const string ApplyPath = CatalogPath + "/apply";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SetupCatalogApplyInput Choose(string key, params Guid[] roomIds) => new([new(key, roomIds)]);

    private static async Task<HttpValidationProblemDetails> ProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(TestSpa.Json, Ct))!;
    }

    // Rot, wenn die Pakete oder das Feld „exists“ aus dem Katalog verschwinden oder nicht gegen die aktiven Behandlungen abgeglichen werden.
    [Fact]
    public async Task Given_der_Startkatalog_When_er_gelesen_und_ein_Paket_angewendet_wird_Then_steht_exists_fuer_dessen_Behandlungen()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);

        var before = await spa.GetAsync<List<SetupCatalogPackageDto>>(CatalogPath, Ct);
        _ = await spa.PostAsync<SetupCatalogApplyResult>(ApplyPath, Choose("massage", spa.RoomA.Id), Ct);
        var after = await spa.GetAsync<List<SetupCatalogPackageDto>>(CatalogPath, Ct);

        Assert.Equal(SetupCatalog.Packages.Select(p => p.Key), before.Select(p => p.Key));
        Assert.All(before.SelectMany(p => p.Treatments), t => Assert.False(t.Exists));
        Assert.All(after.Single(p => p.Key == "massage").Treatments, t => Assert.True(t.Exists));
        Assert.All(after.Where(p => p.Key != "massage").SelectMany(p => p.Treatments), t => Assert.False(t.Exists));
    }

    // Rot, wenn die Anlage keine Qualifikation oder keinen Raum zuordnet, den Preis nicht auf 0 lässt oder ein zweiter Lauf doppelt anlegt.
    [Fact]
    public async Task Given_ein_Paket_When_es_zweimal_angewendet_wird_Then_haben_die_Behandlungen_Raum_und_Qualifikation_und_nichts_ist_doppelt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var package = SetupCatalog.Packages.Single(p => p.Key == "cosmetics");

        var first = await spa.PostAsync<SetupCatalogApplyResult>(ApplyPath, Choose("cosmetics", spa.RoomA.Id), Ct);
        var second = await spa.PostAsync<SetupCatalogApplyResult>(ApplyPath, Choose("cosmetics", spa.RoomB.Id), Ct);

        Assert.Equal(new SetupCatalogApplied("cosmetics", package.Treatments.Count, 0), Assert.Single(first.Packages));
        Assert.Equal(new SetupCatalogApplied("cosmetics", 0, package.Treatments.Count), Assert.Single(second.Packages));
        var qualification = (await spa.GetAsync<List<QualificationDto>>("/api/qualifications", Ct)).Single(q => q.Name == package.Qualification);
        var created = (await spa.GetAsync<List<TreatmentDto>>("/api/treatments", Ct)).Where(t => package.Treatments.Any(i => i.Name == t.Name)).ToList();
        Assert.Equal(package.Treatments.Count, created.Count);
        Assert.All(created, t =>
        {
            Assert.Equal([spa.RoomA.Id], t.RoomIds);
            Assert.Equal([qualification.Id], t.RequiredQualificationIds);
            Assert.Equal(0m, t.Price);
        });
    }

    // Rot, wenn eine Prüfung fehlt (Recht, Paketschlüssel, Raum) oder ein abgelehnter Aufruf trotzdem etwas anlegt.
    [Fact]
    public async Task Given_ungueltige_Aufrufe_When_angewendet_wird_Then_403_oder_400_mit_Klartext_und_nichts_ist_angelegt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        using var reception = await TestUsers.ClientAsync(fixture, Ct, StaffRole.Reception);

        var forbidden = await reception.PostAsJsonAsync(ApplyPath, Choose("massage", spa.RoomA.Id), TestSpa.Json, Ct);
        var unknownPackage = await ProblemAsync(await spa.Client.PostAsJsonAsync(ApplyPath, Choose("sauna", spa.RoomA.Id), TestSpa.Json, Ct));
        var noRoom = await ProblemAsync(await spa.Client.PostAsJsonAsync(ApplyPath, Choose("massage"), TestSpa.Json, Ct));
        var sauna = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Sauna", 0, 10, true), Ct);
        var former = await spa.PostAsync<RoomDto>($"/api/locations/{spa.Location.Id}/rooms", new RoomInput("Altraum", 0), Ct);
        Assert.True((await spa.Client.DeleteAsync($"/api/rooms/{former.Id}", Ct)).IsSuccessStatusCode);
        var areaRoom = await ProblemAsync(await spa.Client.PostAsJsonAsync(ApplyPath, Choose("massage", sauna.Id), TestSpa.Json, Ct));
        var formerRoom = await ProblemAsync(await spa.Client.PostAsJsonAsync(ApplyPath, Choose("massage", former.Id), TestSpa.Json, Ct));
        var nullChoice = await ProblemAsync(await spa.Client.PostAsJsonAsync(ApplyPath, new SetupCatalogApplyInput([null!]), TestSpa.Json, Ct));
        var twice = await ProblemAsync(await spa.Client.PostAsJsonAsync(ApplyPath,
            new SetupCatalogApplyInput([new("massage", [spa.RoomA.Id]), new("massage", [spa.RoomB.Id])]), TestSpa.Json, Ct));
        var badRoom = await ProblemAsync(await spa.Client.PostAsJsonAsync(ApplyPath,
            new SetupCatalogApplyInput([new("cosmetics", [spa.RoomA.Id]), new("massage", [Guid.NewGuid()])]), TestSpa.Json, Ct));

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(SetupCatalog.UnknownPackage("sauna"), Assert.Single(unknownPackage.Errors["packages"]));
        Assert.Equal(SetupCatalog.NoRoom("Massage"), Assert.Single(noRoom.Errors["packages"]));
        Assert.Equal(SetupCatalog.BadRoom("Massage"), Assert.Single(badRoom.Errors["packages"]));
        Assert.Equal(SetupCatalog.BadRoom("Massage"), Assert.Single(areaRoom.Errors["packages"]));
        Assert.Equal(SetupCatalog.BadRoom("Massage"), Assert.Single(formerRoom.Errors["packages"]));
        Assert.Equal(SetupCatalog.EmptyChoice, Assert.Single(nullChoice.Errors["packages"]));
        Assert.Equal(SetupCatalog.DuplicatePackage, Assert.Single(twice.Errors["packages"]));
        var all = await spa.GetAsync<List<SetupCatalogPackageDto>>(CatalogPath, Ct);
        Assert.All(all.SelectMany(p => p.Treatments), t => Assert.False(t.Exists));
    }

    // Rot, wenn die Sperre je Mandant fehlt: dann entstehen Behandlungen doppelt oder der Unique-Index der Qualifikation wirft einen 500er.
    [Fact]
    public async Task Given_zwei_gleichzeitige_Aufrufe_fuer_dasselbe_Paket_When_angewendet_wird_Then_beide_200_und_nichts_ist_doppelt()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var package = SetupCatalog.Packages.Single(p => p.Key == "hands-feet");

        var responses = await Task.WhenAll(Enumerable.Range(0, 2)
            .Select(_ => spa.Client.PostAsJsonAsync(ApplyPath, Choose("hands-feet", spa.RoomA.Id), TestSpa.Json, Ct)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var treatments = await spa.GetAsync<List<TreatmentDto>>("/api/treatments", Ct);
        Assert.All(package.Treatments, item => Assert.Single(treatments, t => t.Name == item.Name));
        Assert.Single(await spa.GetAsync<List<QualificationDto>>("/api/qualifications", Ct), q => q.Name == package.Qualification);
    }

    // Rot, wenn eine vorhandene Qualifikation (andere Schreibweise) nicht wiederverwendet oder eine vorhandene Behandlung nicht übersprungen wird.
    [Fact]
    public async Task Given_vorhandene_Qualifikation_und_Behandlung_des_Pakets_When_angewendet_wird_Then_nur_der_Rest_wird_angelegt_mit_der_vorhandenen_Qualifikation()
    {
        var spa = await TestSpa.CreateAsync(fixture, Ct);
        var package = SetupCatalog.Packages.Single(p => p.Key == "cosmetics");
        var qualification = await spa.PostAsync<QualificationDto>("/api/qualifications", new QualificationInput(package.Qualification.ToLowerInvariant()), Ct);
        var existing = package.Treatments[0];
        _ = await spa.PostAsync<TreatmentDto>("/api/treatments",
            new TreatmentInput(existing.Name.ToUpperInvariant(), 30, 0, 0, 50m, [], [spa.RoomA.Id], []), Ct);

        var result = await spa.PostAsync<SetupCatalogApplyResult>(ApplyPath, Choose("cosmetics", spa.RoomB.Id), Ct);

        Assert.Equal(new SetupCatalogApplied("cosmetics", package.Treatments.Count - 1, 1), Assert.Single(result.Packages));
        Assert.Single(await spa.GetAsync<List<QualificationDto>>("/api/qualifications", Ct),
            q => string.Equals(q.Name, package.Qualification, StringComparison.OrdinalIgnoreCase));
        var added = (await spa.GetAsync<List<TreatmentDto>>("/api/treatments", Ct)).Where(t => package.Treatments.Skip(1).Any(i => i.Name == t.Name)).ToList();
        Assert.Equal(package.Treatments.Count - 1, added.Count);
        Assert.All(added, t => Assert.Equal([qualification.Id], t.RequiredQualificationIds));
    }
}
