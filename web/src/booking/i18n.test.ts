import { afterEach, describe, expect, it, vi } from 'vitest'
import { detectLanguage } from './i18n'

function browserLanguage(value: string | undefined) {
  vi.spyOn(window.navigator, 'language', 'get').mockReturnValue(value as string)
}

describe('detectLanguage', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    window.location.hash = ''
    localStorage.clear()
  })

  it('nimmt eine bekannte Browsersprache', () => {
    browserLanguage('fr-CH')
    expect(detectLanguage()).toBe('fr')
  })

  it('fällt bei unbekannter Browsersprache auf Deutsch zurück', () => {
    browserLanguage('es-ES')
    expect(detectLanguage()).toBe('de')
  })

  it('fällt ohne Browsersprache auf Deutsch zurück', () => {
    browserLanguage(undefined)
    expect(detectLanguage()).toBe('de')
  })

  it('lässt die Sprache aus dem Link vorgehen', () => {
    browserLanguage('es-ES')
    window.location.hash = '#/buchen?lang=it'
    expect(detectLanguage()).toBe('it')
  })
})
