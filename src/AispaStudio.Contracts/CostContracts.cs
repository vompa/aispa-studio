namespace AispaStudio.Contracts;

/// <summary>
/// Kostensätze eines Standorts für die Vollkostenrechnung. Beträge netto pro Monat bzw. Stunde.
/// <c>OtherMonthlyCosts</c>: Gemeinkosten wie Rezeption, Energie, Versicherung, Software; sie werden wie die Miete auf die Raumminuten verteilt.
/// <c>PlannedUtilizationPercent</c>: geplante Auslastung der Räume; Leerstand verteuert jede gebuchte Minute.
/// <c>StaffProductivityPercent</c>: Anteil der bezahlten Arbeitszeit, der in Behandlungen geht (Rest: Pausen, Leerlauf, Besprechungen).
/// <c>DefaultStaffHourlyCost</c> gilt für Personen ohne eigenen Stundensatz.
/// </summary>
public sealed record LocationCostSettingsDto(
    decimal CleaningHourlyCost,
    decimal DefaultStaffHourlyCost,
    decimal OtherMonthlyCosts,
    int PlannedUtilizationPercent,
    int StaffProductivityPercent);

/// <summary>Miet- und Nebenkostenanteil eines Raums pro Monat und Pflegematerial je Reinigung.</summary>
public sealed record RoomCostDto(Guid RoomId, string RoomName, decimal MonthlyCost, decimal CleaningMaterialCost);

public sealed record RoomCostInput(decimal MonthlyCost, decimal CleaningMaterialCost);

/// <summary>Arbeitgeberkosten pro bezahlter Stunde (Lohn, Lohnnebenkosten, Sonderzahlungen anteilig). Leer: Standardsatz des Standorts.</summary>
public sealed record EmployeeCostDto(Guid EmployeeId, string EmployeeName, decimal? HourlyCost);

public sealed record EmployeeCostInput(decimal? HourlyCost);

/// <summary>
/// Verbrauchsmaterial je Behandlung, z. B. 30 ml Massageöl zu 0,04 € pro ml. Mit <c>StockItemId</c> kommen Bezeichnung, Einheit und Preis
/// aus dem Lager, und jede abgeschlossene Behandlung bucht die Menge dort ab.
/// </summary>
public sealed record MaterialLine(string Name, decimal Quantity, string Unit, decimal UnitCost, Guid? StockItemId = null);

/// <summary>Kostenbestandteil einer Behandlung mit nachvollziehbarer Herleitung.</summary>
public sealed record CostPart(string Kind, decimal Amount, string Basis);

/// <summary>
/// Vollkosten einer Behandlung an einem Standort, je Termin. <c>NetPrice</c> ist der Preis ohne Umsatzsteuer.
/// <c>Margin</c> = Nettopreis − Vollkosten. <c>Appointments</c> und <c>ContributionTotal</c> beziehen sich auf den gewählten Monat
/// (stattgefundene und gebuchte Termine, ohne Absagen und Nichterscheinen).
/// <c>Missing</c> nennt fehlende Angaben, ohne die die Rechnung zu niedrig ausfällt.
/// </summary>
public sealed record TreatmentCostDto(
    Guid TreatmentId,
    string TreatmentName,
    int StaffMinutes,
    int RoomMinutes,
    decimal Price,
    decimal NetPrice,
    IReadOnlyList<CostPart> Parts,
    decimal Material,
    decimal Staff,
    decimal Room,
    decimal Cleaning,
    decimal Overhead,
    decimal Total,
    decimal Margin,
    decimal MarginPercent,
    int Appointments,
    decimal ContributionTotal,
    IReadOnlyList<string> Missing);

/// <summary>Grundlagen der Rechnung für den Standort: verfügbare Raumminuten im Monat und daraus die Sätze pro Minute.</summary>
public sealed record CostAnalysisDto(
    Guid LocationId,
    string Month,
    decimal CapacityMinutesPerRoom,
    decimal OverheadPerRoomMinute,
    IReadOnlyList<TreatmentCostDto> Treatments);
