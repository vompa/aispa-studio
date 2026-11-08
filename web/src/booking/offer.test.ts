import { describe, expect, it } from 'vitest'
import { address, byCategory, isAppleDevice, routeUrl } from './offer'

describe('Route zum Standort', () => {
  it('nimmt das Ziel URL-kodiert und gibt keinen Startpunkt an', () => {
    expect(routeUrl('Kärntner Straße 1, 1010 Wien', false)).toBe('https://www.google.com/maps/dir/?api=1&destination=K%C3%A4rntner%20Stra%C3%9Fe%201%2C%201010%20Wien')
    expect(routeUrl('Kärntner Straße 1, 1010 Wien', true)).toBe('https://maps.apple.com/?daddr=K%C3%A4rntner%20Stra%C3%9Fe%201%2C%201010%20Wien')
    expect(routeUrl('A & B', false)).not.toContain('origin')
    expect(routeUrl('A & B', false)).toContain('destination=A%20%26%20B')
  })

  it('erkennt Geräte von Apple, auch das iPad, das sich als Mac meldet', () => {
    const ua = {
      iphone: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15',
      ipadAsMac: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15',
      android: 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/120 Mobile Safari/537.36',
      windows: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36',
    }
    expect(isAppleDevice({ userAgent: ua.iphone })).toBe(true)
    expect(isAppleDevice({ userAgent: ua.ipadAsMac })).toBe(true)
    expect(isAppleDevice({ userAgent: ua.android })).toBe(false)
    expect(isAppleDevice({ userAgent: ua.windows })).toBe(false)
  })
})

describe('Angebot im Kundenbereich', () => {
  it('sortiert nach Kategorie, ohne Kategorie zuletzt', () => {
    const list = [
      { id: 'a', category: null },
      { id: 'b', category: 'Massagen' },
      { id: 'c', category: 'Gesicht' },
      { id: 'd', category: 'Massagen' },
    ]
    expect(byCategory(list).map((t) => t.id)).toEqual(['c', 'b', 'd', 'a'])
  })

  it('schreibt die Anschrift in eine Zeile', () => {
    expect(
      address({
        street: 'Kärntner Straße 1',
        postalCode: '1010',
        city: 'Wien',
        phone: '+43 1',
        email: null,
      }),
    ).toBe('Kärntner Straße 1, 1010 Wien')
    expect(
      address({
        street: null,
        postalCode: null,
        city: null,
        phone: null,
        email: null,
      }),
    ).toBe('')
  })
})
