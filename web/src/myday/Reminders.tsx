import { useEffect, useState } from 'react'
import { api, type DutyReminderDto } from '../api/client'

/**
 * „Erinnerungen“ auf Heute (Slice 117, Ä274): eigene Zertifikate vor oder nach dem Ablauf. Was fällig ist und wem es gehört, entscheidet die API; im Kanal der App liefert
 * sie nur die eigenen Zertifikate. Ohne Erinnerung oder bei einem Fehler steht nichts da, die übrige App bleibt nutzbar.
 */
export function Reminders() {
  const [reminders, setReminders] = useState<DutyReminderDto[]>([])

  useEffect(() => {
    let cancelled = false
    api
      .GET('/api/v1/staff-duties/reminders')
      .then(({ data }) => {
        if (!cancelled && data) setReminders(data)
      })
      .catch(() => undefined)
    return () => {
      cancelled = true
    }
  }, [])

  if (reminders.length === 0) return null
  return (
    <section aria-labelledby="reminders-heading">
      <h3 id="reminders-heading">Erinnerungen</h3>
      <ul className="hints" aria-labelledby="reminders-heading">
        {reminders.map((r) => (
          <li key={`${r.kind}-${r.date}-${r.text}`}>{r.text}</li>
        ))}
      </ul>
    </section>
  )
}
