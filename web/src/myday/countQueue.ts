// Mengen der Inventur, die noch nicht beim Server sind (kein Netz im Lagerraum). Sie liegen im Speicher des Browsers,
// bis sie gesendet sind, und nur für die laufende Zählung. Abmelden löscht sie (offline.ts).

const PREFIX = 'aispa-mein-tag-count:'

type Pending = Record<string, string>

function read(sessionId: string): Pending {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(PREFIX + sessionId) ?? '{}')
    return parsed && typeof parsed === 'object' ? (parsed as Pending) : {}
  } catch {
    return {}
  }
}

function write(sessionId: string, pending: Pending) {
  try {
    if (Object.keys(pending).length === 0) localStorage.removeItem(PREFIX + sessionId)
    else localStorage.setItem(PREFIX + sessionId, JSON.stringify(pending))
  } catch {
    // Speicher voll oder gesperrt: Die Zeile bleibt in der Maske stehen und wird beim nächsten Versuch gesendet.
  }
}

/** Was von dieser Zählung noch nicht gesendet ist: Text des Zählfelds je Material. */
export const pendingCounts = (sessionId: string): Pending => read(sessionId)

/** Eine getippte Menge festhalten, bis der Server sie bestätigt hat. */
export function keepCount(sessionId: string, itemId: string, text: string) {
  write(sessionId, { ...read(sessionId), [itemId]: text })
}

/** Der Server hat `text` gespeichert; wurde inzwischen weitergetippt, bleibt der neuere Stand stehen. */
export function countSaved(sessionId: string, itemId: string, text: string) {
  const pending = read(sessionId)
  if (pending[itemId] !== text) return
  delete pending[itemId]
  write(sessionId, pending)
}

/** Alles Gemerkte löschen; mit `keepSessionId` bleibt nur diese Zählung (ältere sind abgeschlossen oder verworfen). */
export function clearCountQueue(keepSessionId?: string) {
  try {
    for (let i = localStorage.length - 1; i >= 0; i--) {
      const key = localStorage.key(i)
      if (key?.startsWith(PREFIX) && key !== PREFIX + keepSessionId) localStorage.removeItem(key)
    }
  } catch {
    // Kein Speicher verfügbar.
  }
}
