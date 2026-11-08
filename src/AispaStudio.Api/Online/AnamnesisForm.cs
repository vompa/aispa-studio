using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;

namespace AispaStudio.Api.Online;

/// <summary>
/// Der Anamnesebogen für Day-Spa-Behandlungen (keine medizinischen Behandlungen). Derselbe Bogen dient später für Tablet und
/// Papier; deshalb liegt er im Server und nicht in der Oberfläche. Neue Fragen bekommen eine neue <see cref="Version"/>;
/// eine weitere Sprache ändert die Fragen nicht und braucht keine.
/// </summary>
internal static class AnamnesisForm
{
    public const string Version = "2026-10";

    private sealed record Wording(string Label, string? Details);

    private sealed record Question(string Key, AnamnesisQuestionKind Kind, Wording De, Wording En, Wording Fr, Wording It)
    {
        public Wording In(string language) => language switch { "en" => En, "fr" => Fr, "it" => It, _ => De };
    }

    private static readonly Question[] Questions =
    [
        new("allergies", AnamnesisQuestionKind.YesNo,
            new("Haben Sie Allergien oder Unverträglichkeiten (z. B. Öle, Duftstoffe, Nüsse, Latex)?", "Welche?"),
            new("Do you have allergies or intolerances (e.g. oils, fragrances, nuts, latex)?", "Which ones?"),
            new("Avez-vous des allergies ou des intolérances (p. ex. huiles, parfums, fruits à coque, latex) ?", "Lesquelles ?"),
            new("Ha allergie o intolleranze (ad es. oli, profumi, frutta a guscio, lattice)?", "Quali?")),
        new("skin", AnamnesisQuestionKind.YesNo,
            new("Haben Sie zurzeit Hautprobleme, offene Wunden, Entzündungen oder einen Sonnenbrand?", "Wo und was?"),
            new("Do you currently have skin problems, open wounds, inflammation or sunburn?", "Where and what?"),
            new("Avez-vous actuellement des problèmes de peau, des plaies ouvertes, des inflammations ou un coup de soleil ?",
                "Où et de quoi s’agit-il ?"),
            new("Ha attualmente problemi della pelle, ferite aperte, infiammazioni o una scottatura solare?", "Dove e di che cosa si tratta?")),
        new("circulation", AnamnesisQuestionKind.YesNo,
            new("Haben Sie Herz-Kreislauf-Beschwerden, zu hohen oder zu niedrigen Blutdruck oder Venenleiden?", "Bitte kurz beschreiben"),
            new("Do you have heart or circulation problems, high or low blood pressure or vein conditions?", "Please describe briefly"),
            new("Avez-vous des troubles cardiovasculaires, une tension artérielle trop élevée ou trop basse, ou des troubles veineux ?",
                "Veuillez décrire brièvement"),
            new("Ha disturbi cardiovascolari, pressione sanguigna troppo alta o troppo bassa, oppure disturbi venosi?", "Descriva brevemente")),
        new("pregnancy", AnamnesisQuestionKind.YesNo,
            new("Sind Sie schwanger oder stillen Sie?", "Schwangerschaftswoche (freiwillig)"),
            new("Are you pregnant or breastfeeding?", "Week of pregnancy (optional)"),
            new("Êtes-vous enceinte ou allaitez-vous ?", "Semaine de grossesse (facultatif)"),
            new("È incinta o sta allattando?", "Settimana di gravidanza (facoltativo)")),
        new("medication", AnamnesisQuestionKind.YesNo,
            new("Nehmen Sie regelmäßig Medikamente, z. B. Blutverdünner?", "Welche?"),
            new("Do you take medication regularly, e.g. blood thinners?", "Which ones?"),
            new("Prenez-vous régulièrement des médicaments, p. ex. des anticoagulants ?", "Lesquels ?"),
            new("Assume regolarmente farmaci, ad es. anticoagulanti?", "Quali?")),
        new("surgery", AnamnesisQuestionKind.YesNo,
            new("Hatten Sie in den letzten sechs Monaten eine Operation oder Verletzung?", "Was und wann?"),
            new("Have you had surgery or an injury in the last six months?", "What and when?"),
            new("Avez-vous eu une opération ou une blessure au cours des six derniers mois ?", "Quoi et quand ?"),
            new("Ha subito un intervento chirurgico o una lesione negli ultimi sei mesi?", "Che cosa e quando?")),
        new("other", AnamnesisQuestionKind.Text,
            new("Gibt es sonst etwas, das wir für Ihre Behandlung wissen sollten?", null),
            new("Is there anything else we should know for your treatment?", null),
            new("Y a-t-il autre chose que nous devrions savoir pour votre soin ?", null),
            new("C’è qualcos’altro che dovremmo sapere per il suo trattamento?", null)),
    ];

    public static IReadOnlySet<string> Keys { get; } = Questions.Select(q => q.Key).ToHashSet();

    public static bool IsYesNo(string key) => Questions.Any(q => q.Key == key && q.Kind == AnamnesisQuestionKind.YesNo);

    public static AnamnesisFormDefinition Definition(string? language)
    {
        var lang = GuestLanguage.Normalize(language);
        var (intro, consent) = lang switch
        {
            "en" => (
                "Your answers help us to adapt the treatment to you. They are stored encrypted and are only seen by the team treating you.",
                "I consent to the spa processing my health information from this form to carry out my treatments. I can withdraw this consent at any time."),
            "fr" => (
                "Vos réponses nous aident à adapter le soin à vos besoins. Elles sont enregistrées sous forme chiffrée et seule l’équipe qui vous prend en charge peut les consulter.",
                "Je consens à ce que le spa traite les données de santé figurant dans ce questionnaire pour la réalisation de mes soins. Je peux retirer ce consentement à tout moment."),
            "it" => (
                "Le sue risposte ci aiutano ad adattare il trattamento alle sue esigenze. Vengono salvate in forma crittografata e sono visibili solo al team che si occupa del suo trattamento.",
                "Acconsento che la spa tratti i dati relativi alla salute contenuti in questo questionario ai fini dello svolgimento dei miei trattamenti. Posso revocare il consenso in qualsiasi momento."),
            _ => (
                "Ihre Angaben helfen uns, die Behandlung auf Sie abzustimmen. Sie werden verschlüsselt gespeichert und nur vom behandelnden Team eingesehen.",
                "Ich willige ein, dass das Spa meine Gesundheitsangaben aus diesem Bogen zur Durchführung meiner Behandlungen verarbeitet. Ich kann die Einwilligung jederzeit widerrufen."),
        };
        return new AnamnesisFormDefinition(
            Version,
            lang,
            intro,
            consent,
            [.. Questions.Select(q => new AnamnesisQuestion(
                q.Key, q.Kind, q.In(lang).Label, q.In(lang).Details, q.Kind == AnamnesisQuestionKind.YesNo))]);
    }
}
