using AispaStudio.Api.CalendarSubscriptions;
using AispaStudio.Api.Costs;
using AispaStudio.Api.Data;
using AispaStudio.Api.Demo;
using AispaStudio.Api.Reporting;
using AispaStudio.Api.Tenancy;

namespace AispaStudio.Api.Scheduling;

/// <summary>
/// Stammdaten des Beispiel-Spas. Termine, Kassa, Konten und alles Weitere legt <see cref="Demo.DemoSeeder"/> darauf an
/// (nur mit <c>Database:SeedDemoData</c>, also in der Entwicklungsumgebung).
/// </summary>
internal static class DemoData
{
    /// <summary>
    /// Beispiel-Spa als eigener Mandant: Standorte, Räume, Geräte, Team, Behandlungskatalog mit Material und Vorbereitung,
    /// Kostensätze und Kundenstamm. Behandlungen, Dauer und Preise sind angelehnt an öffentliche Preislisten Wiener Day-Spas
    /// (Stand Oktober 2026: 60 Minuten Massage etwa 95 bis 115 €), Namen und Beschreibungen sind eigene.
    /// </summary>
    public static async Task<Guid> SeedAsync(IServiceProvider scoped, TimeProvider time, CancellationToken ct)
    {
        var db = scoped.GetRequiredService<AispaDbContext>();
        var tenant = new Tenant { Name = "AISPA Day Spa GmbH", Country = "AT", CreatedAt = time.GetUtcNow() };
        db.Tenants.Add(tenant);
        scoped.GetRequiredService<TenantContext>().Use(tenant.Id);
        await CalendarSubscriptionDefaults.AddMissingAsync(db, tenant.Country, time.GetUtcNow(), ct);

        var massage = new Qualification { Name = "Massage" };
        var thai = new Qualification { Name = "Thai-Massage" };
        var hotStone = new Qualification { Name = "Hot Stone" };
        var ayurveda = new Qualification { Name = "Ayurveda" };
        var reflex = new Qualification { Name = "Fußreflexzonen" };
        var pregnancy = new Qualification { Name = "Schwangerschaftsmassage" };
        // In Österreich medizinischen Masseurinnen und Heilmasseuren vorbehalten.
        var lymph = new Qualification { Name = "Manuelle Lymphdrainage" };
        var body = new Qualification { Name = "Körperbehandlung und Hamam" };
        var cosmetics = new Qualification { Name = "Kosmetik" };
        var nails = new Qualification { Name = "Hand- und Fußpflege" };
        db.Qualifications.AddRange(massage, thai, hotStone, ayurveda, reflex, pregnancy, lymph, body, cosmetics, nails);

        var vienna = NewLocation("Wien Innere Stadt", (new TimeOnly(9, 0), new TimeOnly(20, 0)), DayOfWeek.Monday, DayOfWeek.Tuesday,
            DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday);
        var salzburg = NewLocation("Salzburg Altstadt", (new TimeOnly(10, 0), new TimeOnly(19, 0)), DayOfWeek.Monday, DayOfWeek.Tuesday,
            DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday);
        // Graz: kleiner Standort ohne Montag, damit Kalender und Berichte auch einen schwächer ausgelasteten Betrieb zeigen.
        var graz = NewLocation("Graz Zentrum", (new TimeOnly(10, 0), new TimeOnly(19, 0)), DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday);
        // Erfundene Anschriften und Telefonnummern.
        (vienna.Street, vienna.PostalCode, vienna.City, vienna.Phone, vienna.Email) =
            ("Beispielgasse 1", "1010", "Wien", "+43 1 0000000", "wien@aispa.example");
        (salzburg.Street, salzburg.PostalCode, salzburg.City, salzburg.Phone, salzburg.Email) =
            ("Beispielgasse 2", "5020", "Salzburg", "+43 662 000000", "salzburg@aispa.example");
        (graz.Street, graz.PostalCode, graz.City, graz.Phone, graz.Email) =
            ("Beispielgasse 3", "8010", "Graz", "+43 316 000000", "graz@aispa.example");
        // Bundesland je Standort (Feiertage) und erfundene Anreise für Wien und Salzburg; Graz hat keine, damit der Assistent auch „nichts hinterlegt“ zeigt.
        (salzburg.Region, graz.Region) = ("AT-5", "AT-6");
        vienna.Directions = "U1 oder U3 bis Stephansplatz, von dort 4 Minuten zu Fuß. Garage Am Graben, 2 Minuten zu Fuß; Fahrradständer im Hof.";
        salzburg.Directions = "Parkgarage Altstadt im Mönchsberg, von dort mit dem Lift und 5 Minuten zu Fuß. Bus 3 bis Hanuschplatz.";
        db.Locations.AddRange(vienna, salzburg, graz);
        DemoCalendarSubscriptions.Add(db, time.GetUtcNow(), vienna, salzburg, graz);

        var lavender = new Room { LocationId = vienna.Id, Name = "Raum Lavendel", CleaningMinutes = 10 };
        var cedar = new Room { LocationId = vienna.Id, Name = "Raum Zeder", CleaningMinutes = 10 };
        var lotus = new Room { LocationId = vienna.Id, Name = "Raum Lotus", CleaningMinutes = 10 };
        var thaiRoom = new Room { LocationId = vienna.Id, Name = "Thai-Raum mit Bodenmatte", CleaningMinutes = 10 };
        var ritualSuite = new Room { LocationId = vienna.Id, Name = "Ritual-Suite mit Dusche", CleaningMinutes = 15, Places = 2 };
        var hamam = new Room { LocationId = vienna.Id, Name = "Hamam", CleaningMinutes = 20 };
        var cabin = new Room { LocationId = vienna.Id, Name = "Kosmetikkabine", CleaningMinutes = 5 };
        var nailDesk = new Room { LocationId = vienna.Id, Name = "Hand- und Fußpflegeplatz", CleaningMinutes = 10 };
        var sauna = new Room { LocationId = vienna.Id, Name = "Finnische Sauna", Places = 12, IsArea = true };
        var relaxRoom = new Room { LocationId = vienna.Id, Name = "Ruheraum", Places = 16, IsArea = true };
        var alpenrose = new Room { LocationId = salzburg.Id, Name = "Raum Alpenrose", CleaningMinutes = 10 };
        var edelweiss = new Room { LocationId = salzburg.Id, Name = "Raum Edelweiß", CleaningMinutes = 5 };
        var stoneRoom = new Room { LocationId = salzburg.Id, Name = "Zirbenstube mit Dusche", CleaningMinutes = 15 };
        var schlossberg = new Room { LocationId = graz.Id, Name = "Raum Schloßberg", CleaningMinutes = 10 };
        var mur = new Room { LocationId = graz.Id, Name = "Raum Mur", CleaningMinutes = 10 };
        var grazCabin = new Room { LocationId = graz.Id, Name = "Kosmetikraum Graz", CleaningMinutes = 5 };
        db.Rooms.AddRange(lavender, cedar, lotus, thaiRoom, ritualSuite, hamam, cabin, nailDesk, sauna, relaxRoom, alpenrose, edelweiss, stoneRoom,
            schlossberg, mur, grazCabin);

        var stoneHeater = new Equipment { LocationId = vienna.Id, Name = "Hot-Stone-Wärmer 1" };
        var stoneHeater2 = new Equipment { LocationId = vienna.Id, Name = "Hot-Stone-Wärmer 2" };
        var stampSteamer = new Equipment { LocationId = vienna.Id, Name = "Kräuterstempel-Dämpfer" };
        var hydrafacial = new Equipment { LocationId = vienna.Id, Name = "Hydrafacial-Gerät" };
        var stoneHeaterSalzburg = new Equipment { LocationId = salzburg.Id, Name = "Hot-Stone-Wärmer" };
        var stoneHeaterGraz = new Equipment { LocationId = graz.Id, Name = "Hot-Stone-Wärmer Graz" };
        db.Equipment.AddRange(stoneHeater, stoneHeater2, stampSteamer, hydrafacial, stoneHeaterSalzburg, stoneHeaterGraz);
        Equipment[] heaters = [stoneHeater, stoneHeater2, stoneHeaterSalzburg, stoneHeaterGraz];

        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        var tuesdayToSaturday = new[] { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };
        db.Employees.AddRange(
            NewEmployee("Lena Huber", "#2f6f5e", [massage, hotStone, lymph, pregnancy, reflex], vienna.Id, weekdays, new(9, 0), new(17, 0)),
            NewEmployee("Marco Berger", "#7a4fa3", [massage, ayurveda, hotStone], vienna.Id, tuesdayToSaturday, new(12, 0), new(20, 0)),
            NewEmployee("Sophie Gruber", "#b5524a", [cosmetics], vienna.Id, [.. weekdays, DayOfWeek.Saturday], new(9, 0), new(17, 0)),
            NewEmployee("Malee Chaiyasit", "#c28a2c", [thai, massage, hotStone, reflex], vienna.Id, weekdays, new(11, 0), new(19, 0)),
            NewEmployee("Siriporn Kaewdee", "#a34f7a", [thai, massage, hotStone, body], vienna.Id, tuesdayToSaturday, new(12, 0), new(20, 0), "Siri"),
            NewEmployee("Elif Demir", "#4f7aa3", [body, cosmetics, nails], vienna.Id, weekdays, new(9, 0), new(17, 0)),
            WithHours(
                NewEmployee("Jana Wolf", "#3b6ea8", [massage, hotStone, cosmetics, body], salzburg.Id, weekdays, new(10, 0), new(18, 0)),
                vienna.Id, [DayOfWeek.Saturday], new(10, 0), new(18, 0)),
            NewEmployee("Theresa Lechner", "#6a8f3a", [massage, body, reflex, pregnancy], salzburg.Id, tuesdayToSaturday, new(10, 0), new(19, 0)),
            NewEmployee("Katharina Kern", "#8a5a2b", [massage, hotStone, reflex], graz.Id, tuesdayToSaturday, new(10, 0), new(18, 0)),
            NewEmployee("Daniel Url", "#2b6f8a", [massage, hotStone], graz.Id, tuesdayToSaturday, new(11, 0), new(19, 0)),
            NewEmployee("Ivana Marić", "#9a3f6e", [cosmetics], graz.Id, tuesdayToSaturday, new(10, 0), new(18, 0)));

        Room[] massageRooms = [lavender, cedar, lotus, alpenrose, stoneRoom, schlossberg, mur];
        Room[] viennaMassageRooms = [lavender, cedar, lotus];
        Room[] facialRooms = [cabin, edelweiss, grazCabin];
        Room[] bodyRooms = [ritualSuite, stoneRoom];

        // Öl, Einwegauflage und Wäsche wie in den meisten Massagen; Mengen und Einkaufspreise sind Schätzwerte.
        Material Oil(decimal ml) => new("Massageöl neutral", ml, "ml", 0.04m);
        Material AromaOil(decimal ml) => new("Aromaöl, Mischung nach Wahl", ml, "ml", 0.09m);
        var cover = new Material("Einweg-Liegenauflage", 1, "Stk", 0.35m);
        var linen = new Material("Wäsche (Handtücher, Laken)", 1, "Set", 1.20m);

        // Kategorie der folgenden Behandlungen; gruppiert das Angebot im Kundenbereich und in den Stammdaten.
        var category = "";
        Treatment Offer(string name, int duration, int preparation, int followUp, decimal price, Qualification[] qualifications, Room[] rooms,
            Equipment[] equipment, Material[] materials, (string Text, int? MinutesBefore)[] steps, int handsOn = 0, int processing = 0)
        {
            var treatment = NewTreatment(name, duration, preparation, followUp, price, qualifications, rooms, equipment);
            treatment.HandsOnMinutes = handsOn;
            treatment.ProcessingMinutes = processing;
            treatment.Category = category;
            db.Treatments.Add(treatment);
            db.TreatmentMaterials.AddRange(materials.Select((m, i) => new TreatmentMaterial
            {
                TreatmentId = treatment.Id,
                Position = i,
                Name = m.Name,
                Quantity = m.Quantity,
                Unit = m.Unit,
                UnitCost = m.UnitCost,
            }));
            db.TreatmentPreparationSteps.AddRange(steps.Select((s, i) => new TreatmentPreparationStep
            {
                TreatmentId = treatment.Id,
                Position = i,
                Text = s.Text,
                MinutesBefore = s.MinutesBefore,
            }));
            return treatment;
        }

        category = "Massagen";
        // Massagen
        Offer("Klassische Massage", 50, 5, 10, 79m, [massage], massageRooms, [], [Oil(20), cover, linen],
            [("Liege vorwärmen, Druckstärke aus den Vorlieben ansehen", 5)]);
        Offer("Rückenmassage Express", 25, 5, 5, 49m, [massage], massageRooms, [], [Oil(10), cover, linen], []);
        Offer("Sportmassage", 50, 5, 10, 89m, [massage], massageRooms, [], [Oil(20), new("Wärmebalsam", 5, "g", 0.12m), cover, linen], []);
        Offer("Traditionelle Thai-Massage (60 Min.)", 60, 5, 10, 95m, [thai], [thaiRoom], [],
            [new("Thai-Kleidung (Hose, Shirt), Wäscherei", 1, "Set", 0.90m), linen],
            [("Bequeme Thai-Kleidung in passender Größe bereitlegen; ohne Öl, auf der Matte", 5)]);
        Offer("Traditionelle Thai-Massage (90 Min.)", 90, 5, 10, 125m, [thai], [thaiRoom], [],
            [new("Thai-Kleidung (Hose, Shirt), Wäscherei", 1, "Set", 0.90m), linen],
            [("Bequeme Thai-Kleidung in passender Größe bereitlegen; ohne Öl, auf der Matte", 5)]);
        Offer("Thai-Aromaölmassage (60 Min.)", 60, 5, 10, 99m, [thai], viennaMassageRooms, [], [AromaOil(25), cover, linen],
            [("Drei Duftmischungen zur Auswahl hinstellen", 5)]);
        Offer("Thai-Aromaölmassage (90 Min.)", 90, 5, 10, 129m, [thai], viennaMassageRooms, [], [AromaOil(35), cover, linen],
            [("Drei Duftmischungen zur Auswahl hinstellen", 5)]);
        Offer("Balinesische Massage", 60, 5, 10, 99m, [massage], viennaMassageRooms, [], [AromaOil(25), cover, linen], []);
        Offer("Thai-Kräuterstempelmassage (60 Min.)", 60, 15, 10, 115m, [thai], viennaMassageRooms, [stampSteamer],
            [new("Kräuterstempel", 2, "Stk", 6.50m), Oil(20), cover, linen],
            [("Kräuterstempel in den Dämpfer legen", 20), ("Stempeltemperatur am Unterarm prüfen", 2)]);
        Offer("Thai-Kräuterstempelmassage (90 Min.)", 90, 15, 10, 139m, [thai], viennaMassageRooms, [stampSteamer],
            [new("Kräuterstempel", 2, "Stk", 6.50m), Oil(30), cover, linen],
            [("Kräuterstempel in den Dämpfer legen", 20), ("Stempeltemperatur am Unterarm prüfen", 2)]);
        Offer("Lava-Hot-Stone-Massage (60 Min.)", 60, 15, 15, 109m, [massage, hotStone], massageRooms, heaters,
            [Oil(25), new("Desinfektion Steine", 20, "ml", 0.03m), cover, linen],
            [("Steine im Wärmer auf etwa 55 °C bringen", 30), ("Temperatur mit dem Thermometer prüfen", 5)]);
        Offer("Lava-Hot-Stone-Massage (90 Min.)", 90, 15, 15, 135m, [massage, hotStone], massageRooms, heaters,
            [Oil(35), new("Desinfektion Steine", 20, "ml", 0.03m), cover, linen],
            [("Steine im Wärmer auf etwa 55 °C bringen", 30), ("Temperatur mit dem Thermometer prüfen", 5)]);
        Offer("Fußreflexzonenmassage", 50, 10, 5, 79m, [reflex], [lavender, cedar, lotus, alpenrose, schlossberg], [],
            [new("Fußbadesalz", 30, "g", 0.03m), new("Fußbalsam", 8, "g", 0.10m), linen], [("Warmes Fußbad vorbereiten", 10)]);
        Offer("Massage für Schwangere", 60, 10, 10, 99m, [massage, pregnancy], [lotus, alpenrose], [], [Oil(20), cover, linen],
            [("Lagerungskissen für die Seitenlage bereitlegen", 10), ("Schwangerschaftswoche im Termin vermerkt?", null)]);
        Offer("Ayurveda Abhyanga", 90, 15, 15, 139m, [ayurveda], [cedar], [], [new("Sesam-Kräuteröl", 80, "ml", 0.03m), linen, cover],
            [("Kräuteröl im Wasserbad auf Körpertemperatur bringen", 15)]);
        Offer("Manuelle Lymphdrainage", 60, 5, 5, 89m, [lymph], [lavender, lotus], [], [cover, linen], []);
        // Die teuerste Massage verlangt an jedem Standort und von allen Gästen die volle Ausfallgebühr.
        Offer("AISPA Signaturmassage", 90, 15, 10, 145m, [thai], viennaMassageRooms, [stampSteamer],
            [AromaOil(30), new("Kräuterstempel", 1, "Stk", 6.50m), cover, linen],
            [("Kräuterstempel in den Dämpfer legen", 20), ("Ingwertee für danach vorbereiten", 5)]).NoShowFeePercent = 100;

        category = "Gesicht";
        // Gesicht
        Offer("Gesichtsbehandlung Klassik", 60, 5, 5, 95m, [cosmetics], facialRooms, [],
            [new("Reinigung, Peeling, Maske, Pflege", 1, "Satz", 6.80m), new("Einweg-Kosmetiktücher", 6, "Stk", 0.05m)],
            [("Hautnotizen der letzten Behandlung ansehen", 5)], handsOn: 25, processing: 15);
        Offer("Express-Facial für die Mittagspause", 30, 5, 5, 59m, [cosmetics], facialRooms, [],
            [new("Reinigung, Maske, Pflege", 1, "Satz", 3.90m)], []);
        Offer("Hydrafacial", 45, 10, 10, 139m, [cosmetics], [cabin], [hydrafacial],
            [new("Hydrafacial-Aufsatz und Seren", 1, "Satz", 27.50m)], [("Gerät spülen, Seren nach Hauttyp einsetzen", 10)]);
        Offer("Anti-Aging-Gesichtsbehandlung mit Gesichtsmassage", 90, 5, 10, 149m, [cosmetics], facialRooms, [],
            [new("Wirkstoffampulle, Maske, Pflege", 1, "Satz", 14.00m), new("Einweg-Kosmetiktücher", 8, "Stk", 0.05m)],
            [("Hautnotizen der letzten Behandlung ansehen", 5)]);

        category = "Körper und Hamam";
        // Körper und Hamam
        Offer("Kokos-Körperpeeling", 30, 10, 10, 65m, [body], [ritualSuite, hamam, stoneRoom], [],
            [new("Kokospeeling", 80, "g", 0.11m), linen], [("Dusche vorheizen, Handtücher wärmen", 10)]);
        Offer("Reismilch-Körperpackung", 45, 10, 10, 85m, [body], bodyRooms, [],
            [new("Reismilch-Packung", 1, "Anw.", 5.40m), new("Wärmefolie", 1, "Stk", 0.60m), linen],
            [("Wärmeliege einschalten", 15)], handsOn: 15, processing: 20);
        Offer("Hamam-Ritual mit Schaumwaschung", 60, 15, 15, 109m, [body], [hamam], [],
            [new("Olivenseife", 30, "g", 0.04m), new("Peeling-Handschuh (Geschenk an Gast)", 1, "Stk", 2.20m),
                new("Hamamtuch und Handtücher", 1, "Set", 1.60m)],
            [("Wärmestein im Hamam aufheizen", 30), ("Tee und Trockenfrüchte für den Ruheraum", 5)]);

        category = "Rituale";
        // Rituale: gebuchte Zeit ist die betreute Zeit; die Sauna davor nutzt der Gast selbst.
        Offer("Ritual Kokos und warme Steine", 120, 15, 20, 219m, [body, massage, hotStone], bodyRooms, heaters,
            [new("Kokospeeling", 80, "g", 0.11m), new("Kokos-Körpermaske", 1, "Anw.", 6.20m), Oil(25), linen, cover],
            [("Steine im Wärmer auf etwa 55 °C bringen", 30), ("Dusche vorheizen, Handtücher wärmen", 10)]);
        Offer("Ritual Rose und Reismilch", 120, 10, 20, 209m, [body, massage], bodyRooms, [],
            [new("Rosenpeeling", 80, "g", 0.12m), new("Reismilch-Packung", 1, "Anw.", 5.40m), AromaOil(25), linen, cover],
            [("Wärmeliege einschalten", 15), ("Rosenblüten-Fußbad vorbereiten", 10)]);
        Offer("Ritual Grüner Tee und Algen", 150, 10, 20, 245m, [body, thai], [ritualSuite], [],
            [new("Mangostan-Peeling", 80, "g", 0.13m), new("Algen-Grüntee-Maske", 1, "Anw.", 7.10m), AromaOil(35), linen, cover],
            [("Wärmeliege einschalten", 15), ("Grüntee für danach bereitstellen", 5)]);
        Offer("Zirben-Ritual", 105, 10, 20, 199m, [body, massage], [stoneRoom], [],
            [new("Heublumen-Peeling", 80, "g", 0.10m), new("Zirbenöl", 30, "ml", 0.11m), linen, cover],
            [("Zirbenkissen anwärmen", 10)]);

        category = "Hände und Füße";
        // Hände und Füße
        Offer("Maniküre mit Lack", 45, 5, 10, 52m, [nails], [nailDesk], [],
            [new("Feilen und Nagelhautpflege (Einweg)", 1, "Satz", 0.80m), new("Lack", 1, "Anw.", 0.60m)], []);
        Offer("Pediküre", 60, 10, 10, 69m, [nails], [nailDesk], [],
            [new("Feilen und Hornhautpflege (Einweg)", 1, "Satz", 1.40m), new("Fußbadesalz", 30, "g", 0.03m)],
            [("Fußwanne desinfizieren und füllen", 10)]);

        // Kostensätze für die Vollkostenrechnung; Personal inkl. Lohnnebenkosten und Sonderzahlungen, Miete anteilig je Raum.
        // Kurze Körperbehandlungen und Hand- und Fußpflege liegen damit absichtlich knapp an oder unter den Vollkosten:
        // So zeigt die Kostenansicht im Beispiel, was sie aufdecken soll.
        db.LocationCostSettings.AddRange(
            new LocationCostSettings { LocationId = vienna.Id, CleaningHourlyCost = 24m, DefaultStaffHourlyCost = 24m, OtherMonthlyCosts = 9500m },
            new LocationCostSettings { LocationId = salzburg.Id, CleaningHourlyCost = 22m, DefaultStaffHourlyCost = 23m, OtherMonthlyCosts = 4800m },
            new LocationCostSettings { LocationId = graz.Id, CleaningHourlyCost = 21m, DefaultStaffHourlyCost = 22m, OtherMonthlyCosts = 3200m });
        db.RoomCosts.AddRange(
            new RoomCost { RoomId = lavender.Id, MonthlyCost = 950m, CleaningMaterialCost = 0.80m },
            new RoomCost { RoomId = cedar.Id, MonthlyCost = 950m, CleaningMaterialCost = 0.80m },
            new RoomCost { RoomId = lotus.Id, MonthlyCost = 950m, CleaningMaterialCost = 0.80m },
            new RoomCost { RoomId = thaiRoom.Id, MonthlyCost = 1100m, CleaningMaterialCost = 0.90m },
            new RoomCost { RoomId = ritualSuite.Id, MonthlyCost = 1400m, CleaningMaterialCost = 1.50m },
            new RoomCost { RoomId = hamam.Id, MonthlyCost = 1800m, CleaningMaterialCost = 2.40m },
            new RoomCost { RoomId = cabin.Id, MonthlyCost = 750m, CleaningMaterialCost = 0.60m },
            new RoomCost { RoomId = nailDesk.Id, MonthlyCost = 500m, CleaningMaterialCost = 0.70m },
            new RoomCost { RoomId = alpenrose.Id, MonthlyCost = 700m, CleaningMaterialCost = 0.80m },
            new RoomCost { RoomId = edelweiss.Id, MonthlyCost = 600m, CleaningMaterialCost = 0.60m },
            new RoomCost { RoomId = stoneRoom.Id, MonthlyCost = 1000m, CleaningMaterialCost = 1.50m },
            new RoomCost { RoomId = schlossberg.Id, MonthlyCost = 620m, CleaningMaterialCost = 0.80m },
            new RoomCost { RoomId = mur.Id, MonthlyCost = 620m, CleaningMaterialCost = 0.80m },
            new RoomCost { RoomId = grazCabin.Id, MonthlyCost = 480m, CleaningMaterialCost = 0.60m });

        // Gemeinsamer Kundenstamm aller Standorte, frei erfunden; die ersten zwölf sind die Stammgäste aus den Tests.
        DemoCustomers.Add(db, time.GetUtcNow());

        await db.SaveChangesAsync(ct);
        return tenant.Id;
    }

    private sealed record Material(string Name, decimal Quantity, string Unit, decimal UnitCost);

    private static Location NewLocation(string name, (TimeOnly Opens, TimeOnly Closes) hours, params DayOfWeek[] days)
    {
        var location = new Location { Name = name, TimeZone = "Europe/Vienna", Currency = "EUR", Region = "AT-9" };
        location.OpeningHours.AddRange(days.Select(d =>
            new LocationOpeningHours { LocationId = location.Id, Day = d, Opens = hours.Opens, Closes = hours.Closes }));
        return location;
    }

    private static Employee NewEmployee(string name, string color, Qualification[] qualifications, Guid locationId,
        DayOfWeek[] days, TimeOnly start, TimeOnly end, string? preferredName = null)
    {
        var employee = new Employee { DisplayName = name, PreferredName = preferredName, Color = color };
        employee.Qualifications.AddRange(qualifications);
        return WithHours(employee, locationId, days, start, end);
    }

    private static Employee WithHours(Employee employee, Guid locationId, DayOfWeek[] days, TimeOnly start, TimeOnly end)
    {
        employee.WorkingHours.AddRange(days.Select(d => new EmployeeWorkingHours
        {
            EmployeeId = employee.Id,
            LocationId = locationId,
            Day = d,
            Start = start,
            End = end,
        }));
        return employee;
    }

    private static Treatment NewTreatment(string name, int duration, int preparation, int followUp, decimal price,
        Qualification[] qualifications, Room[] rooms, Equipment[] equipment)
    {
        var treatment = new Treatment
        {
            Name = name,
            DurationMinutes = duration,
            PreparationMinutes = preparation,
            FollowUpMinutes = followUp,
            Price = price,
        };
        treatment.RequiredQualifications.AddRange(qualifications);
        treatment.Rooms.AddRange(rooms);
        treatment.Equipment.AddRange(equipment);
        return treatment;
    }
}
