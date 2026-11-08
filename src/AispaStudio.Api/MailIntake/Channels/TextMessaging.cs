using AispaStudio.Contracts;
using Microsoft.Extensions.Options;

namespace AispaStudio.Api.MailIntake.Channels;

/// <summary>Kurznachricht an einen Gast. <c>To</c> ist eine Telefonnummer im Format E.164.</summary>
/// <param name="Template">
/// Nur WhatsApp: freigegebene Vorlage statt freiem Text (nötig außerhalb von 24 Stunden nach der letzten Nachricht des Gastes).
/// <c>Text</c> bleibt die lesbare Fassung fürs Log.
/// </param>
/// <param name="From">Absendernummer (E.164), z. B. die Nummer des Spas, an die der Gast geschrieben hat; leer: die konfigurierte.</param>
/// <param name="SenderId">Kennung des Absenders beim Anbieter, falls er sie statt der Nummer braucht (Meta: Phone-Number-ID).</param>
public sealed record OutgoingText(
    BookingChannel Channel, string To, string Text, TextTemplate? Template = null, string? From = null, string? SenderId = null);

/// <param name="Name">Meta: Name der Vorlage; Twilio: Content-SID (HX…).</param>
/// <param name="Parameters">Werte für {{1}}, {{2}} … in dieser Reihenfolge.</param>
public sealed record TextTemplate(string Name, string Language, IReadOnlyList<string> Parameters);

/// <summary>Versand von SMS und WhatsApp. Gesendet wird immer aus der Queue, nie im HTTP-Aufruf.</summary>
public interface ITextMessageSender
{
    Task SendAsync(OutgoingText message, CancellationToken ct);
}

/// <summary>Ein Anbieter für einen oder mehrere Kanäle (Twilio für SMS und WhatsApp, Meta für WhatsApp).</summary>
public interface ITextChannelProvider
{
    string Name { get; }

    Task SendAsync(OutgoingText message, CancellationToken ct);
}

/// <summary>
/// Abschnitt <c>TextChannels</c>: welcher Anbieter welchen Kanal bedient. Ohne Angabe werden Nachrichten nur ins Log
/// geschrieben (Entwicklung) bzw. nicht versendet (Betrieb), wie beim Mailversand.
/// </summary>
public sealed class TextChannelOptions
{
    public const string Section = "TextChannels";

    /// <summary>„Twilio“ oder leer.</summary>
    public string? SmsProvider { get; set; }

    /// <summary>„Twilio“, „Meta“ (WhatsApp Cloud API) oder leer.</summary>
    public string? WhatsAppProvider { get; set; }
}

/// <summary>Wählt je Kanal den konfigurierten Anbieter.</summary>
internal sealed partial class TextMessageRouter(
    IEnumerable<ITextChannelProvider> providers,
    IOptions<TextChannelOptions> options,
    IHostEnvironment env,
    ILogger<TextMessageRouter> log) : ITextMessageSender
{
    public Task SendAsync(OutgoingText message, CancellationToken ct)
    {
        var name = message.Channel == BookingChannel.WhatsApp ? options.Value.WhatsAppProvider : options.Value.SmsProvider;
        var provider = providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (provider is not null)
        {
            return provider.SendAsync(message, ct);
        }

        if (env.IsDevelopment())
        {
            LogMessage(log, message.Channel, message.To, message.Text);
        }
        else
        {
            NotSent(log, message.Channel);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Channel} an {To}:\n{Text}")]
    private static partial void LogMessage(ILogger logger, BookingChannel channel, string to, string text);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Channel} nicht versendet: kein Anbieter eingerichtet (TextChannels).")]
    private static partial void NotSent(ILogger logger, BookingChannel channel);
}
