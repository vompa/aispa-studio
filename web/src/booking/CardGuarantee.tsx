import { useEffect, useState } from 'react'
import type { OnlineAppointment } from './api'
import { Alert, Status } from './components'
import { formatDateTime, formatPrice, useT } from './i18n'

type Call = () => Promise<{ data?: OnlineAppointment }>

/**
 * Karte für die Ausfallgebühr hinterlegen. Zur Seite des Zahlungsanbieters geht es nur auf Knopfdruck (WCAG 3.2.5); solange
 * die Seite vorbereitet wird, fragt die Ansicht kurz nach. Im Testbetrieb wird das Hinterlegen hier simuliert.
 */
export function CardGuaranteeBox({ appointment, onChange, reload, test, retry, accept }: {
  appointment: OnlineAppointment
  onChange: (a: OnlineAppointment) => void
  reload: Call
  test: (saved: boolean) => Promise<{ data?: OnlineAppointment }>
  retry: Call
  /** Zustimmung zur Gebühr, die die Rezeption per Link angefordert hat; fehlt bei der Online-Buchung (dort beim Buchen). */
  accept?: Call
}) {
  const { t, lang } = useT()
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)
  const [agreed, setAgreed] = useState(false)
  const card = appointment.cardGuarantee
  const preparing = card?.status === 'Pending' && !card.testMode && !card.setupUrl && appointment.state === 'Booked'

  useEffect(() => {
    if (!preparing) return
    const timer = setTimeout(async () => {
      try {
        const { data } = await reload()
        if (data) onChange(data)
      } catch {
        // Beim nächsten Durchlauf erneut versuchen.
      }
    }, 1500)
    return () => clearTimeout(timer)
  }, [preparing, reload, onChange, appointment])

  if (!card) return null
  const amount = formatPrice(card.feeAmount, card.currency, lang)
  const holdUntil = card.holdUntil && appointment.state === 'Booked' ? t.cardHoldUntil(formatDateTime(card.holdUntil, appointment.timeZone, lang)) : null

  async function run(call: Call) {
    setBusy(true)
    setFailed(false)
    try {
      const { data } = await call()
      if (data) onChange(data)
      else setFailed(true)
    } catch {
      setFailed(true)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card-guarantee" aria-labelledby={`card-${appointment.id}`}>
      <h3 id={`card-${appointment.id}`}>{t.cardTitle}</h3>
      {failed && <Alert>{t.errorGeneric}</Alert>}
      {card.status === 'AwaitingConsent' && appointment.state === 'Booked' && accept && (
        <>
          <p id={`card-terms-${appointment.id}`}>{t.feeTerms(card.feePercent, amount, card.deadlineHours)}</p>
          <label className="check">
            <input
              type="checkbox"
              checked={agreed}
              onChange={(e) => setAgreed(e.target.checked)}
              aria-describedby={`card-terms-${appointment.id}`}
            />
            <span>{t.feeAccept(amount)}</span>
          </label>
          <p className="actions">
            <button type="button" onClick={() => void run(accept)} disabled={busy || !agreed}>
              {t.cardAccept}
            </button>
          </p>
        </>
      )}
      {card.status === 'Pending' && appointment.state === 'Booked' && (
        <>
          <p>{t.cardMissing}</p>
          {holdUntil && <p>{holdUntil}</p>}
          {card.testMode ? (
            <div className="test-payment">
              <p>{t.cardTestText}</p>
              <div className="actions">
                <button type="button" onClick={() => void run(() => test(true))} disabled={busy}>
                  {t.cardTestSave}
                </button>
                <button type="button" className="secondary" onClick={() => void run(() => test(false))} disabled={busy}>
                  {t.cardTestCancel}
                </button>
              </div>
            </div>
          ) : card.setupUrl ? (
            <p className="actions">
              <a className="button" href={card.setupUrl}>
                {t.cardSave}
              </a>
            </p>
          ) : (
            <Status>{t.cardPreparing}</Status>
          )}
        </>
      )}
      {card.status === 'Failed' && (
        <>
          <p>{t.cardFailed}</p>
          {holdUntil && <p>{holdUntil}</p>}
          {appointment.state === 'Booked' && (
            <p className="actions">
              <button type="button" onClick={() => void run(retry)} disabled={busy}>
                {t.cardRetry}
              </button>
            </p>
          )}
        </>
      )}
      {(card.status === 'Saved' || card.status === 'Charging' || card.status === 'ChargeFailed') && <p>{t.cardSaved(amount)}</p>}
      {card.status === 'Charged' && <p>{t.cardCharged(amount)}</p>}
      {card.status === 'Refunded' && <p>{t.cardRefunded(amount)}</p>}
      {card.status === 'Released' && <p>{t.cardReleased}</p>}
    </section>
  )
}
