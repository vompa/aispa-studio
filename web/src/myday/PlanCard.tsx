import { useState } from 'react'
import { api, type CustomerPreferences, type RestockLine, type TreatmentCard } from '../api/client'
import { amount, guestName, parseAmount, problemText, shortDate, timeIn } from './format'

const PREFERENCES: { key: Exclude<keyof CustomerPreferences, 'prefersQuiet'>; label: string }[] = [
  { key: 'pressure', label: 'Druck' },
  { key: 'scent', label: 'Duft' },
  { key: 'roomTemperature', label: 'Raumtemperatur' },
  { key: 'music', label: 'Musik' },
  { key: 'focusAreas', label: 'Fokus' },
  { key: 'avoidAreas', label: 'Nicht behandeln' },
  { key: 'other', label: 'Sonstiges' },
]

const STATUS_LABEL: Partial<Record<TreatmentCard['status'], string>> = {
  CheckedIn: 'Eingecheckt',
  Completed: 'Abgeschlossen',
  NoShow: 'Nicht erschienen',
  Cancelled: 'Storniert',
}

export type Highlight = 'now' | 'next' | null

type Message = { text: string; error: boolean }

/** Eine Behandlungskarte. Abhaken ist optimistisch und wird bei einem Fehler zurückgenommen. */
export function PlanCard({
  card,
  timeZone,
  isToday,
  highlight,
  offline,
  onStep,
  onRestock,
  onReported,
}: {
  card: TreatmentCard
  timeZone: string
  isToday: boolean
  highlight: Highlight
  offline: boolean
  onStep: (appointmentId: string, stepId: string, done: boolean) => void
  onRestock: (appointmentId: string, lines: RestockLine[]) => void
  onReported: () => void
}) {
  const [note, setNote] = useState(card.followUpNote ?? '')
  const [saving, setSaving] = useState(false)
  const [pending, setPending] = useState<ReadonlySet<string>>(new Set())
  const [message, setMessage] = useState<Message | null>(null)
  const [amounts, setAmounts] = useState<Record<string, string>>({})
  const [answering, setAnswering] = useState<ReadonlySet<string>>(new Set())
  const [reporting, setReporting] = useState(false)
  const start = timeIn(card.start, timeZone)
  const headingId = `card-${card.appointmentId}`
  const steps = card.todo.filter((t) => t.stepId)
  const preferences = PREFERENCES.filter((p) => card.preferences[p.key])
  const lastNote = card.history.find((h) => h.note)
  // Raumcheck (ADR 0068): erst nach dem Abschluss der Behandlung.
  const restock = card.status === 'Completed' ? (card.restock ?? []) : []
  const openLines = restock.filter((l) => l.state === 'Open').length
  const running = card.status === 'CheckedIn' && Boolean(card.startedAt)
  const canReport = isToday && (card.status === 'Booked' || card.status === 'CheckedIn')

  /** Die API prüft Person, Tag und Reihenfolge; danach lädt die App den Plan neu, damit Status und Raumcheck vom Server kommen. */
  async function report() {
    if (offline) {
      setMessage({ text: 'Nur mit Verbindung.', error: true })
      return
    }
    if (reporting) return
    setReporting(true)
    const path = { params: { path: { appointmentId: card.appointmentId } } }
    try {
      const { data, error } = running ? await api.POST('/api/v1/appointments/{appointmentId}/finish', path) : await api.POST('/api/v1/appointments/{appointmentId}/begin', path)
      if (data) {
        setMessage(null)
        onReported()
      } else {
        setMessage({ text: problemText(error, 'Das konnte nicht gemeldet werden.'), error: true })
      }
    } catch {
      setMessage({ text: 'Keine Verbindung. Noch nicht gemeldet.', error: true })
    } finally {
      setReporting(false)
    }
  }

  async function toggle(stepId: string, text: string, done: boolean) {
    if (offline || pending.has(stepId)) return
    setPending((p) => new Set(p).add(stepId))
    onStep(card.appointmentId, stepId, done)
    let ok = false
    try {
      const { response } = await api.PUT('/api/v1/reports/appointments/{appointmentId}/preparation/{stepId}', {
        params: { path: { appointmentId: card.appointmentId, stepId } },
        body: { done },
      })
      ok = response.ok
    } catch {
      ok = false
    }
    if (!ok) {
      onStep(card.appointmentId, stepId, !done)
      setMessage({ text: `„${text}“ konnte nicht gespeichert werden. Bitte noch einmal versuchen.`, error: true })
    } else {
      setMessage(null)
    }
    setPending((p) => {
      const next = new Set(p)
      next.delete(stepId)
      return next
    })
  }

  /** Raumcheck: „Aufgefüllt“ bucht der Server vom Hauptlager in den Raumschrank, „Reicht noch“ speichert nur die Antwort. */
  async function answer(line: RestockLine, state: 'Refilled' | 'Enough') {
    if (offline) {
      setMessage({ text: 'Nur mit Verbindung.', error: true })
      return
    }
    if (answering.has(line.itemId)) return
    const quantity = state === 'Refilled' ? parseAmount(amounts[line.itemId] ?? String(line.suggested)) : null
    if (state === 'Refilled' && (quantity === null || quantity <= 0)) {
      setMessage({ text: `Menge für ${line.itemName} angeben.`, error: true })
      return
    }
    setAnswering((a) => new Set(a).add(line.itemId))
    try {
      const { data, error } = await api.PUT('/api/v1/appointments/{appointmentId}/room-care/restock/{itemId}', {
        params: { path: { appointmentId: card.appointmentId, itemId: line.itemId } },
        body: { state, quantity },
      })
      if (data) {
        onRestock(card.appointmentId, data.restock ?? [])
        setMessage(state === 'Refilled' ? { text: `${line.itemName} aufgefüllt.`, error: false } : null)
      } else {
        setMessage({ text: problemText(error, 'Die Antwort konnte nicht gespeichert werden.'), error: true })
      }
    } catch {
      setMessage({ text: 'Keine Verbindung. Die Antwort ist noch nicht gespeichert.', error: true })
    } finally {
      setAnswering((a) => {
        const next = new Set(a)
        next.delete(line.itemId)
        return next
      })
    }
  }

  async function saveNote() {
    if (offline || saving) return
    setSaving(true)
    try {
      const { error, response } = await api.PUT('/api/v1/reports/appointments/{appointmentId}/follow-up', {
        params: { path: { appointmentId: card.appointmentId } },
        body: { note },
      })
      setMessage(response.ok ? { text: 'Notiz gespeichert.', error: false } : { text: problemText(error, 'Notiz konnte nicht gespeichert werden.'), error: true })
    } catch {
      setMessage({ text: 'Keine Verbindung. Die Notiz ist noch nicht gespeichert.', error: true })
    } finally {
      setSaving(false)
    }
  }

  return (
    <article className={`panel card${highlight ? ' highlight' : ''}`} aria-labelledby={headingId} aria-current={highlight ? 'time' : undefined}>
      {highlight && <p className="badge">{highlight === 'now' ? 'Läuft gerade' : 'Als Nächstes'}</p>}
      <h3 id={headingId}>
        <span className="card-time">{start}</span> {card.treatmentName}
      </h3>
      <p className="card-meta">
        bis {timeIn(card.end, timeZone)} ({card.durationMinutes} Min.) · {card.roomName}
        {card.equipmentName && ` · ${card.equipmentName}`}
      </p>
      <p className="card-guest">
        <strong>{guestName(card.customerName)}</strong> · {card.visitNumber === 1 ? 'Erstbesuch' : `${card.visitNumber}. Besuch`}
        {card.occasion && ` · ${card.occasion}`}
      </p>
      {running ? <p className="muted">Läuft seit {timeIn(card.startedAt!, timeZone)}</p> : STATUS_LABEL[card.status] && <p className="muted">{STATUS_LABEL[card.status]}</p>}
      {canReport && (
        <div className="actions">
          <button type="button" aria-disabled={offline || reporting || undefined} onClick={() => void report()}>
            {running ? 'Beendet melden' : 'Begonnen melden'}
          </button>
        </div>
      )}
      {card.allergyFlag && <p className="allergy">Allergie angegeben: bitte den Fragebogen beachten.</p>}
      <p className="muted">{card.anamnesisStatus}</p>
      {card.bookingNote && <p>Hinweis zur Buchung: {card.bookingNote}</p>}

      {restock.length > 0 && (
        <>
          <h4>
            Raum prüfen {openLines > 0 && <span className="muted">({openLines} offen)</span>}
          </h4>
          <ul className="restock">
            {restock.map((l) => (
              <li key={l.itemId} className={l.state === 'Open' ? undefined : 'answered'}>
                <p>
                  {l.itemName}: {amount(l.quantity)} von {amount(l.minimum)} {l.unit}
                  {l.state === 'Open' && `, ${amount(l.suggested)} ${l.unit} nachfüllen`}
                  {l.state === 'Refilled' && <span className="pill ok">aufgefüllt</span>}
                  {l.state === 'Enough' && <span className="pill">reicht</span>}
                </p>
                {l.state === 'Open' && (
                  <div className="restock-row">
                    <div className="field compact">
                      <label htmlFor={`amount-${card.appointmentId}-${l.itemId}`}>Menge ({l.unit})</label>
                      <input
                        id={`amount-${card.appointmentId}-${l.itemId}`}
                        type="text"
                        inputMode="decimal"
                        value={amounts[l.itemId] ?? String(l.suggested).replace('.', ',')}
                        readOnly={offline}
                        onChange={(e) => setAmounts((a) => ({ ...a, [l.itemId]: e.target.value }))}
                      />
                    </div>
                    <div className="actions">
                      <button
                        type="button"
                        className="ok"
                        aria-label={`Aufgefüllt: ${l.itemName}`}
                        aria-disabled={offline || answering.has(l.itemId) || undefined}
                        onClick={() => void answer(l, 'Refilled')}
                      >
                        Aufgefüllt
                      </button>
                      <button
                        type="button"
                        className="secondary"
                        aria-label={`Reicht noch: ${l.itemName}`}
                        aria-disabled={offline || answering.has(l.itemId) || undefined}
                        onClick={() => void answer(l, 'Enough')}
                      >
                        Reicht noch
                      </button>
                    </div>
                  </div>
                )}
              </li>
            ))}
          </ul>
        </>
      )}

      {(preferences.length > 0 || card.preferences.prefersQuiet) && (
        <>
          <h4>Vorlieben</h4>
          <dl className="prefs">
            {preferences.map((p) => (
              <div key={p.key}>
                <dt>{p.label}</dt>
                <dd>{card.preferences[p.key]}</dd>
              </div>
            ))}
            {card.preferences.prefersQuiet && (
              <div>
                <dt>Gespräch</dt>
                <dd>Lieber wenig</dd>
              </div>
            )}
          </dl>
        </>
      )}

      {lastNote && (
        <>
          <h4>Vom letzten Mal</h4>
          <p>
            {lastNote.note} <span className="muted">({shortDate(lastNote.start, timeZone)}, {lastNote.employeeName})</span>
          </p>
        </>
      )}

      {card.todo.length > 0 && (
        <>
          <h4>
            Vorbereitung{' '}
            {steps.length > 0 && (
              <span className="muted">
                ({steps.filter((s) => s.done).length} von {steps.length} erledigt)
              </span>
            )}
          </h4>
          <ul className="todo">
            {card.todo.map((t, i) =>
              t.stepId ? (
                <li key={t.stepId}>
                  <label className="check">
                    <input
                      type="checkbox"
                      checked={t.done}
                      aria-disabled={offline || undefined}
                      onChange={(e) => void toggle(t.stepId!, t.text, e.target.checked)}
                    />
                    <span>
                      {t.text}
                      {t.dueAt && <span className="muted"> · ab {timeIn(t.dueAt, timeZone)}</span>}
                    </span>
                  </label>
                </li>
              ) : (
                <li key={`${t.source}-${i}`} className="todo-text">
                  {t.text} <span className="muted">({t.source})</span>
                </li>
              ),
            )}
          </ul>
        </>
      )}

      <div className="field">
        <label htmlFor={`note-${card.appointmentId}`}>Notiz für die nächste Behandlung</label>
        <textarea
          id={`note-${card.appointmentId}`}
          rows={3}
          maxLength={2000}
          value={note}
          readOnly={offline}
          onChange={(e) => setNote(e.target.value)}
        />
      </div>
      <div className="actions">
        <button type="button" className="secondary" aria-disabled={offline || saving} onClick={saveNote}>
          Notiz speichern
        </button>
      </div>
      <p role="status" className={message?.error ? 'message error' : 'message'}>
        {message?.text}
      </p>
    </article>
  )
}
