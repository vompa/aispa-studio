using System.Text.Json;
using System.Text.Json.Serialization;

namespace AispaStudio.Client;

/// <summary>JSON-Einstellungen der API: Web-Standard, Aufzählungen als Text.</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
}
