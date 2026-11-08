import { openHelp } from "./help";

/**
 * Fragezeichen neben einer Einstellung; öffnet die Hilfe genau bei deren Beschreibung. Das Zeichen kommt aus dem CSS,
 * damit der Text eines Feldes oder einer Überschrift, in dem der Knopf steht, gleich bleibt.
 */
export function HelpButton({
  topic,
  section,
  label,
}: {
  topic: string;
  section?: string;
  label?: string;
}) {
  return (
    <button
      type="button"
      className="help-button"
      title="Hilfe"
      aria-label={`Hilfe${label ? ` zu ${label}` : ""}`}
      onClick={() => openHelp(topic, section)}
    />
  );
}
