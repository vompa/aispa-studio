namespace AispaStudio.Api.Sync;

/// <summary>Eine Zeile aus <c>sync.history</c>, wie <see cref="EntityHistory"/>, <c>AppointmentHistory</c> und <see cref="ActivityFeed"/> sie lesen (ADR 0082).</summary>
internal sealed class HistoryRow
{
    public long Seq { get; init; }
    public required string Schema { get; init; }
    public required string Table { get; init; }
    public required string Operation { get; init; }
    public DateTimeOffset ChangedAt { get; init; }
    public string? UserId { get; init; }
    public required string Channel { get; init; }
    public Guid? CustomerId { get; init; }
    public string? Subject { get; init; }
    public string? RowKey { get; init; }
    public string? Changes { get; init; }
    public string? Context { get; init; }

    /// <summary>Die Spalten dieser Klasse als SELECT-Liste für <c>sync.history h</c>.</summary>
    public const string Columns = """
        h.seq AS "Seq", h.table_schema AS "Schema", h.table_name AS "Table", h.operation::text AS "Operation", h.changed_at AS "ChangedAt", h.user_id AS "UserId",
        h.channel AS "Channel", h.customer_id AS "CustomerId", h.subject AS "Subject", h.row_key::text AS "RowKey", h.changes::text AS "Changes", h.context::text AS "Context"
        """;
}
