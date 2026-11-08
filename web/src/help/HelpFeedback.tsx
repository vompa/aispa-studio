import { useEffect, useRef, useState } from 'react'
import { FEEDBACK_TEXT_MAX, hasFeedback, recordFeedback } from './feedback'

/**
 * „War das hilfreich?“ am Fuß eines Themas (Slice 55). Ja oder Nein öffnet ein freiwilliges Textfeld, „Senden“ legt die Antwort
 * auf diesem PC ab und zeigt den Dank. Hat das Thema schon eine Rückmeldung, steht die Frage nicht mehr da.
 */
export function HelpFeedback({ topic }: { topic: string }) {
  const [known] = useState(() => hasFeedback(topic))
  const [answer, setAnswer] = useState<'yes' | 'no' | null>(null)
  const [text, setText] = useState('')
  const [sent, setSent] = useState(false)

  const thanks = useRef<HTMLParagraphElement>(null)
  // Der Knopf „Senden“ verschwindet: der Fokus zieht auf den Dank, damit er nicht auf dem Dokument landet.
  useEffect(() => {
    if (sent) thanks.current?.focus()
  }, [sent])

  if (known) return null

  return (
    <section className="help-feedback" aria-label="Rückmeldung zum Thema">
      <p ref={thanks} role="status" tabIndex={-1}>
        {sent ? 'Danke für die Rückmeldung.' : null}
      </p>
      {!sent && (
        <>
      <p id="help-feedback-title" className="help-foot-title">
        War das hilfreich?
      </p>
      <div className="help-feedback-choice">
        {(['yes', 'no'] as const).map((a) => (
          <button key={a} type="button" className="small" aria-pressed={answer === a} onClick={() => setAnswer(a)}>
            {a === 'yes' ? 'Ja' : 'Nein'}
          </button>
        ))}
      </div>
      {answer && (
        <>
          <label className="help-feedback-text">
            <span className="muted">Möchten Sie etwas ergänzen? (freiwillig) Keine Namen von Gästen und keine Kontaktdaten eintragen.</span>
            <textarea rows={3} maxLength={FEEDBACK_TEXT_MAX} value={text} onChange={(e) => setText(e.target.value)} />
          </label>
          <button
            type="button"
            className="small"
            onClick={() => {
              recordFeedback(topic, answer, text)
              setSent(true)
            }}
          >
            Senden
          </button>
        </>
      )}
        </>
      )}
    </section>
  )
}
