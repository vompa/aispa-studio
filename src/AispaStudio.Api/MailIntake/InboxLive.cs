using AispaStudio.Api.Live;
using AispaStudio.Api.Tenancy;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wolverine;

namespace AispaStudio.Api.MailIntake;

/// <summary>Nachricht in der Queue: im Posteingang eines Spas hat sich eine Nachricht geändert.</summary>
public sealed record InboxChangedMessage(Guid TenantId, Guid MailId);

/// <summary>Meldet die Änderung an alle offenen Posteingänge des Spas. Die Oberfläche lädt danach selbst nach, mit ihren Rechten.</summary>
public static class InboxChangedHandler
{
    public static Task Handle(InboxChangedMessage message, IHubContext<LiveHub, ILiveClient> hub, ILogger<LiveHub> logger) =>
        logger.SendAsync("Posteingang", () => hub.Clients.Group(LiveHubContract.InboxGroup(message.TenantId)).InboxChanged(new InboxChangedEvent(message.MailId)));
}

/// <summary>
/// Merkt sich beim Speichern jede neue oder geänderte Nachricht im Posteingang und schickt nach erfolgreichem Speichern eine
/// <see cref="InboxChangedMessage"/> in die Queue. So erfasst es jede Stelle (Postfach, Webhooks, Queue, Rezeption), ohne dass sie
/// selbst daran denken muss.
/// </summary>
internal sealed class InboxChangeInterceptor(IServiceProvider services) : SaveChangesInterceptor
{
    private readonly List<InboxChangedMessage> _pending = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await PublishAsync();
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        PublishAsync().AsTask().GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pending.Clear();
        base.SaveChangesFailed(eventData);
    }

    private void Collect(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<InboundMail>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            // Der Mandant wird erst beim Speichern gestempelt (AispaDbContext), deshalb hier den des Aufrufs als Rückfall.
            var tenantId = entry.Property(TenantModel.Property).CurrentValue as Guid? is { } id && id != Guid.Empty
                ? id
                : services.GetRequiredService<TenantContext>().TenantId;
            if (tenantId is { } tenant)
            {
                _pending.Add(new InboxChangedMessage(tenant, entry.Entity.Id));
            }
        }
    }

    private async ValueTask PublishAsync()
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var messages = _pending.Distinct().ToList();
        _pending.Clear();
        var bus = services.GetRequiredService<IMessageBus>();
        foreach (var message in messages)
        {
            await bus.PublishAsync(message);
        }
    }
}
