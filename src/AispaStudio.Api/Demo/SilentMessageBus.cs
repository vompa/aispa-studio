using System.Reflection;
using Wolverine;

namespace AispaStudio.Api.Demo;

/// <summary>
/// Nachrichtenbus, der alles verwirft. Das Einspielen des Beispiel-Spas bucht Termine über den <see cref="Scheduling.BookingService"/>, der
/// jede Änderung in die Queue stellt (Live-Anzeige, Lagerabgleich, Warteliste, Serien). Das Einspielen gleicht das Lager selbst ab;
/// die Nachrichten würden tausendfach Zeit und Datenbank kosten und mit dem Einspielen um dieselben Zeilen streiten.
/// </summary>
internal class SilentMessageBus : DispatchProxy
{
    public static IMessageBus Create() => Create<IMessageBus, SilentMessageBus>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var type = targetMethod?.ReturnType;
        if (type is null || type == typeof(void))
        {
            return null;
        }

        if (type == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (type == typeof(ValueTask))
        {
            return default(ValueTask);
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type.GetGenericArguments()[0]).Invoke(null, [DefaultOf(type.GetGenericArguments()[0])]);
        }

        throw new NotSupportedException($"Der stumme Nachrichtenbus kennt {targetMethod!.Name} nicht (Rückgabe {type.Name}).");
    }

    private static object? DefaultOf(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
}
