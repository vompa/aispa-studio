/**
 * Adresse der Hilfe-Seite (`/help/`): `?thema=<kennung>&abschnitt=<anker>`. Die Windows-App ergänzt `oberflaeche` und `rolle`,
 * damit die Suche passende Themen vorreiht, und eine laufende Nummer `n`, damit der Browser dieselbe Stelle noch einmal lädt.
 */
export interface HelpTarget {
  topic: string | null;
  section?: string;
}

export interface HelpPageParams extends HelpTarget {
  surface?: string;
  role?: string;
}

export function readHelpUrl(search: string): HelpPageParams {
  const q = new URLSearchParams(search);
  return {
    topic: q.get("thema"),
    section: q.get("abschnitt") ?? undefined,
    surface: q.get("oberflaeche") ?? undefined,
    role: q.get("rolle") ?? undefined,
  };
}

/** Die Adresse zu einem Ziel; Oberfläche und Rolle bleiben erhalten, `n` entfällt. */
export function buildHelpUrl(target: HelpTarget, keep: HelpPageParams): string {
  const q = new URLSearchParams();
  if (target.topic) q.set("thema", target.topic);
  if (target.topic && target.section) q.set("abschnitt", target.section);
  if (keep.surface) q.set("oberflaeche", keep.surface);
  if (keep.role) q.set("rolle", keep.role);
  const text = q.toString();
  return text ? `?${text}` : "";
}
