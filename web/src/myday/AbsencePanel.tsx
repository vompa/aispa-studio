import { type FormEvent, useCallback, useEffect, useState } from 'react'
import { api, type AbsenceRequestInput, type MyAbsenceRequestDto } from '../api/client'
import { addDays, dayLabel, problemText } from './format'

const KINDS: { value: AbsenceRequestInput['kind']; label: string }[] = [
  { value: 'Vacation', label: 'Urlaub' },
  { value: 'DayOff', label: 'Frei' },
  { value: 'Training', label: 'Schulung' },
  { value: 'Sick', label: 'Krankenstand' },
]

const STATE_LABEL: Record<MyAbsenceRequestDto['state'], string> = {
  Open: 'Offen',
  Approved: 'Genehmigt',
  Rejected: 'Abgelehnt',
}

/** Abwesenheit beantragen und die eigenen Anträge mit Entscheidung sehen. Geht nur mit Verbindung; entschieden wird in der Windows-App. */
export function AbsencePanel({ today, onSignedOut }: { today: string; onSignedOut: () => void }) {
  const [requests, setRequests] = useState<MyAbsenceRequestDto[] | null>(null)
  const [loadProblem, setLoadProblem] = useState<string | null>(null)
  const [kind, setKind] = useState<AbsenceRequestInput['kind']>('Vacation')
  const [from, setFrom] = useState(() => addDays(today, 1))
  const [until, setUntil] = useState(() => addDays(today, 1))
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ text: string; error: boolean } | null>(null)

  const load = useCallback(() => {
    api.GET('/api/v1/absence-requests/mine').then(
      ({ data, response }) => {
        if (response.status === 401) return onSignedOut()
        setLoadProblem(data ? null : 'Die Anträge konnten nicht geladen werden.')
        setRequests(data ?? null)
      },
      () => setLoadProblem('Anträge gehen nur mit Verbindung.'),
    )
  }, [onSignedOut])

  useEffect(load, [load])

  function changeFrom(value: string) {
    setFrom(value)
    if (value && until < value) setUntil(value)
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (busy) return
    setBusy(true)
    try {
      const { data, error } = await api.POST('/api/v1/absence-requests', { body: { kind, from, until, note: note.trim() || null } })
      if (data) {
        setNote('')
        setMessage({ text: 'Antrag gestellt. Die Entscheidung steht danach hier in der Liste.', error: false })
        load()
      } else {
        setMessage({ text: problemText(error, 'Der Antrag konnte nicht gestellt werden.'), error: true })
      }
    } catch {
      setMessage({ text: 'Keine Verbindung. Der Antrag ist noch nicht gestellt.', error: true })
    } finally {
      setBusy(false)
    }
  }

  return (
    <section aria-labelledby="absence-heading">
      <h2 id="absence-heading">Abwesenheit beantragen</h2>
      {loadProblem && (
        <div className="notice" role="status">
          <p>
            <strong>{loadProblem}</strong>
          </p>
        </div>
      )}
      <form className="panel" onSubmit={(e) => void submit(e)} noValidate>
        <div className="field">
          <label htmlFor="absence-kind">Art</label>
          <select id="absence-kind" value={kind} onChange={(e) => setKind(e.target.value as AbsenceRequestInput['kind'])}>
            {KINDS.map((k) => (
              <option key={k.value} value={k.value}>
                {k.label}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="absence-from">Von</label>
          <input id="absence-from" type="date" value={from} min={today} onChange={(e) => changeFrom(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="absence-until">Bis</label>
          <input id="absence-until" type="date" value={until} min={from} onChange={(e) => setUntil(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="absence-note">Anmerkung für die Leitung (optional)</label>
          <textarea id="absence-note" rows={2} maxLength={500} value={note} onChange={(e) => setNote(e.target.value)} />
        </div>
        <div className="actions">
          <button type="submit" aria-disabled={busy || undefined}>
            Antrag stellen
          </button>
        </div>
        <p role="status" className={message?.error ? 'message error' : 'message'}>
          {message?.text}
        </p>
      </form>

      <h3>Meine Anträge</h3>
      {requests?.length === 0 && <p className="empty">Noch keine Anträge</p>}
      {requests && requests.length > 0 && (
        <ul className="requests">
          {requests.map((r) => (
            <li key={r.id} className="panel">
              <p>
                <strong>{KINDS.find((k) => k.value === r.kind)?.label}</strong>
                <span className={`pill state-${r.state.toLowerCase()}`}>{STATE_LABEL[r.state]}</span>
              </p>
              <p>{r.from === r.until ? dayLabel(r.from) : `${dayLabel(r.from)} bis ${dayLabel(r.until)}`}</p>
              {r.note && <p className="muted">{r.note}</p>}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
