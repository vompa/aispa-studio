import { useEffect, useRef, useState } from 'react'
import { api, fieldErrors, type OnlineLocation, type OnlineVoucherOffer, type VoucherKind, type VoucherOrderDto } from './api'
import { Alert, Field, Heading, Status } from './components'
import { formatPrice, useT, type Language } from './i18n'
import { href } from './router'
import { useSession } from './session'

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/** Betrag aus der Eingabe, mit Komma oder Punkt; ungültig: NaN. */
function parseAmount(text: string): number {
  const normalized = text.trim().replace(',', '.')
  return /^\d+(\.\d{1,2})?$/.test(normalized) ? Number(normalized) : Number.NaN
}

/**
 * Gutschein kaufen: Wertgutschein mit Betrag oder Gutschein für eine Behandlung, Empfänger und Nachricht, Käuferdaten. Danach geht
 * es zum Zahlungsanbieter; der Gutschein kommt nach der Zahlung per Mail.
 */
export function VoucherShopPage({ navigatedHere }: { navigatedHere: boolean }) {
  const { t, lang } = useT()
  const { session } = useSession()
  const [locations, setLocations] = useState<OnlineLocation[] | null>(null)
  const [locationId, setLocationId] = useState<string | null>(null)
  const [loaded, setLoaded] = useState<{ locationId: string; offer: OnlineVoucherOffer } | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    api.GET('/api/online/locations').then(
      ({ data }) => {
        if (!data) return setFailed(true)
        setLocations(data)
        if (data.length > 0) setLocationId(data[0].id)
      },
      () => setFailed(true),
    )
  }, [])

  useEffect(() => {
    if (!locationId) return
    let active = true
    api.GET('/api/online/locations/{locationId}/vouchers', { params: { path: { locationId } } }).then(
      ({ data }) => active && (data ? setLoaded({ locationId, offer: data }) : setFailed(true)),
      () => active && setFailed(true),
    )
    return () => {
      active = false
    }
  }, [locationId])

  const offer = loaded && loaded.locationId === locationId ? loaded.offer : null

  return (
    <>
      <Heading focus={navigatedHere}>{t.voucherTitle}</Heading>
      <p>{t.voucherIntro}</p>
      {failed && <Alert>{t.errorOffline}</Alert>}
      {!failed && !locations && <Status>{t.loading}</Status>}
      {locations && locations.length > 1 && (
        <div className="field">
          <label htmlFor="voucher-location">{t.voucherLocation}</label>
          <select id="voucher-location" value={locationId ?? ''} onChange={(e) => setLocationId(e.target.value)}>
            {locations.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </select>
        </div>
      )}
      {locations && !offer && !failed && <Status>{t.loading}</Status>}
      {offer && !offer.available && <Alert>{offer.reason ?? t.voucherUnavailable}</Alert>}
      {offer && offer.available && session !== undefined && <VoucherForm key={offer.locationId} offer={offer} lang={lang} />}
    </>
  )
}

function VoucherForm({ offer, lang }: { offer: OnlineVoucherOffer; lang: Language }) {
  const { t } = useT()
  const { session } = useSession()
  const needsName = !session?.hasProfile
  const needsEmail = !session
  const money = (n: number) => formatPrice(n, offer.currency, lang)
  const [kind, setKind] = useState<VoucherKind>('MultiPurpose')
  const [preset, setPreset] = useState<string>(String(offer.suggestedAmounts[1] ?? offer.suggestedAmounts[0] ?? offer.minAmount))
  const [other, setOther] = useState('')
  const [treatmentId, setTreatmentId] = useState<string | null>(offer.treatments[0]?.id ?? null)
  const [recipient, setRecipient] = useState('')
  const [message, setMessage] = useState('')
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [privacy, setPrivacy] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failed, setFailed] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const summaryRef = useRef<HTMLDivElement>(null)

  const amount = kind === 'SinglePurpose' ? (offer.treatments.find((x) => x.id === treatmentId)?.price ?? Number.NaN) : preset === 'other' ? parseAmount(other) : Number(preset)
  const amountValid = !Number.isNaN(amount) && amount >= offer.minAmount && amount <= offer.maxAmount

  function validate(): Record<string, string> {
    const e: Record<string, string> = {}
    if (kind === 'MultiPurpose' && !amountValid) e.amount = t.voucherAmountInvalid(money(offer.minAmount), money(offer.maxAmount))
    if (kind === 'SinglePurpose' && !treatmentId) e.treatmentId = t.required
    if (needsName && !firstName.trim()) e.firstName = t.required
    if (needsName && !lastName.trim()) e.lastName = t.required
    if (needsEmail && !emailPattern.test(email.trim())) e.email = t.invalidEmail
    if (!privacy) e.privacy = t.privacyRequired
    return e
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    setFailed(null)
    const found = validate()
    setErrors(found)
    if (Object.keys(found).length > 0) {
      summaryRef.current?.focus()
      return
    }

    setBusy(true)
    try {
      const { data, error, response } = await api.POST('/api/online/vouchers/orders', {
        body: {
          locationId: offer.locationId,
          kind,
          amount: kind === 'MultiPurpose' ? amount : null,
          treatmentId: kind === 'SinglePurpose' ? treatmentId : null,
          buyer: needsName ? { firstName, lastName, email: needsEmail ? email : (session?.email ?? ''), phone: null } : null,
          recipientName: recipient || null,
          message: message || null,
          acceptPrivacy: privacy,
          language: lang,
        },
      })
      if (data) {
        window.location.assign(data.paymentUrl)
        return
      }
      if (response.status === 409) {
        setFailed((error as { title?: string } | undefined)?.title ?? t.voucherUnavailable)
      } else {
        const mapped: Record<string, string> = {}
        for (const [key, messages] of Object.entries(fieldErrors(error))) {
          const field = key.replace(/^buyer\./, '').replace(/^contact\./, '')
          mapped[field === 'acceptPrivacy' ? 'privacy' : field] = messages[0]
        }
        setErrors(mapped)
        if (Object.keys(mapped).length === 0) setFailed(t.errorGeneric)
      }
      summaryRef.current?.focus()
    } catch {
      setFailed(t.errorOffline)
    } finally {
      setBusy(false)
    }
  }

  const errorCount = Object.keys(errors).length

  return (
    <form onSubmit={submit} noValidate className="step">
      <div ref={summaryRef} tabIndex={-1}>
        {errorCount > 0 && <Alert>{t.formErrors(errorCount)}</Alert>}
        {failed && <Alert>{failed}</Alert>}
      </div>
      {offer.testPayment && <p className="notice-test">{t.voucherTestMode}</p>}
      <fieldset>
        <legend>{t.voucherKind}</legend>
        <div className="choices">
          <label className="choice">
            <input type="radio" name="kind" checked={kind === 'MultiPurpose'} onChange={() => setKind('MultiPurpose')} />
            <span className="choice-text">
              <span className="choice-label">{t.voucherValue}</span>
              <span className="choice-detail">{t.voucherValueHint}</span>
            </span>
          </label>
          {offer.treatments.length > 0 && (
            <label className="choice">
              <input type="radio" name="kind" checked={kind === 'SinglePurpose'} onChange={() => setKind('SinglePurpose')} />
              <span className="choice-text">
                <span className="choice-label">{t.voucherTreatment}</span>
                <span className="choice-detail">{t.voucherTreatmentHint}</span>
              </span>
            </label>
          )}
        </div>
      </fieldset>

      {kind === 'MultiPurpose' ? (
        <fieldset aria-describedby={errors.amount ? 'amount-error' : undefined}>
          <legend>{t.voucherAmount}</legend>
          <div className="times">
            {offer.suggestedAmounts.map((a) => (
              <label key={a} className="time">
                <input type="radio" name="amount" checked={preset === String(a)} onChange={() => setPreset(String(a))} />
                <span>{money(a)}</span>
              </label>
            ))}
            <label className="time">
              <input type="radio" name="amount" checked={preset === 'other'} onChange={() => setPreset('other')} />
              <span>{t.voucherOtherAmount}</span>
            </label>
          </div>
          {preset === 'other' && (
            <Field
              label={`${t.voucherOtherAmount} (${offer.currency})`}
              value={other}
              onChange={setOther}
              inputMode="decimal"
              hint={t.voucherAmountHint(money(offer.minAmount), money(offer.maxAmount))}
              error={errors.amount}
              maxLength={8}
            />
          )}
          {errors.amount && preset !== 'other' && (
            <span id="amount-error" className="field-error">
              {errors.amount}
            </span>
          )}
        </fieldset>
      ) : (
        <fieldset>
          <legend>{t.voucherChooseTreatment}</legend>
          <div className="choices">
            {offer.treatments.map((x) => (
              <label key={x.id} className="choice">
                <input type="radio" name="treatment" checked={treatmentId === x.id} onChange={() => setTreatmentId(x.id)} />
                <span className="choice-text">
                  <span className="choice-label">{x.name}</span>
                  <span className="choice-detail">
                    {t.minutes(x.durationMinutes)} · {money(x.price)}
                  </span>
                </span>
              </label>
            ))}
          </div>
        </fieldset>
      )}

      <Field label={t.voucherRecipient} value={recipient} onChange={setRecipient} hint={t.voucherRecipientHint} maxLength={100} />
      <Field label={t.voucherMessage} value={message} onChange={setMessage} multiline maxLength={300} />

      <h2>{t.voucherBuyer}</h2>
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
      {needsEmail && (
        <Field label={t.email} value={email} onChange={setEmail} type="email" autoComplete="email" required hint={t.voucherEmailHint} error={errors.email} maxLength={254} />
      )}
      <div className="field">
        <label className="check">
          <input
            type="checkbox"
            aria-required="true"
            checked={privacy}
            onChange={(e) => setPrivacy(e.target.checked)}
            aria-invalid={errors.privacy ? true : undefined}
            aria-describedby={errors.privacy ? 'voucher-privacy-error' : undefined}
          />
          <span>
            {t.privacyVoucher} <span className="required">({t.required})</span>
          </span>
        </label>
        {errors.privacy && (
          <span id="voucher-privacy-error" className="field-error">
            {errors.privacy}
          </span>
        )}
      </div>
      <div className="summary">
        <h3>{t.voucherTotal}</h3>
        <p>{amountValid || kind === 'SinglePurpose' ? money(amount) : '–'}</p>
      </div>
      <div className="actions">
        <button type="submit" disabled={busy} aria-disabled={busy}>
          {busy ? t.voucherPaying : t.voucherPay}
        </button>
      </div>
    </form>
  )
}

/** Bestellung nach dem Bezahlen: wartet auf die Kasse, zeigt dann den Gutschein zum Ausdrucken. Im Testbetrieb mit Testzahlung. */
/** Zur Zahlungsseite des Anbieters geht es nur auf Knopfdruck, damit nichts ungefragt den Kontext wechselt (WCAG 3.2.5). */
function PayNow({ url }: { url: string }) {
  const { t } = useT()
  return (
    <>
      <Status>{t.voucherPayOpen}</Status>
      <p className="actions">
        <a className="button" href={url}>
          {t.voucherPayNow}
        </a>
      </p>
    </>
  )
}

export function VoucherOrderPage({ token, navigatedHere }: { token: string; navigatedHere: boolean }) {
  const { t, lang } = useT()
  const [order, setOrder] = useState<VoucherOrderDto | null | undefined>(undefined)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let active = true
    let timer: ReturnType<typeof setTimeout> | undefined
    const load = async () => {
      try {
        const { data } = await api.GET('/api/online/vouchers/orders/{token}', { params: { path: { token } } })
        if (!active) return
        setOrder(data ?? null)
        // Bezahlt, aber noch nicht ausgestellt (oder Zahlung noch nicht bestätigt): weiter nachsehen.
        if (data && (data.status === 'Paid' || (data.status === 'Pending' && !data.testPayment))) timer = setTimeout(load, 1500)
      } catch {
        if (active) setOrder(null)
      }
    }
    void load()
    return () => {
      active = false
      clearTimeout(timer)
    }
  }, [token, order?.status])

  async function testPayment(paid: boolean) {
    setBusy(true)
    try {
      const { data } = await api.POST('/api/online/vouchers/orders/{token}/test-payment', { params: { path: { token } }, body: { paid } })
      if (data) setOrder(data)
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <Heading focus={navigatedHere}>{t.voucherOrderTitle}</Heading>
      {order === undefined && <Status>{t.loading}</Status>}
      {order === null && <Alert>{t.linkInvalid}</Alert>}
      {order?.status === 'Pending' && order.testPayment && (
        <section className="test-payment" aria-labelledby="test-payment">
          <h2 id="test-payment">{t.voucherTestPayment}</h2>
          <p>{t.voucherTestPaymentText}</p>
          <p className="summary-amount">
            {t.voucherTotal}: <strong>{formatPrice(order.amount, order.currency, lang)}</strong>
          </p>
          <div className="actions">
            <button type="button" onClick={() => void testPayment(true)} disabled={busy}>
              {t.voucherTestPay}
            </button>
            <button type="button" className="secondary" onClick={() => void testPayment(false)} disabled={busy}>
              {t.voucherTestCancel}
            </button>
          </div>
        </section>
      )}
      {order?.status === 'Pending' && !order.testPayment && !order.paymentUrl && <Status>{t.voucherWaitingPayment}</Status>}
      {order?.status === 'Pending' && !order.testPayment && order.paymentUrl && <PayNow url={order.paymentUrl} />}
      {order?.status === 'Paid' && <Status>{t.voucherIssuing}</Status>}
      {order?.status === 'Cancelled' && (
        <>
          <Alert>{t.voucherCancelled}</Alert>
          <p>
            <a href={href({ page: 'voucher' })}>{t.voucherTryAgain}</a>
          </p>
        </>
      )}
      {order?.status === 'Failed' && <Alert>{t.voucherFailed}</Alert>}
      {order?.status === 'Refunded' && <Status>{t.voucherRefunded}</Status>}
      {order?.status === 'Issued' && (
        <>
          <Status tone="success">{t.voucherIssued}</Status>
          <article className="voucher-card print-voucher" aria-label={t.voucherOrderTitle}>
            <p className="voucher-location">{order.locationName}</p>
            <p className="voucher-what">
              {order.treatmentName ?? t.voucherValue}
              {!order.treatmentName && <> {t.voucherWorth(formatPrice(order.amount, order.currency, lang))}</>}
            </p>
            {order.recipientName && (
              <p>
                {t.voucherFor}: <strong>{order.recipientName}</strong>
              </p>
            )}
            {order.message && <p className="voucher-message">{order.message}</p>}
            <p className="voucher-code">
              <span>{t.voucherCode}</span>
              <strong>{order.code}</strong>
            </p>
            <p className="hint">{t.voucherRedeem(order.locationName)}</p>
          </article>
          <div className="actions">
            <button type="button" className="secondary" onClick={() => window.print()}>
              {t.voucherPrint}
            </button>
            <a href={href({ page: 'book' })}>{t.navBook}</a>
          </div>
        </>
      )}
    </>
  )
}
