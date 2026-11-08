import { useEffect, useId, type ReactNode } from 'react'
import { ICON, type Tone } from './stockFormat'

export function Icon({ path, size = 16 }: { path: string; size?: number }) {
  return (
    <svg viewBox="0 0 24 24" width={size} height={size} fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d={path} />
    </svg>
  )
}

/** Status-Pille (5.8). */
export function Pill({ tone = 'neutral', children }: { tone?: Tone; children: ReactNode }) {
  return <span className={`stock-pill${tone === 'neutral' ? '' : ` ${tone}`}`}>{children}</span>
}

export type FieldWidth = 'number' | 'date' | 'money' | 'select' | 'text' | 'full'

/**
 * Feld mit Beschriftung über dem Feld (5.2): per htmlFor an das Feld gebunden, der Hilfe-Knopf steht außerhalb des Labels.
 * Das Feld selbst baut der Aufrufer aus der übergebenen Kennung.
 */
export function Field({
  label,
  help,
  width = 'text',
  hint,
  error,
  children,
}: {
  label: string
  help?: ReactNode
  width?: FieldWidth
  hint?: string
  error?: string | null
  children: (id: string) => ReactNode
}) {
  const id = useId()
  return (
    <div className={`stock-field w-${width}`}>
      <div className="stock-label-row">
        <label htmlFor={id}>{label}</label>
        {help}
      </div>
      {children(id)}
      {hint && <p className="stock-hint">{hint}</p>}
      {error && (
        <p className="stock-error" role="alert">
          {error}
        </p>
      )}
    </div>
  )
}

/** Hinweis (5.9) mit Symbol und Wortlaut. */
export function Notice({ tone = 'neutral', children }: { tone?: 'neutral' | 'warn' | 'error'; children: ReactNode }) {
  return (
    <div className={`stock-notice${tone === 'neutral' ? '' : ` ${tone}`}`} role={tone === 'error' ? 'alert' : undefined}>
      <Icon path={tone === 'error' ? ICON.error : ICON.info} />
      <div>{children}</div>
    </div>
  )
}

/** Rückfrage (5.5, Modal): Esc schließt; der bestätigende Knopf ist der einzige rot gefüllte. */
export function ConfirmModal({
  title,
  children,
  confirmLabel,
  onConfirm,
  onCancel,
}: {
  title: string
  children: ReactNode
  confirmLabel: string
  onConfirm: () => void
  onCancel: () => void
}) {
  const titleId = useId()
  useEffect(() => {
    const close = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onCancel()
    }
    document.addEventListener('keydown', close)
    return () => document.removeEventListener('keydown', close)
  }, [onCancel])
  return (
    <div className="stock-modal-backdrop">
      <div className="stock-modal" role="dialog" aria-modal="true" aria-labelledby={titleId}>
        <h2 id={titleId}>{title}</h2>
        <div>{children}</div>
        <div className="stock-actions">
          <button type="button" className="stock-btn danger filled" onClick={onConfirm}>
            {confirmLabel}
          </button>
          <button type="button" className="stock-btn" onClick={onCancel} autoFocus>
            Abbrechen
          </button>
        </div>
      </div>
    </div>
  )
}
