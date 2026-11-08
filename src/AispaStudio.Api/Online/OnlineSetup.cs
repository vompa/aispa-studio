using System.Threading.RateLimiting;
using AispaStudio.Api.System;

namespace AispaStudio.Api.Online;

/// <summary>Dienste und Endpunkte des Kundenbereichs.</summary>
internal static class OnlineSetup
{
    public const string PublicRateLimit = "online-public";
    public const string SensitiveRateLimit = "online-sensitive";

    public static IServiceCollection AddOnlineBooking(this IServiceCollection services, IConfiguration configuration, ModuleSwitches modules)
    {
        services.Configure<OnlineBookingOptions>(configuration.GetSection(OnlineBookingOptions.Section));
        services.Configure<HealthDataOptions>(configuration.GetSection(HealthDataOptions.Section));
        services.AddCustomerAuth(configuration);
        services.AddScoped<AccessLinks>();
        services.AddScoped<CustomerAccounts>();
        services.AddScoped<OnlinePortal>();
        services.AddScoped<OnlineRules>();
        services.AddSingleton<HealthKeyRing>();
        services.AddSingleton<HealthDataProtector>();
        services.AddScoped<AnamnesisStore>();
        services.AddScoped<Waitlist>();
        services.AddScoped<VoucherShop>();
        services.AddScoped<CardGuarantees>();
        services.AddScoped<OnlineRefunds>();
        services.AddScoped<CardRequests>();
        services.AddScoped<Deposits>();
        services.AddSingleton<SavedCardCleanup>();
        services.AddSingleton<UnsecuredBookingRelease>();
        services.AddSingleton<PaymentRecovery>();
        if (modules.IsOn(AppModule.OnlineBooking))
        {
            services.AddBackgroundService<SavedCardCleanup>(modules);
            services.AddBackgroundService<UnsecuredBookingRelease>(modules);
            services.AddBackgroundService<PaymentRecovery>(modules);
        }

        // Online-Zahlung (ADR 0054): Stripe steht jedem Spa offen, das ein Zahlungskonto einrichtet (Connect oder eigene Schlüssel).
        // „Test“ simuliert Zahlungen für Spas ohne Konto, „None“ schaltet Online-Zahlung ganz ab. Ohne bereites Konto verkauft der
        // Kundenbereich keine Gutscheine und verlangt keine Ausfallgebühr.
        services.Configure<OnlineVoucherOptions>(configuration.GetSection(OnlineVoucherOptions.Section));
        services.AddSingleton<PaymentSecrets>();
        services.AddScoped<PaymentAccounts>();
        var paymentProvider = configuration[$"{OnlineVoucherOptions.Section}:PaymentProvider"];
        if (!string.Equals(paymentProvider, "None", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient(StripePaymentProvider.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(20));
            services.AddSingleton<StripeAccountClient>();
            services.AddSingleton<IOnlinePaymentProvider, StripePaymentProvider>();
        }

        if (string.Equals(paymentProvider, "Test", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IOnlinePaymentProvider, TestPaymentProvider>();
        }

        // Papierbögen lesen: Claude, sobald ein API-Schlüssel eingerichtet ist (Ai:Claude:ApiKey), sonst von Hand übertragen.
        services.Configure<AnamnesisReaderOptions>(configuration.GetSection("Ai:Claude"));
        // Wie bei der Terminannahme schaltet Ai:Provider=RuleBased die KI ganz ab.
        if (string.IsNullOrWhiteSpace(configuration["Ai:Claude:ApiKey"])
            || string.Equals(configuration["Ai:Provider"], "RuleBased", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAnamnesisReader, ManualAnamnesisReader>();
        }
        else
        {
            services.AddSingleton<IAnamnesisReader, ClaudeAnamnesisReader>();
        }

        // Öffentliche Endpunkte je IP-Adresse begrenzen: schützt vor Durchprobieren von Codes und vor Massenbuchungen.
        var options = configuration.GetSection(OnlineBookingOptions.Section).Get<OnlineBookingOptions>() ?? new();
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(PublicRateLimit, ctx => PerIp(ctx, options.PublicRequestsPerMinute));
            o.AddPolicy(SensitiveRateLimit, ctx => PerIp(ctx, options.SensitiveRequestsPerMinute));
        });

        return services;
    }

    public static IEndpointRouteBuilder MapOnlineEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapOnlineBookingEndpoints();
        routes.MapOnlineRulesEndpoints();
        routes.MapCustomerAuthEndpoints();
        routes.MapCustomerPortalEndpoints();
        routes.MapWaitlistEndpoints();
        routes.MapVoucherShopEndpoints();
        routes.MapCardGuaranteeEndpoints();
        routes.MapDepositEndpoints();
        routes.MapOnlineRefundEndpoints();
        routes.MapPaymentAccountEndpoints();
        return routes;
    }

    private static RateLimitPartition<string> PerIp(HttpContext context, int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unbekannt",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
}
