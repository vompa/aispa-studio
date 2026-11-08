import { useEffect, useState } from 'react'
import { api, type DayPlanDto } from '../api/client'
import { addDays, dayLabel, mondayOf, standLabel, timeIn } from './format'
import { CACHED_AT_HEADER, OFFLINE_HEADER } from './offline'

type Day = { date: string; plan: DayPlanDto | null; cachedAt: string | null }

const DAYS = [0, 1, 2, 3, 4, 5, 6]

/** Die eigene Woche aus den Tagesplänen; der Service Worker hält sie für den Weg ohne Netz vor. */
export function Week({
  locationId,
  timeZone,
  today,
  onOpenDay,
  onSignedOut,
}: {
  locationId: string
  timeZone: string
  today: string
  onOpenDay: (date: string) => void
  onSignedOut: () => void
}) {
  const [monday, setMonday] = useState(() => mondayOf(today))
  const [days, setDays] = useState<Day[] | null>(null)

  useEffect(() => {
    let cancelled = false
    void Promise.all(
      DAYS.map((offset): Promise<Day & { signedOut?: boolean }> => {
        const date = addDays(monday, offset)
        return api
          .GET('/api/v1/reports/locations/{locationId}/day-plan', { params: { path: { locationId }, query: { date } } })
          .then(({ data, response }) => ({
            date,
            plan: data ?? null,
            cachedAt: response.headers.get(OFFLINE_HEADER) === '1' ? response.headers.get(CACHED_AT_HEADER) : null,
            signedOut: response.status === 401,
          }))
          .catch(() => ({ date, plan: null, cachedAt: null }))
      }),
    ).then((loaded) => {
      if (cancelled) return
      if (loaded.some((d) => d.signedOut)) onSignedOut()
      else setDays(loaded)
    })
    return () => {
      cancelled = true
    }
  }, [locationId, monday, onSignedOut])

  function goTo(next: string) {
    setDays(null)
    setMonday(next)
  }

  const cached = days?.map((d) => d.cachedAt).filter((c): c is string => c !== null).sort()[0]
  return (
    <section aria-labelledby="week-heading">
      <nav className="day-nav" aria-label="Woche wählen">
        <button type="button" className="secondary" onClick={() => goTo(addDays(monday, -7))}>
          ‹ Vorwoche
        </button>
        <button type="button" className="secondary" onClick={() => goTo(addDays(monday, 7))}>
          Folgewoche ›
        </button>
      </nav>
      <h2 id="week-heading">Woche ab {dayLabel(monday)}</h2>
      {cached && (
        <div className="notice" role="status">
          <p>
            <strong>Offline, Stand {standLabel(cached, timeZone, today)}</strong>
          </p>
        </div>
      )}
      {!days && (
        <p className="muted" role="status">
          Lade Woche …
        </p>
      )}
      {days && (
        <ul className="week">
          {days.map((d) => (
            <li key={d.date}>
              <button type="button" className="secondary" aria-current={d.date === today ? 'date' : undefined} onClick={() => onOpenDay(d.date)}>
                <strong>{dayLabel(d.date)}</strong>
                <span>{summary(d, timeZone)}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function summary({ plan }: Day, timeZone: string) {
  if (!plan) return 'Nicht geladen'
  const shifts = plan.shifts.length === 0 ? 'Kein Dienst' : `Dienst ${plan.shifts.map((s) => `${timeIn(s.start, timeZone)}–${timeIn(s.end, timeZone)}`).join(', ')}`
  const count = plan.cards.filter((c) => c.status !== 'Cancelled').length
  return `${shifts} · ${count === 1 ? '1 Termin' : `${count} Termine`}`
}
