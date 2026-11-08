import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { useRef, useState } from 'react'
import { afterEach, describe, expect, it } from 'vitest'
import { FormField, FormGroup } from './FormField'
import { useFocusFirstInvalid } from './useFocusFirstInvalid'

afterEach(cleanup)

function Demo() {
  const ref = useRef<HTMLDivElement>(null)
  const focusFirstInvalid = useFocusFirstInvalid(ref)
  const [errors, setErrors] = useState<{ name?: string; city?: string }>({})
  const save = () => {
    setErrors({ name: 'Bitte einen Namen eingeben.', city: 'Bitte einen Ort eingeben.' })
    focusFirstInvalid()
  }
  return (
    <div ref={ref}>
      <FormGroup title="Eintrag">
        <FormField label="Name" required error={errors.name} hint="Wie in der Liste.">
          {(p) => <input {...p} />}
        </FormField>
        <FormField label="Ort" error={errors.city}>
          {(p) => <input {...p} />}
        </FormField>
      </FormGroup>
      <button type="button" onClick={save}>
        Speichern
      </button>
    </div>
  )
}

describe('FormField und useFocusFirstInvalid', () => {
  it('Given ein Formular mit zwei ungültigen Feldern When gespeichert wird Then liegt der Fokus im ersten ungültigen Feld', async () => {
    render(<Demo />)

    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))

    const name = screen.getByLabelText(/^Name/)
    await waitFor(() => expect(document.activeElement).toBe(name))
    expect(name.getAttribute('aria-invalid')).toBe('true')
    expect(screen.getByLabelText(/^Ort/).getAttribute('aria-invalid')).toBe('true')
  })

  it('Given ein erneutes Speichern mit Fokus außerhalb When gespeichert wird Then springt der Fokus wieder ins erste ungültige Feld', async () => {
    render(<Demo />)
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))
    const name = screen.getByLabelText(/^Name/)
    await waitFor(() => expect(document.activeElement).toBe(name))
    screen.getByLabelText(/^Ort/).focus()

    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))

    await waitFor(() => expect(document.activeElement).toBe(name))
  })

  it('Given ein Fehler und ein Hilfetext When das Feld gezeichnet wird Then ersetzt der Fehler den Hilfetext und hängt per aria-describedby am Feld', () => {
    render(<Demo />)
    expect(screen.getByText('Wie in der Liste.')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }))

    expect(screen.queryByText('Wie in der Liste.')).toBeNull()
    const name = screen.getByLabelText(/^Name/)
    expect(document.getElementById(name.getAttribute('aria-describedby')!)!.textContent).toContain('Bitte einen Namen eingeben.')
  })
})
