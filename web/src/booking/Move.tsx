import { useEffect, useState } from 'react'
import { api, type OnlineAppointment, type OnlineLocation, type OnlineTreatment } from './api'
import { Alert, Status } from './components'
import { localDate, useT } from './i18n'
import { TimeStep } from './BookPage'

type MoveCall = (start: string) => Promise<{ data?: OnlineAppointment; response: Response }>

/** Termin selbst verschieben: dieselbe Zeitwahl wie beim Buchen, nur mit den online freien Zeiten derselben Behandlung. */
export function MoveBox({ appointment, move, onMoved, onClose }: {
  appointment: OnlineAppointment
  move: MoveCall
  onMoved: (a: OnlineAppointment) => void
  onClose: () => void
}) {
  const { t } = useT()
  const [offer, setOffer] = useState<{ location: OnlineLocation; treatment: OnlineTreatment } | null | undefined>(undefined)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let current = true
    Promise.all([
      api.GET('/api/online/locations'),
      api.GET('/api/online/locations/{locationId}/treatments', { params: { path: { locationId: appointment.locationId } } }),
    ]).then(
      ([locations, treatments]) => {
        if (!current) return
        const location = locations.data?.find((l) => l.id === appointment.locationId)
        const treatment = treatments.data?.find((x) => x.id === appointment.treatmentId)
        setOffer(location && treatment ? { location, treatment } : null)
      },
      () => current && setOffer(null),
    )
    return () => {
      current = false
    }
  }, [appointment.locationId, appointment.treatmentId])

  async function choose(start: string) {
    if (busy) return
    setBusy(true)
    setError(null)
    try {
      const { data, response } = await move(start)
      if (data) onMoved(data)
      else setError(response.status === 409 ? t.moveTaken : t.errorGeneric)
    } catch {
      setError(t.errorOffline)
    } finally {
      setBusy(false)
    }
  }

  if (offer === undefined) return <Status>{t.loading}</Status>
  if (offer === null) return <Alert>{t.moveNotOnline}</Alert>
  return (
    <section className="card" aria-label={t.moveTitle}>
      {error && <Alert>{error}</Alert>}
      <TimeStep
        focus
        location={offer.location}
        treatment={offer.treatment}
        value={null}
        initialDay={localDate(appointment.start, appointment.timeZone)}
        onNext={(start) => void choose(start)}
        onBack={onClose}
        nextLabel={t.moveSubmit}
      />
    </section>
  )
}
