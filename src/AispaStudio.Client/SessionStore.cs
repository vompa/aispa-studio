namespace AispaStudio.Client;

/// <summary>Merkt das Anmelde-Cookie zwischen zwei Starts der App.</summary>
public interface ISessionStore
{
    string? Load();

    void Save(string cookie);

    void Clear();
}

/// <summary>Nur im Speicher: nach dem Beenden ist eine neue Anmeldung nötig (Tests, andere Betriebssysteme).</summary>
public sealed class MemorySessionStore : ISessionStore
{
    private string? _cookie;

    public string? Load() => _cookie;

    public void Save(string cookie) => _cookie = cookie;

    public void Clear() => _cookie = null;
}
