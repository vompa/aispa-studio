import { useId, type ReactNode } from 'react'
import { ICON } from '../stock/stockFormat'
import { Icon } from '../stock/stockUi'
import './formField.css'

export type FormFieldInputProps = {
  id: string
  'aria-invalid': boolean
  'aria-describedby'?: string
  'aria-required'?: boolean
}

/** Feld nach masken.md R6 bis R8: Beschriftung über dem Feld, Hilfetext darunter, ein Fehler ersetzt den Hilfetext und hängt per aria-describedby am Feld. */
export function FormField({
  label,
  required,
  hint,
  error,
  width = 'full',
  className,
  children,
}: {
  label: string
  required?: boolean
  hint?: string
  error?: string
  width?: 'short' | 'date' | 'full'
  /** Zusatzklasse für die Breite eines Feldes außerhalb eines Rasters, z. B. `field-wide`. */
  className?: string
  children: (props: FormFieldInputProps) => ReactNode
}) {
  const id = useId()
  const noteId = `${id}-note`
  return (
    <div className={`stock-field form-field ${width}${className ? ` ${className}` : ''}`}>
      <div className="stock-label-row">
        <label htmlFor={id}>
          {label}
          {required && <span aria-hidden="true"> *</span>}
        </label>
      </div>
      {children({ id, 'aria-invalid': !!error, 'aria-describedby': error || hint ? noteId : undefined, 'aria-required': required || undefined })}
      {error ? (
        <p id={noteId} className="stock-error form-message">
          <Icon path={ICON.error} size={14} />
          {error}
        </p>
      ) : (
        hint && (
          <p id={noteId} className="stock-hint">
            {hint}
          </p>
        )
      )}
    </div>
  )
}

/** Gruppe nach masken.md R1: `<fieldset>` mit `<legend>`, Überschrift und optionalem Hilfe-Knopf. */
export function FormGroup({ title, help, children }: { title: string; help?: ReactNode; children: ReactNode }) {
  return (
    <fieldset className="form-group">
      <legend>
        <h3>{title}</h3>
        {help}
      </legend>
      {children}
    </fieldset>
  )
}
