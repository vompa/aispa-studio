using System.Globalization;
using System.Text;
using AispaStudio.Api.Data;
using AispaStudio.Api.Roster;
using AispaStudio.Api.TimeTracking;
using AispaStudio.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Payroll;

/// <summary>
/// Monatswerte für die Lohnverrechnung: gearbeitete Stunden, davon Sonntags-, Feiertags- und Nachtstunden,
/// dazu Urlaubs-, Kranken- und Schulungstage. Gearbeitete Stunden kommen aus der Zeiterfassung (Ist), wo an einem Tag gestempelt wurde,
/// sonst aus dem geltenden Plan (Dienstplan, sonst Wochenarbeitszeit). Abwesenheiten kommen immer aus dem Dienstplan.
/// Die Zuschlagssätze kennt die Lohnverrechnung über die Lohnart, hier gehen nur Mengen hinaus.
/// </summary>
internal sealed class PayrollExport(AispaDbContext db, RosterService roster, TimeTrackingService times, TimeProvider time)
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    public async Task<PayrollPreviewDto> PreviewAsync(int year, int month, CancellationToken ct)
    {
        var settings = await db.PayrollSettings.AsNoTracking().SettingsAsync(ct);
        var from = new DateOnly(year, month, 1);
        var until = from.AddMonths(1).AddDays(-1);
        // Feiertage gelten dort, wo der Dienst stattfindet: je Standort nach Bundesland oder Kanton.
        var regions = await PublicHolidays.LocationsAsync(db, ct);
        var holidays = regions.ToDictionary(r => r.Key, r => r.Value.For(year));
        var usedLocations = new HashSet<Guid>();

        // Auch wer inzwischen ausgeschieden ist, aber im Monat eingeteilt war.
        var employees = await db.Employees.AsNoTracking().Include(e => e.WorkingHours)
            .Where(e => e.IsActive || db.RosterDays.Any(d => d.EmployeeId == e.Id && d.Date >= from && d.Date <= until))
            .OrderBy(e => e.DisplayName)
            .ToListAsync(ct);
        var numbers = await db.EmployeePayrolls.AsNoTracking().ToDictionaryAsync(p => p.EmployeeId, p => p.PersonnelNumber, ct);
        var plans = await roster.PlansAsync(employees, from, until, ct);
        var actual = await times.LocalIntervalsAsync([.. employees.Select(e => e.Id)], from, until, ct);
        var plannedOnly = 0;

        var lines = new List<PayrollLine>();
        var warnings = new List<string>();
        foreach (var employee in employees)
        {
            var totals = new Dictionary<PayrollItem, decimal>();
            void Add(PayrollItem item, decimal quantity) => totals[item] = totals.GetValueOrDefault(item) + quantity;

            for (var date = from; date <= until; date = date.AddDays(1))
            {
                var plan = plans[(employee.Id, date)];
                var worked = actual.GetValueOrDefault((employee.Id, date))
                    ?? [.. plan.Shifts.Select(s => (s.LocationId!.Value, s.Start!.Value, s.End!.Value))];
                if (!actual.ContainsKey((employee.Id, date)) && plan.Shifts.Any())
                {
                    plannedOnly++;
                }

                foreach (var shift in worked)
                {
                    var hours = Hours(shift.Start, shift.End);
                    Add(PayrollItem.Hours, hours);
                    usedLocations.Add(shift.LocationId);
                    if (holidays[shift.LocationId].Contains(date))
                    {
                        Add(PayrollItem.HolidayHours, hours);
                    }
                    else if (date.DayOfWeek == DayOfWeek.Sunday)
                    {
                        Add(PayrollItem.SundayHours, hours);
                    }

                    Add(PayrollItem.NightHours, NightHours(shift.Start, shift.End, settings.NightStart, settings.NightEnd));
                }

                // Abwesenheit zählt einmal je Tag, auch wenn sie neben einem kurzen Dienst steht.
                foreach (var kind in plan.Entries.Select(e => e.Kind).Where(k => k != RosterKind.Work).Distinct())
                {
                    switch (kind)
                    {
                        case RosterKind.Vacation:
                            Add(PayrollItem.VacationDays, 1);
                            break;
                        case RosterKind.Sick:
                            Add(PayrollItem.SickDays, 1);
                            break;
                        case RosterKind.Training:
                            Add(PayrollItem.TrainingDays, 1);
                            break;
                    }
                }
            }

            var number = numbers.GetValueOrDefault(employee.Id);
            var employeeLines = totals.Where(t => t.Value > 0).OrderBy(t => t.Key)
                .Select(t => new PayrollLine(employee.Id, employee.DisplayName, number, t.Key, Math.Round(t.Value, 2),
                    t.Key >= PayrollItem.VacationDays ? "Tage" : "h", settings.WageType(t.Key)))
                .ToList();
            if (employeeLines.Count > 0 && number is null)
            {
                warnings.Add($"{employee.DisplayName}: keine Personalnummer; ohne sie lässt sich nichts übergeben.");
            }

            lines.AddRange(employeeLines);
        }

        foreach (var id in usedLocations.Where(id => regions[id].Region is null).OrderBy(id => regions[id].Name))
        {
            warnings.Add($"{regions[id].Name}: kein Bundesland oder Kanton hinterlegt; es zählen nur die landesweiten Feiertage.");
        }

        if (until >= DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime))
        {
            warnings.Add("Der Monat ist noch nicht vorbei: die restlichen Tage stammen aus dem Plan.");
        }

        if (actual.Count > 0 && plannedOnly > 0)
        {
            warnings.Add($"An {plannedOnly} Diensttagen wurde nicht gestempelt; dort zählen die geplanten Stunden.");
        }

        if (lines.Any(l => l.WageType is null))
        {
            warnings.Add("Für manche Positionen ist keine Lohnart hinterlegt; sie werden nicht übergeben.");
        }

        return new(year, month, settings.Country, lines, warnings);
    }

    public async Task<(byte[] Content, string FileName, string ContentType)> FileAsync(PayrollFormat format, int year, int month, CancellationToken ct)
    {
        var preview = await PreviewAsync(year, month, ct);
        var settings = await db.PayrollSettings.AsNoTracking().SettingsAsync(ct);
        var lines = preview.Lines.Where(l => l.PersonnelNumber is not null && l.WageType is not null).ToList();
        return format switch
        {
            PayrollFormat.DatevLodas => (Ansi.GetBytes(Lodas(lines, settings, year, month)), $"LODAS_Bewegungsdaten_{year}{month:00}.txt", "text/plain; charset=windows-1252"),
            PayrollFormat.DatevLohnUndGehalt => (Ansi.GetBytes(LohnUndGehalt(lines, settings, year, month)), $"LuG_Bewegungsdaten_{year}{month:00}.txt", "text/plain; charset=windows-1252"),
            _ => (Ansi.GetBytes(Bmd(lines, year, month)), $"BMD_Lohn_{year}{month:00}.csv", "text/csv; charset=windows-1252"),
        };
    }

    /// <summary>
    /// DATEV LODAS, ASCII-Import der Bewegungsdaten (Satzart u_lod_bwd_buchung_standard).
    /// Bearbeitungsschlüssel 1 = Stunden, 2 = Tage; Abrechnungszeitraum ist der Monatserste.
    /// </summary>
    internal static string Lodas(IReadOnlyList<PayrollLine> lines, PayrollSettings s, int year, int month)
    {
        var text = new StringBuilder();
        text.Append("[Allgemein]\r\nZiel=LODAS\r\nVersion_SST=1.0\r\n")
            .Append(CultureInfo.InvariantCulture, $"BeraterNr={s.DatevConsultantNumber}\r\nMandantenNr={s.DatevClientNumber}\r\n")
            .Append("Datumsformat=TT.MM.JJJJ\r\n\r\n[Satzbeschreibung]\r\n")
            .Append("1;u_lod_bwd_buchung_standard;pnr#bwd;abrechnung_zeitraum#bwd;bs_wert_butab#bwd;bs_nr#bwd;la_eigene#bwd;\r\n\r\n")
            .Append("[Bewegungsdaten]\r\n");
        var period = new DateOnly(year, month, 1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        foreach (var line in lines)
        {
            var key = line.Unit == "h" ? 1 : 2;
            text.Append(CultureInfo.InvariantCulture, $"1;{line.PersonnelNumber};{period};{line.Quantity.ToString("0.00", German)};{key};{line.WageType};\r\n");
        }

        return text.ToString();
    }

    /// <summary>DATEV Lohn und Gehalt, Bewegungsdaten: Kopfzeile Berater, Mandant, Monat; je Zeile Personalnummer, Lohnart, Wert.</summary>
    internal static string LohnUndGehalt(IReadOnlyList<PayrollLine> lines, PayrollSettings s, int year, int month)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{s.DatevConsultantNumber} {s.DatevClientNumber} {month:00}/{year}\r\n");
        foreach (var line in lines)
        {
            text.Append(CultureInfo.InvariantCulture, $"{line.PersonnelNumber} {line.WageType} {line.Quantity.ToString("0.00", CultureInfo.InvariantCulture)}\r\n");
        }

        return text.ToString();
    }

    /// <summary>BMD Lohn: CSV für den Import der Lohnarten-Bewegungen (Feldzuordnung einmalig in BMD als Importvorlage anlegen).</summary>
    internal static string Bmd(IReadOnlyList<PayrollLine> lines, int year, int month)
    {
        var text = new StringBuilder("Dienstnehmernummer;Lohnart;Menge;Einheit;Abrechnungsmonat;Name\r\n");
        foreach (var line in lines)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"{line.PersonnelNumber};{line.WageType};{line.Quantity.ToString("0.00", German)};{(line.Unit == "h" ? "Std" : "Tage")};{month:00}.{year};{Field(line.DisplayName)}\r\n");
        }

        return text.ToString();
    }

    internal static decimal Hours(TimeOnly start, TimeOnly end) => (decimal)(end - start).TotalMinutes / 60m;

    /// <summary>Anteil eines Dienstes (am selben Tag) in der Nachtzeit, die über Mitternacht reicht (z. B. 22 bis 6 Uhr).</summary>
    internal static decimal NightHours(TimeOnly start, TimeOnly end, TimeOnly nightStart, TimeOnly nightEnd)
    {
        static decimal Overlap(TimeOnly a, TimeOnly b, TimeSpan c, TimeSpan d)
        {
            var from = Math.Max(a.ToTimeSpan().TotalMinutes, c.TotalMinutes);
            var until = Math.Min(b.ToTimeSpan().TotalMinutes, d.TotalMinutes);
            return until > from ? (decimal)(until - from) / 60m : 0;
        }

        return nightStart > nightEnd
            ? Overlap(start, end, TimeSpan.Zero, nightEnd.ToTimeSpan()) + Overlap(start, end, nightStart.ToTimeSpan(), TimeSpan.FromHours(24))
            : Overlap(start, end, nightStart.ToTimeSpan(), nightEnd.ToTimeSpan());
    }

    private static string Field(string value) => value.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');

    /// <summary>DATEV und BMD lesen Windows-1252 (ANSI).</summary>
    private static Encoding Ansi
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252);
        }
    }
}
