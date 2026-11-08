using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Mail;

/// <summary>
/// Wie ein Gast in Nachrichten angesprochen wird (ADR 0064): deutsche Texte mit Anrede („Frau Mag. Huber“), die übrigen Sprachen mit dem Vornamen.
/// Der Rufname kommt nie vor.
/// </summary>
public readonly record struct GuestName(string FirstName, string GreetingName)
{
    /// <summary>Ohne Kundendatensatz oder ohne Angaben zur Anrede: der Vorname wie bisher.</summary>
    public static GuestName FromFirstName(string firstName) => new(firstName, firstName);

    /// <summary>Name für die Begrüßung in der Sprache der Nachricht: deutsch mit Anrede, sonst der Vorname.</summary>
    public string For(string language) => language == "de" ? GreetingName : FirstName;

    /// <summary>Titel in der Anrede richten sich nach dem Land des Standorts, um den es geht.</summary>
    public static GuestName Of(Customer customer, string? locationTimeZone) => new(customer.FirstName,
        PersonNames.GreetingName(customer.Salutation, customer.TitlesBefore, customer.FirstName, customer.LastName,
            PersonNames.CountryOfTimeZone(locationTimeZone), customer.TitleInGreeting));
}
