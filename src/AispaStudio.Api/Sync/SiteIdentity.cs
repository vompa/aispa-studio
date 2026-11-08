using AispaStudio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AispaStudio.Api.Sync;

/// <summary>
/// Kennung dieser Datenbank aus <c>sync.site</c> (ADR 0041). Jede Datenbank, ob Einzelplatz oder Server, hat genau eine; sie wird
/// nie abgeglichen. Kassen gehören einer Stelle, damit nur dort Belege entstehen und die Signaturkette lückenlos bleibt.
/// </summary>
public sealed class SiteIdentity
{
    private Guid? _id;

    /// <summary>Die Kennung oder <c>null</c>, solange das Änderungsprotokoll nicht eingerichtet ist.</summary>
    public Guid? Get(AispaDbContext db)
    {
        if (_id is null && db.Database.SqlQueryRaw<bool>("SELECT to_regclass('sync.site') IS NOT NULL AS \"Value\"").Single())
        {
            _id = db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM sync.site").Single();
        }

        return _id;
    }
}
