import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { useState } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ConfirmDialog } from './ConfirmDialog'

afterEach(cleanup)

/** Auslöser „Löschen“ öffnet die Rückfrage; Abbrechen und Bestätigen schließen sie wie auf den Seiten. */
function Harness({
  onConfirm = () => {},
  confirmDisabled = false,
  danger,
  initialFocus,
  withField = false,
  title = 'Schließzeit löschen?',
  confirmLabel = 'Endgültig löschen',
}: {
  onConfirm?: () => void
  confirmDisabled?: boolean
  danger?: boolean
  initialFocus?: 'cancel' | 'confirm' | 'field'
  withField?: boolean
  title?: string
  confirmLabel?: string
}) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        Löschen
      </button>
      {open && (
        <ConfirmDialog
          title={title}
          text="Die Schließzeit ist danach weg."
          confirmLabel={confirmLabel}
          confirmDisabled={confirmDisabled}
          danger={danger}
          initialFocus={initialFocus}
          onConfirm={() => {
            onConfirm()
            setOpen(false)
          }}
          onCancel={() => setOpen(false)}
        >
          {withField && (
            <>
              <input type="checkbox" aria-label="Alle" />
              <label htmlFor="reason">Grund</label>
              <input id="reason" defaultValue="Doppelt gebucht" />
            </>
          )}
        </ConfirmDialog>
      )}
    </>
  )
}

function openFromTrigger(name = 'Schließzeit löschen?') {
  const trigger = screen.getByRole('button', { name: 'Löschen' })
  trigger.focus()
  fireEvent.click(trigger)
  return { trigger, dialog: screen.getByRole('alertdialog', { name }) }
}

describe('ConfirmDialog', () => {
  it('Given eine Rückfrage When sie sich öffnet Then liegt der Fokus auf „Abbrechen“ und der Text ist per aria-describedby verbunden', () => {
    render(<Harness />)
    const { dialog } = openFromTrigger()

    expect(dialog.tagName).toBe('DIALOG')
    expect(document.activeElement).toBe(within(dialog).getByRole('button', { name: 'Abbrechen' }))
    const describedBy = dialog.getAttribute('aria-describedby')
    expect(describedBy).toBeTruthy()
    expect(document.getElementById(describedBy!)?.textContent).toBe('Die Schließzeit ist danach weg.')
  })

  it('Given eine Rückfrage When sie über Abbrechen schließt Then liegt der Fokus wieder auf dem Auslöser', () => {
    render(<Harness />)
    const { trigger, dialog } = openFromTrigger()

    fireEvent.click(within(dialog).getByRole('button', { name: 'Abbrechen' }))

    expect(screen.queryByRole('alertdialog')).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })

  it('Given eine Rückfrage When sie mit Esc schließt Then liegt der Fokus wieder auf dem Auslöser', () => {
    render(<Harness />)
    const { trigger } = openFromTrigger()

    fireEvent.keyDown(document.activeElement!, { key: 'Escape' })

    expect(screen.queryByRole('alertdialog')).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })

  it('Given eine Rückfrage When sie über Bestätigen schließt Then wird bestätigt und der Fokus liegt wieder auf dem Auslöser', () => {
    const onConfirm = vi.fn()
    render(<Harness onConfirm={onConfirm} />)
    const { trigger, dialog } = openFromTrigger()

    fireEvent.click(within(dialog).getByRole('button', { name: 'Endgültig löschen' }))

    expect(onConfirm).toHaveBeenCalledOnce()
    expect(screen.queryByRole('alertdialog')).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })

  it('Given eine gesperrte Aktion When die Rückfrage offen ist Then ist Bestätigen gesperrt und Abbrechen bleibt bedienbar', () => {
    render(<Harness confirmDisabled />)
    const { dialog } = openFromTrigger()

    expect((within(dialog).getByRole('button', { name: 'Endgültig löschen' }) as HTMLButtonElement).disabled).toBe(true)
    expect((within(dialog).getByRole('button', { name: 'Abbrechen' }) as HTMLButtonElement).disabled).toBe(false)
  })

  it('Given eine Rückfrage When sie offen ist Then steht die Aktion links vor „Abbrechen“', () => {
    render(<Harness />)
    const { dialog } = openFromTrigger()

    const names = within(dialog)
      .getAllByRole('button')
      .map((b) => b.textContent)
    expect(names).toEqual(['Endgültig löschen', 'Abbrechen'])
  })

  it('Given eine neutrale Rückfrage (danger aus) When sie sich öffnet Then liegt der Fokus auf dem primären Knopf', () => {
    render(<Harness danger={false} title="Aktion archivieren?" confirmLabel="Archivieren" />)
    const { dialog } = openFromTrigger('Aktion archivieren?')

    expect(document.activeElement).toBe(within(dialog).getByRole('button', { name: 'Archivieren' }))
  })

  it('Given eine neutrale Rückfrage When Esc gedrückt wird Then schließt sie ohne zu bestätigen', () => {
    const onConfirm = vi.fn()
    render(<Harness danger={false} onConfirm={onConfirm} />)
    const { trigger } = openFromTrigger()

    fireEvent.keyDown(document.activeElement!, { key: 'Escape' })

    expect(onConfirm).not.toHaveBeenCalled()
    expect(screen.queryByRole('alertdialog')).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })

  it('Given eine neutrale Rückfrage mit gesperrter Aktion When sie sich öffnet Then liegt der Fokus auf „Abbrechen“', () => {
    render(<Harness danger={false} confirmDisabled />)
    const { dialog } = openFromTrigger()

    expect(document.activeElement).toBe(within(dialog).getByRole('button', { name: 'Abbrechen' }))
  })

  it('Given eine Rückfrage mit Feld und initialFocus „field“ When sie sich öffnet Then liegt der Fokus im Feld „Grund“ und nicht im Kontrollkästchen davor', () => {
    render(<Harness initialFocus="field" withField />)
    const { dialog } = openFromTrigger()

    const field = within(dialog).getByRole('textbox', { name: 'Grund' }) as HTMLInputElement
    expect(document.activeElement).toBe(field)
    expect(field.selectionStart).toBe(field.value.length)
  })

  it('Given eine zerstörende Rückfrage mit initialFocus „confirm“ When sie sich öffnet Then bleibt der Fokus auf „Abbrechen“ (nie die zerstörende Aktion)', () => {
    render(<Harness initialFocus="confirm" />)
    const { dialog } = openFromTrigger()

    expect(document.activeElement).toBe(within(dialog).getByRole('button', { name: 'Abbrechen' }))
  })

  it('Given initialFocus „field“ ohne Feld When sie sich öffnet Then liegt der Fokus auf „Abbrechen“', () => {
    render(<Harness initialFocus="field" />)
    const { dialog } = openFromTrigger()

    expect(document.activeElement).toBe(within(dialog).getByRole('button', { name: 'Abbrechen' }))
  })
})
