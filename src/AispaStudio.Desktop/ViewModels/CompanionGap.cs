using AispaStudio.Contracts;

namespace AispaStudio.Desktop.ViewModels;

/// <summary>
/// Ein Hinweis „Unvollständig“ als Meldung der Leiste „KI und Hilfe“: Satz mit der Maske, in der die Angabe steht, und der Knopf „Jetzt ergänzen“ nur für Personen
/// mit dem Recht; sonst steht „Nur Leitung“ im Satz und es gibt keinen Knopf.
/// </summary>
public sealed record CompanionGap(string Message, string? ActionText, AssistantLink? Link)
{
    public bool HasAction => Link is not null;

    public static CompanionGap Of(AssistantIncomplete gap) =>
        new($"{gap.Text} Zu ergänzen unter {gap.Where}.{(gap.Hint is null ? "" : $" {gap.Hint}.")}", gap.Link?.Label, gap.Link);
}
