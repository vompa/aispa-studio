using AispaStudio.Api.Data;
using AispaStudio.Api.MailIntake.Assistant;
using AispaStudio.Api.Scheduling;
using AispaStudio.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace AispaStudio.Api.MailIntake;

/// <summary>
/// Die Rezeption entscheidet, wer der Absender einer Nachricht ist (ADR 0061, ADR 0008): Vorschlag übernehmen, neu anlegen oder die
/// Zuordnung lösen. Erst danach gelten Daten aus der Kundenkarte für Antwort und Buchung.
/// </summary>
internal static class InboxCustomerEndpoints
{
    public static RouteGroupBuilder MapInboxCustomerEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{mailId:guid}/customer/assign", async Task<Results<Ok<InboundMailDto>, NotFound, ValidationProblem>> (
                Guid mailId, AssignCustomerInput input, AispaDbContext db, MailIntakeProcessor processor, IMessageBus bus, IReplyReviser reviser,
                CancellationToken ct) =>
            {
                if (await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct) is not { } mail)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check(CanDecide(mail), "status", Settled);
                var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == input.CustomerId, ct);
                errors.Check(customer is not null, nameof(input.CustomerId), "Diesen Kunden gibt es nicht.");
                if (errors.Any || customer is null)
                {
                    return errors.Problem();
                }

                if (input.RememberContact)
                {
                    await RememberContactAsync(mail, customer, db, ct);
                }

                return await SettleAsync(mail, customer.Id, processor, bus, reviser, db, ct);
            })
            .WithName("AssignInboundMailCustomer")
            .WithSummary("Ordnet die Nachricht einem Kunden zu (ein Vorschlag oder ein anderer); optional merkt die Kundenkarte die Mailadresse bzw. Nummer des Absenders. "
                + "Erst danach nutzen Antwort und Buchung Daten der Kundenkarte.");

        group.MapPost("/{mailId:guid}/customer/new", async Task<Results<Ok<InboundMailDto>, NotFound, ValidationProblem>> (
                Guid mailId, NewCustomerFromMailInput? input, AispaDbContext db, MailIntakeProcessor processor, IMessageBus bus, IReplyReviser reviser,
                TimeProvider time, CancellationToken ct) =>
            {
                if (await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct) is not { } mail)
                {
                    return TypedResults.NotFound();
                }

                if (await ProvisionalGuestAsync(mail, db, ct) is { } provisional)
                {
                    // Der Gast steht schon (vorläufig) im Kalender: „bewusst neu“ heißt, ihn so zu bestätigen, nicht einen zweiten anzulegen.
                    if (input is not null)
                    {
                        provisional.FirstName = Pick(input.FirstName, provisional.FirstName) ?? "";
                        provisional.LastName = Pick(input.LastName, provisional.LastName) ?? "";
                        provisional.Email = Pick(input.Email, provisional.Email);
                        provisional.Phone = Pick(input.Phone, provisional.Phone);
                    }

                    await db.SaveChangesAsync(ct);
                    return await SettleAsync(mail, provisional.Id, processor, bus, reviser, db, ct);
                }

                var customer = MailIntakeProcessor.NewCustomer(mail, time.GetUtcNow());
                if (input is not null)
                {
                    customer.FirstName = Pick(input.FirstName, customer.FirstName) ?? "";
                    customer.LastName = Pick(input.LastName, customer.LastName) ?? "";
                    customer.Email = Pick(input.Email, customer.Email);
                    customer.Phone = Pick(input.Phone, customer.Phone);
                }

                var errors = new Errors();
                errors.Check(CanDecide(mail), "status", Settled);
                errors.Check(customer.FirstName.Length > 0 || customer.LastName.Length > 0, nameof(NewCustomerFromMailInput.LastName), "Vor- oder Nachname angeben.");
                errors.Check(customer.Email is not { } email || email.Contains('@', StringComparison.Ordinal), nameof(NewCustomerFromMailInput.Email), "Keine gültige E-Mail-Adresse.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                db.Customers.Add(customer);
                await db.SaveChangesAsync(ct);
                return await SettleAsync(mail, customer.Id, processor, bus, reviser, db, ct);
            })
            .WithName("CreateInboundMailCustomer")
            .WithSummary("Legt den Kunden neu an, vorbefüllt aus Name, Telefon und Mailadresse der Nachricht (Felder im Aufruf überschreiben), und ordnet die Nachricht zu. "
                + "Die Kundenkarte vermerkt, aus welchem Kanal er stammt.");

        group.MapPost("/{mailId:guid}/customer/release", async Task<Results<Ok<InboundMailDto>, NotFound, ValidationProblem>> (
                Guid mailId, AispaDbContext db, MailIntakeProcessor processor, IReplyReviser reviser, CancellationToken ct) =>
            {
                if (await db.InboundMails.FirstOrDefaultAsync(m => m.Id == mailId, ct) is not { } mail)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Errors();
                errors.Check(CanDecide(mail), "status", Settled);
                errors.Check(mail is { CustomerConfirmed: true, CustomerId: not null }, "customer", "Dieser Nachricht ist kein Kunde zugeordnet.");
                if (errors.Any)
                {
                    return errors.Problem();
                }

                await processor.ReleaseCustomerAsync(mail, ct);
                return TypedResults.Ok(MailIntakeEndpoints.ToDto(mail, reviser.Available));
            })
            .WithName("ReleaseInboundMailCustomer")
            .WithSummary("Löst die Zuordnung (der Kunde war es doch nicht). Der Kunde wird nicht mehr vorgeschlagen; die Nachricht wartet wieder auf eine Entscheidung.");

        return group;
    }

    private const string Settled = "Zu dieser Nachricht ist schon ein Termin eingetragen oder storniert, oder sie wird gerade verarbeitet; die Zuordnung lässt sich nicht mehr ändern.";

    /// <summary>Zuordnen, lösen und neu anlegen gehen nur, solange die Rezeption noch entscheidet.</summary>
    private static bool CanDecide(InboundMail mail) =>
        (mail.Status is InboundMailStatus.NeedsReview or InboundMailStatus.Proposed
            && (mail.AppointmentId is null || mail.Kind == InboundMailKind.Cancellation)
            || IsAssignmentOpen(mail))
        && !mail.Reproposing && mail.ReplyStatus != ReplyStatus.Sending;

    /// <summary>Treatwell-Buchung, die schon im Kalender steht, deren Gast aber noch nicht zugeordnet ist (ADR 0062).</summary>
    private static bool IsAssignmentOpen(InboundMail mail) =>
        mail is { Channel: BookingChannel.Treatwell, Kind: InboundMailKind.TreatwellBooking, Status: InboundMailStatus.Booked, AppointmentId: not null, CustomerConfirmed: false, CustomerId: not null };

    private static Task<Customer?> ProvisionalGuestAsync(InboundMail mail, AispaDbContext db, CancellationToken ct) =>
        IsAssignmentOpen(mail)
            ? db.Customers.FirstOrDefaultAsync(c => c.Id == mail.CustomerId && c.AssignmentOpen, ct)
            : Task.FromResult<Customer?>(null);

    private static string? Pick(string? given, string? fallback) => string.IsNullOrWhiteSpace(given) ? fallback : given.Trim();

    private static async Task<Results<Ok<InboundMailDto>, NotFound, ValidationProblem>> SettleAsync(
        InboundMail mail, Guid customerId, MailIntakeProcessor processor, IMessageBus bus, IReplyReviser reviser, AispaDbContext db, CancellationToken ct)
    {
        if (await ProvisionalGuestAsync(mail, db, ct) is { } provisional)
        {
            // Der Termin steht schon im Kalender: er wandert auf den gewählten Kunden, der vorläufige Gast entfällt (ADR 0062).
            if (await processor.RelinkTreatwellGuestAsync(mail, provisional.Id, customerId, ct) is { } problem)
            {
                var errors = new Errors();
                errors.Add("customer", problem);
                return errors.Problem();
            }

            return TypedResults.Ok(MailIntakeEndpoints.ToDto(mail, reviser.Available));
        }

        mail.CustomerId = customerId;
        mail.CustomerConfirmed = true;
        mail.CustomerCandidates = [];
        var rerun = mail is { Kind: InboundMailKind.TreatwellBooking, AppointmentId: null, Proposals.Count: 0 };
        if (rerun)
        {
            // Die Treatwell-Buchung wartete nur auf den Kunden: jetzt in den Kalender übernehmen (Queue).
            mail.Status = InboundMailStatus.Received;
            mail.Note = null;
            await db.SaveChangesAsync(ct);
            await bus.PublishAsync(new InboundMailReceived(mail.Id));
        }
        else
        {
            await processor.ApplyCustomerAsync(mail, ct);
        }

        return TypedResults.Ok(MailIntakeEndpoints.ToDto(mail, reviser.Available));
    }

    /// <summary>
    /// Mailadresse bzw. Nummer des Absenders in der Kundenkarte merken, damit die nächste Nachricht von dort sicher zugeordnet wird.
    /// Eine Mailadresse wird die Hauptadresse, wenn es noch keine gibt, sonst eine weitere. Eine Nummer wird nur eingetragen, wenn die
    /// Kundenkarte noch keine hat (je Kunde gibt es eine Nummer).
    /// </summary>
    private static async Task RememberContactAsync(InboundMail mail, Customer customer, AispaDbContext db, CancellationToken ct)
    {
        if (MailIntakeProcessor.IsShortMessage(mail.Channel))
        {
            if (string.IsNullOrWhiteSpace(customer.Phone))
            {
                customer.Phone = mail.From.Length <= 50 ? mail.From : mail.From[..50];
            }

            return;
        }

        var address = (mail.Channel == BookingChannel.Treatwell ? mail.GuestEmail : mail.From)?.Trim();
        if (address is not { Length: > 0 and <= 256 } || !address.Contains('@', StringComparison.Ordinal))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            customer.Email = address;
        }
        else if (!string.Equals(customer.Email.Trim(), address, StringComparison.OrdinalIgnoreCase))
        {
            var normalized = CustomerEmail.Normalize(address);
            if (!await db.CustomerEmails.AnyAsync(e => e.CustomerId == customer.Id && e.NormalizedAddress == normalized, ct))
            {
                db.CustomerEmails.Add(new CustomerEmail { CustomerId = customer.Id, Address = address, NormalizedAddress = normalized });
            }
        }
    }
}
