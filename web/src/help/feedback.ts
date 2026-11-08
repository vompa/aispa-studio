/**
 * Rückmeldung je Hilfethema (Slice 55): „War das hilfreich?“ mit Ja/Nein und freiwilligem Text. Sie liegt nur im Speicher dieses
 * PCs bzw. Browsers (wie die KI-Rückmeldung und die offenen Fragen), ohne Person, Rolle, Seite oder Gästedaten; es gibt keine
 * zentrale Sammlung.
 */
export interface TopicFeedback {
  topic: string
  answer: 'yes' | 'no'
  text: string
  /** ISO-8601 in UTC. */
  at: string
}

const KEY = 'aispa.help.feedback'
export const FEEDBACK_TEXT_MAX = 500

const isEntry = (e: unknown): e is TopicFeedback => {
  const f = e as Partial<TopicFeedback> | null
  return typeof f?.topic === 'string' && (f.answer === 'yes' || f.answer === 'no') && typeof f.text === 'string' && typeof f.at === 'string'
}

/** Fremdes oder Kaputtes im Speicher fällt weg, damit Ablegen und Anzeigen nie werfen. */
export function listFeedback(): TopicFeedback[] {
  try {
    const stored: unknown = JSON.parse(localStorage.getItem(KEY) ?? '[]')
    return Array.isArray(stored) ? stored.filter(isEntry) : []
  } catch {
    return []
  }
}

export const hasFeedback = (topic: string) => listFeedback().some((f) => f.topic === topic)

/** Legt die Rückmeldung ab; je Thema gilt die letzte. */
export function recordFeedback(topic: string, answer: 'yes' | 'no', text: string, now = new Date()) {
  const entry: TopicFeedback = { topic, answer, text: text.trim().slice(0, FEEDBACK_TEXT_MAX), at: now.toISOString() }
  try {
    localStorage.setItem(KEY, JSON.stringify([...listFeedback().filter((f) => f.topic !== topic), entry]))
  } catch {
    // Ohne Speicher (privates Fenster, voll) geht die Rückmeldung verloren, die Arbeit geht weiter.
  }
}
