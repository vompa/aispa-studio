import { type CSSProperties } from 'react'
import type { components } from '../api/schema'
import { timeIn } from './format'

type Board = components['schemas']['DisplayBoard']
type Person = components['schemas']['DisplayPerson']
type Span = components['schemas']['DisplaySpan']

const HOUR_MS = 3_600_000

const STATE_TEXT: Record<Person['state'], string> = {
  InTreatment: 'in Behandlung',
  Free: 'frei',
  OffShift: 'nicht im Dienst',
}

/** Sichtbarer Bereich der Tagesleisten: von der ersten bis zur letzten Stunde, in der jemand Dienst hat oder einen Termin. */
function timeline(board: Board) {
  const spans = board.people.flatMap((p) => [...p.shifts, ...p.appointments])
  const dayStart = new Date(board.dayStart).getTime()
  const startMs = spans.length ? Math.min(...spans.map((s) => new Date(s.start).getTime())) : dayStart + 8 * HOUR_MS
  const endMs = spans.length ? Math.max(...spans.map((s) => new Date(s.end).getTime())) : dayStart + 18 * HOUR_MS
  const from = dayStart + Math.floor((startMs - dayStart) / HOUR_MS) * HOUR_MS
  const until = Math.max(from + HOUR_MS, dayStart + Math.ceil((endMs - dayStart) / HOUR_MS) * HOUR_MS)
  const hours = Array.from({ length: Math.round((until - from) / HOUR_MS) + 1 }, (_, i) => from + i * HOUR_MS)
  return { from, until, hours }
}

const percent = (ms: number, from: number, until: number) => Math.min(100, Math.max(0, ((ms - from) / (until - from)) * 100))

function place(span: Span, from: number, until: number): CSSProperties {
  const left = percent(new Date(span.start).getTime(), from, until)
  const right = percent(new Date(span.end).getTime(), from, until)
  return { left: `${left}%`, width: `${Math.max(right - left, 0.5)}%` }
}

function describe(person: Person, zone: string) {
  const range = (s: Span) => `${timeIn(s.start, zone)} bis ${timeIn(s.end, zone)}`
  const shifts = person.shifts.length ? `Dienst ${person.shifts.map(range).join(', ')}` : 'kein Dienst'
  const appointments = person.appointments.length ? `, Termine ${person.appointments.map(range).join(', ')}` : ', keine Termine'
  return `${person.name}: ${shifts}${appointments}`
}

/** Die Tafel: Tagesleiste je Person, Räume und nächste Termine. Nur Anzeige, nichts zum Anklicken. */
export function BoardView({ board, now }: { board: Board; now: Date }) {
  const zone = board.timeZone
  const { from, until, hours } = timeline(board)
  const nowPercent = percent(now.getTime(), from, until)
  const showNow = now.getTime() >= from && now.getTime() <= until

  return (
    <div className="board">
      <section aria-labelledby="people-heading" className="panel people">
        <h2 id="people-heading">Heute im Dienst</h2>
        {board.people.length === 0 && <p className="empty">Heute ist niemand eingeteilt.</p>}
        {board.people.length > 0 && (
          <div className="scale" aria-hidden="true">
            <span className="name" />
            <span className="track">
              {hours.map((h) => (
                <span key={h} className="tick" style={{ left: `${percent(h, from, until)}%` }}>
                  {Number(timeIn(new Date(h), zone).slice(0, 2))}
                </span>
              ))}
            </span>
          </div>
        )}
        <ul className="rows">
          {board.people.map((person) => (
            <li key={person.name} className="row">
              <span className="name">
                {person.name}
                <span className={`state ${person.state}`}>{STATE_TEXT[person.state]}</span>
              </span>
              <span className="track" role="img" aria-label={describe(person, zone)}>
                {person.shifts.map((s) => (
                  <span key={s.start} className="shift" style={place(s, from, until)} />
                ))}
                {person.appointments.map((a) => (
                  <span key={a.start} className="appointment" style={{ ...place(a, from, until), backgroundColor: person.color }} />
                ))}
                {showNow && <span className="now" style={{ left: `${nowPercent}%` }} />}
              </span>
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="rooms-heading" className="panel rooms">
        <h2 id="rooms-heading">Räume</h2>
        <ul>
          {board.rooms.map((room) => (
            <li key={room.name} className={room.free ? 'free' : 'busy'}>
              <span>{room.name}</span>
              <span>
                {room.free ? (room.until ? `frei bis ${timeIn(room.until, zone)}` : 'frei') : `belegt bis ${room.until ? timeIn(room.until, zone) : 'Tagesende'}`}
              </span>
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="next-heading" className="panel next">
        <h2 id="next-heading">Nächste Termine</h2>
        {board.next.length === 0 && <p className="empty">Heute kommt kein Termin mehr.</p>}
        <ul>
          {board.next.map((appointment) => (
            <li key={`${appointment.start}-${appointment.person}-${appointment.initials}`}>
              <span className="time">{timeIn(appointment.start, zone)}</span>
              <span className="initials">{appointment.initials}</span>
              <span className="where">
                {appointment.person}, {appointment.room}
              </span>
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
