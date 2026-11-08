import { type FormEvent, useState } from 'react'
import { api, type StockItemDto } from '../api/client'
import { BarcodeScan } from './BarcodeScan'
import { problemText } from './format'
import { findItemByCode } from './stockLookup'

/** Menge aus dem Feld (Komma oder Punkt, höchstens drei Nachkommastellen, größer als 0); sonst `null`. */
function parseQuantity(text: string): number | null {
  const trimmed = text.trim().replace(',', '.')
  if (!/^\d{1,6}(\.\d{1,3})?$/.test(trimmed)) return null
  const value = Number(trimmed)
  return value > 0 ? value : null
}

const amountText = (value: number) => value.toLocaleString('de-AT', { maximumFractionDigits: 3 })

/**
 * Wareneingang am Handy: Code scannen oder eingeben, Material bestätigen, Menge (und optional Charge und Ablaufdatum), buchen.
 * Nur mit dem Recht „Buchen“ sichtbar; die API prüft es noch einmal. Geht nur mit Verbindung.
 */
export function ReceivePanel({ locationId, onSignedOut }: { locationId: string; onSignedOut: () => void }) {
  const [item, setItem] = useState<StockItemDto | null>(null)
  const [quantity, setQuantity] = useState('')
  const [batch, setBatch] = useState('')
  const [expiresOn, setExpiresOn] = useState('')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ text: string; error: boolean } | null>(null)

  async function onCode(code: string): Promise<string | null> {
    const found = await findItemByCode(code)
    if ('problem' in found) return found.problem
    setItem(found.item)
    setQuantity('')
    setBatch('')
    setExpiresOn('')
    setMessage(null)
    return null
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!item || busy) return
    const amount = parseQuantity(quantity)
    if (amount === null) {
      setMessage({ text: 'Bitte eine Menge über 0 mit höchstens drei Nachkommastellen eingeben.', error: true })
      return
    }
    setBusy(true)
    try {
      const { data, error, response } = await api.POST('/api/v1/stock/locations/{locationId}/movements', {
        params: { path: { locationId } },
        body: { itemId: item.id, kind: 'GoodsReceipt', quantity: amount, batch: batch.trim() || null, expiresOn: expiresOn || null },
      })
      if (response.status === 401) return onSignedOut()
      if (data) {
        setMessage({ text: `${amountText(amount)} ${item.unit} ${item.name} gebucht.`, error: false })
        setItem(null)
      } else {
        setMessage({ text: problemText(error, 'Der Wareneingang konnte nicht gebucht werden.'), error: true })
      }
    } catch {
      setMessage({ text: 'Keine Verbindung. Der Wareneingang ist noch nicht gebucht.', error: true })
    } finally {
      setBusy(false)
    }
  }

  return (
    <section aria-labelledby="receive-heading">
      <h2 id="receive-heading">Wareneingang</h2>
      <p className="muted">Zuerst den Code der Lieferung scannen oder eingeben. Das geht nur mit Verbindung.</p>
      <BarcodeScan onCode={onCode} />

      {item && (
        <form className="panel" onSubmit={(e) => void submit(e)} noValidate>
          <h3>{item.name}</h3>
          <div className="field">
            <label htmlFor="receive-quantity">Menge ({item.unit})</label>
            <input id="receive-quantity" type="text" inputMode="decimal" autoComplete="off" value={quantity} onChange={(e) => setQuantity(e.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="receive-batch">Charge (optional)</label>
            <input id="receive-batch" type="text" autoComplete="off" maxLength={60} value={batch} onChange={(e) => setBatch(e.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="receive-expires">Ablaufdatum (optional)</label>
            <input id="receive-expires" type="date" value={expiresOn} onChange={(e) => setExpiresOn(e.target.value)} />
          </div>
          <div className="actions">
            <button type="submit" aria-disabled={busy || undefined}>
              Wareneingang buchen
            </button>
          </div>
        </form>
      )}
      <p role="status" className={message?.error ? 'message error' : 'message'}>
        {message?.text}
      </p>
    </section>
  )
}
