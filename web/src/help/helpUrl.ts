/**
 * Adresse der Hilfe-Seite (`/help/`): `?thema=<kennung>&abschnitt=<anker>`. Die Windows-App ergänzt `oberflaeche` und `rolle`,
 * damit die Suche passende Themen vorreiht, und eine laufende Nummer `n`, damit der Browser dieselbe Stelle noch einmal lädt.
 * `ansicht=leiste` zeigt nur das Thema oder den Abschnitt, ohne Navigation: die Hilfe in der Leiste „KI und Hilfe“ der Windows-App.
 */
export interface HelpTarget {
  topic: string | null;
  section?: string;
}

export interface HelpPageParams extends HelpTarget {
  surface?: string;
  role?: string;
  /** `leiste`: schmale Ansicht für die Leiste „KI und Hilfe“ (ADR 0079, Nachtrag). */
  view?: "leiste";
  /** `einzelplatz`: die Windows-App läuft im Einzelplatz; Themen mit `betrieb: server` fehlen (T-110). */
  operation?: "einzelplatz";
}

export function readHelpUrl(search: string): HelpPageParams {
  const q = new URLSearchParams(search);
  return {
    topic: q.get("thema"),
    section: q.get("abschnitt") ?? undefined,
    surface: q.get("oberflaeche") ?? undefined,
    role: q.get("rolle") ?? undefined,
    view: q.get("ansicht") === "leiste" ? "leiste" : undefined,
    operation: q.get("betrieb") === "einzelplatz" ? "einzelplatz" : undefined,
  };
}

/** Die Adresse zu einem Ziel; Oberfläche, Rolle, Ansicht und Betrieb bleiben erhalten, `n` entfällt. */
export function buildHelpUrl(target: HelpTarget, keep: HelpPageParams): string {
  const q = new URLSearchParams();
  if (target.topic) q.set("thema", target.topic);
  if (target.topic && target.section) q.set("abschnitt", target.section);
  if (keep.surface) q.set("oberflaeche", keep.surface);
  if (keep.role) q.set("rolle", keep.role);
  if (keep.view) q.set("ansicht", keep.view);
  if (keep.operation) q.set("betrieb", keep.operation);
  const text = q.toString();
  return text ? `?${text}` : "";
}
