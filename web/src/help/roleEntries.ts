/** Einstieg je Rolle auf der Startseite der Hilfe: die ersten Abläufe, die jemand in dieser Rolle braucht. */
export interface RoleEntry {
  /** Kennung wie in der Kopfangabe `rollen` eines Themas. */
  role: string;
  label: string;
  /** Kennungen der Themen in der Reihenfolge, in der man sie lernt. */
  topics: string[];
}

export const roleEntries: RoleEntry[] = [
  {
    role: "rezeption",
    label: "Rezeption",
    topics: [
      "kalender-termin-buchen",
      "kalender-freie-zeit-suchen",
      "kalender-status-setzen",
      "kassa-termin-kassieren",
      "kassa-tagesabschluss",
    ],
  },
  {
    role: "therapeut",
    label: "Therapeut:in",
    topics: [
      "kalender-status-setzen",
      "berichte-tagesplan-ansehen",
      "kalender-termin-finden",
      "hygiene-tagesstart-abhaken",
    ],
  },
  {
    role: "leitung",
    label: "Leitung",
    topics: [
      "kalender-termin-verschieben",
      "kalender-termin-stornieren",
      "kassa-beleg-stornieren",
      "kassa-tagesabschluss",
    ],
  },
  {
    role: "inhaber",
    label: "Inhaber:in",
    topics: [
      "kassa-tagesabschluss",
      "kassa-gutschein-verkaufen",
      "kassa-beleg-stornieren",
      "kalender-paar-und-spa-tag",
    ],
  },
];

/** Die Rolle des Kontexts zuerst; die Rolle `manager` (Geschäftsführung) liest den Einstieg der Leitung. */
export function orderedRoleEntries(role: string | undefined): RoleEntry[] {
  const own = role === "manager" ? "leitung" : role;
  return [...roleEntries].sort(
    (a, b) => Number(b.role === own) - Number(a.role === own),
  );
}

/**
 * Kennungen der Gruppe „Erste Schritte“ in der Navigation (T-028): mit Rolle nur deren Einstieg,
 * sonst die Einstiege aller Rollen, jede Kennung einmal und in Lernreihenfolge.
 */
export function firstStepIds(role: string | undefined): string[] {
  const own = role === "manager" ? "leitung" : role;
  const entries = own ? roleEntries.filter((e) => e.role === own) : roleEntries;
  return [...new Set(entries.flatMap((e) => e.topics))];
}
