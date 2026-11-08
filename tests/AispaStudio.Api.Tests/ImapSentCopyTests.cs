using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using AispaStudio.Api.MailIntake.Mailboxes;
using MailKit.Security;
using MimeKit;

namespace AispaStudio.Api.Tests;

/// <summary>Kopie in „Gesendet“ gegen einen kleinen nachgebildeten IMAP-Server: Ordner finden und die Mail ablegen.</summary>
public sealed partial class ImapSentCopyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Legt_die_Mail_im_als_Gesendet_gekennzeichneten_Ordner_ab()
    {
        await using var server = FakeImap.Start(specialUse: true, ("INBOX", ""), ("Gesendete Elemente", "\\Sent"), ("Sent", ""));
        await ImapSentCopy.AppendAsync(server.Options, null, Message(), Ct);

        var (folder, flags, body) = Assert.Single(server.Appended);
        Assert.Equal("Gesendete Elemente", folder);
        Assert.Contains("\\Seen", flags, StringComparison.Ordinal);
        Assert.Contains("Subject: Ihr Termin", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Kennzeichnung_nimmt_sie_einen_ueblichen_Namen_und_sonst_den_angegebenen_Ordner()
    {
        await using (var server = FakeImap.Start(specialUse: false, ("INBOX", ""), ("Gesendet", "")))
        {
            await ImapSentCopy.AppendAsync(server.Options, null, Message(), Ct);
            Assert.Equal("Gesendet", Assert.Single(server.Appended).Folder);
        }

        await using (var server = FakeImap.Start(specialUse: false, ("INBOX", ""), ("Ausgang/Kunden", "")))
        {
            var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => ImapSentCopy.AppendAsync(server.Options, null, Message(), Ct));
            Assert.Contains("Ordner beim Postfach angeben", missing.Message, StringComparison.Ordinal);
            await Assert.ThrowsAsync<InvalidOperationException>(() => ImapSentCopy.VerifyAsync(server.Options, null, Ct));
            await ImapSentCopy.VerifyAsync(server.Options, "Ausgang/Kunden", Ct);
            Assert.Empty(server.Appended);

            await ImapSentCopy.AppendAsync(server.Options, "Ausgang/Kunden", Message(), Ct);
            Assert.Equal("Ausgang/Kunden", Assert.Single(server.Appended).Folder);
        }
    }

    private static MimeMessage Message()
    {
        var message = new MimeMessage { Subject = "Ihr Termin", Body = new TextPart("plain") { Text = "Bis bald." } };
        message.From.Add(new MailboxAddress("Spa am See", "termine@spa.example"));
        message.To.Add(new MailboxAddress("Eva", "eva@gast.example"));
        return message;
    }

    /// <summary>Genug IMAP für MailKit: LOGIN, LIST, APPEND mit Literal; alles andere bekommt ein OK.</summary>
    private sealed partial class FakeImap : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly (string Name, string Attributes)[] _folders;
        private readonly bool _specialUse;
        private readonly Task _loop;

        private FakeImap(bool specialUse, (string, string)[] folders)
        {
            _specialUse = specialUse;
            _folders = folders;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _loop = Task.Run(AcceptAsync);
        }

        public List<(string Folder, string Flags, string Body)> Appended { get; } = [];

        public ImapOptions Options => new()
        {
            Host = "127.0.0.1",
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port,
            Security = SecureSocketOptions.None,
            UserName = "termine@spa.example",
            Password = "geheim",
        };

        public static FakeImap Start(bool specialUse, params (string, string)[] folders) => new(specialUse, folders);

        private string Capabilities => _specialUse ? "IMAP4rev1 SPECIAL-USE" : "IMAP4rev1";

        private async Task AcceptAsync()
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch (Exception ex) when (ex is SocketException or ObjectDisposedException or InvalidOperationException)
                {
                    return;
                }

                using (client)
                {
                    await ServeAsync(client.GetStream());
                }
            }
        }

        private async Task ServeAsync(NetworkStream stream)
        {
            async Task Send(string text) => await stream.WriteAsync(Encoding.UTF8.GetBytes(text + "\r\n"));
            await Send($"* OK [CAPABILITY {Capabilities}] Test-IMAP bereit");
            while (await ReadLineAsync(stream) is { } line)
            {
                var parts = line.Split(' ', 3);
                var (tag, command, rest) = (parts[0], parts.Length > 1 ? parts[1].ToUpperInvariant() : "", parts.Length > 2 ? parts[2] : "");
                switch (command)
                {
                    case "CAPABILITY":
                        await Send($"* CAPABILITY {Capabilities}");
                        break;
                    case "LIST":
                        foreach (var (name, attributes) in Matching(rest))
                        {
                            await Send($"* LIST ({attributes}) \"/\" \"{name}\"");
                        }

                        break;
                    case "APPEND":
                        var append = AppendPattern().Match(rest);
                        await Send("+ bereit");
                        var buffer = new byte[int.Parse(append.Groups["size"].Value, CultureInfo.InvariantCulture)];
                        await stream.ReadExactlyAsync(buffer);
                        await ReadLineAsync(stream);
                        Appended.Add((Unquote(append.Groups["folder"].Value), append.Groups["flags"].Value, Encoding.UTF8.GetString(buffer)));
                        break;
                    case "LOGOUT":
                        await Send("* BYE");
                        await Send($"{tag} OK fertig");
                        return;
                }

                await Send(command == "LOGIN" ? $"{tag} OK [CAPABILITY {Capabilities}] angemeldet" : $"{tag} OK erledigt");
            }
        }

        private IEnumerable<(string Name, string Attributes)> Matching(string arguments)
        {
            var match = ListPattern().Match(arguments);
            var pattern = Unquote(match.Groups["pattern"].Value);
            if (pattern.Length == 0)
            {
                return [("", "\\Noselect")];
            }

            return pattern is "*" or "%"
                ? _folders
                : _folders.Where(f => f.Name.Equals(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private static string Unquote(string value) => value.Length >= 2 && value[0] == '"' ? value[1..^1] : value;

        private static async Task<string?> ReadLineAsync(NetworkStream stream)
        {
            var bytes = new List<byte>();
            var one = new byte[1];
            while (await stream.ReadAsync(one) == 1)
            {
                if (one[0] == '\n')
                {
                    return Encoding.UTF8.GetString([.. bytes]).TrimEnd('\r');
                }

                bytes.Add(one[0]);
            }

            return null;
        }

        [GeneratedRegex("""^(?:\([^)]*\) )?(?<reference>"[^"]*"|\S+) (?<pattern>"[^"]*"|\S+)""")]
        private static partial Regex ListPattern();

        [GeneratedRegex("""^(?<folder>"[^"]*"|\S+) (?:\((?<flags>[^)]*)\) )?(?:"[^"]*" )?\{(?<size>\d+)\+?\}$""")]
        private static partial Regex AppendPattern();

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _loop;
            _listener.Dispose();
        }
    }
}
