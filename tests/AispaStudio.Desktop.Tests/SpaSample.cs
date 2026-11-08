using AispaStudio.Contracts;

namespace AispaStudio.Desktop.Tests;

/// <summary>Ein voller Tag in einem Wiener Day-Spa (Namen wie im Beispiel-Spa), für Ansichten und Bildschirmfotos.</summary>
internal static class SpaSample
{
    public static readonly DateOnly Day = new(2026, 10, 19);
    public static readonly Guid LocationId = Guid.NewGuid();
    public static readonly LocationDto Vienna = new(LocationId, "Wien Innere Stadt", "Europe/Vienna", "EUR",
        [new OpeningHours(DayOfWeek.Monday, new(9, 0), new(20, 0))]);

    public static readonly LocationDto Salzburg = new(Guid.NewGuid(), "Salzburg", "Europe/Vienna", "EUR", []);

    public static readonly EmployeeDto Lena = new(Guid.NewGuid(), "Lena Huber", "#2f6f5e", [], [], ["en", "it"]);
    public static readonly EmployeeDto Marco = new(Guid.NewGuid(), "Marco Berger", "#7a4fa3", [], []);
    public static readonly EmployeeDto Sophie = new(Guid.NewGuid(), "Sophie Gruber", "#b5524a", [], []);
    public static readonly EmployeeDto Malee = new(Guid.NewGuid(), "Malee Chaiyasit", "#c28a2c", [], [], ["en"]);
    public static readonly EmployeeDto Siriporn = new(Guid.NewGuid(), "Siriporn Kaewdee", "#a34f7a", [], []);
    public static readonly EmployeeDto Elif = new(Guid.NewGuid(), "Elif Demir", "#4f7aa3", [], []);

    public static readonly RoomDto Lavender = new(Guid.NewGuid(), LocationId, "Raum Lavendel", 10);
    public static readonly RoomDto Cedar = new(Guid.NewGuid(), LocationId, "Raum Zeder", 10);
    public static readonly RoomDto Lotus = new(Guid.NewGuid(), LocationId, "Raum Lotus", 10);
    public static readonly RoomDto Thai = new(Guid.NewGuid(), LocationId, "Thai-Raum", 10);
    public static readonly RoomDto Hamam = new(Guid.NewGuid(), LocationId, "Hamam", 20);
    public static readonly RoomDto Cabin = new(Guid.NewGuid(), LocationId, "Kosmetikkabine", 5);

    public static readonly TreatmentDto[] Treatments =
    [
        T("Klassische Massage", 50, 89m, Lavender, Cedar, Lotus),
        T("Aromaöl-Massage", 60, 98m, Lavender, Cedar, Lotus),
        T("Hot-Stone-Massage", 80, 129m, Lotus) with { CheckAnamnesis = true },
        T("Traditionelle Thai-Massage", 60, 79m, Thai),
        T("Hamam-Ritual", 45, 69m, Hamam),
        T("Gesichtsbehandlung Classic", 60, 95m, Cabin),
        T("Fußreflexzonenmassage", 30, 49m, Lavender, Cedar),
        T("Ayurveda Abhyanga", 75, 115m, Lotus, Cedar),
    ];

    /// <summary>Kassa-Artikel für den Saunaeintritt; der Ruheraum hat bewusst keinen.</summary>
    public static readonly Guid SaunaAdmissionId = Guid.NewGuid();

    public static readonly RoomDto Sauna = new(Guid.NewGuid(), LocationId, "Finnische Sauna", 0, Places: 12, IsArea: true, AdmissionArticleId: SaunaAdmissionId);
    public static readonly RoomDto Relax = new(Guid.NewGuid(), LocationId, "Ruheraum", 0, Places: 20, IsArea: true);

    /// <summary>Paar am Nachmittag: zwei Termine einer gemeinsamen Buchung.</summary>
    public static readonly Guid CoupleId = Guid.NewGuid();

    public static readonly LocationResources Resources = new(Vienna, [Lavender, Cedar, Lotus, Thai, Hamam, Cabin, Sauna, Relax], [],
        [Lena, Marco, Sophie, Malee, Siriporn, Elif], Treatments, []);

    /// <summary>Wiener Ortszeit am Beispieltag (Winterzeit noch nicht, also UTC+2 bis 25.10.).</summary>
    public static DateTimeOffset At(int hour, int minute = 0) => new(2026, 10, 19, hour - 2, minute, 0, TimeSpan.Zero);

    public static readonly AppointmentDto Selected = A(10, 30, Lena, Lotus, 2, "Brigitte Müller", AppointmentStatus.Booked, "Lavendelöl, wenig Gespräch, Fokus Nacken");

    public static CalendarDay CalendarDay() => WithGuests(new(LocationId, Day, "Europe/Vienna", [new TimeRange(At(9), At(20))],
        [
            new EmployeeShift(Lena.Id, At(9), At(17)),
            new EmployeeShift(Marco.Id, At(12), At(20)),
            new EmployeeShift(Sophie.Id, At(9), At(17)),
            new EmployeeShift(Malee.Id, At(11), At(19)),
            new EmployeeShift(Siriporn.Id, At(12), At(20)),
            new EmployeeShift(Elif.Id, At(9), At(17)),
        ],
        [
            A(9, 0, Lena, Lavender, 0, "Markus Steiner", AppointmentStatus.Completed),
            Selected,
            A(13, 0, Lena, Cedar, 1, "Sabine Gruber", AppointmentStatus.Booked),
            A(14, 15, Lena, Lavender, 6, "Petra Hofer", AppointmentStatus.Booked),
            A(15, 0, Lena, Lotus, 7, "Ines Fuchs", AppointmentStatus.Booked),
            A(12, 0, Marco, Lotus, 7, "Dr. Wolfgang Bauer", AppointmentStatus.Booked),
            A(14, 0, Marco, Cedar, 1, "Nina Weiss", AppointmentStatus.Booked) with { GroupId = CoupleId },
            A(16, 0, Marco, Lotus, 2, "Stefan Koller", AppointmentStatus.Booked),
            A(18, 0, Marco, Lavender, 0, "Tobias Wimmer", AppointmentStatus.Booked),
            A(9, 15, Sophie, Cabin, 5, "Elif Berger", AppointmentStatus.Completed),
            A(10, 30, Sophie, Cabin, 5, "Laura Novak", AppointmentStatus.CheckedIn, "Erstbesuch, Anamnese liegt vor"),
            A(13, 30, Sophie, Cabin, 5, "Julia Wagner", AppointmentStatus.Booked) with { Source = AppointmentSource.Email },
            A(15, 0, Sophie, Cabin, 5, "Maria Egger", AppointmentStatus.Booked),
            A(11, 0, Malee, Thai, 3, "Jana Svoboda", AppointmentStatus.CheckedIn),
            A(12, 30, Malee, Thai, 3, "Clemens Haas", AppointmentStatus.Booked),
            A(14, 30, Malee, Thai, 3, "Valentina Rossi", AppointmentStatus.Booked) with { Source = AppointmentSource.Online },
            A(17, 0, Malee, Thai, 3, "Hannah Schmid", AppointmentStatus.Booked),
            A(12, 0, Siriporn, Hamam, 4, "Paula König", AppointmentStatus.Booked),
            A(13, 30, Siriporn, Thai, 6, "Florian Aigner", AppointmentStatus.Booked),
            A(16, 0, Siriporn, Cedar, 1, "Katrin Huber", AppointmentStatus.Booked),
            A(9, 0, Elif, Hamam, 4, "Thomas Huber", AppointmentStatus.NoShow),
            A(10, 0, Elif, Hamam, 4, "Eva Pichler", AppointmentStatus.CheckedIn),
            A(11, 30, Elif, Lavender, 6, "Martin Lechner", AppointmentStatus.Booked),
            A(14, 0, Elif, Lotus, 1, "Jakob Weiss", AppointmentStatus.Booked) with { GroupId = CoupleId },
        ],
        [new BlockDto(Guid.NewGuid(), ResourceKind.Employee, Elif.Id, At(12, 30), At(13, 30), "Schulung Peeling")]));

    /// <summary>
    /// Der Beispieltag um <paramref name="days"/> Tage später (Wiener Ortszeit, also auch über die Zeitumstellung am 25.10.), mit neuen Ids und je Tag
    /// anderen Terminen, damit eine Woche nach einer Woche aussieht.
    /// </summary>
    public static CalendarDay CalendarDayAfter(int days)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        DateTimeOffset Later(DateTimeOffset t) =>
            new(TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTime(t, zone).DateTime.AddDays(days), zone), TimeSpan.Zero);

        var day = CalendarDay();
        var groups = new Dictionary<Guid, Guid>();
        return day with
        {
            Date = Day.AddDays(days),
            Opening = [.. day.Opening.Select(o => new TimeRange(Later(o.Start), Later(o.End)))],
            Shifts = [.. day.Shifts.Select(s => s with { Start = Later(s.Start), End = Later(s.End) })],
            Appointments = [.. day.Appointments.Where((_, i) => days == 0 || (i + days) % 3 != 0).Select(a => a with
            {
                Id = Guid.NewGuid(),
                GroupId = a.GroupId is { } g ? groups.TryAdd(g, Guid.NewGuid()) ? groups[g] : groups[g] : null,
                Start = Later(a.Start),
                End = Later(a.End),
                BlockedFrom = Later(a.BlockedFrom),
                BlockedUntil = Later(a.BlockedUntil),
            })],
            Blocks = [.. day.Blocks.Select(b => b with { Id = Guid.NewGuid(), From = Later(b.From), Until = Later(b.Until) })],
        };
    }

    /// <summary>Im Urlaub, ohne Termine: steht nur in der Tagesleiste.</summary>
    public static readonly EmployeeDto Johanna = new(Guid.NewGuid(), "Johanna Lechner", "#5b8c3a", [], []);

    /// <summary>Raumansicht mit einem Paarraum (Zeder) und einem langen Raumnamen, damit der Kopf zeigt, wie er kürzt und trennt.</summary>
    public static readonly LocationResources ResourcesWithCoupleRoom = Resources with
    {
        Rooms = [.. Resources.Rooms.Select(r => r.Id == Cedar.Id ? r with { Places = 2 } : r.Id == Cabin.Id ? r with { Name = "Hand- und Fußpflege, Kosmetikkabine" } : r)],
    };

    public static readonly LocationResources ResourcesWithAbsent = Resources with { Employees = [.. Resources.Employees, Johanna] };

    /// <summary>
    /// Derselbe Tag mit dem, was den Alltag stört: Siriporn ist krank gemeldet, Johanna im Urlaub, Sophie hat eine Pause,
    /// Lena bleibt keine Zeit mehr für die gesetzliche Pause, die Hot-Stone-Steine müssen vorgeheizt werden, und es gibt Notizen.
    /// </summary>
    public static CalendarDay BusyDay()
    {
        var day = CalendarDay();
        return day with
        {
            Shifts = [.. day.Shifts.Where(s => s.EmployeeId != Siriporn.Id)],
            Appointments = [.. day.Appointments.Select(a => a.Id == Selected.Id ? a with { BlockedFrom = a.Start.AddMinutes(-15) } : a)],
            Blocks = [.. day.Blocks, new BlockDto(Guid.NewGuid(), ResourceKind.Employee, Sophie.Id, At(12, 30), At(13), "Pause")],
            Absences =
            [
                new CalendarAbsence(Siriporn.Id, RosterKind.Sick, Day, null),
                new CalendarAbsence(Johanna.Id, RosterKind.Vacation, Day.AddDays(4), null),
            ],
            Breaks =
            [
                new CalendarBreak(Lena.Id, 30, 0, "Keine Zeit mehr für die gesetzliche Pause: 30 Min. bei 8 Std. Dienst."),
                new CalendarBreak(Sophie.Id, 30, 30, null),
            ],
            Notes =
            [
                new CalendarNoteDto(Guid.NewGuid(), Day, "Lieferung Massageöle gegen 11 Uhr, bitte im Lager einräumen", null, null, null, false, At(8)),
                new CalendarNoteDto(Guid.NewGuid(), Day, "Sauna-Wartung erledigt", null, null, null, true, At(8)),
                new CalendarNoteDto(Guid.NewGuid(), Day, "Herr Lang ruft wegen Gutschein zurück", ResourceKind.Employee, Marco.Id, At(13, 15), false, At(9)),
            ],
            Preparations = [new CalendarPreparation(Treatments[2].Id, "Steine auf 55 °C aufheizen")],
        };
    }

    /// <summary>
    /// Der Beispieltag mit Einwirkzeit (Schritt 13): Sophies Gesichtsbehandlung um 13:30 dauert 90 Minuten mit 25 Minuten am Gast, 45 Minuten
    /// Einwirkzeit und 20 Minuten Ausarbeitung; in der Einwirkzeit liegt eine 15-Minuten-Behandlung derselben Person (zwei Bahnen).
    /// </summary>
    public static CalendarDay ProcessingDay()
    {
        var day = CalendarDay();
        var facial = day.Appointments.Single(a => a.EmployeeId == Sophie.Id && a.Start == At(13, 30));
        var quick = A(13, 55, Sophie, Lavender, 6, "Karin Ebner", AppointmentStatus.Booked) with
        {
            End = At(14, 10),
            BlockedFrom = At(13, 55),
            BlockedUntil = At(14, 10),
        };
        return day with
        {
            Appointments =
            [
                .. day.Appointments.Select(a => a.Id == facial.Id ? a with
                {
                    End = At(15),
                    BlockedUntil = At(15, 10),
                    PreparationMinutes = 0,
                    FollowUpMinutes = 10,
                    HandsOnMinutes = 25,
                    ProcessingMinutes = 45,
                }
                : a),
                quick,
            ],
        };
    }

    /// <summary>
    /// Der volle Tag mit Raumpflege als Aufgabe: je Raum ist die erste Pflege erledigt, danach überfällig (Pflegezeit vor 11:40) oder offen;
    /// Tagesstart: Lavendel 1 von 3, Zeder geprüft, Lotus abgenommen, Thai-Raum und Kabine offen, Hamam ohne Checkliste.
    /// </summary>
    public static CalendarDay BusyDayWithCare()
    {
        var day = BusyDay();
        var now = At(11, 40);
        var tasks = new List<RoomCareTaskDto>();
        foreach (var group in day.Appointments.Where(a => a.Status != AppointmentStatus.Cancelled).GroupBy(a => a.RoomId))
        {
            var room = Resources.Rooms.First(r => r.Id == group.Key);
            if (room.CleaningMinutes == 0)
            {
                continue;
            }

            var first = true;
            foreach (var a in group.OrderBy(a => a.Start))
            {
                var until = a.BlockedUntil.AddMinutes(room.CleaningMinutes);
                var state = first ? RoomCareState.Done : until <= now ? RoomCareState.Overdue : RoomCareState.Open;
                tasks.Add(new RoomCareTaskDto(a.Id, [a.Id], room.Id, room.Name, $"{room.Name} pflegen bis {until.ToOffset(TimeSpan.FromHours(2)):HH:mm}",
                    a.BlockedUntil, until, a.EmployeeId, state, first ? until : null, first ? "Rita Steiner" : null));
                first = false;
            }
        }

        RoomDayStateDto State(RoomDto room, RoomStartState start, int items, int done) => new(room.Id, room.Name, start, items, done,
            start == RoomStartState.Approved ? At(9, 5) : null, start == RoomStartState.Approved ? "Marta Leitner" : null,
            tasks.Count(t => t.RoomId == room.Id && t.State == RoomCareState.Open), tasks.Count(t => t.RoomId == room.Id && t.State == RoomCareState.Overdue), null, null);
        return day with
        {
            RoomCare = new RoomCareDay(tasks,
            [
                State(Lavender, RoomStartState.Partial, 3, 1), State(Cedar, RoomStartState.Checked, 2, 2), State(Lotus, RoomStartState.Approved, 2, 2),
                State(Thai, RoomStartState.Open, 2, 0), State(Hamam, RoomStartState.NoChecklist, 0, 0), State(Cabin, RoomStartState.Open, 1, 0),
            ]),
        };
    }

    /// <summary>Tagesstart-Kontrolle: Lavendel teilweise, Zeder geprüft, Thai-Raum offen.</summary>
    public static RoomStartDayDto RoomStart() => new(LocationId, Day,
    [
        new RoomStartRoomDto(Lavender.Id, Lavender.Name, RoomStartState.Partial,
            [new(Guid.NewGuid(), "Raum lüften", true, At(8, 40), "Rita Steiner"), new(Guid.NewGuid(), "Liegen beziehen", false, null, null), new(Guid.NewGuid(), "Duftöl auffüllen", false, null, null)], null, null),
        new RoomStartRoomDto(Cedar.Id, Cedar.Name, RoomStartState.Checked,
            [new(Guid.NewGuid(), "Raum lüften", true, At(8, 42), "Rita Steiner"), new(Guid.NewGuid(), "Handtücher auffüllen", true, At(8, 44), "Rita Steiner")], null, null),
        new RoomStartRoomDto(Thai.Id, Thai.Name, RoomStartState.Open,
            [new(Guid.NewGuid(), "Matten reinigen", false, null, null), new(Guid.NewGuid(), "Temperatur prüfen", false, null, null)], null, null),
    ]);

    public static ServingDayDto Servings() => new(LocationId, Day,
    [
        new ServingItemDto(Selected.Id, Guid.NewGuid(), "Kräutertee", At(10, 30), Selected.TreatmentName, Selected.RoomId, Lotus.Name, Selected.CustomerId, Selected.CustomerName,
            "Rooibos, ohne Zucker", true, false, null, null),
        new ServingItemDto(Selected.Id, Guid.NewGuid(), "Früchteteller", At(11, 10), Selected.TreatmentName, Selected.RoomId, Lotus.Name, Selected.CustomerId, Selected.CustomerName,
            "Rooibos, ohne Zucker", true, false, null, null),
    ],
    [
        new ServingTotalDto("Früchteteller", 6, [At(11, 10), At(11, 40), At(12, 40), At(13, 20), At(15, 10), At(16, 30)]),
        new ServingTotalDto("Kräutertee", 9, [At(9, 30), At(10, 30), At(11, 30)]),
    ]);

    public static List<HandbookEntryDto> Reminders() =>
    [
        new(Guid.NewGuid(), "Gäste mit Hund", "Hunde bleiben im Eingangsbereich, Napf steht unter der Garderobe.", "Empfang", true, [], true, At(8)),
        new(Guid.NewGuid(), "Schlüssel Saunabereich", "Bei Sebastian Gruber an der Rezeption abholen und abends zurückgeben.", "Sauna", true, [DayOfWeek.Monday], true, At(8)),
    ];

    public static StockDemandDto StockDemand() => new(LocationId, Day, Day, 24, [],
        [new StockDemandLine(Guid.NewGuid(), "Lavendelöl", "ml", 120, 260, 100, 9, 0, StockWarningKind.Insufficient, At(14, 30), "Lavendelöl: Bestand reicht nur bis 14:30")]);

    /// <summary>Was der Ausfall-Assistent für Siriporns Termine vorschlägt.</summary>
    /// <summary>Antwort auf „heute ab 17 Uhr eine Aromaöl-Massage, nicht bei Marco“.</summary>
    public static CalendarRequestResult CalendarRequest()
    {
        var treatment = Treatments[1];
        CalendarProposal P(int hour, int minute, EmployeeDto who, RoomDto room, int fromWish, string reason) =>
            new(At(hour, minute), At(hour + 1, minute), treatment.Id, treatment.Name, who.Id, who.DisplayName, room.Id, room.Name, null, 98m, fromWish, reason);
        return new(
            new CalendarWish(Day, new TimeOnly(17, 0), null, treatment.Id, null, [Marco.Id]),
            [
                new(CalendarWishPart.Day, "Mo. 19.10."),
                new(CalendarWishPart.Time, "ab ca. 17:00"),
                new(CalendarWishPart.Treatment, treatment.Name, treatment.Id),
                new(CalendarWishPart.Excluded, "nicht Marco Berger", Marco.Id),
            ],
            [
                P(17, 0, Sophie, Lotus, 0, "zur Wunschzeit · schließt lückenlos an"),
                P(17, 0, Lena, Cedar, 0, "zur Wunschzeit"),
                P(17, 45, Sophie, Lavender, 45, "45 Min. später · schließt lückenlos an"),
            ],
            "3 Vorschläge für Aromaöl-Massage am Mo. 19.10. Gebucht wird erst nach deiner Bestätigung.",
            "Antwort eines KI-Assistenten. Zahlen und Zeiten stammen aus Kalender und Kassa; bitte vor dem Buchen kurz prüfen.");
    }

    public static AbsenceProposal AbsenceProposal()
    {
        var own = CalendarDay().Appointments.Where(a => a.EmployeeId == Siriporn.Id).OrderBy(a => a.Start).ToList();
        return new(Siriporn.Id, Day,
        [
            new AbsenceProposalItem(own[0], AbsenceOption.SameTime, own[0].Start, Elif.Id, Elif.DisplayName, Hamam.Id, Hamam.Name,
                "Elif Demir hat die Qualifikation und ist um 12:00 frei; Raum Hamam bleibt."),
            new AbsenceProposalItem(own[1], AbsenceOption.OtherTime, At(15, 30), Malee.Id, Malee.DisplayName, Thai.Id, Thai.Name,
                "Um 13:30 ist niemand frei. Nächste freie Zeit: 15:30 bei Malee Chaiyasit in Thai-Raum."),
            new AbsenceProposalItem(own[2], AbsenceOption.None, null, null, null, null, null,
                "An diesem Tag ist niemand mit der Qualifikation frei. Bitte den Gast kontaktieren und einen anderen Tag anbieten."),
        ]);
    }

    /// <summary>Wie <see cref="AbsenceProposal()"/>, nur dass für den dritten Termin ein anderer Standort Platz hat.</summary>
    public static AbsenceProposal AbsenceProposalWithOtherLocation()
    {
        var proposal = AbsenceProposal();
        var last = proposal.Items[^1];
        var elsewhere = last with
        {
            Option = AbsenceOption.OtherLocation,
            Start = last.Appointment.Start,
            EmployeeId = Malee.Id,
            EmployeeName = Malee.DisplayName,
            RoomId = Thai.Id,
            RoomName = Thai.Name,
            Explanation = "In Wien ist an diesem Tag niemand frei. In Baden geht es zur gleichen Zeit bei Malee Chaiyasit in Thai-Raum; nur nach Rücksprache mit dem Gast übernehmen.",
            LocationId = Guid.Parse("0f0f0f0f-0000-4000-8000-00000000ba0e"),
            LocationName = "Baden",
        };
        return proposal with { Items = [.. proposal.Items.Take(proposal.Items.Count - 1), elsewhere] };
    }

    /// <summary>Besuche und Hinweise der Gäste, wie sie der Server zum Kalendertag liefert: Stammgäste, Erstbesuche, Hinweise.</summary>
    private static CalendarDay WithGuests(CalendarDay day) => day with
    {
        Guests = [.. day.Appointments.DistinctBy(a => a.CustomerId).Select(a => a.CustomerName switch
        {
            "Brigitte Müller" => new CalendarGuest(a.CustomerId, 9, 6, 0, Day.AddDays(-35), "Stammkundin, mag Lavendelöl", Salutation: CustomerSalutation.Female),
            "Stefan Koller" or "Hannah Schmid" or "Ines Fuchs" => new CalendarGuest(a.CustomerId, 5, 4, 0, Day.AddDays(-28), null),
            "Sabine Gruber" or "Clemens Haas" => new CalendarGuest(a.CustomerId, 0, 0, 0, null, null),
            "Valentina Rossi" => new CalendarGuest(a.CustomerId, 0, 0, 0, null, null, Language: "it"),
            "Martin Lechner" => new CalendarGuest(a.CustomerId, 2, 2, 2, Day.AddDays(-90), null),
            _ => new CalendarGuest(a.CustomerId, 2, 1, 0, Day.AddDays(-60), null),
        })],
    };

    public static List<AreaDay> Areas()
    {
        AreaVisitDto V(RoomDto room, string name, int persons, int from, int until, bool cancelled = false, string? note = null) =>
            new(Guid.NewGuid(), LocationId, room.Id, Guid.NewGuid(), name, persons, At(from), At(until), cancelled, note);

        return
        [
            new(Sauna.Id, Sauna.Name, 12,
                [
                    V(Sauna, "Eva Pichler", 1, 9, 12), V(Sauna, "Gerhard Lang", 2, 10, 13, note: "mit Begleitung"),
                    V(Sauna, "Andrea Stadler", 4, 11, 14), V(Sauna, "Michael Brunner", 3, 11, 13), V(Sauna, "Sandra Leitner", 2, 12, 15),
                    V(Sauna, "Otto Kainz", 1, 10, 12, cancelled: true),
                ],
                [
                    new(At(9), 1), new(At(10), 3), new(At(11), 10), new(At(12), 11), new(At(13), 6), new(At(14), 2), new(At(15), 0),
                ]),
            new(Relax.Id, Relax.Name, 20,
                [V(Relax, "Brigitte Müller", 1, 12, 15), V(Relax, "Laura Novak", 2, 13, 16)],
                [new(At(12), 1), new(At(13), 3), new(At(15), 2), new(At(16), 0)]),
        ];
    }

    /// <summary>Freie Startzeiten für ein Paar mit Massage und danach Gesichtsbehandlung.</summary>
    /// <summary>Freie Zeiten für eine Aromaöl-Massage am Beispieltag, wie sie der Planer liefert.</summary>
    public static List<AvailableSlot> FreeSlots() =>
    [
        new(At(11, 50), At(12, 50), Lena.Id, Lavender.Id, null, SlotFit.Snug),
        new(At(12), At(13), Lena.Id, Lavender.Id, null, SlotFit.Snug),
        new(At(13, 30), At(14, 30), Malee.Id, Thai.Id, null, SlotFit.Snug),
        new(At(14), At(15), Siriporn.Id, Hamam.Id, null, SlotFit.Snug),
        new(At(15, 30), At(16, 30), Malee.Id, Thai.Id, null, SlotFit.LeavesGap, "lässt 30 Min. Lücke danach"),
        new(At(16), At(17), Sophie.Id, Lotus.Id, null, SlotFit.Snug),
        new(At(17), At(18), Siriporn.Id, Hamam.Id, null, SlotFit.Snug),
        new(At(18), At(19), Malee.Id, Thai.Id, null, SlotFit.Snug),
    ];

    public static List<GroupSlot> CoupleSlots() =>
    [
        .. new[] { At(11), At(15), At(15, 30), At(16) }.Select(start => new GroupSlot(start, start.AddMinutes(130), false,
        [
            new GroupSlotItem(start, start.AddMinutes(60), Siriporn.Id, Lavender.Id, null),
            new GroupSlotItem(start, start.AddMinutes(60), Marco.Id, Cedar.Id, null),
            new GroupSlotItem(start.AddMinutes(70), start.AddMinutes(130), Sophie.Id, Cabin.Id, null),
        ],
        [new GroupSlotArea(start.AddMinutes(70), start.AddMinutes(130), Relax.Id, Guid.Empty)])),
    ];

    public static List<WaitlistStaffEntry> Waitlist()
    {
        WaitlistStaffEntry W(string name, int treatment, int from, int days, DayPart part, int offers) =>
            new(Guid.NewGuid(), Guid.NewGuid(), name, Treatments[treatment].Id, Treatments[treatment].Name, Day.AddDays(from), Day.AddDays(from + days),
                part, WaitlistStatus.Waiting, At(8), offers, offers > 0 ? At(9) : null);
        return
        [
            W("Katharina Wolf", 2, 0, 3, DayPart.Afternoon, 1),
            W("Georg Pichler", 0, 1, 5, DayPart.Evening, 0),
            W("Mira Novak", 4, 2, 2, DayPart.Any, 0),
            W("Helene Strauss", 5, 4, 7, DayPart.Morning, 2),
        ];
    }

    public static readonly Guid InboxMailId = Guid.NewGuid();

    public static List<InboundMailSummary> Inbox() =>
    [
        new(InboxMailId, BookingChannel.Email, "c.haider@example.at", "Claudia Haider", "Hot-Stone am Donnerstag?", At(11, 32),
            InboundMailStatus.Proposed, InboundMailKind.BookingRequest, ReplyStatus.Draft, "Hot-Stone-Massage Do. 22.10. um 17 Uhr, Wunsch: Lena", null),
        new(Guid.NewGuid(), BookingChannel.WhatsApp, "+43 664 5550123", "Thomas Gruber", "WhatsApp-Nachricht", At(11, 5),
            InboundMailStatus.Booked, InboundMailKind.BookingRequest, ReplyStatus.Sent, "Thai-Massage morgen 14 Uhr, eingetragen", Guid.NewGuid()),
        new(Guid.NewGuid(), BookingChannel.Phone, "+43 1 5124477", null, "Anruf (Mailbox)", At(10, 48),
            InboundMailStatus.NeedsReview, InboundMailKind.Question, ReplyStatus.None, "Fragt nach Gutschein für Paar-Spa-Tag", null),
        new(Guid.NewGuid(), BookingChannel.Treatwell, "noreply@treatwell.at", "Treatwell", "Neue Buchung: Aromaöl-Massage", At(9, 20),
            InboundMailStatus.Booked, InboundMailKind.TreatwellBooking, ReplyStatus.None, "Buchung über Treatwell übernommen", Guid.NewGuid()),
        new(Guid.NewGuid(), BookingChannel.Email, "m.berger@example.de", "Martina Berger", "Termin absagen", At(8, 55),
            InboundMailStatus.Cancelled, InboundMailKind.Cancellation, ReplyStatus.Sent, "Storniert: Gesichtsbehandlung Mi. 21.10.", null),
    ];

    public static InboundMailDto InboxMail() => new(InboxMailId, BookingChannel.Email, "c.haider@example.at", "Claudia Haider",
        "Hot-Stone am Donnerstag?",
        "Liebes Spa-Team,\n\nich hätte gerne am Donnerstag um 17 Uhr eine Hot-Stone-Massage, am liebsten bei Lena.\n\nViele Grüße\nClaudia Haider",
        At(11, 32), InboundMailStatus.Proposed, InboundMailKind.BookingRequest, "de",
        "Hot-Stone-Massage Do. 22.10. um 17 Uhr, Wunsch: Lena Huber", "17:00 ist bei Lena Huber belegt; drei freie Zeiten vorgeschlagen.",
        LocationId, Treatments[2].Id, null, null,
        [
            new AvailableSlot(At(15).AddDays(3), At(16, 20).AddDays(3), Lena.Id, Lotus.Id, null),
            new AvailableSlot(At(16).AddDays(3), At(17, 20).AddDays(3), Lena.Id, Lotus.Id, null),
            new AvailableSlot(At(17, 30).AddDays(3), At(18, 50).AddDays(3), Marco.Id, Lotus.Id, null),
        ],
        ReplyStatus.Draft, "c.haider@example.at", "Re: Hot-Stone am Donnerstag?",
        "Liebe Frau Haider,\n\nvielen Dank für Ihre Anfrage. Um 17:00 ist Lena leider schon ausgebucht. Wir könnten Ihnen am Donnerstag 15:00 oder 16:00 bei Lena anbieten, oder 17:30 bei Marco.\n\nWelche Zeit passt Ihnen?\n\nHerzliche Grüße\nIhr Spa-Team Wien Innere Stadt",
        "Diese Antwort wurde mit Unterstützung von KI erstellt und vom Spa-Team geprüft.", At(11, 33), null);

    public static readonly Guid HaiderId = Guid.NewGuid();

    /// <summary>Absender, der keinem Kunden sicher zuzuordnen ist: Vorschläge statt Buchung, nichts aus der Kundenkarte im Entwurf (ADR 0061).</summary>
    public static InboundMailDto InboxMailWhoIsThis() => InboxMail() with
    {
        Status = InboundMailStatus.NeedsReview,
        Note = "Wer ist das? Der Absender ist keinem Kunden sicher zuzuordnen. Bitte einen Vorschlag zuordnen oder bewusst neu anlegen; gebucht wird erst danach.",
        CustomerCandidates =
        [
            new(HaiderId, "Claudia Haider", 3, "gleicher Name, Telefon endet auf 4567, letzter Besuch 14.09.2026"),
            new(Guid.NewGuid(), "Claudia Haidinger", 4, "ähnlicher Name, letzter Besuch 03.05.2026"),
        ],
    };

    /// <summary>
    /// Treatwell-Buchung: steht schon im Kalender (vorab bezahlt), der Gast ist vorläufig angelegt, und in Treatwell ist sie noch anzunehmen (ADR 0062).
    /// Erfundenes Beispiel, nicht von Treatwell.
    /// </summary>
    public static InboundMailDto InboxMailTreatwell() => InboxMail() with
    {
        Channel = BookingChannel.Treatwell,
        From = "bookings@partner.treatwell.com",
        FromName = null,
        Subject = "Neue Buchung über Treatwell",
        Body = "Hallo, Sie haben eine neue Buchung erhalten.\nKunde: Claudia Haider\nDatum & Uhrzeit: 22. Oktober 2026 um 17:00 Uhr\nZahlungsstatus: Online bezahlt (Treatwell Pay)\nGesamtbetrag: 35,00 €\nBuchungs-ID: #TW-9823711",
        Status = InboundMailStatus.Booked,
        Kind = InboundMailKind.TreatwellBooking,
        Summary = "Treatwell TW-9823711: Hot-Stone-Massage am 22.10.2026 17:00, Claudia Haider · Vorab über Treatwell bezahlt: 35,00 € · gelesen mit KI",
        Note = "Wer ist das? Der Gast ist vorläufig angelegt, der Termin steht schon im Kalender. Bitte einen Vorschlag zuordnen oder den Gast bewusst als neu bestätigen.",
        Proposals = [],
        ReplyStatus = ReplyStatus.None,
        ReplyTo = null,
        ReplySubject = null,
        ReplyBody = null,
        AiDisclosure = null,
        AppointmentId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        CustomerConfirmed = false,
        CustomerCandidates =
        [
            new(HaiderId, "Claudia Haider", 3, "gleicher Name, Telefon endet auf 4567, letzter Besuch 14.09.2026"),
            new(Guid.NewGuid(), "Claudia Haidinger", 4, "ähnlicher Name, letzter Besuch 03.05.2026"),
        ],
        PlatformReference = "TW-9823711",
        PlatformPayment = PlatformPayment.Prepaid,
        PrepaidAmount = 35m,
        AcceptInPlatformPending = true,
        CustomerAssignmentOpen = true,
    };

    /// <summary>Termin aus einer Treatwell-Buchung: vorab bezahlt, in Treatwell noch anzunehmen, Gast vorläufig.</summary>
    public static AppointmentDto TreatwellAppointment() => Selected with
    {
        Source = AppointmentSource.Treatwell,
        CustomerName = "Claudia Haider",
        Note = null,
        PlatformPayment = PlatformPayment.Prepaid,
        PrepaidAmount = 35m,
        AcceptInPlatformPending = true,
        CustomerAssignmentOpen = true,
    };

    /// <summary>Italienische Anfrage, übersetzt, mit überarbeitbarem Entwurf auf Italienisch und Rückübersetzung.</summary>
    public static InboundMailDto InboxMailItalian() => InboxMail() with
    {
        From = "giulia.romano@example.it",
        FromName = "Giulia Romano",
        Subject = "Massaggio giovedì?",
        Body = "Buongiorno,\n\nvorrei prenotare un massaggio con pietre calde giovedì alle 17, possibilmente con Lena.\n\nCordiali saluti\nGiulia Romano",
        Language = "it",
        Summary = "Hot-Stone-Massage Do. 22.10. um 17 Uhr, Wunsch: Lena Huber",
        ReplyTo = "giulia.romano@example.it",
        ReplySubject = "Re: Massaggio giovedì?",
        ReplyBody = "Gentile Signora Romano,\n\ngrazie per la sua richiesta. Alle 17:00 Lena è purtroppo già occupata. Possiamo offrirle giovedì alle 15:00 o alle 16:00 con Lena, oppure alle 17:30 con Marco.\n\nQuale orario preferisce?\n\nCordiali saluti\nIl team della Spa Wien Innere Stadt",
        AiDisclosure = "Questo messaggio è stato redatto da un assistente IA. Per domande basta rispondere, il nostro team legge i messaggi.",
        Translation = MessageTranslationState.Done,
        TranslatedSubject = "Massage am Donnerstag?",
        TranslatedBody = "Guten Tag,\n\nich möchte am Donnerstag um 17 Uhr eine Massage mit heißen Steinen buchen, möglichst bei Lena.\n\nMit freundlichen Grüßen\nGiulia Romano",
        ReplyTranslation = "Sehr geehrte Frau Romano,\n\ndanke für Ihre Anfrage. Um 17:00 ist Lena leider schon belegt. Wir können Ihnen Donnerstag um 15:00 oder 16:00 bei Lena anbieten oder um 17:30 bei Marco.\n\nWelche Zeit ist Ihnen lieber?\n\nMit freundlichen Grüßen\nIhr Spa-Team Wien Innere Stadt",
        CanRevise = true,
        CanUndoReply = true,
    };

    /// <summary>Antwort des Gasts auf den gesendeten Vorschlag: verknüpft, Donnerstag ausgelassen, zweiter Vorschlag am Freitag.</summary>
    public static InboundMailDto InboxMailFollowUp() => InboxMail() with
    {
        Subject = "Re: Hot-Stone am Donnerstag?",
        Body = "Hallo,\n\nDonnerstag geht sich bei mir leider doch nicht aus. Hätten Sie am Freitag am Nachmittag etwas?\n\nViele Grüße\nClaudia Haider",
        ReceivedAt = At(11, 52),
        Summary = "Hot-Stone-Massage Fr. 23.10. am Nachmittag, Wunsch: Lena Huber",
        Note = null,
        Proposals =
        [
            new AvailableSlot(At(13).AddDays(4), At(14, 20).AddDays(4), Lena.Id, Lotus.Id, null),
            new AvailableSlot(At(14, 30).AddDays(4), At(15, 50).AddDays(4), Lena.Id, Lotus.Id, null),
            new AvailableSlot(At(16).AddDays(4), At(17, 20).AddDays(4), Marco.Id, Lotus.Id, null),
        ],
        ReplySubject = "Re: Hot-Stone am Donnerstag?",
        ReplyBody = "Liebe Frau Haider,\n\ndanke für Ihre Rückmeldung. Am Freitag können wir Ihnen 13:00 oder 14:30 bei Lena anbieten, oder 16:00 bei Marco.\n\nWelche Zeit passt Ihnen?\n\nHerzliche Grüße\nIhr Spa-Team Wien Innere Stadt",
        PreviousMailId = Guid.NewGuid(),
        RejectedStarts = [At(15).AddDays(3), At(16).AddDays(3), At(17, 30).AddDays(3)],
        ProposalRound = 2,
        CanRepropose = true,
    };

    public static readonly CustomerDto[] Customers =
    [
        new(Guid.NewGuid(), "Brigitte", "Müller", "b.mueller@example.at", "+43 664 123 45 67", new(1968, 10, 21), "Stammkundin, mag Lavendelöl",
            Salutation: CustomerSalutation.Female, TitlesBefore: "Mag.", Address: new CustomerAddress("Neubaugasse 12/4", "1070", "Wien", "AT")),
        new(Guid.NewGuid(), "Bernhard", "Müllner", null, "+43 676 555 01 02", null, null),
    ];

    /// <summary>Kennzahlen von Brigitte Müller, wie sie die Rezeption sieht (ohne Umsatz).</summary>
    public static CustomerInsights Insights(bool finance = false) => new(
        14, 7, new DateOnly(2026, 9, 21), 33, 2, "7 Besuche in zwölf Monaten",
        [new DateOnly(2026, 7, 10)], [new DateOnly(2026, 3, 2)],
        [new(Lena.Id, Lena.DisplayName, 9, finance ? 1062m : null, finance ? "EUR" : null), new(Marco.Id, Marco.DisplayName, 5, finance ? 540m : null, finance ? "EUR" : null)],
        finance ? [new CustomerRevenue("EUR", 1602m, 845m)] : null,
        [new GuestPackage(Guid.NewGuid(), "Hot-Stone-Massage", 10, 7, new DateOnly(2027, 6, 30))],
        true, "Zahlt meist mit der Firmenkarte. Mann holt um 18 Uhr ab.");

    /// <summary>
    /// Kundenüberblick von Brigitte Müller für den Abschnitt „Kunde“ im Termin-Panel: 14 Besuche, einmal nicht erschienen, nächster Termin in 14 Tagen;
    /// mit <paramref name="finance"/> zusätzlich der offene Betrag und das Paket.
    /// </summary>
    public static CustomerSummary Summary(bool finance = false) => new(
        14, new DateOnly(2026, 9, 21), 1,
        new CustomerNextAppointment(Guid.NewGuid(), At(10, 30).AddDays(14), "Hot-Stone-Massage"),
        [
            new(Guid.NewGuid(), At(15).AddDays(-28), "Hot-Stone-Massage", Lena.DisplayName),
            new(Guid.NewGuid(), At(17).AddDays(-63), "Aromaöl-Massage", Marco.DisplayName),
            new(Guid.NewGuid(), At(9).AddDays(-101), "Gesichtsbehandlung Classic", Lena.DisplayName),
            new(Guid.NewGuid(), At(11).AddDays(-140), "Klassische Massage", Lena.DisplayName),
        ],
        "Stammkundin, mag Lavendelöl. Kommt gern mit ihrer Schwester, bitte dann Nachbarkabinen geben und vorher anrufen, ob der Termin passt.",
        "+43 664 123 45 67", "b.mueller@example.at", true,
        finance ? new CustomerOpenItems(45m, "EUR", [new GuestPackage(Guid.NewGuid(), "Hot-Stone-Massage", 10, 7, new DateOnly(2027, 6, 30))]) : null);

    public static List<CustomerFlagDto> CustomerFlags() =>
    [
        new(Guid.NewGuid(), CustomerFlagKind.MoreTime, null, null, null, new DateOnly(2026, 9, 2), true),
        new(Guid.NewGuid(), CustomerFlagKind.NotWithMe, Marco.Id, Marco.DisplayName, null, new DateOnly(2026, 6, 15), false),
    ];

    public static CustomerPreferences Wishes() => new("kräftig", "Lavendel", "eher warm", null, true, "Nacken und Schultern", "Füße", null);

    /// <summary>Termine von Brigitte Müller für die Kundenkarte.</summary>
    public static List<AppointmentDto> CustomerHistory() =>
    [
        Selected with { Id = Guid.NewGuid(), Start = At(10, 30).AddDays(14), End = At(11, 50).AddDays(14) },
        Selected,
        Selected with { Id = Guid.NewGuid(), Start = At(15).AddDays(-28), End = At(16, 20).AddDays(-28), Status = AppointmentStatus.Completed },
        Selected with
        {
            Id = Guid.NewGuid(), TreatmentName = "Aromaöl-Massage", Start = At(17).AddDays(-63), End = At(18).AddDays(-63),
            Status = AppointmentStatus.Completed, Price = 98m,
        },
        Selected with
        {
            Id = Guid.NewGuid(), TreatmentName = "Gesichtsbehandlung Classic", Start = At(9).AddDays(-101), End = At(10).AddDays(-101),
            Status = AppointmentStatus.NoShow, Price = 95m,
        },
        Selected with
        {
            Id = Guid.NewGuid(), TreatmentName = "Hamam-Ritual", Start = At(14).AddDays(-150), End = At(14, 45).AddDays(-150),
            Status = AppointmentStatus.Completed, Price = 69m,
        },
    ];

    public static readonly Guid Revenue = Guid.NewGuid();

    public static readonly CashRegisterDto Register = new(Guid.NewGuid(), LocationId, "WIEN-1", FiscalCountry.AT,
        "Day-Spa Wien GmbH\nKärntner Straße 12, 1010 Wien\nUID ATU12345678", true, 1841, null, "1a2b3c", [],
        [PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.Voucher, PaymentMethod.InstantTransfer]);

    public static readonly List<ArticleDto> Articles =
    [
        new(Guid.NewGuid(), "Massageöl Lavendel 100 ml", 24.90m, TaxCategory.Normal, Revenue),
        new(Guid.NewGuid(), "Saunatuch-Verleih", 5m, TaxCategory.Normal, Revenue),
        new(Guid.NewGuid(), "Ingwertee", 3.50m, TaxCategory.Reduced1, Revenue),
        new(Guid.NewGuid(), "Gesichtscreme Hydro 50 ml", 39m, TaxCategory.Normal, Revenue),
        new(Guid.NewGuid(), "Bademantel-Verleih", 6m, TaxCategory.Normal, Revenue),
        new(SaunaAdmissionId, "Tageseintritt Sauna", 29m, TaxCategory.Reduced2, Revenue),
    ];

    /// <summary>Heute noch nicht abgerechnet: erledigte und eingecheckte Termine ohne die gerade kassierte und die geparkte Rechnung.</summary>
    public static List<AppointmentDto> Unbilled() =>
        [.. CalendarDay().Appointments
            .Where(a => a.Status is AppointmentStatus.Completed or AppointmentStatus.CheckedIn && a.CustomerName is not ("Markus Steiner" or "Jana Svoboda"))
            .OrderBy(a => a.Start)];

    /// <summary>Heute noch nicht abgerechnete Eintritte: zwei in der Sauna, einer im Ruheraum (ohne Artikel).</summary>
    public static List<AreaVisitDto> UnbilledVisits() =>
    [
        new(SaunaVisitId, LocationId, Sauna.Id, Guid.NewGuid(), "Gerhard Lang", 2, At(10), At(13), false, "mit Begleitung"),
        new(Guid.NewGuid(), LocationId, Relax.Id, Guid.NewGuid(), "Brigitte Müller", 1, At(12), At(15), false, null),
    ];

    public static readonly Guid SaunaVisitId = Guid.NewGuid();

    /// <summary>
    /// Liste „Heute abzurechnen“, wie der Server sie einteilt (Slice 137): abgeschlossene Termine unter „Wartet auf Abrechnung“ (fertig seit ihrem Ende),
    /// die übrigen Termine und die Eintritte unter „Später heute“, nach Beginn; Person und Raum aus den Stammdaten, Eintrittspreis aus dem Artikel des Bereichs.
    /// </summary>
    public static PosDayListDto DayList(IReadOnlyList<AreaVisitDto>? visits = null, IReadOnlyList<PosDayEntryDto>? done = null)
    {
        var entries = Unbilled().Select(DayEntry).Concat((visits ?? []).Select(DayEntry)).OrderBy(e => e.Start).ToList();
        var waiting = entries.Where(e => e.Appointment?.Status == AppointmentStatus.Completed).ToList();
        return new([.. waiting.Select(e => e with { FinishedAt = e.Appointment!.End })], [.. entries.Except(waiting)], done ?? []);
    }

    /// <summary>Termin als Zeile der Tagesliste mit Person und Raum aus den Stammdaten.</summary>
    public static PosDayEntryDto DayEntry(AppointmentDto a) => new(a.Start, a.CustomerName, a.TreatmentName,
        Resources.Employees.FirstOrDefault(e => e.Id == a.EmployeeId)?.DisplayName, Resources.Rooms.FirstOrDefault(r => r.Id == a.RoomId)?.Name, a.Price, null, a, null);

    /// <summary>Eintritt als Zeile der Tagesliste; Preis wie beim Server: Artikel des Bereichs mal Personen, ohne Artikel keiner.</summary>
    public static PosDayEntryDto DayEntry(AreaVisitDto v)
    {
        var room = Resources.Rooms.First(r => r.Id == v.RoomId);
        var area = $"Eintritt {room.Name}";
        decimal? price = room.AdmissionArticleId is { } id ? Articles.First(a => a.Id == id).Price * v.Persons : null;
        return new(v.From, v.CustomerName, v.Persons == 1 ? area : $"{v.Persons} × {area}", null, room.Name, price, null, null, v);
    }

    public static readonly AppointmentDto Paying = CalendarDay().Appointments.First(a => a.CustomerName == "Markus Steiner");

    public static readonly Guid TreatmentLineId = Guid.NewGuid();

    public static readonly Guid ArticleLineId = Guid.NewGuid();

    /// <summary>Offene Rechnung; <c>discount</c> ist ein Rabatt auf den Termin, <c>voucher</c> ein verkaufter Wertgutschein.</summary>
    public static BillDto Bill(bool withArticle = true, decimal discount = 0, decimal? voucher = null)
    {
        List<BillLineDto> lines = [new(TreatmentLineId, BillLineKind.Appointment, $"{Paying.TreatmentName} (Lena Huber)", 1, Paying.Price, discount,
            Paying.Price - discount, 0, TaxCategory.Normal, 20, Revenue, Paying.Id, TreatmentId: Paying.TreatmentId)];
        if (withArticle)
        {
            lines.Add(new(ArticleLineId, BillLineKind.Article, Articles[0].Name, 2, 24.90m, 0, 49.80m, 0, TaxCategory.Normal, 20, Revenue, null));
        }

        if (voucher is { } value)
        {
            lines.Add(new(Guid.NewGuid(), BillLineKind.Voucher, "Wertgutschein", 1, value, 0, value, 0, TaxCategory.Zero, 0, Revenue, null));
        }

        var total = lines.Sum(l => l.Amount);
        return new(BillId, Register.Id, Paying.CustomerId, Paying.CustomerName, BillStatus.Open, lines, total, 0, total, []);
    }

    public static readonly Guid BillId = Guid.NewGuid();

    /// <summary>10er-Block des zahlenden Gastes für seine Behandlung, 7 davon offen.</summary>
    public static List<VoucherDto> Packages() =>
        [new(Guid.NewGuid(), "PK7Q-M2XA-9RDT", VoucherKind.SinglePurpose, 790m, 553m, Paying.TreatmentId, VoucherStatus.Active, new DateOnly(2027, 6, 30),
            10, 7, Paying.CustomerId)];

    /// <summary>Offene Rechnung mit einem Saunaeintritt für zwei Personen (13 % USt in Österreich).</summary>
    public static BillDto VisitBill()
    {
        List<BillLineDto> lines = [new(Guid.NewGuid(), BillLineKind.AreaVisit, "Tageseintritt Sauna 19.10.2026 10:00, Finnische Sauna", 2, 29m, 0, 58m, 0,
            TaxCategory.Reduced2, 13, Revenue, null, SaunaVisitId)];
        return new(BillId, Register.Id, Guid.NewGuid(), "Gerhard Lang", BillStatus.Open, lines, 58m, 0, 58m, []);
    }

    public static readonly BillDto Parked = new(Guid.NewGuid(), Register.Id, null, "Jana Svoboda", BillStatus.Open, [], 79m, 0, 79m, []);

    /// <summary>Beleg über der Kleinbetragsgrenze mit Unternehmensangaben des Standorts, wie ihn der Server für Rechnungen liefert.</summary>
    public static ReceiptDto LargeReceipt() => Receipt() with
    {
        Total = 480m,
        SmallInvoiceLimit = 400m,
        Company = new LocationCompany("Day-Spa Wien GmbH", "ATU12345678", "FN 123456a", "Handelsgericht Wien"),
    };

    public static readonly InvoiceRecipient Recipient = new("Beispiel Consulting GmbH", "Ringstraße 1\n1010 Wien", "ATU87654321");

    public static ReceiptDto Receipt() => new(Guid.NewGuid(), Register.Id, Register.Code, 1842, ReceiptKind.Sale, At(11, 40), Register.ReceiptHeader,
        BillId, Paying.CustomerId,
        [
            new($"{Paying.TreatmentName} (Lena Huber)", 1, Paying.Price, TaxCategory.Normal, 20, Revenue, "4000", Paying.Id),
            new(Articles[0].Name, 2, 49.80m, TaxCategory.Normal, 20, Revenue, "4100", null),
        ],
        [new(TaxCategory.Normal, 20, 138.80m, 115.67m, 23.13m)],
        [new(PaymentMethod.Voucher, 50m, "GS-7K3M-Q9", null), new(PaymentMethod.Cash, 100m, null, null)],
        138.80m, 11.20m, null, null, null, "_R1-AT1_WIEN-1_1842_2026-10-19T11:40:00_...", false, []);

    private static AppointmentDto A(int hour, int minute, EmployeeDto who, RoomDto room, int treatment, string customer, AppointmentStatus status,
        string? note = null)
    {
        var t = Treatments[treatment];
        var start = At(hour, minute);
        return new(Guid.NewGuid(), LocationId, t.Id, t.Name, Guid.NewGuid(), customer, who.Id, room.Id, null, start, start.AddMinutes(t.DurationMinutes),
            start, start.AddMinutes(t.DurationMinutes + 10), status, t.Price, note, At(8));
    }

    private static TreatmentDto T(string name, int minutes, decimal price, params RoomDto[] rooms) =>
        new(Guid.NewGuid(), name, minutes, 0, 10, price, [], [.. rooms.Select(r => r.Id)], [], null);
}
