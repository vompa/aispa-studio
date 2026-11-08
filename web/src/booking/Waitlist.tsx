import { useEffect, useRef, useState } from 'react'
import { api, fieldErrors, type DayPart, type OnlineLocation, type OnlineTreatment, type WaitlistConfirmation, type WaitlistEntryDto } from './api'
import { Alert, Field, Heading, Status } from './components'
import { useNotice } from './notice'
import { addDays, formatDateTime, formatDay, useT } from './i18n'
import { href } from './router'
import { useSession } from './session'

/** Wie im Server: höchstens 60 Tage je Eintrag. */
export const WAITLIST_MAX_DAYS = 60

const dayParts: DayPart[] = ['Any', 'Morning', 'Afternoon', 'Evening']
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/**
 * Eintrag auf die Warteliste, wenn in den angezeigten Tagen nichts Passendes frei ist. Vorbelegt mit dem gewählten Tag und
 * der Woche danach.
 */
export function WaitlistForm({ location, treatment, firstDay, firstBookable, lastBookable, focus, onBack, onJoined }: {
  location: OnlineLocation
  treatment: OnlineTreatment
  firstDay: string
  firstBookable: string
  lastBookable: string
  focus: boolean
  onBack: () => void
  onJoined: (confirmation: WaitlistConfirmation, email: string) => void
}) {
  const { t, lang } = useT()
  const { session, refresh } = useSession()
  const needsName = !session?.hasProfile
  const needsEmail = !session
  const latest = (from: string) => {
    const limit = addDays(from, WAITLIST_MAX_DAYS - 1)
    return limit < lastBookable ? limit : lastBookable
  }
  const [from, setFrom] = useState(firstDay)
  const [until, setUntil] = useState(() => {
    const week = addDays(firstDay, 6)
    return week < latest(firstDay) ? week : latest(firstDay)
  })
  const [dayPart, setDayPart] = useState<DayPart>('Any')
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [privacy, setPrivacy] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failed, setFailed] = useState(false)
  const [busy, setBusy] = useState(false)
  const summaryRef = useRef<HTMLDivElement>(null)

  function validate(): Record<string, string> {
    const e: Record<string, string> = {}
    if (!from || from < firstBookable || from > lastBookable) e.from = t.waitlistUntilInvalid
    if (!until || until < from || until > latest(from)) e.until = t.waitlistUntilInvalid
    if (needsName && !firstName.trim()) e.firstName = t.required
    if (needsName && !lastName.trim()) e.lastName = t.required
    if (needsEmail && !emailPattern.test(email.trim())) e.email = t.invalidEmail
    if (!privacy) e.privacy = t.privacyRequired
    return e
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    setFailed(false)
    const found = validate()
    setErrors(found)
    if (Object.keys(found).length > 0) {
      summaryRef.current?.focus()
      return
    }

    setBusy(true)
    try {
      const { data, error } = await api.POST('/api/online/waitlist', {
        body: {
          locationId: location.id,
          treatmentId: treatment.id,
          from,
          until,
          dayPart,
          contact: needsName ? { firstName, lastName, email: needsEmail ? email : (session?.email ?? ''), phone: null } : null,
          acceptPrivacy: privacy,
          language: lang,
        },
      })
      if (data) {
        if (session) await refresh()
        onJoined(data, needsEmail ? email.trim() : (session?.email ?? ''))
      } else {
        const mapped: Record<string, string> = {}
        for (const [key, messages] of Object.entries(fieldErrors(error))) {
          const field = key.replace(/^contact\./, '')
          mapped[field === 'acceptPrivacy' ? 'privacy' : field === 'treatmentId' ? 'general' : field] = messages[0]
        }
        setErrors(mapped)
        setFailed(Object.keys(mapped).length === 0)
        summaryRef.current?.focus()
      }
    } catch {
      setFailed(true)
    } finally {
      setBusy(false)
    }
  }

  const errorCount = Object.keys(errors).length

  return (
    <form onSubmit={submit} noValidate>
      <Heading level={2} focus={focus}>
        {t.waitlistTitle}
      </Heading>
      <p>{t.waitlistIntro}</p>
      <div ref={summaryRef} tabIndex={-1}>
        {errorCount > 0 && <Alert>{errors.general ?? t.formErrors(errorCount)}</Alert>}
        {failed && <Alert>{t.errorGeneric}</Alert>}
      </div>
      <p className="hint">
        {treatment.name} · {location.name}
      </p>
      <div className="two">
        <Field label={t.waitlistFrom} type="date" value={from} onChange={setFrom} min={firstBookable} max={lastBookable} required error={errors.from} />
        <Field
          label={t.waitlistUntil}
          type="date"
          value={until}
          onChange={setUntil}
          min={from}
          max={latest(from || firstBookable)}
          required
          hint={t.waitlistRangeHint(WAITLIST_MAX_DAYS)}
          error={errors.until}
        />
      </div>
      <fieldset>
        <legend>{t.dayPart}</legend>
        <div className="choices">
          {dayParts.map((p) => (
            <label key={p} className="choice">
              <input type="radio" name="dayPart" value={p} checked={dayPart === p} onChange={() => setDayPart(p)} />
              <span className="choice-text">
                <span className="choice-label">{t.dayParts[p]}</span>
              </span>
            </label>
          ))}
        </div>
      </fieldset>
      {session ? (
        <p>{t.signedInAs(session.email)}</p>
      ) : (
        <p className="hint">
          {t.guestOrSignIn} <a href={href({ page: 'signin' })}>{t.navSignIn}</a>
        </p>
      )}
      {needsName && (
        <div className="two">
          <Field label={t.firstName} value={firstName} onChange={setFirstName} autoComplete="given-name" required error={errors.firstName} maxLength={100} />
          <Field label={t.lastName} value={lastName} onChange={setLastName} autoComplete="family-name" required error={errors.lastName} maxLength={100} />
        </div>
      )}
      {needsEmail && <Field label={t.email} value={email} onChange={setEmail} type="email" autoComplete="email" required error={errors.email} maxLength={254} />}
      <div className="field">
        <label className="check">
          <input
            type="checkbox"
            aria-required="true"
            checked={privacy}
            onChange={(e) => setPrivacy(e.target.checked)}
            aria-invalid={errors.privacy ? true : undefined}
            aria-describedby={errors.privacy ? 'waitlist-privacy-error' : undefined}
          />
          <span>
            {t.privacyWaitlist} <span className="required">({t.required})</span>
          </span>
        </label>
        {errors.privacy && (
          <span id="waitlist-privacy-error" className="field-error">
            {errors.privacy}
          </span>
        )}
      </div>
      <div className="actions">
        <button type="button" className="secondary" onClick={onBack}>
          {t.back}
        </button>
        <button type="submit" disabled={busy} aria-disabled={busy}>
          {busy ? t.waitlistJoining : t.waitlistSubmit}
        </button>
      </div>
    </form>
  )
}

export function WaitlistJoined({ confirmation, email, onAgain }: { confirmation: WaitlistConfirmation; email: string; onAgain: () => void }) {
  const { t } = useT()
  return (
    <>
      <Heading focus>{t.waitlistJoinedTitle}</Heading>
      <Status tone="success">{t.waitlistJoinedText(email)}</Status>
      <WaitlistCard entry={confirmation.entry} />
      <div className="actions">
        <a className="button secondary" href={href({ page: 'waitlist', token: confirmation.manageToken })}>
          {t.waitlistManage}
        </a>
        <button type="button" className="link" onClick={onAgain}>
          {t.bookAnother}
        </button>
      </div>
    </>
  )
}

type LeaveCall = () => Promise<{ data?: WaitlistEntryDto }>

/** Eintrag mit Zeitraum, Tageszeit und Stand; „Austragen“ nur, solange er wartet. */
export function WaitlistCard({ entry, leave, onLeft }: { entry: WaitlistEntryDto; leave?: LeaveCall; onLeft?: (e: WaitlistEntryDto) => void }) {
  const { t, lang } = useT()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(false)

  async function doLeave() {
    if (!leave || busy) return
    setBusy(true)
    setError(false)
    try {
      const { data } = await leave()
      if (data) onLeft?.(data)
      else setError(true)
    } catch {
      setError(true)
    } finally {
      setBusy(false)
    }
  }

  return (
    <article className="card" aria-label={`${t.waitlistTitle}: ${entry.treatmentName}`}>
      <p className="badge">{t.waitlistStatus[entry.status]}</p>
      <dl className="facts">
        <dt>{t.stepTreatment}</dt>
        <dd>{entry.treatmentName}</dd>
        <dt>{t.stepLocation}</dt>
        <dd>{entry.locationName}</dd>
        <dt>{t.stepTime}</dt>
        <dd>
          {t.waitlistRange(formatDay(entry.from, lang, 'short'), formatDay(entry.until, lang, 'short'))}, {t.dayParts[entry.dayPart]}
        </dd>
      </dl>
      {entry.lastOfferedAt && <p className="hint">{t.waitlistLastOffer(formatDateTime(entry.lastOfferedAt, Intl.DateTimeFormat().resolvedOptions().timeZone, lang))}</p>}
      {error && <Alert>{t.errorGeneric}</Alert>}
      {leave && entry.status === 'Waiting' && (
        <div className="actions">
          <button type="button" className="secondary" onClick={doLeave} aria-disabled={busy}>
            {t.waitlistLeave}
          </button>
        </div>
      )}
    </article>
  )
}

/** Eintrag über den Link aus der Mail: ansehen und austragen, ohne Konto. */
export function WaitlistPage({ token, navigatedHere }: { token: string; navigatedHere: boolean }) {
  const { t } = useT()
  const [entry, setEntry] = useState<WaitlistEntryDto | null | undefined>(undefined)
  const [message, setMessage] = useNotice()

  useEffect(() => {
    api.GET('/api/online/waitlist/{token}', { params: { path: { token } } }).then(
      ({ data }) => setEntry(data ?? null),
      () => setEntry(null),
    )
  }, [token])

  return (
    <>
      <Heading focus={navigatedHere}>{t.waitlistTitle}</Heading>
      {entry === undefined && <Status>{t.loading}</Status>}
      {entry === null && <Alert>{t.linkInvalid}</Alert>}
      {message && (
        <Status tone="success" focus key={message.n}>
          {message.text}
        </Status>
      )}
      {entry && (
        <WaitlistCard
          entry={entry}
          leave={() => api.POST('/api/online/waitlist/{token}/cancel', { params: { path: { token } } })}
          onLeft={(e) => {
            setEntry(e)
            setMessage(t.waitlistLeft)
          }}
        />
      )}
      <p>
        <a href={href({ page: 'book' })}>{t.navBook}</a>
      </p>
    </>
  )
}
