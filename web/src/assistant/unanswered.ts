/**
 * Offene Fragen an die Hilfe (Slice 56): Fragen, auf die der Assistent nichts gefunden hat, für die Leitung. Sie liegen nur im Speicher dieses
 * Browsers, ohne Person und Standort; den Text hat der Server schon bereinigt (Kundennamen, E-Mail, Telefon). Nach 90 Tagen fallen sie weg.
 */
export interface UnansweredQuestion {
  question: string
  page: string | null
  /** Zuletzt gefragt, ISO-8601 in UTC. */
  at: string
  count: number
}

const KEY = 'aispa.help.unanswered'
const KEEP_DAYS = 90
const DAY_MS = 24 * 60 * 60 * 1000

const isEntry = (e: unknown): e is UnansweredQuestion => {
  const q = e as Partial<UnansweredQuestion> | null
  return (
    typeof q?.question === 'string' && (q.page === null || typeof q.page === 'string') && typeof q.at === 'string' && typeof q.count === 'number'
  )
}

/** Fremdes oder Kaputtes im Speicher fällt weg, damit Ablegen und Anzeigen nie werfen. */
function load(): UnansweredQuestion[] {
  try {
    const stored: unknown = JSON.parse(localStorage.getItem(KEY) ?? '[]')
    return Array.isArray(stored) ? stored.filter(isEntry) : []
  } catch {
    return []
  }
}

function save(entries: UnansweredQuestion[]) {
  try {
    localStorage.setItem(KEY, JSON.stringify(entries))
  } catch {
    // Ohne Speicher (privates Fenster, voll) geht die Frage verloren, die Arbeit geht weiter.
  }
}

const same = (a: UnansweredQuestion, question: string, page: string | null) =>
  a.page === page && a.question.toLocaleLowerCase('de') === question.toLocaleLowerCase('de')

const fresh = (entries: UnansweredQuestion[], now: Date) =>
  entries.filter((e) => now.getTime() - new Date(e.at).getTime() <= KEEP_DAYS * DAY_MS)

/** Legt die Frage ab; dieselbe Frage auf derselben Seite zählt nur hoch und bekommt den neuen Zeitpunkt. */
export function recordUnanswered(question: string, page: string | null, now = new Date()) {
  const entries = fresh(load(), now)
  const known = entries.find((e) => same(e, question, page))
  const entry = { question, page, at: now.toISOString(), count: (known?.count ?? 0) + 1 }
  save([entry, ...entries.filter((e) => e !== known)])
}

/** Die Fragen der letzten 90 Tage, die neueste zuerst. */
export function listUnanswered(now = new Date()): UnansweredQuestion[] {
  return fresh(load(), now).sort((a, b) => b.at.localeCompare(a.at))
}

/** „Erledigt“: der Eintrag wird gelöscht. */
export function removeUnanswered(entry: UnansweredQuestion) {
  save(load().filter((e) => !same(e, entry.question, entry.page)))
}
