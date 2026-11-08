import { useCallback, useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { api, type OnlineAppointment, type WaitlistEntryDto } from './api'
import { Alert, AppointmentFacts, Field, Heading, Status } from './components'
import { useNotice } from './notice'
import { formatDateTime, formatPrice, useT } from './i18n'
import { addPasskey, passkeysSupported } from './passkeys'
import { href } from './router'
import { useSession } from './session'
import { WaitlistCard } from './Waitlist'
import { CardGuaranteeBox } from './CardGuarantee'
import { DepositBox } from './Deposit'
import { MoveBox } from './Move'
import { OwnVouchers } from './OwnVouchers'

/** Startseite der Kunden-App: nächster Termin, alle weiteren Termine (auch die von der Rezeption gebuchten), Storno, Warteliste, Gutscheine, eigene Daten und Passkey. */
export function MyAppointmentsPage({ navigatedHere }: { navigatedHere: boolean }) {
  const { t } = useT()
  const { session } = useSession()
  const [appointments, setAppointments] = useState<OnlineAppointment[] | null>(null)
  const [failed, setFailed] = useState(false)
  const [message, setMessage] = useNotice()
  const [waitlist, setWaitlist] = useState<WaitlistEntryDto[]>([])

  const load = useCallback(async () => {
    try {
      const { data } = await api.GET('/api/online/me/appointments')
      if (data) setAppointments(data)
      else setFailed(true)
    } catch {
      setFailed(true)
    }
  }, [])

  const signedIn = Boolean(session)
  useEffect(() => {
    if (!signedIn) return
    api.GET('/api/online/me/appointments').then(
      ({ data }) => (data ? setAppointments(data) : setFailed(true)),
      () => setFailed(true),
    )
    api.GET('/api/online/me/waitlist').then(
      ({ data }) => setWaitlist(data ?? []),
      () => setWaitlist([]),
    )
  }, [signedIn])

  if (session === undefined) return <Status>{t.loading}</Status>
  if (session === null) {
    return (
      <>
        <Heading focus={navigatedHere}>{t.mineTitle}</Heading>
        <p>
          <a className="button" href={href({ page: 'signin' })}>
            {t.navSignIn}
          </a>{' '}
          <a className="button secondary" href={href({ page: 'book' })}>
            {t.navBook}
          </a>
        </p>
      </>
    )
  }

  const upcoming = appointments?.filter((a) => a.state === 'Booked').reverse() ?? []
  const past = appointments?.filter((a) => a.state !== 'Booked') ?? []
  const [next, ...later] = upcoming

  const bookedCard = (a: OnlineAppointment): ReactNode => (
    <>
      <AppointmentCard
        appointment={a}
        onCancelled={async () => {
          setMessage(t.cancelDone)
          await load()
        }}
        cancel={(acceptFee) => api.POST('/api/online/me/appointments/{appointmentId}/cancel', { params: { path: { appointmentId: a.id }, query: { acceptFee } } })}
        calendarUrl={`/api/online/me/appointments/${a.id}/calendar.ics`}
        linkAnamnesis
        attend={() => api.POST('/api/online/me/appointments/{appointmentId}/confirm', { params: { path: { appointmentId: a.id } } })}
        move={(start) =>
          api.POST('/api/online/me/appointments/{appointmentId}/move', { params: { path: { appointmentId: a.id } }, body: { start } })
        }
        onMoved={async () => {
          setMessage(t.moveDone)
          await load()
        }}
        onAttended={(changed) => {
          setMessage(t.attendDone)
          setAppointments((all) => all?.map((x) => (x.id === changed.id ? changed : x)) ?? null)
        }}
      />
      <CardGuaranteeBox
        appointment={a}
        onChange={(changed) => setAppointments((all) => all?.map((x) => (x.id === changed.id ? changed : x)) ?? null)}
        reload={async () => {
          const { data } = await api.GET('/api/online/me/appointments')
          return { data: data?.find((x) => x.id === a.id) }
        }}
        test={(saved) =>
          api.POST('/api/online/me/appointments/{appointmentId}/card/test', { params: { path: { appointmentId: a.id } }, body: { saved } })
        }
        retry={() => api.POST('/api/online/me/appointments/{appointmentId}/card/retry', { params: { path: { appointmentId: a.id } } })}
        accept={() => api.POST('/api/online/me/appointments/{appointmentId}/card/accept', { params: { path: { appointmentId: a.id } } })}
      />
      <DepositBox
        appointment={a}
        onChange={(changed) => setAppointments((all) => all?.map((x) => (x.id === changed.id ? changed : x)) ?? null)}
        reload={async () => {
          const { data } = await api.GET('/api/online/me/appointments')
          return { data: data?.find((x) => x.id === a.id) }
        }}
        test={(paid) =>
          api.POST('/api/online/me/appointments/{appointmentId}/deposit/test', { params: { path: { appointmentId: a.id } }, body: { paid } })
        }
        retry={() => api.POST('/api/online/me/appointments/{appointmentId}/deposit/retry', { params: { path: { appointmentId: a.id } } })}
      />
    </>
  )

  return (
    <>
      <Heading focus={navigatedHere}>{t.mineTitle}</Heading>
      <p className="hint">{t.signedInAs(session.email)}</p>
      {message && (
        <Status tone="success" focus key={message.n}>
          {message.text}
        </Status>
      )}
      {failed && <Alert>{t.errorGeneric}</Alert>}
      {appointments === null && !failed && <Status>{t.loading}</Status>}

      {appointments && (
        <>
          {next ? (
            <section aria-labelledby="next">
              <h2 id="next">{t.nextAppointment}</h2>
              <div className="next-appointment" key={next.id}>{bookedCard(next)}</div>
            </section>
          ) : (
            <section aria-labelledby="upcoming">
              <h2 id="upcoming">{t.upcoming}</h2>
              <p>
                {t.noAppointments} <a href={href({ page: 'book' })}>{t.navBook}</a>
              </p>
            </section>
          )}
          {later.length > 0 && (
            <section aria-labelledby="upcoming">
              <h2 id="upcoming">{t.upcoming}</h2>
              <ul className="cards">
                {later.map((a) => (
                  <li key={a.id}>{bookedCard(a)}</li>
                ))}
              </ul>
            </section>
          )}
          {waitlist.some((w) => w.status === 'Waiting') && (
            <section aria-labelledby="waitlist">
              <h2 id="waitlist">{t.waitlistMine}</h2>
              <ul className="cards">
                {waitlist
                  .filter((w) => w.status === 'Waiting')
                  .map((w) => (
                    <li key={w.id}>
                      <WaitlistCard
                        entry={w}
                        leave={() => api.POST('/api/online/me/waitlist/{entryId}/cancel', { params: { path: { entryId: w.id } } })}
                        onLeft={(left) => {
                          setWaitlist((all) => all.map((x) => (x.id === left.id ? left : x)))
                          setMessage(t.waitlistLeft)
                        }}
                      />
                    </li>
                  ))}
              </ul>
            </section>
          )}
          {past.length > 0 && (
            <section aria-labelledby="past">
              <h2 id="past">{t.past}</h2>
              <ul className="cards">
                {past.map((a) => (
                  <li key={a.id}>
                    <AppointmentCard appointment={a} />
                  </li>
                ))}
              </ul>
            </section>
          )}
        </>
      )}

      <OwnVouchers />
      <Profile />
    </>
  )
}

type AttendCall = () => Promise<{ data?: OnlineAppointment; response: Response }>
type CancelCall = (acceptFee: boolean) => Promise<{ data?: OnlineAppointment; response: Response }>

/** Termin mit Storno in zwei Schritten (versehentliches Stornieren vermeiden, WCAG 3.3.4). */
export function AppointmentCard({ appointment, cancel, onCancelled, attend, onAttended, move, onMoved, calendarUrl, linkAnamnesis }: {
  appointment: OnlineAppointment
  cancel?: CancelCall
  onCancelled?: (a: OnlineAppointment) => void | Promise<void>
  /** Zusage „Ich komme“ (Link aus Bestätigung oder Erinnerung). */
  attend?: AttendCall
  onAttended?: (a: OnlineAppointment) => void | Promise<void>
  /** Selbst verschieben auf eine freie Zeit (innerhalb der Stornofrist, höchstens zweimal). */
  move?: (start: string) => Promise<{ data?: OnlineAppointment; response: Response }>
  onMoved?: (a: OnlineAppointment) => void | Promise<void>
  /** Adresse der .ics-Datei aus der API. */
  calendarUrl?: string
  /** Link zum Fragebogen; er braucht ein Konto, der Gast mit Mail-Link bekommt einen eigenen Link per Mail. */
  linkAnamnesis?: boolean
}) {
  const { t, lang } = useT()
  const [confirming, setConfirming] = useState(false)
  const [moving, setMoving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const questionId = useId()
  const cancelRef = useRef<HTMLButtonElement>(null)
  const keepRef = useRef<HTMLButtonElement>(null)
  const asked = useRef(false)

  // Der Knopf, der den Fokus hatte, verschwindet beim Nachfragen und danach wieder: Fokus mitnehmen (WCAG 2.4.3).
  useEffect(() => {
    if (confirming) keepRef.current?.focus()
    else if (asked.current) cancelRef.current?.focus()
    asked.current = confirming
  }, [confirming])

  // Betrag und Frist kommen aus der API; hier wird nichts gerechnet.
  const lateFee = !appointment.canCancel && appointment.canCancelLate && appointment.lateCancelFee != null
    ? formatPrice(appointment.lateCancelFee, appointment.currency, lang)
    : null

  async function doAttend() {
    if (!attend || busy) return
    setBusy(true)
    setError(null)
    try {
      const { data } = await attend()
      if (data) await onAttended?.(data)
      else setError(t.errorGeneric)
    } catch {
      setError(t.errorOffline)
    } finally {
      setBusy(false)
    }
  }

  async function doCancel() {
    if (!cancel || busy) return
    setBusy(true)
    setError(null)
    try {
      const { data, response } = await cancel(lateFee !== null)
      if (data) await onCancelled?.(data)
      else setError(response.status === 409 ? t.cancelTooLate : t.errorGeneric)
    } catch {
      setError(t.errorOffline)
    } finally {
      setBusy(false)
      setConfirming(false)
    }
  }

  return (
    <article className="card" aria-label={`${appointment.treatmentName}, ${formatDateTime(appointment.start, appointment.timeZone, lang)}`}>
      {appointment.state === 'Cancelled' && <p className="badge">{t.cancelled}</p>}
      <AppointmentFacts appointment={appointment} />
      {appointment.state === 'Booked' && (
        <>
          {appointment.guestHint && <p>{appointment.guestHint}</p>}
          {appointment.confirmedAt && <p className="hint">{t.attendConfirmed}</p>}
          <p className="hint">
            {appointment.anamnesisSubmitted ? t.anamnesisDone : t.anamnesisMissing}
            {!appointment.anamnesisSubmitted && linkAnamnesis && (
              <>
                {' · '}
                <a href={href({ page: 'anamnesis' })}>{t.fillAnamnesis}</a>
              </>
            )}
          </p>
          <p className="hint">
            {appointment.canCancel
              ? t.cancelUntil(formatDateTime(appointment.cancellableUntil, appointment.timeZone, lang))
              : lateFee
                ? t.lateCancelHint(lateFee)
                : t.cancelTooLate}
          </p>
        </>
      )}
      {error && error !== t.required && <Alert>{error}</Alert>}
      {appointment.state === 'Booked' && !appointment.confirmedAt && attend && !confirming && (
        <div className="actions">
          <button type="button" onClick={doAttend} aria-disabled={busy}>
            {t.attend}
          </button>
        </div>
      )}
      {appointment.state === 'Booked' && calendarUrl && !confirming && (
        <div className="actions">
          <a className="button secondary" href={calendarUrl} download="termin.ics">
            {t.addToCalendar}
          </a>
        </div>
      )}
      {(appointment.canCancel || lateFee) && cancel && (
        <div className="actions" role={confirming ? 'group' : undefined} aria-labelledby={confirming ? questionId : undefined}>
          {confirming ? (
            <>
              <span id={questionId} className="confirm-question">
                {lateFee ? t.lateCancelConfirm(lateFee) : t.cancelConfirm}
              </span>
              <button type="button" className="danger" onClick={doCancel} aria-disabled={busy}>
                {t.cancelYes}
              </button>
              <button ref={keepRef} type="button" className="secondary" onClick={() => setConfirming(false)}>
                {t.cancelNo}
              </button>
            </>
          ) : (
            <>
              {appointment.canMove && move && !moving && (
                <button type="button" className="secondary" onClick={() => setMoving(true)}>
                  {t.move}
                </button>
              )}
              <button ref={cancelRef} type="button" className="secondary" onClick={() => setConfirming(true)}>
                {t.cancel}
              </button>
            </>
          )}
        </div>
      )}
      {moving && move && (
        <MoveBox
          appointment={appointment}
          move={move}
          onClose={() => setMoving(false)}
          onMoved={async (a) => {
            setMoving(false)
            await onMoved?.(a)
          }}
        />
      )}
    </article>
  )
}

function Profile() {
  const { t } = useT()
  const { session, refresh } = useSession()
  const [firstName, setFirstName] = useState(session?.firstName ?? '')
  const [lastName, setLastName] = useState(session?.lastName ?? '')
  const [phone, setPhone] = useState(session?.phone ?? '')
  const [status, setStatus] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [missingFocus, setMissingFocus] = useState(0)

  async function save(event: React.FormEvent) {
    event.preventDefault()
    setStatus(null)
    setError(null)
    if (!firstName.trim() || !lastName.trim()) {
      setMissingFocus((n) => n + 1)
      return setError(t.required)
    }
    const { data } = await api.PUT('/api/online/me/profile', { body: { firstName, lastName, phone: phone || null } })
    if (data) {
      await refresh()
      setStatus(t.profileSaved)
    } else setError(t.errorGeneric)
  }

  async function passkey() {
    setStatus(null)
    setError(null)
    try {
      if (await addPasskey()) {
        await refresh()
        setStatus(t.passkeyAdded)
      } else setError(t.passkeyFailed)
    } catch {
      setError(t.passkeyFailed)
    }
  }

  return (
    <section aria-labelledby="profile">
      <h2 id="profile">{t.profileTitle}</h2>
      {status && <Status tone="success">{status}</Status>}
      {error && <Alert>{error}</Alert>}
      <form onSubmit={save} noValidate>
        <div className="two">
          <Field
            label={t.firstName}
            value={firstName}
            onChange={setFirstName}
            autoComplete="given-name"
            required
            maxLength={100}
            error={error === t.required && !firstName.trim() ? t.required : undefined}
            focus={!firstName.trim() ? missingFocus : 0}
          />
          <Field
            label={t.lastName}
            value={lastName}
            onChange={setLastName}
            autoComplete="family-name"
            required
            maxLength={100}
            error={error === t.required && !lastName.trim() ? t.required : undefined}
            focus={firstName.trim() && !lastName.trim() ? missingFocus : 0}
          />
        </div>
        <Field label={t.phone} value={phone} onChange={setPhone} type="tel" autoComplete="tel" maxLength={40} />
        <div className="actions">
          <button type="submit">{t.saveProfile}</button>
          {passkeysSupported() && session && session.passkeyCount === 0 && (
            <button type="button" className="secondary" onClick={passkey}>
              {t.addPasskey}
            </button>
          )}
        </div>
      </form>
    </section>
  )
}
