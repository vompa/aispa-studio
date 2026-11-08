import { useEffect, useState } from 'react'
import type { OnlineAppointment } from './api'
import { Alert, Status } from './components'
import { formatDateTime, formatPrice, useT } from './i18n'

type Call = () => Promise<{ data?: OnlineAppointment }>

/**
 * Anzahlung oder Vorauszahlung eines Online-Termins bezahlen. Zur Seite des Zahlungsanbieters geht es nur auf Knopfdruck
 * (WCAG 3.2.5); solange die Seite vorbereitet oder der Beleg ausgestellt wird, fragt die Ansicht kurz nach. Im Testbetrieb
 * wird die Zahlung hier simuliert.
 */
export function DepositBox({ appointment, onChange, reload, test, retry }: {
  appointment: OnlineAppointment
  onChange: (a: OnlineAppointment) => void
  reload: Call
  test: (paid: boolean) => Promise<{ data?: OnlineAppointment }>
  retry: Call
}) {
  const { t, lang } = useT()
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)
  const deposit = appointment.deposit
  const booked = appointment.state === 'Booked'
  const waiting = (deposit?.status === 'Pending' && !deposit.testMode && !deposit.paymentUrl && booked) || deposit?.status === 'Paid'

  useEffect(() => {
    if (!waiting) return
    const timer = setTimeout(async () => {
      try {
        const { data } = await reload()
        if (data) onChange(data)
      } catch {
        // Beim nächsten Durchlauf erneut versuchen.
      }
    }, 1500)
    return () => clearTimeout(timer)
  }, [waiting, reload, onChange, appointment])

  if (!deposit) return null
  const amount = formatPrice(deposit.amount, deposit.currency, lang)
  const payUntil = deposit.payUntil && booked ? t.depositPayUntil(formatDateTime(deposit.payUntil, appointment.timeZone, lang)) : null

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
    <section className="card-guarantee" aria-labelledby={`deposit-${appointment.id}`}>
      <h3 id={`deposit-${appointment.id}`}>{t.depositTitle(deposit.percent)}</h3>
      {failed && <Alert>{t.errorGeneric}</Alert>}
      {deposit.status === 'Pending' && booked && (
        <>
          <p>{t.depositMissing(amount)}</p>
          {payUntil && <p>{payUntil}</p>}
          {deposit.testMode ? (
            <div className="test-payment">
              <p>{t.depositTestText}</p>
              <div className="actions">
                <button type="button" onClick={() => void run(() => test(true))} disabled={busy}>
                  {t.depositTestPay}
                </button>
                <button type="button" className="secondary" onClick={() => void run(() => test(false))} disabled={busy}>
                  {t.cardTestCancel}
                </button>
              </div>
            </div>
          ) : deposit.paymentUrl ? (
            <p className="actions">
              <a className="button" href={deposit.paymentUrl}>
                {t.depositPay(amount)}
              </a>
            </p>
          ) : (
            <Status>{t.depositPreparing}</Status>
          )}
        </>
      )}
      {deposit.status === 'Failed' && (
        <>
          <p>{t.depositFailed}</p>
          {payUntil && <p>{payUntil}</p>}
          {booked && (
            <p className="actions">
              <button type="button" onClick={() => void run(retry)} disabled={busy}>
                {t.cardRetry}
              </button>
            </p>
          )}
        </>
      )}
      {deposit.status === 'Paid' && <Status>{t.depositPaid(amount)}</Status>}
      {deposit.status === 'Receipted' && <p>{t.depositReceipted(amount, deposit.receipt ?? '')}</p>}
      {deposit.status === 'Refunded' && <p>{t.depositRefunded(amount)}</p>}
      {deposit.status === 'Released' && <p>{t.depositReleased}</p>}
      {deposit.status === 'Cancelled' && <p>{t.depositCancelled}</p>}
    </section>
  )
}
