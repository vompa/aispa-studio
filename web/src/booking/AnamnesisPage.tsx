import { useEffect, useRef, useState } from 'react'
import { api, fieldErrors, type AnamnesisFormDefinition, type AnamnesisLinkInfo } from './api'
import { Alert, Field, Heading, Status } from './components'
import { formatDateTime, useT } from './i18n'
import { href } from './router'
import { useSession } from './session'

type Answer = { yes: boolean | null; details: string }

/**
 * Gesundheitsfragebogen: über den Link aus der Mail oder vom Papierbogen (ohne Konto), am Tablet des Empfangs oder angemeldet.
 * Die Fragen kommen vom Server, damit alle Wege denselben Bogen haben. Ja/Nein als Optionsfelder; „Ja“ öffnet ein beschriftetes Feld für Details.
 */
export function AnamnesisPage({ token, navigatedHere }: { token?: string; navigatedHere: boolean }) {
  const { t, lang } = useT()
  const { session } = useSession()
  const [form, setForm] = useState<AnamnesisFormDefinition | null>(null)
  const [loadedInfo, setInfo] = useState<AnamnesisLinkInfo | null | undefined>(undefined)
  const [answers, setAnswers] = useState<Record<string, Answer>>({})
  const [consent, setConsent] = useState(false)
  const [signature, setSignature] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failed, setFailed] = useState(false)
  const [busy, setBusy] = useState(false)
  const [sent, setSent] = useState(false)
  const summaryRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    api.GET('/api/online/anamnesis/form', { params: { query: { language: lang } } }).then(
      ({ data }) => setForm(data ?? null),
      () => setFailed(true),
    )
  }, [lang])

  useEffect(() => {
    if (token) {
      api.GET('/api/online/anamnesis/{token}', { params: { path: { token } } }).then(
        ({ data }) => setInfo(data ?? null),
        () => setInfo(null),
      )
    } else if (session) {
      api.GET('/api/online/me/anamnesis').then(
        ({ data }) => setInfo(data ?? null),
        () => setInfo(null),
      )
    }
  }, [token, session])

  // Ohne Link und ohne Anmeldung gibt es nichts zu laden.
  const info = !token && session === null ? null : loadedInfo

  function answer(key: string, change: Partial<Answer>) {
    setAnswers((a) => ({ ...a, [key]: { ...(a[key] ?? { yes: null, details: '' }), ...change } }))
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (!form) return
    setFailed(false)
    const found: Record<string, string> = {}
    for (const q of form.questions) {
      if (q.kind === 'YesNo' && answers[q.key]?.yes == null) found[q.key] = t.answerRequired
    }
    if (!consent) found.consentHealthData = t.consentRequired
    if (!signature.trim()) found.signature = t.required
    setErrors(found)
    if (Object.keys(found).length > 0) {
      summaryRef.current?.focus()
      return
    }

    const body = {
      answers: form.questions.map((q) => ({
        key: q.key,
        yes: q.kind === 'YesNo' ? (answers[q.key]?.yes ?? null) : null,
        details: answers[q.key]?.details || null,
      })),
      consentHealthData: consent,
      signature,
      language: lang,
    }
    setBusy(true)
    try {
      const result = token
        ? await api.POST('/api/online/anamnesis/{token}', { params: { path: { token } }, body })
        : await api.POST('/api/online/me/anamnesis', { body })
      if (result.response.ok) {
        setSent(true)
      } else {
        const server = fieldErrors(result.error)
        setErrors(Object.fromEntries(Object.entries(server).map(([k, v]) => [k, v[0]])))
        setFailed(Object.keys(server).length === 0)
        summaryRef.current?.focus()
      }
    } catch {
      setFailed(true)
    } finally {
      setBusy(false)
    }
  }

  if (sent && loadedInfo?.channel === 'Tablet') {
    // Das Tablet geht an die nächste Person: keine Links in ein Konto, der Link selbst ist jetzt verbraucht.
    return (
      <>
        <Heading focus>{t.anamnesisTitle}</Heading>
        <Status tone="success">{t.anamnesisTabletThanks}</Status>
      </>
    )
  }

  if (sent) {
    return (
      <>
        <Heading focus>{t.anamnesisTitle}</Heading>
        <Status tone="success">{t.anamnesisThanks}</Status>
        <p>
          <a href={href({ page: session ? 'mine' : 'book' })}>{session ? t.navMine : t.bookAnother}</a>
        </p>
      </>
    )
  }

  if (info === null) {
    return (
      <>
        <Heading focus={navigatedHere}>{t.anamnesisTitle}</Heading>
        {token ? <Alert>{t.linkInvalid}</Alert> : <a className="button" href={href({ page: 'signin' })}>{t.navSignIn}</a>}
      </>
    )
  }

  if (!form || info === undefined) {
    return (
      <>
        <Heading focus={navigatedHere}>{t.anamnesisTitle}</Heading>
        {failed ? <Alert>{t.errorGeneric}</Alert> : <Status>{t.loading}</Status>}
      </>
    )
  }

  const what = info.treatmentName && info.start && info.timeZone
    ? `${info.treatmentName}, ${formatDateTime(info.start, info.timeZone, lang)}`
    : ''
  const errorCount = Object.keys(errors).length

  return (
    <form onSubmit={submit} noValidate>
      <Heading focus={navigatedHere}>{t.anamnesisTitle}</Heading>
      <p>{t.anamnesisFor(info.firstName, what)}</p>
      <p className="hint">{form.intro}</p>
      {info.lastSubmittedAt && <p className="hint">{t.anamnesisLast(formatDateTime(info.lastSubmittedAt, info.timeZone ?? 'Europe/Vienna', lang))}</p>}
      <div ref={summaryRef} tabIndex={-1}>
        {errorCount > 0 && <Alert>{t.formErrors(errorCount)}</Alert>}
        {failed && <Alert>{t.errorGeneric}</Alert>}
      </div>

      {form.questions.map((q) =>
        q.kind === 'YesNo' ? (
          <fieldset
            key={q.key}
            className="question"
            aria-describedby={errors[q.key] ? `${q.key}-error` : undefined}
          >
            <legend>
              {q.label}
              <span className="required"> ({t.required})</span>
            </legend>
            <div className="yesno">
              <label className="choice inline">
                <input type="radio" name={q.key} checked={answers[q.key]?.yes === true} onChange={() => answer(q.key, { yes: true })} />
                <span>{t.yes}</span>
              </label>
              <label className="choice inline">
                <input type="radio" name={q.key} checked={answers[q.key]?.yes === false} onChange={() => answer(q.key, { yes: false })} />
                <span>{t.no}</span>
              </label>
            </div>
            {errors[q.key] && (
              <span id={`${q.key}-error`} className="field-error">
                {errors[q.key]}
              </span>
            )}
            {answers[q.key]?.yes && q.detailsLabel && (
              <Field label={q.detailsLabel} value={answers[q.key]?.details ?? ''} onChange={(v) => answer(q.key, { details: v })} maxLength={1000} />
            )}
          </fieldset>
        ) : (
          <Field key={q.key} label={q.label} value={answers[q.key]?.details ?? ''} onChange={(v) => answer(q.key, { details: v })} multiline maxLength={1000} />
        ),
      )}

      <fieldset className="question">
        <legend>{t.consent}</legend>
        <label className="check">
          <input
            type="checkbox"
            aria-required="true"
            checked={consent}
            onChange={(e) => setConsent(e.target.checked)}
            aria-invalid={errors.consentHealthData ? true : undefined}
            aria-describedby={errors.consentHealthData ? 'consent-error' : undefined}
          />
          <span>
            {form.consentText} <span className="required">({t.required})</span>
          </span>
        </label>
        {errors.consentHealthData && (
          <span id="consent-error" className="field-error">
            {errors.consentHealthData}
          </span>
        )}
      </fieldset>
      <Field label={t.signature} value={signature} onChange={setSignature} autoComplete="name" required error={errors.signature} maxLength={200} />
      <div className="actions">
        <button type="submit" disabled={busy}>
          {busy ? t.submitting : t.submitAnamnesis}
        </button>
      </div>
    </form>
  )
}
