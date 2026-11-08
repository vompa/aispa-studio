using AispaStudio.Contracts;

namespace AispaStudio.Api.BusinessInfo;

/// <summary>Die Kategorien der Betriebsinformationen in Worten, wie sie die Oberfläche und der Assistent zeigen.</summary>
internal static class BusinessContactLabels
{
    public static string Of(BusinessContactCategory category) => category switch
    {
        BusinessContactCategory.Insurance => "Versicherung",
        BusinessContactCategory.Craftsman => "Handwerker und Technik",
        BusinessContactCategory.Supplier => "Lieferant",
        BusinessContactCategory.Authority => "Behörde",
        BusinessContactCategory.Accounting => "Buchhaltung",
        BusinessContactCategory.Payroll => "Lohnverrechnung",
        BusinessContactCategory.TaxAdvisor => "Steuerberatung",
        BusinessContactCategory.TrustedPerson => "Vertrauensperson",
        BusinessContactCategory.Emergency => "Notfall",
        _ => "Sonstiges",
    };

    /// <summary>Wörter, unter denen man die Kategorie fragt (neben ihrer Bezeichnung); der Assistent findet damit „Steuerberater“ in der Kategorie „Steuerberatung“.</summary>
    public static string Keywords(BusinessContactCategory category) => category switch
    {
        BusinessContactCategory.Insurance => "Versicherung Versicherer Polizze",
        BusinessContactCategory.Craftsman => "Handwerker Techniker Technik",
        BusinessContactCategory.Authority => "Behörde Amt",
        BusinessContactCategory.Accounting => "Buchhaltung Buchhalter",
        BusinessContactCategory.Payroll => "Lohnverrechnung Lohnverrechner",
        BusinessContactCategory.TaxAdvisor => "Steuerberatung Steuerberater",
        BusinessContactCategory.Emergency => "Notfall Notdienst",
        _ => Of(category),
    };
}
