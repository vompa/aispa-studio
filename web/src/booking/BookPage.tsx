import { useEffect, useRef, useState } from 'react'
import {
  api,
  fieldErrors,
  type OnlineAvailability,
  type OnlineBookingConfirmation,
  type OnlineAssistantAnswer,
  type OnlineLocation,
  type OnlineTreatment,
  type WaitlistConfirmation,
} from './api'
import { Alert, AppointmentFacts, RetryAlert, Field, Heading, Status } from './components'
import { addDays, formatDay, formatPrice, formatTime, localDate, useT } from './i18n'
import { href, type BookPrefill } from './router'
import { address, byCategory } from './offer'
import { useSession } from './session'
import { WaitlistForm, WaitlistJoined } from './Waitlist'
import { CardGuaranteeBox } from './CardGuarantee'
import { DepositBox } from './Deposit'

type Step = 'location' | 'treatment' | 'time' | 'details' | 'waitlist'

type WaitlistStart = { firstDay: string; firstBookable: string; lastBookable: string }

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/** Reihenfolge der Felder im Formular „Ihre Angaben“; die Fehlerübersicht nennt die Fehler in dieser Reihenfolge. */
const DETAIL_FIELDS = ['firstName', 'lastName', 'email', 'phone', 'note', 'voucher', 'privacy', 'fee']
const detailId = (key: string) => `details-${key}`

/**
 * Buchung in vier Schritten: Standort, Behandlung, Tag und Uhrzeit, Angaben. Jeder Schritt ist ein eigenes Formular mit
 * „Weiter“; eine Auswahl allein wechselt nie die Ansicht (WCAG 3.2.2). Erledigte Schritte bleiben als Zusammenfassung mit
 * „Ändern“ sichtbar. Ist nichts Passendes frei, geht es von der Zeitauswahl auf die Warteliste; deren Mail führt mit
 * Standort, Behandlung und Tag (<c>prefill</c>) direkt zurück zur Zeitauswahl.
 */
export function BookPage({ navigatedHere, prefill }: { navigatedHere: boolean; prefill?: BookPrefill }) {
  const { t, lang } = useT()
  const { session } = useSession()

  const [locations, setLocations] = useState<OnlineLocation[] | null>(null)
  const [loadError, setLoadError] = useState(false)
  const [attempt, setAttempt] = useState(0)
  const [step, setStep] = useState<Step>(prefill ? 'time' : 'location')
  const [locationId, setLocationId] = useState<string | null>(prefill?.locationId ?? null)
  // Behandlungen je Standort; gehören sie zu einem anderen Standort, gilt die Liste als noch nicht geladen.
  const [loaded, setLoaded] = useState<{ locationId: string; list: OnlineTreatment[] } | null>(null)
  const [treatmentId, setTreatmentId] = useState<string | null>(prefill?.treatmentId ?? null)
  const [confirmation, setConfirmation] = useState<OnlineBookingConfirmation | null>(null)
  const [start, setStart] = useState<string | null>(null)
  // Preis der gewählten Zeit, wenn die Behandlung Preise nach Uhrzeit hat.
  const [slotPrice, setSlotPrice] = useState<number | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [bookedEmail, setBookedEmail] = useState('')
  const [waitlist, setWaitlist] = useState<WaitlistStart | null>(null)
  const [joined, setJoined] = useState<WaitlistConfirmation | null>(null)
  // Fokus erst nach einem Schrittwechsel auf die Überschrift setzen, nicht beim ersten Laden.
  const [moved, setMoved] = useState(false)

  function go(next: Step) {
    setMoved(true)
    setStep(next)
  }

  useEffect(() => {
    api.GET('/api/online/locations').then(
      ({ data }) => {
        if (!data) return setLoadError(true)
        // Standorte ohne Online-Buchung erscheinen hier nicht (Gutscheine gibt es dort trotzdem).
        const bookable = data.filter((l) => l.bookingEnabled)
        setLocations(bookable)
        // Link aus der Warteliste mit einem Standort, den es nicht (mehr) gibt: normal von vorn.
        if (prefill && !bookable.some((l) => l.id === prefill.locationId)) {
          setLocationId(null)
          setTreatmentId(null)
          setStep('location')
        }
        // Nur ein Standort: nicht danach fragen.
        if (bookable.length === 1 && (!prefill || bookable[0].id !== prefill.locationId)) {
          setLocationId(bookable[0].id)
          setStep('treatment')
        }
      },
      () => setLoadError(true),
    )
    // Die Vorbelegung ändert sich nicht, solange die Seite steht: BookingApp setzt dafür einen key.
  }, [prefill, attempt])

  useEffect(() => {
    if (!locationId) return
    api.GET('/api/online/locations/{locationId}/treatments', { params: { path: { locationId } } }).then(
      ({ data }) => setLoaded({ locationId, list: data ?? [] }),
      () => setLoadError(true),
    )
  }, [locationId, attempt])

  const treatments = loaded && loaded.locationId === locationId ? loaded.list : null

  const location = locations?.find((l) => l.id === locationId) ?? null
  const treatment = treatments?.find((x) => x.id === treatmentId) ?? null
  const steps: Step[] = locations && locations.length > 1 ? ['location', 'treatment', 'time', 'details'] : ['treatment', 'time', 'details']
  const stepNumber = steps.indexOf(step === 'waitlist' ? 'details' : step) + 1

  if (joined) {
    return (
      <WaitlistJoined
        confirmation={joined}
        email={bookedEmail}
        onAgain={() => {
          setJoined(null)
          setStart(null)
          go(steps[0])
        }}
      />
    )
  }

  if (confirmation) {
    return (
      <Confirmation
        confirmation={confirmation}
        email={bookedEmail}
        onAgain={() => {
          setConfirmation(null)
          setStart(null)
          go(steps[0])
        }}
      />
    )
  }

  if (loadError || !locations)
    return (
      <>
        <Heading focus={navigatedHere}>{t.bookTitle}</Heading>
        {loadError ? (
          <RetryAlert
            onRetry={() => {
              setLoadError(false)
              setAttempt((n) => n + 1)
            }}
          >
            {t.errorOffline}
          </RetryAlert>
        ) : (
          <Status>{t.loading}</Status>
        )}
      </>
    )
  if (locations.length === 0)
    return (
      <>
        <Heading focus={navigatedHere}>{t.bookTitle}</Heading>
        <p>{t.noOnlineBooking}</p>
      </>
    )

  return (
    <>
      <Heading focus={navigatedHere}>{t.bookTitle}</Heading>
      {notice && <Alert>{notice}</Alert>}

      {locations.length > 1 && step !== 'location' && location && (
        <Done label={t.stepLocation} value={location.name} onChange={() => go('location')} />
      )}
      {step !== 'location' && step !== 'treatment' && treatment && (
        <Done
          label={t.stepTreatment}
          value={`${treatment.name} · ${t.minutes(treatment.durationMinutes)} · ${formatPrice(treatment.price, treatment.currency, lang)}`}
          onChange={() => go('treatment')}
        />
      )}
      {step === 'details' && start && location && (
        <Done
          label={t.stepTime}
          value={`${formatDay(localDate(start, location.timeZone), lang)}, ${formatTime(start, location.timeZone, lang)}`}
          onChange={() => go('time')}
        />
      )}

      <section className="step" aria-label={t.stepOf(stepNumber, steps.length)}>
        <p className="step-count" aria-hidden="true">
          {t.stepOf(stepNumber, steps.length)}
        </p>
        {step === 'location' && (
          <ChoiceStep
            title={t.chooseLocation}
            name="location"
            options={locations.map((l) => ({ id: l.id, label: l.name, detail: address(l.contact) || undefined }))}
            value={locationId}
            onNext={(id) => {
              if (id !== locationId) setTreatmentId(null)
              setLocationId(id)
              go('treatment')
            }}
            focus={moved}
          />
        )}
        {step === 'treatment' &&
          (treatments === null ? (
            <Status>{t.loading}</Status>
          ) : treatments.length === 0 ? (
            <>
                <Heading level={2} focus={moved}>
                  {t.chooseTreatment}
                </Heading>
                <p>{t.noTreatments}</p>
                {locations.length > 1 && (
                  <button type="button" onClick={() => go('location')}>
                    {t.chooseOtherLocation}
                  </button>
                )}
            </>
          ) : (
            <>
              <ChoiceStep
                title={t.chooseTreatment}
                name="treatment"
                options={byCategory(treatments).map((x) => ({
                  id: x.id,
                  label: x.name,
                  detail: [x.category, t.minutes(x.durationMinutes), formatPrice(x.price, x.currency, lang)].filter(Boolean).join(' · '),
                }))}
                value={treatmentId}
                onNext={(id) => {
                  setTreatmentId(id)
                  setStart(null)
                  go('time')
                }}
                onBack={locations.length > 1 ? () => go('location') : undefined}
                focus={moved}
              />
              {location && (
                <WishStep
                  location={location}
                  onPick={(id, value) => {
                    setNotice(null)
                    setTreatmentId(id)
                    setStart(value)
                    setSlotPrice(null)
                    go('details')
                  }}
                />
              )}
            </>
          ))}
        {step === 'time' && treatments?.length === 0 && <p>{t.noTreatments}</p>}
        {step === 'time' && location && treatment && (
          <TimeStep
            location={location}
            treatment={treatment}
            value={start}
            initialDay={prefill?.date ?? null}
            focus={moved}
            onBack={() => go('treatment')}
            onWaitlist={(w) => {
              setWaitlist(w)
              go('waitlist')
            }}
            onNext={(value, price) => {
              setNotice(null)
              setStart(value)
              setSlotPrice(price)
              go('details')
            }}
          />
        )}
        {step === 'waitlist' && location && treatment && waitlist && session !== undefined && (
          <WaitlistForm
            location={location}
            treatment={treatment}
            {...waitlist}
            focus={moved}
            onBack={() => go('time')}
            onJoined={(c, email) => {
              setBookedEmail(email)
              setJoined(c)
            }}
          />
        )}
        {step === 'details' && location && treatment && start && session !== undefined && (
          <DetailsStep
            location={location}
            treatment={slotPrice === null ? treatment : { ...treatment, price: slotPrice }}
            start={start}
            focus={moved}
            onBack={() => go('time')}
            onBooked={(c, email) => {
              setBookedEmail(email)
              setConfirmation(c)
            }}
            onTaken={() => {
              setNotice(t.slotTaken)
              setStart(null)
              go('time')
            }}
          />
        )}
      </section>
    </>
  )
}

function Done({ label, value, onChange }: { label: string; value: string; onChange: () => void }) {
  const { t } = useT()
  return (
    <div className="done">
      <span>
        <span className="done-label">{label}: </span>
        {value}
      </span>
      <button type="button" className="link" onClick={onChange}>
        {t.change}
        <span className="visually-hidden">: {label}</span>
      </button>
    </div>
  )
}

type Choice = { id: string; label: string; detail?: string }

/** Auswahl als Gruppe echter Optionsfelder: mit Pfeiltasten bedienbar und von Screenreadern als Gruppe angesagt. */
/** Terminwunsch in eigenen Worten: der Assistent schlägt bis zu drei freie Zeiten vor, ausgewählt wird wie gewohnt. */
function WishStep({ location, onPick }: { location: OnlineLocation; onPick: (treatmentId: string, start: string) => void }) {
  const { t, lang } = useT()
  const [wish, setWish] = useState('')
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)
  const [answer, setAnswer] = useState<OnlineAssistantAnswer | null>(null)

  async function ask() {
    if (busy || !wish.trim()) return
    setBusy(true)
    setFailed(false)
    try {
      const { data } = await api.POST('/api/online/locations/{locationId}/assistant', {
        params: { path: { locationId: location.id } },
        body: { question: wish.trim() },
      })
      setAnswer(data ?? null)
      setFailed(!data)
    } catch {
      setFailed(true)
    } finally {
      setBusy(false)
    }
  }

  return (
    <form
      className="wish"
      onSubmit={(e) => {
        e.preventDefault()
        void ask()
      }}
    >
      <h3>{t.wishTitle}</h3>
      <Field label={t.wishLabel} value={wish} onChange={setWish} hint={t.wishHint} multiline maxLength={500} />
      <div className="actions">
        <button type="submit" className="secondary" aria-disabled={busy || !wish.trim()}>
          {t.wishButton}
        </button>
      </div>
      {failed && <Alert>{t.errorGeneric}</Alert>}
      {/* Die Live-Region ist immer da, damit auch die erste Antwort vorgelesen wird. */}
      <div aria-live="polite">
        {busy && <p className="hint">{t.loading}</p>}
        {answer && !busy && (
          <>
          <p>{answer.text}</p>
          {answer.suggestions.length > 0 && (
            <div className="choices" role="group" aria-label={t.wishSuggestions}>
              {answer.suggestions.map((s) => (
                <button key={`${s.treatmentId}-${s.start}`} type="button" className="secondary" onClick={() => onPick(s.treatmentId, s.start)}>
                  {s.treatmentName} · {formatDay(localDate(s.start, location.timeZone), lang, 'short')}, {formatTime(s.start, location.timeZone, lang)}
                </button>
              ))}
            </div>
          )}
          <p className="hint">{answer.disclosure}</p>
          </>
        )}
      </div>
    </form>
  )
}

function ChoiceStep({ title, name, options, value, onNext, onBack, focus }: {
  title: string
  focus: boolean
  name: string
  options: Choice[]
  value: string | null
  onNext: (id: string) => void
  onBack?: () => void
}) {
  const { t } = useT()
  const [selected, setSelected] = useState(value ?? (options.length === 1 ? options[0].id : null))
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        if (selected) onNext(selected)
      }}
    >
      <fieldset>
        <legend>
          <Heading level={2} focus={focus}>
            {title}
          </Heading>
        </legend>
        <div className="choices">
          {options.map((o) => (
            <label key={o.id} className="choice">
              <input type="radio" name={name} value={o.id} checked={selected === o.id} onChange={() => setSelected(o.id)} />
              <span className="choice-text">
                <span className="choice-label">{o.label}</span>
                {o.detail && <span className="choice-detail">{o.detail}</span>}
              </span>
            </label>
          ))}
        </div>
      </fieldset>
      <div className="actions">
        {onBack && (
          <button type="button" className="secondary" onClick={onBack}>
            {t.back}
          </button>
        )}
        <button type="submit" disabled={!selected}>
          {t.next}
        </button>
      </div>
    </form>
  )
}

/** Preis zur gewählten Zeit, wenn die Verfügbarkeit Preise je Zeit liefert. */
function priceOf(days: { times: string[]; prices?: number[] | null }[], time: string): number | null {
  for (const d of days) {
    const i = d.times.indexOf(time)
    if (i >= 0) return d.prices?.[i] ?? null
  }
  return null
}

/** Schließzeiten des Standorts (ADR 0073) vor der Wahl der Zeit: in ihnen gibt es nichts zu buchen; der Text stammt vom Spa, sonst gilt der Standardsatz der Art. */
function ClosureNotices({ location }: { location: OnlineLocation }) {
  const { t, lang } = useT()
  const closures = location.closures ?? []
  if (closures.length === 0) return null
  const clock = (time: string | null | undefined) => (time ? time.slice(0, 5) : undefined)
  return (
    <div className="closure-notices" role="note">
      {closures.map((c) => (
        <p key={`${c.fromDate}${c.fromTime ?? ''}`} className="guest-note">
          <strong>
            {t.closedNotice({
              from: formatDay(c.fromDate, lang),
              until: formatDay(c.untilDate, lang),
              fromTime: clock(c.fromTime),
              untilTime: clock(c.untilTime),
              sameDay: c.fromDate === c.untilDate,
            })}
            .
          </strong>{' '}
          {c.text ?? t.closureDefault[c.kind]}
        </p>
      ))}
    </div>
  )
}

export function TimeStep({ location, treatment, value, initialDay, onNext, onBack, onWaitlist, focus, nextLabel }: {
  focus: boolean
  location: OnlineLocation
  treatment: OnlineTreatment
  value: string | null
  initialDay: string | null
  onNext: (start: string, price: number | null) => void
  onBack: () => void
  /** Ohne: kein Angebot für die Warteliste (z. B. beim Verschieben). */
  onWaitlist?: (start: WaitlistStart) => void
  nextLabel?: string
}) {
  const { t, lang } = useT()
  const [from, setFrom] = useState<string | null>(value ? null : initialDay)
  const [loaded, setLoaded] = useState<{ from: string | null; data: OnlineAvailability } | null>(null)
  const [failed, setFailed] = useState(false)
  const [attempt, setAttempt] = useState(0)
  const [day, setDay] = useState<string | null>(value ? localDate(value, location.timeZone) : initialDay)
  const [time, setTime] = useState<string | null>(value)

  useEffect(() => {
    let current = true
    api
      .GET('/api/online/locations/{locationId}/availability', {
        params: { path: { locationId: location.id }, query: { treatmentId: treatment.id, from: from ?? undefined, days: 7 } },
      })
      .then(
        ({ data }) => {
          if (!current) return
          if (!data) return setFailed(true)
          setLoaded({ from, data })
          // Ersten Tag mit freien Zeiten vorschlagen, wenn noch keiner gewählt ist.
          setDay((d) => (d && data.days.some((x) => x.date === d) ? d : (data.days.find((x) => x.times.length > 0)?.date ?? data.days[0]?.date ?? null)))
        },
        () => current && setFailed(true),
      )
    return () => {
      current = false
    }
  }, [location.id, treatment.id, from, attempt])

  if (failed) return (
      <RetryAlert
        onRetry={() => {
          setFailed(false)
          setAttempt((n) => n + 1)
        }}
      >
        {t.errorGeneric}
      </RetryAlert>
    )

  // Beim Blättern bleiben die bisherigen Tage stehen, bis die neuen da sind; so behält der Knopf den Fokus.
  const availability = loaded?.data ?? null
  const paging = !!loaded && loaded.from !== from
  const days = availability?.days ?? []
  const selectedDay = days.find((d) => d.date === day)
  const windowStart = days[0]?.date
  const canEarlier = availability && windowStart && windowStart > availability.firstBookableDate
  const canLater = availability && days.length > 0 && days[days.length - 1].date < availability.lastBookableDate

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault()
        if (time) onNext(time, priceOf(days, time))
      }}
    >
      <Heading level={2} focus={focus}>
        {t.stepTime}
      </Heading>
      <p className="hint">{t.timeZoneHint(location.timeZone)}</p>
      <ClosureNotices location={location} />
      {!availability ? (
        <Status>{t.loading}</Status>
      ) : (
        <>
          <fieldset aria-busy={paging}>
            <legend>{t.chooseDay}</legend>
            <p className="visually-hidden" role="status">
              {paging ? t.loading : ''}
            </p>
            <div className="days">
              {days.map((d) => (
                <label key={d.date} className={d.times.length === 0 ? 'day empty' : 'day'}>
                  <input
                    type="radio"
                    name="day"
                    value={d.date}
                    checked={day === d.date}
                    onChange={() => {
                      setDay(d.date)
                      setTime(null)
                    }}
                  />
                  <span className="day-name">{formatDay(d.date, lang, 'short')}</span>
                  <span className="day-count">{t.timesFree(d.times.length)}</span>
                </label>
              ))}
            </div>
            <div className="actions compact">
              {/* aria-disabled statt disabled: ein gesperrter Knopf verliert den Fokus, wenn man ans Ende blättert. */}
              <button
                type="button"
                className="secondary"
                aria-disabled={!canEarlier || paging}
                onClick={() => {
                  if (canEarlier && windowStart && !paging) setFrom(addDays(windowStart, -7))
                }}
              >
                {t.earlierDays}
              </button>
              <button
                type="button"
                className="secondary"
                aria-disabled={!canLater || paging}
                onClick={() => {
                  if (canLater && !paging) setFrom(addDays(days[days.length - 1].date, 1))
                }}
              >
                {t.laterDays}
              </button>
            </div>
          </fieldset>
          {days.every((d) => d.times.length === 0) ? (
            <p>{t.noTimesAtAll}</p>
          ) : selectedDay && selectedDay.times.length === 0 ? (
            <p>{t.noTimesThatDay}</p>
          ) : (
            selectedDay && (
              <fieldset>
                <legend>
                  {t.chooseTime}: {formatDay(selectedDay.date, lang)}
                </legend>
                <div className="times">
                  {selectedDay.times.map((s, i) => {
                    const price = selectedDay.prices?.[i]
                    return (
                      <label key={s} className="time">
                        <input type="radio" name="time" value={s} checked={time === s} onChange={() => setTime(s)} />
                        <span>{formatTime(s, location.timeZone, lang)}</span>
                        {price !== undefined && price !== treatment.price && (
                          <span className="time-price">
                            {' '}
                            {formatPrice(price, treatment.currency, lang)}
                          </span>
                        )}
                      </label>
                    )
                  })}
                </div>
              </fieldset>
            )
          )}
          {days.length > 0 && onWaitlist && (
            <p className="waitlist-offer">
              {t.waitlistOffer}{' '}
              <button
                type="button"
                className="link"
                onClick={() =>
                  onWaitlist({
                    firstDay: selectedDay?.date ?? days[0].date,
                    firstBookable: availability.firstBookableDate,
                    lastBookable: availability.lastBookableDate,
                  })
                }
              >
                {t.waitlistJoin}
              </button>
            </p>
          )}
        </>
      )}
      <div className="actions">
        <button type="button" className="secondary" onClick={onBack}>
          {t.back}
        </button>
        <button type="submit" disabled={!time}>
          {nextLabel ?? t.next}
        </button>
      </div>
    </form>
  )
}

function DetailsStep({ location, treatment, start, onBack, onBooked, onTaken, focus }: {
  focus: boolean
  location: OnlineLocation
  treatment: OnlineTreatment
  start: string
  onBack: () => void
  onBooked: (c: OnlineBookingConfirmation, email: string) => void
  onTaken: () => void
}) {
  const { t, lang } = useT()
  const { session, refresh } = useSession()
  const needsName = !session?.hasProfile
  const needsEmail = !session
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [note, setNote] = useState('')
  const [voucher, setVoucher] = useState('')
  const [refused, setRefused] = useState(false)
  const [privacy, setPrivacy] = useState(false)
  const [feeAccepted, setFeeAccepted] = useState(false)
  // Eine Anzahlung ersetzt die Karte für die Ausfallgebühr; beidem stimmt der Gast mit demselben Häkchen zu.
  const depositPercent = treatment.depositPercent ?? location.depositPercent ?? 0
  const feePercent = depositPercent > 0 ? 0 : (treatment.noShowFeePercent ?? location.noShowFeePercent ?? 0)
  const percent = depositPercent > 0 ? depositPercent : feePercent
  const fee = percent > 0 ? formatPrice(Math.round(treatment.price * percent) / 100, treatment.currency, lang) : null
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failed, setFailed] = useState(false)
  const [busy, setBusy] = useState(false)
  const summaryRef = useRef<HTMLDivElement>(null)

  function validate(): Record<string, string> {
    const e: Record<string, string> = {}
    if (needsName && !firstName.trim()) e.firstName = t.required
    if (needsName && !lastName.trim()) e.lastName = t.required
    if (needsEmail && !emailPattern.test(email.trim())) e.email = t.invalidEmail
    if (!privacy) e.privacy = t.privacyRequired
    if (fee && !feeAccepted) e.fee = depositPercent > 0 ? t.depositRequired : t.feeRequired
    return e
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (busy) return
    setFailed(false)
    setRefused(false)
    const found = validate()
    setErrors(found)
    if (Object.keys(found).length > 0) {
      summaryRef.current?.focus()
      return
    }

    setBusy(true)
    try {
      const { data, error, response } = await api.POST('/api/online/bookings', {
        body: {
          locationId: location.id,
          treatmentId: treatment.id,
          start,
          contact: needsName ? { firstName, lastName, email: needsEmail ? email : (session?.email ?? ''), phone: phone || null } : null,
          note: note || null,
          acceptPrivacy: privacy,
          language: lang,
          acceptNoShowFee: feeAccepted,
          voucherCode: voucher.trim() || null,
        },
      })
      if (data) {
        if (session) await refresh()
        onBooked(data, needsEmail ? email.trim() : (session?.email ?? ''))
      } else if (response.status === 409) {
        onTaken()
      } else if (response.status === 403) {
        setRefused(true)
        summaryRef.current?.focus()
      } else {
        const server = fieldErrors(error)
        const mapped: Record<string, string> = {}
        for (const [key, messages] of Object.entries(server)) {
          const field = key.replace(/^contact\./, '')
          if (field === 'voucherCode') mapped.voucher = t.voucherInvalid
          else mapped[field === 'acceptPrivacy' ? 'privacy' : field === 'acceptNoShowFee' ? 'fee' : field] = messages[0]
        }
        setErrors(mapped)
        setFailed(Object.keys(mapped).length === 0)
        summaryRef.current?.focus()
      }
    } catch {
      setFailed(true)
      summaryRef.current?.focus()
    } finally {
      setBusy(false)
    }
  }

  const errorCount = Object.keys(errors).length
  const fieldLabels: Record<string, string> = { firstName: t.firstName, lastName: t.lastName, email: t.email, phone: t.phone, note: t.note, voucher: t.voucher }
  // Sprunglinks statt nur einer Zahl (B61): Textfelder nennen Beschriftung und Fehler, die Häkchen tragen ihren Fehlertext selbst. Der Hash gehört den Routen, deshalb nur der Fokus.
  const position = (key: string) => (DETAIL_FIELDS.includes(key) ? DETAIL_FIELDS.indexOf(key) : DETAIL_FIELDS.length)
  const errorKeys = Object.keys(errors).sort((a, b) => position(a) - position(b))
  const jumpTo = (key: string) => (e: React.MouseEvent) => {
    e.preventDefault()
    document.getElementById(detailId(key))?.focus()
  }

  return (
    <form onSubmit={submit} noValidate>
      <Heading level={2} focus={focus}>
        {t.stepDetails}
      </Heading>
      <div ref={summaryRef} tabIndex={-1}>
        {errorCount > 0 && (
          <div role="alert" className="alert">
            <p>{t.formErrors(errorCount)}</p>
            <ul>
              {errorKeys.map((key) => (
                <li key={key}>
                  {DETAIL_FIELDS.includes(key) ? (
                    <a href={`#${detailId(key)}`} onClick={jumpTo(key)}>
                      {fieldLabels[key] ? `${fieldLabels[key]}: ${errors[key]}` : errors[key]}
                    </a>
                  ) : (
                    errors[key]
                  )}
                </li>
              ))}
            </ul>
          </div>
        )}
        {failed && <Alert>{t.errorGeneric}</Alert>}
        {refused && <Alert>{t.bookingRefused}</Alert>}
      </div>
      {session ? (
        <p>{t.signedInAs(session.email)}</p>
      ) : (
        <p className="hint">
          {t.guestOrSignIn} <a href={href({ page: 'signin' })}>{t.navSignIn}</a>
        </p>
      )}
      {needsName && (
        <div className="two">
          <Field id={detailId('firstName')} label={t.firstName} value={firstName} onChange={setFirstName} autoComplete="given-name" required error={errors.firstName} maxLength={100} />
          <Field id={detailId('lastName')} label={t.lastName} value={lastName} onChange={setLastName} autoComplete="family-name" required error={errors.lastName} maxLength={100} />
        </div>
      )}
      {needsEmail && (
        <Field id={detailId('email')} label={t.email} value={email} onChange={setEmail} type="email" autoComplete="email" required hint={t.emailHint} error={errors.email} maxLength={254} />
      )}
      {needsName && <Field id={detailId('phone')} label={t.phone} value={phone} onChange={setPhone} type="tel" autoComplete="tel" hint={t.phoneHint} maxLength={40} />}
      <Field id={detailId('note')} label={t.note} value={note} onChange={setNote} multiline maxLength={500} />
      <Field id={detailId('voucher')} label={t.voucher} value={voucher} onChange={setVoucher} hint={t.voucherHint} error={errors.voucher} maxLength={40} />
      <div className="field">
        <label className="check">
          <input
            id={detailId('privacy')}
            type="checkbox"
            aria-required="true"
            checked={privacy}
            onChange={(e) => setPrivacy(e.target.checked)}
            aria-invalid={errors.privacy ? true : undefined}
            aria-describedby={errors.privacy ? 'privacy-error' : undefined}
          />
          <span>
            {t.privacy} <span className="required">({t.required})</span>
          </span>
        </label>
        {errors.privacy && (
          <span id="privacy-error" className="field-error">
            {errors.privacy}
          </span>
        )}
      </div>
      {fee && (
        <div className="field">
          <p id="fee-terms" className="hint">
            {depositPercent > 0
              ? t.depositTerms(depositPercent, fee, location.cancellationDeadlineHours)
              : t.feeTerms(feePercent, fee, location.cancellationDeadlineHours)}
            {location.noShowFeeNewCustomersOnly && ` ${depositPercent > 0 ? t.depositNewGuestsOnly : t.feeNewGuestsOnly}`}
          </p>
          <label className="check">
            <input
              id={detailId('fee')}
              type="checkbox"
              aria-required="true"
              checked={feeAccepted}
              onChange={(e) => setFeeAccepted(e.target.checked)}
              aria-invalid={errors.fee ? true : undefined}
              aria-describedby={errors.fee ? 'fee-terms fee-error' : 'fee-terms'}
            />
            <span>
              {depositPercent > 0 ? t.depositAccept(fee) : t.feeAccept(fee)} <span className="required">({t.required})</span>
            </span>
          </label>
          {errors.fee && (
            <span id="fee-error" className="field-error">
              {errors.fee}
            </span>
          )}
        </div>
      )}
      <div className="summary">
        <h3>{t.summary}</h3>
        <p>
          {treatment.name}, {formatDay(localDate(start, location.timeZone), lang)}, {formatTime(start, location.timeZone, lang)} · {location.name} ·{' '}
          {formatPrice(treatment.price, treatment.currency, lang)}
        </p>
        <p className="hint">{t.cancelPolicy(location.cancellationDeadlineHours)}</p>
        {location.guestNote && (
          <p className="guest-note">
            <strong>{t.guestNote}</strong> {location.guestNote}
          </p>
        )}
      </div>
      <div className="actions">
        <button type="button" className="secondary" onClick={onBack}>
          {t.back}
        </button>
        <button type="submit" aria-disabled={busy}>
          {busy ? t.booking : t.bookNow}
        </button>
      </div>
    </form>
  )
}

function Confirmation({ confirmation, email, onAgain }: { confirmation: OnlineBookingConfirmation; email: string; onAgain: () => void }) {
  const { t } = useT()
  const [appointment, setAppointment] = useState(confirmation.appointment)
  const token = confirmation.manageToken
  return (
    <>
      <Heading focus>{t.confirmedTitle}</Heading>
      <Status tone="success">{t.confirmedText(email)}</Status>
      <AppointmentFacts appointment={appointment} />
      <CardGuaranteeBox
        appointment={appointment}
        onChange={setAppointment}
        reload={() => api.GET('/api/online/bookings/{token}', { params: { path: { token } } })}
        test={(saved) => api.POST('/api/online/bookings/{token}/card/test', { params: { path: { token } }, body: { saved } })}
        retry={() => api.POST('/api/online/bookings/{token}/card/retry', { params: { path: { token } } })}
      />
      <DepositBox
        appointment={appointment}
        onChange={setAppointment}
        reload={() => api.GET('/api/online/bookings/{token}', { params: { path: { token } } })}
        test={(paid) => api.POST('/api/online/bookings/{token}/deposit/test', { params: { path: { token } }, body: { paid } })}
        retry={() => api.POST('/api/online/bookings/{token}/deposit/retry', { params: { path: { token } } })}
      />
      <p>{t.confirmedAnamnesis}</p>
      <div className="actions">
        <a className="button" href={href({ page: 'anamnesis', token: confirmation.anamnesisToken })}>
          {t.fillAnamnesis}
        </a>
        <a className="button secondary" href={href({ page: 'manage', token: confirmation.manageToken })}>
          {t.manageBooking}
        </a>
        <button type="button" className="link" onClick={onAgain}>
          {t.bookAnother}
        </button>
      </div>
    </>
  )
}
