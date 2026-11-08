import { afterEach, describe, expect, it, vi } from 'vitest'
import { installAppShortcuts } from './appShortcuts'

type Bridge = { invokeCSharpAction?: (message: unknown) => void }

// R02 Slice 31 (Ä74): im eingebetteten Browser der Windows-App erreicht Strg+Leertaste die App nicht; die Seite meldet die Taste als Nachricht.
describe('Strg+Leertaste im eingebetteten Browser', () => {
  const bridge = window as unknown as Bridge
  const press = (init: KeyboardEventInit) => {
    const event = new KeyboardEvent('keydown', { key: ' ', cancelable: true, ...init })
    window.dispatchEvent(event)
    return event
  }

  afterEach(() => {
    delete bridge.invokeCSharpAction
    window.history.replaceState({}, '', '/')
  })

  it('Given die Verwaltung in der Windows-App When Strg+Leertaste gedrückt wird Then geht „companion“ an die App und die Taste ist verbraucht', () => {
    window.history.replaceState({}, '', '/?eingebettet=1')
    const sent = vi.fn()
    bridge.invokeCSharpAction = sent
    const uninstall = installAppShortcuts()

    const event = press({ code: 'Space', ctrlKey: true })

    expect(sent).toHaveBeenCalledWith({ type: 'companion' })
    expect(event.defaultPrevented).toBe(true)

    uninstall()
    press({ code: 'Space', ctrlKey: true })
    expect(sent).toHaveBeenCalledTimes(1)
  })

  it('Given eine Taste ohne Scan-Code (Bildschirmtastatur, Fernzugriff: `code` leer) When Strg+Leertaste kommt Then zählt `key`', () => {
    window.history.replaceState({}, '', '/?eingebettet=1')
    const sent = vi.fn()
    bridge.invokeCSharpAction = sent
    const uninstall = installAppShortcuts()

    press({ code: '', ctrlKey: true })

    expect(sent).toHaveBeenCalledWith({ type: 'companion' })
    uninstall()
  })

  it('Given die Hilfe im Browser (nicht eingebettet) When Strg+Leertaste gedrückt wird Then schickt das Modul nichts und lässt die Taste der Web-Leiste', () => {
    const sent = vi.fn()
    bridge.invokeCSharpAction = sent
    const uninstall = installAppShortcuts()

    const event = press({ code: 'Space', ctrlKey: true })

    expect(sent).not.toHaveBeenCalled()
    expect(event.defaultPrevented).toBe(false)
    uninstall()
  })

  it('Given eingebettet When die Leertaste ohne Strg, mit Umschalt oder Alt oder eine andere Taste mit Strg kommt Then passiert nichts', () => {
    window.history.replaceState({}, '', '/?eingebettet=1')
    const sent = vi.fn()
    bridge.invokeCSharpAction = sent
    const uninstall = installAppShortcuts()

    press({ code: 'Space' })
    press({ code: 'Space', ctrlKey: true, shiftKey: true })
    press({ code: 'Space', ctrlKey: true, altKey: true })
    press({ key: 'a', code: 'KeyA', ctrlKey: true })

    expect(sent).not.toHaveBeenCalled()
    uninstall()
  })
})
