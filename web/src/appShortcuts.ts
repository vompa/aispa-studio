/**
 * Kurzbefehle, die im eingebetteten Browser der Windows-App (Verwaltung nach ADR 0053, Hilfe nach ADR 0079) die App bekommen muss: WebView2 verbraucht
 * die Tasten, Avalonia sieht sie nicht. Strg+Leertaste geht deshalb als Nachricht `{ type: 'companion' }` über denselben Kanal wie „gespeichert“
 * (`savedSignal.ts`), und die App schaltet damit „KI und Hilfe“. Im Browser außerhalb der App bleibt die Web-Leiste zuständig (`Companion.tsx`).
 */
type AppBridge = { invokeCSharpAction?: (message: unknown) => void }

/** Hört auf Strg+Leertaste, solange die Seite eingebettet ist; gibt die Funktion zum Abmelden zurück. */
export function installAppShortcuts(): () => void {
  // Einmal beim Start gelesen: die Hilfe schreibt ihr Ziel später ohne „eingebettet“ in die Adresse.
  const embedded = new URLSearchParams(window.location.search).has('eingebettet')
  const onKey = (e: KeyboardEvent) => {
    // `code` fehlt bei Tasten ohne Scan-Code (Bildschirmtastatur, Fernzugriff), dann zählt `key`.
    const space = e.code === 'Space' || e.key === ' '
    if (!embedded || !e.ctrlKey || !space || e.altKey || e.shiftKey || e.metaKey) return
    e.preventDefault()
    ;(window as unknown as AppBridge).invokeCSharpAction?.({ type: 'companion' })
  }
  window.addEventListener('keydown', onKey)
  return () => window.removeEventListener('keydown', onKey)
}
