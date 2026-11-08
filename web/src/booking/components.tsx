import { useEffect, useId, useRef } from 'react'
import { formatDateTime, formatPrice, useT } from './i18n'
import type { OnlineAppointment } from './api'
import type { components } from '../api/schema'
import { address, isAppleDevice, routeUrl } from './offer'

type LocationContact = components['schemas']['LocationContact']

/**
 * Überschrift einer Seite oder eines Schritts. Mit `focus` bekommt sie beim Erscheinen den Fokus, damit Screenreader nach
 * einem Seiten- oder Schrittwechsel an der richtigen Stelle weiterlesen (die Seite lädt dabei nicht neu). Beim ersten Laden
 * bleibt der Fokus am Seitenanfang.
 */
export function Heading({ children, level = 1, focus = false, id }: { children: React.ReactNode; level?: 1 | 2; focus?: boolean; id?: string }) {
  const ref = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    if (focus) ref.current?.focus()
  }, [focus])
  const Tag = level === 1 ? 'h1' : 'h2'
  return (
    <Tag ref={ref} id={id} tabIndex={-1} className="heading">
      {children}
    </Tag>
  )
}

/** Fehlermeldung mit Weg heraus: „Erneut versuchen“ lädt neu (B60). */
export function RetryAlert({ children, onRetry }: { children: React.ReactNode; onRetry: () => void }) {
  const { t } = useT()
  return (
    <>
      <Alert>{children}</Alert>
      <button type="button" onClick={onRetry}>
        {t.retry}
      </button>
    </>
  )
}

/** Fehlermeldung, die sofort vorgelesen wird. */
export function Alert({ children }: { children: React.ReactNode }) {
  return (
    <p role="alert" className="alert">
      {children}
    </p>
  )
}

/**
 * Hinweis, der höflich vorgelesen wird (Laden, Erfolg). Mit `focus` bekommt er beim Erscheinen den Fokus: für Ergebnisse
 * nach einer Aktion, deren Knopf dabei verschwindet. So bleibt der Fokus nicht am Seitenende hängen, und das Ergebnis wird
 * sicher vorgelesen, auch wenn eine Live-Region erst mit ihrem Text erscheint.
 */
export function Status({ children, tone, focus = false }: { children: React.ReactNode; tone?: 'success'; focus?: boolean }) {
  const ref = useRef<HTMLParagraphElement>(null)
  useEffect(() => {
    if (focus) ref.current?.focus()
  }, [focus])
  return (
    <p ref={ref} role="status" tabIndex={focus ? -1 : undefined} className={tone === 'success' ? 'status success' : 'status'}>
      {children}
    </p>
  )
}

type FieldProps = {
  label: string
  value: string
  onChange: (value: string) => void
  type?: 'text' | 'email' | 'tel' | 'date'
  min?: string
  max?: string
  autoComplete?: string
  required?: boolean
  hint?: string
  error?: string
  multiline?: boolean
  inputMode?: 'numeric' | 'decimal'
  maxLength?: number
  /** Feste ID des Eingabefelds, damit die Fehlerübersicht dorthin verlinken kann. */
  id?: string
  /** Fokus ins Feld bei jedem neuen Wert ungleich 0 (Zähler: nach einem Fehler oder wenn das Feld einen Knopf ersetzt). */
  focus?: number
}

/** Eingabefeld mit sichtbarer Beschriftung; Hinweis und Fehler sind per aria-describedby verknüpft. */
export function Field({ label, value, onChange, type = 'text', autoComplete, required, hint, error, multiline, inputMode, maxLength, min, max, id: fixedId, focus = 0 }: FieldProps) {
  const generatedId = useId()
  const id = fixedId ?? generatedId
  const ref = useRef<HTMLInputElement & HTMLTextAreaElement>(null)
  useEffect(() => {
    if (focus) ref.current?.focus()
  }, [focus])
  const { t } = useT()
  const describedBy = [hint && `${id}-hint`, error && `${id}-error`].filter(Boolean).join(' ') || undefined
  const common = {
    ref,
    id,
    value,
    required,
    maxLength,
    'aria-invalid': error ? true : undefined,
    'aria-describedby': describedBy,
  }
  return (
    <div className="field">
      <label htmlFor={id}>
        {label}
        {required && <span className="required"> ({t.required})</span>}
      </label>
      {hint && (
        <span id={`${id}-hint`} className="hint">
          {hint}
        </span>
      )}
      {multiline ? (
        <textarea {...common} rows={3} onChange={(e) => onChange(e.target.value)} />
      ) : (
        <input
          {...common}
          type={type}
          autoComplete={autoComplete}
          inputMode={inputMode}
          min={min}
          max={max}
          onChange={(e) => onChange(e.target.value)}
        />
      )}
      {error && (
        <span id={`${id}-error`} className="field-error">
          {error}
        </span>
      )}
    </div>
  )
}

/**
 * Anschrift des Standorts mit „Route planen“ und, wenn hinterlegt, dem Anreise-Text. „Route planen“ öffnet die Karten-App des Geräts mit dem Standort als Ziel;
 * der Startpunkt ist der aktuelle Standort des Geräts, den die Karten-App selbst erfragt (hier gibt es keine Standortabfrage). Ohne Anschrift gibt es keinen Knopf.
 */
export function LocationRoute({ contact }: { contact: LocationContact }) {
  const { t } = useT()
  const destination = address(contact)
  return (
    <>
      {destination && (
        <>
          <br />
          <span className="address">{destination}</span>
          <br />
          <a className="button secondary route" href={routeUrl(destination, isAppleDevice())} target="_blank" rel="noopener noreferrer">
            {t.planRoute}
            <span className="visually-hidden"> ({t.planRouteNewTab})</span>
          </a>
        </>
      )}
      {contact.directions && (
        <p className="directions">
          <strong>{t.directions}:</strong> {contact.directions}
        </p>
      )}
    </>
  )
}

/** Eckdaten eines Termins als Beschreibungsliste. */
export function AppointmentFacts({ appointment }: { appointment: OnlineAppointment }) {
  const { t, lang } = useT()
  return (
    <dl className="facts">
      <dt>{t.stepTreatment}</dt>
      <dd>{appointment.treatmentName}</dd>
      <dt>{t.stepTime}</dt>
      <dd>{formatDateTime(appointment.start, appointment.timeZone, lang)}</dd>
      <dt>{t.stepLocation}</dt>
      <dd>
        {appointment.locationName}
        {appointment.locationContact && <LocationRoute contact={appointment.locationContact} />}
      </dd>
      <dt>{t.price}</dt>
      <dd>{formatPrice(appointment.price, appointment.currency, lang)}</dd>
    </dl>
  )
}
