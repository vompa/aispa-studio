/**
 * Kurzbefehle, die im eingebetteten Browser der Windows-App (Verwaltung nach ADR 0053, Hilfe nach ADR 0079) die App bekommen muss: WebView2 verbraucht
 * die Tasten, Avalonia sieht sie nicht. Strg+Leertaste geht deshalb als Nachricht `{ type: 'companion' }` über denselben Kanal wie „gespeichert“
 * (`savedSignal.ts`), und die App schaltet damit „KI und Hilfe“. Im Browser außerhalb der App bleibt die Web-Leiste zuständig (`Companion.tsx`).
 * F1 geht als `{ type: 'f1' }` (die App zeigt die Hilfe zum Fokus), Esc in der Hilfe der Leiste (`ansicht=leiste`) als `{ type: 'escape' }` (die App schließt sie).
 */
type AppBridge = { invokeCSharpAction?: (message: unknown) => void }

/** Hört auf Strg+Leertaste, F1 und (in der Leiste) Esc, solange die Seite eingebettet ist; gibt die Funktion zum Abmelden zurück. */
export function installAppShortcuts(): () => void {
  // Einmal beim Start gelesen: die Hilfe schreibt ihr Ziel später ohne „eingebettet“ in die Adresse.
  const query = new URLSearchParams(window.location.search)
  const embedded = query.has('eingebettet')
  const inPanel = query.get('ansicht') === 'leiste'
  const send = (e: KeyboardEvent, type: string) => {
    e.preventDefault()
    ;(window as unknown as AppBridge).invokeCSharpAction?.({ type })
  }
  const onKey = (e: KeyboardEvent) => {
    if (!embedded || e.altKey || e.shiftKey || e.metaKey) return
    // `code` fehlt bei Tasten ohne Scan-Code (Bildschirmtastatur, Fernzugriff), dann zählt `key`.
    const space = e.code === 'Space' || e.key === ' '
    if (e.ctrlKey && space) send(e, 'companion')
    else if (!e.ctrlKey && e.key === 'F1') send(e, 'f1')
    else if (!e.ctrlKey && e.key === 'Escape' && inPanel) send(e, 'escape')
  }
  window.addEventListener('keydown', onKey)
  return () => window.removeEventListener('keydown', onKey)
}
