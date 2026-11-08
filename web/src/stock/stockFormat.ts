import type { StockMovementKind } from '../api/client'

/** Symbole des Design-Briefs: die Pfade aus src/AispaStudio.Desktop/Controls/Icons.cs (Raster 24, Strichstärke 1,5). */
export const ICON = {
  plus: 'M12 5v14M5 12h14',
  close: 'M18 6L6 18M6 6l12 12',
  move: 'M5 9l-3 3 3 3M9 5l3-3 3 3M15 19l-3 3-3-3M19 9l3 3-3 3M2 12h20M12 2v20',
  chevronRight: 'M9 18l6-6-6-6',
  // Hinweise: Kreis aus dem Symbol „Hilfe“, dazu die Zeichen der Symbole „Haken“ und „Schließen“.
  info: 'M12 2a10 10 0 1 1 0 20a10 10 0 0 1 0-20zM12 11v6M12 7h.01',
  error: 'M12 2a10 10 0 1 1 0 20a10 10 0 0 1 0-20zM15 9l-6 6M9 9l6 6',
} as const

export const KIND_LABEL: Record<StockMovementKind, string> = {
  GoodsReceipt: 'Wareneingang',
  Consumption: 'Verbrauch',
  Sale: 'Verkauf',
  Count: 'Inventur',
  Correction: 'Korrektur',
  Transfer: 'Umbuchung',
  Refill: 'Nachfüllen',
}

export const quantity = (value: number) => value.toLocaleString('de-AT', { maximumFractionDigits: 3 })

export const isoDate = (date: Date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`

/** Kalenderdatum „JJJJ-MM-TT“ als „1.3.2027“. */
export const shortDate = (date: string) => new Date(`${date}T12:00:00Z`).toLocaleDateString('de-AT', { timeZone: 'UTC' })

/** Zahl aus einer Eingabe mit Komma; NaN, wenn es keine ist. */
export const parseNumber = (text: string) => {
  const value = Number(text.replace(/\s/g, '').replace(',', '.'))
  return Number.isFinite(value) ? value : NaN
}

export type Tone = 'warn' | 'crit' | 'accent' | 'neutral'
