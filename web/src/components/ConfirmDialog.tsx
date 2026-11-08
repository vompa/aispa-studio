import { useEffect, useId, useRef, useState, type ReactNode } from 'react'
import './confirmDialog.css'

/** Wohin der Fokus beim Öffnen geht: „Abbrechen“, der Bestätigen-Knopf oder das erste Eingabefeld in `children`. */
export type ConfirmInitialFocus = 'cancel' | 'confirm' | 'field'

/** Erstes Eingabefeld; Kontrollkästchen und Optionsfelder sind Auswahl, keine Eingabe. */
const FIELD_SELECTOR = 'input:not([type=checkbox]):not([type=radio]):not([type=hidden]):not(:disabled), textarea:not(:disabled), select:not(:disabled)'

/**
 * Rückfrage als natives `<dialog>` (modal, Design-Brief 5.27): Alert, der nur über seine Knöpfe oder Esc schließt. Titel (eine Frage), Text mit der Folge,
 * darunter optional Eingaben oder Auswahl (`children`) und eine Fehlermeldung; die Aktion steht links, „Abbrechen“ rechts. Startfokus nach 5.27 (`initialFocus`):
 * zerstörende Aktion auf „Abbrechen“ (nie auf die Aktion), neutrale Rückfrage auf den primären Knopf, Rückfrage mit Eingabefeld ins Feld. Beim Entfernen des Dialogs kehrt der Fokus zum Auslöser zurück. Der Text hängt per `aria-describedby` am Dialog.
 * Der Dialog ist offen, solange er eingehängt ist: wer ihn zeigt, hängt ihn bedingt ein und beantwortet `onCancel` (Abbrechen und Esc) und `onConfirm`.
 */
export function ConfirmDialog({
  title,
  text,
  confirmLabel,
  onConfirm,
  onCancel,
  danger = true,
  initialFocus = danger ? 'cancel' : 'confirm',
  confirmDisabled = false,
  error,
  wide = false,
  className,
  children,
}: {
  title: string
  text: ReactNode
  /** `null`: noch nichts zu bestätigen (Auswahl lädt oder ist leer), dann bleibt nur „Abbrechen“. */
  confirmLabel: string | null
  onConfirm: () => void
  onCancel: () => void
  /** Zerstörende Aktion: roter, gefüllter Knopf; sonst der primäre Knopf. */
  danger?: boolean
  /** Ohne Angabe folgt sie `danger`: zerstörend „cancel“, sonst „confirm“. Fehlt das Ziel oder ist es gesperrt, bleibt der Fokus auf „Abbrechen“. */
  initialFocus?: ConfirmInitialFocus
  confirmDisabled?: boolean
  error?: string | null
  /** 640 statt 420 breit, für Rückfragen mit Auswahl und Text; die Seite scrollt in sich. */
  wide?: boolean
  /** Zusätzliche Klasse für Stile des Inhalts (`children`). */
  className?: string
  children?: ReactNode
}) {
  const ref = useRef<HTMLDialogElement>(null)
  const cancelRef = useRef<HTMLButtonElement>(null)
  const confirmRef = useRef<HTMLButtonElement>(null)
  const titleId = useId()
  const textId = useId()
  // Der Auslöser hat beim ersten Rendern den Fokus; StrictMode rendert und führt Effekte doppelt aus, der Initialisierer behält den ersten Wert.
  const [trigger] = useState(() => (document.activeElement instanceof HTMLElement ? document.activeElement : null))

  useEffect(() => {
    let fieldObserver: MutationObserver | undefined
    const dialog = ref.current
    if (dialog) {
      // StrictMode führt den Effekt doppelt aus: der zweite Lauf trifft den schon offenen Dialog und setzt Fokus und Beobachter neu.
      if (!dialog.open) dialog.showModal()
      // showModal() fokussiert das erste Steuerelement; die Rückfrage setzt den Startfokus selbst (Brief 5.27).
      cancelRef.current?.focus()
      // Eine zerstörende Aktion ist nie der Startfokus (Brief 5.27).
      if (initialFocus === 'confirm' && !danger && confirmRef.current && !confirmRef.current.disabled) confirmRef.current.focus()
      if (initialFocus === 'field') {
        const focusField = () => {
          const field = dialog.querySelector<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>(FIELD_SELECTOR)
          if (!field) return false
          field.focus()
          try {
            if (!(field instanceof HTMLSelectElement)) field.setSelectionRange(field.value.length, field.value.length)
          } catch {
            // number, email, date u. a. kennen keine Textmarke.
          }
          return true
        }
        // Felder, die erst nach dem Laden erscheinen: nachholen, solange „Abbrechen“ den Fokus hat (niemand hat ihn bewegt).
        if (!focusField()) {
          fieldObserver = new MutationObserver(() => {
            if (document.activeElement !== cancelRef.current) fieldObserver?.disconnect()
            else if (focusField()) fieldObserver?.disconnect()
          })
          fieldObserver.observe(dialog, { childList: true, subtree: true })
        }
      }
    }
    return () => {
      fieldObserver?.disconnect()
      // showModal() schließt offene Popover: lag der Auslöser in einem Menü, geht der Fokus an dessen Knopf.
      const popover = trigger?.closest('[popover]')
      const invoker = popover && [...document.querySelectorAll<HTMLElement>('[popovertarget]')].find((b) => b.getAttribute('popovertarget') === popover.id)
      const target = invoker ?? trigger
      if (target?.isConnected) target.focus()
    }
  }, [trigger, initialFocus, danger])

  return (
    <dialog
      ref={ref}
      role="alertdialog"
      className={['confirm-dialog', wide && 'confirm-dialog--wide', className].filter(Boolean).join(' ')}
      aria-labelledby={titleId}
      aria-describedby={textId}
      onClose={onCancel}
    >
      <h2 id={titleId}>{title}</h2>
      <p id={textId} className="confirm-dialog__text">
        {text}
      </p>
      {children}
      {error && (
        <p className="confirm-dialog__error" role="alert">
          {error}
        </p>
      )}
      <div className="confirm-dialog__actions">
        {confirmLabel !== null && (
          <button
            type="button"
            className={danger ? 'confirm-dialog__btn danger' : 'confirm-dialog__btn primary'}
            disabled={confirmDisabled}
            onClick={onConfirm}
            ref={confirmRef}
          >
            {confirmLabel}
          </button>
        )}
        <button type="button" className="confirm-dialog__btn ghost" onClick={onCancel} ref={cancelRef}>
          Abbrechen
        </button>
      </div>
    </dialog>
  )
}
