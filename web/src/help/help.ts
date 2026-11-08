import { useSyncExternalStore } from "react";

/** Was die Hilfe gerade zeigt: ein Thema, darin optional ein Abschnitt; `topic: null` ist die Übersicht. */
export interface HelpRequest {
  topic: string | null;
  section?: string;
}

let current: HelpRequest | null = null;
const listeners = new Set<() => void>();

function set(next: HelpRequest | null) {
  current = next;
  listeners.forEach((l) => l());
}

/** Öffnet die Hilfe von überall, auch aus Seiten ohne Zugriff auf den Rahmen der App. */
export const openHelp = (topic: string | null, section?: string) =>
  set({ topic, section });
export const closeHelp = () => set(null);

export function useHelpRequest(): HelpRequest | null {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    () => current,
  );
}
