using AispaStudio.Api.Assistant;
using AispaStudio.Api.Assistant.Answers;
using AispaStudio.Api.LocalAi;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.MailIntake.Channels;
using AispaStudio.Api.MailIntake.Mailboxes;
using AispaStudio.Api.Reporting;
using AispaStudio.Api.Retention;
using AispaStudio.Api.System;
using Wolverine;
using Wolverine.Postgresql;

namespace AispaStudio.Api.MailIntake;

internal static class MailIntakeSetup
{
    public const string Queue = "mail-intake";

    /// <summary>Queue für „Jetzt prüfen“ in den Rollen <c>Api</c> und <c>Worker</c>.</summary>
    public const string MailboxCheckQueue = "mailbox-check";

    /// <summary>Nachrichten der Mail-Queue: dieselbe Liste für das Routing in jeder Rolle und für die <see cref="MailIntakeFailurePolicy"/>.</summary>
    public static readonly IReadOnlySet<Type> QueuedMessageTypes = new HashSet<Type>
    {
        typeof(InboundMailReceived), typeof(ReplyDraftRequested), typeof(ReplySendRequested), typeof(MailTranslationRequested),
        typeof(ReplyRevisionRequested), typeof(ReproposeRequested), typeof(TwilioRecordingReceived),
    };

    /// <summary>
    /// Dienste der Terminannahme. Postfach nach <c>MailIntake:Mailbox</c>: „Imap“, „Graph“ oder ohne Angabe das Test-Postfach; gesendet wird über den gemeinsamen Mailversand (<c>AddMail</c>). KI: Claude, sobald ein API-Schlüssel
    /// konfiguriert (<c>Ai:Claude:ApiKey</c>) oder in der App eingetragen ist (ADR 0103), dazu optional das lokale Modell (<c>Ai:Local:Url</c>), sonst die regelbasierte Variante ohne KI.
    /// </summary>
    public static IHostApplicationBuilder AddMailIntake(this IHostApplicationBuilder builder, ModuleSwitches modules)
    {
        var services = builder.Services;
        services.Configure<MailIntakeOptions>(builder.Configuration.GetSection("MailIntake"));
        services.Configure<ClaudeOptions>(builder.Configuration.GetSection("Ai:Claude"));

        services.AddSingleton<TestInbox>();
        services.AddHttpClient(GraphMailbox.HttpClientName);

        // Eigene Postfächer der Spas (Einstellungen in der Datenbank, Geheimnis per Data Protection verschlüsselt).
        services.AddSingleton<SpaMailboxSecrets>();
        services.AddSingleton<ISpaMailboxFactory, SpaMailboxFactory>();
        services.AddSingleton<SpaMailboxRegistry>();
        services.Configure<OperatorOptions>(builder.Configuration.GetSection(OperatorOptions.Section));
        services.Configure<ImapOptions>(builder.Configuration.GetSection("MailIntake:Imap"));
        services.Configure<GraphOptions>(builder.Configuration.GetSection("MailIntake:Graph"));
        switch (builder.Configuration["MailIntake:Mailbox"]?.ToUpperInvariant())
        {
            case "IMAP":
                services.AddSingleton<IMailbox, ImapMailbox>();
                break;
            case "GRAPH":
                services.AddSingleton<IMailbox, GraphMailbox>();
                break;
            default:
                services.AddSingleton<IMailbox>(sp => sp.GetRequiredService<TestInbox>());
                break;
        }

        // Versand: IMailSender aus AddMail (SMTP, ohne Zugang nur ins Log).

        // KI (ADR 0057): Claude mit Schlüssel; das optionale lokale Modell (Ai:Local:Url) übernimmt das Lesen von Terminwünschen
        // und Fragen, wenn Claude nicht antwortet oder kein Schlüssel da ist. Formulieren, Übersetzen und Zusammenfassen bleibt Claude.
        // Den Schlüssel gibt es je Spa (ADR 0103): Konfiguration vor App. Welche Umsetzung gilt, entscheidet sich je Scope (Anfrage oder
        // Hintergrundauftrag nach TenantContext.Use), deshalb ohne Neustart. Ohne Schlüssel genau die Umsetzungen wie früher ohne Claude.
        var aiOff = string.Equals(builder.Configuration["Ai:Provider"], "RuleBased", StringComparison.OrdinalIgnoreCase);
        var local = services.AddLocalAi(builder.Configuration);
        var useLocal = !aiOff && local.Enabled;
        services.AddSingleton<ClaudeKeyStore>();
        services.AddSingleton<ClaudeClients>();
        services.AddSingleton<IClaudeKeyCheck, AnthropicKeyCheck>();
        services.AddScoped<ClaudeAccess>();
        services.AddScoped<ClaudeJson>();

        // Ohne Claude und ohne lokales Modell gibt es kein IAiJson (null): Der Treatwell-Leser nimmt dann gleich die Regeln.
        services.AddScoped<IAiJson>(sp => ClaudeOn(sp)
            ? useLocal
                ? new FallbackAiJson(sp.GetRequiredService<ClaudeJson>(), sp.GetRequiredService<LocalModelJson>(), sp.GetRequiredService<ILogger<FallbackAiJson>>())
                : sp.GetRequiredService<ClaudeJson>()
            : useLocal ? sp.GetRequiredService<LocalModelJson>() : null!);
        services.AddSingleton<RuleBasedBookingAssistant>();
        services.AddSingleton<RuleBasedAssistantClassifier>();
        if (useLocal)
        {
            services.AddSingleton<LocalBookingAssistant>();
            services.AddSingleton<LocalAssistantClassifier>();
        }

        services.AddScoped<IBookingAssistant>(sp => ClaudeOn(sp)
            ? ActivatorUtilities.CreateInstance<ClaudeBookingAssistant>(sp)
            : useLocal ? sp.GetRequiredService<LocalBookingAssistant>() : sp.GetRequiredService<RuleBasedBookingAssistant>());
        services.AddScoped<IAssistantClassifier>(sp => ClaudeOn(sp)
            ? ActivatorUtilities.CreateInstance<ModelAssistantClassifier>(sp)
            : useLocal ? sp.GetRequiredService<LocalAssistantClassifier>() : sp.GetRequiredService<RuleBasedAssistantClassifier>());
        services.AddScoped<IAnswerPhraser>(sp => ClaudeOn(sp)
            ? new ModelAnswerPhraser(sp.GetRequiredService<ClaudeJson>(), sp.GetRequiredService<ILogger<ModelAnswerPhraser>>())
            : new NoAnswerPhraser());
        services.AddClaudeOr<IBriefingSummarizer, ClaudeBriefingSummarizer, NoBriefingSummarizer>();
        services.AddClaudeOr<IManagementSummarizer, ClaudeManagementSummarizer, NoManagementSummarizer>();
        services.AddClaudeOr<IManagementAnalyst, ClaudeManagementAnalyst, NoManagementAnalyst>();
        services.AddClaudeOr<IRebookWriter, ClaudeRebookWriter, NoRebookWriter>();
        services.AddClaudeOr<IQuestionAnswerer, ClaudeQuestionAnswerer, NoQuestionAnswerer>();
        services.AddClaudeOr<IMessageTranslator, ClaudeMessageTranslator, NoMessageTranslator>();
        services.AddClaudeOr<IReplyReviser, ClaudeReplyReviser, NoReplyReviser>();

        // SMS, WhatsApp und Anrufe: Anbieter nach TextChannels, ohne Angabe nur ins Log.
        services.Configure<TextChannelOptions>(builder.Configuration.GetSection(TextChannelOptions.Section));
        services.Configure<TwilioOptions>(builder.Configuration.GetSection($"{TextChannelOptions.Section}:Twilio"));
        services.Configure<MetaWhatsAppOptions>(builder.Configuration.GetSection($"{TextChannelOptions.Section}:Meta"));
        services.Configure<VoiceOptions>(builder.Configuration.GetSection($"{TextChannelOptions.Section}:Voice"));
        services.AddHttpClient(TwilioProvider.HttpClientName);
        services.AddHttpClient(MetaWhatsAppProvider.HttpClientName);
        services.AddSingleton<ITextChannelProvider, TwilioProvider>();
        services.AddSingleton<ITextChannelProvider, MetaWhatsAppProvider>();
        services.AddSingleton<ITextMessageSender, TextMessageRouter>();
        services.AddSingleton<ITwilioRecordings, TwilioRecordings>();
        // Spracherkennung: Azure AI Speech in einer EU-Region, sobald ein Schlüssel da ist; sonst die Test-Variante.
        if (string.Equals(builder.Configuration["TextChannels:Voice:SpeechProvider"], "Azure", StringComparison.OrdinalIgnoreCase))
        {
            var speech = builder.Configuration.GetSection("TextChannels:Voice:Azure");
            services.AddOptions<AzureSpeechOptions>().Bind(speech)
                .Validate(o => { AzureSpeechToText.Validate(o); return true; }).ValidateOnStart();
            services.AddHttpClient(AzureSpeechToText.HttpClientName, c => c.Timeout = TimeSpan.FromMinutes(2)).WithoutStandardResilience();
            services.AddSingleton<ISpeechToText, AzureSpeechToText>();
        }
        else
        {
            services.AddSingleton<ISpeechToText, FakeSpeechToText>();
        }

        services.AddScoped<MailIngestion>();
        services.AddScoped<SpaMailSender>();
        services.AddScoped<CustomerMatcher>();
        services.AddScoped<SenderRuleStore>();
        services.AddScoped<TreatwellReader>();
        services.AddScoped<MailIntakeProcessor>();
        services.AddScoped<SpaFacts>();
        services.AddSingleton<MailboxPoller>();
        if (modules.IsOn(AppModule.BookingIntake))
        {
            services.AddBackgroundService<MailboxPoller>(modules);
        }

        return builder;
    }

    /// <summary>Ob für das Spa des Scopes ein Claude-Schlüssel gilt (ADR 0103).</summary>
    public static bool ClaudeOn(IServiceProvider services) => services.GetRequiredService<ClaudeAccess>().Available;

    /// <summary>Mit Schlüssel die Claude-Umsetzung, sonst die Umsetzung ohne KI; entschieden je Scope.</summary>
    public static IServiceCollection AddClaudeOr<TService, TClaude, TFallback>(this IServiceCollection services)
        where TService : class
        where TClaude : class, TService
        where TFallback : class, TService
    {
        services.AddSingleton<TFallback>();
        services.AddScoped<TService>(sp => ClaudeOn(sp) ? ActivatorUtilities.CreateInstance<TClaude>(sp) : sp.GetRequiredService<TFallback>());
        return services;
    }

    /// <summary>
    /// Eigene Queue, eine Nachricht nach der anderen (Reihenfolge, Last auf der KI begrenzt). Rolle <c>All</c>: lokale Queue wie bisher.
    /// Rollen <c>Api</c> und <c>Worker</c> (ADR 0087): PostgreSQL-Queues von Wolverine; nur der Worker hört, und nur ein Knoten zugleich.
    /// </summary>
    public static void AddMailIntakeQueue(this WolverineOptions opts, IPostgresqlBackedPersistence persistence, ModuleSwitches modules)
    {
        if (modules.Role == HostingRole.All)
        {
            opts.LocalQueue(Queue).Sequential();
            opts.Publish(rule =>
            {
                foreach (var type in QueuedMessageTypes)
                {
                    rule.Message(type);
                }

                rule.ToLocalQueue(Queue);
            });
            return;
        }

        // Gleicher Zeitstempel zweier Nachrichten: ohne feste Reihenfolge der Abfrage könnte die spätere zuerst kommen.
        persistence.EnableMessageTransport(transport => transport.EnableDeterministicQueueOrdering());

        // Dauerhaft über die Outbox (SendInline kennt die PostgreSQL-Queue nicht).
        opts.Publish(rule =>
        {
            foreach (var type in QueuedMessageTypes)
            {
                rule.Message(type);
            }

            rule.ToPostgresqlQueue(Queue);
        });
        // „Jetzt prüfen“ läuft dort, wo der Takt läuft: Die Postfach-Sperre im MailboxPoller gilt nur je Prozess.
        opts.PublishMessage<CheckSpaMailbox>().ToPostgresqlQueue(MailboxCheckQueue);

        // Nur der Leader hört, eine Nachricht nach der anderen. ListenWithStrictOrdering taugt hier nicht: Der Leader verteilt
        // exklusive Hörer gleichmäßig auf alle Knoten, auch auf Api-Knoten ohne Hörer, und die Queue stand dann still (gemessen).
        // Leader kann deshalb nur der Worker werden.
        if (modules.Role == HostingRole.Worker)
        {
            opts.ListenToPostgresqlQueue(Queue).ListenOnlyAtLeader().Sequential();
            opts.ListenToPostgresqlQueue(MailboxCheckQueue).ListenOnlyAtLeader().Sequential();
        }
        else
        {
            opts.Durability.LeadershipEligible = false;
        }

        opts.Policies.Add(new MailIntakeFailurePolicy());
    }
}
