import { type ChangeEvent, type FormEvent, useEffect, useState } from 'react'
import { api, type DeliveryNoteSuggestion, type PurchaseOrderDto } from '../api/client'
import { problemText } from './format'

type LineInput = { quantity: string; batch: string; expiresOn: string; check: string | null }
type Orders = { kind: 'loading' } | { kind: 'ready'; orders: PurchaseOrderDto[] } | { kind: 'problem'; text: string }

const emptyLine: LineInput = { quantity: '', batch: '', expiresOn: '', check: null }
const amountText = (value: number) => value.toLocaleString('de-AT', { maximumFractionDigits: 3 })
const asInput = (value: number) => String(value).replace('.', ',')

/** Menge aus dem Feld: leer heißt „nichts geliefert“ (0); Komma oder Punkt, höchstens drei Nachkommastellen; sonst `null`. */
function parseQuantity(text: string): number | null {
  const trimmed = text.trim().replace(',', '.')
  if (trimmed === '') return 0
  return /^\d{1,6}(\.\d{1,3})?$/.test(trimmed) ? Number(trimmed) : null
}

/**
 * Lieferung zu einer Bestellung am Handy: Bestellung wählen, Lieferschein fotografieren, die von der KI vorgeschlagenen Mengen prüfen, buchen.
 * Ohne Foto trägt man die Mengen von Hand ein. Nur mit dem Recht, Standorte zu verwalten; die API prüft es noch einmal. Das Foto wird nicht gespeichert.
 */
export function DeliveryPanel({ locationId, onSignedOut }: { locationId: string; onSignedOut: () => void }) {
  const [state, setState] = useState<Orders>({ kind: 'loading' })
  const [orderId, setOrderId] = useState('')
  const [lines, setLines] = useState<Record<string, LineInput>>({})
  const [deliveryNote, setDeliveryNote] = useState('')
  const [reading, setReading] = useState(false)
  const [read, setRead] = useState<DeliveryNoteSuggestion | null>(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ text: string; error: boolean } | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => {
    let cancelled = false
    api.GET('/api/v1/stock/locations/{locationId}/orders', { params: { path: { locationId } } }).then(
      ({ data, error, response }) => {
        if (cancelled) return
        if (response.status === 401) return onSignedOut()
        if (!data) return setState({ kind: 'problem', text: problemText(error, 'Die Bestellungen konnten nicht geladen werden.') })
        const open = data.filter((o) => o.status === 'Ordered' || o.status === 'PartiallyReceived')
        setState({ kind: 'ready', orders: open })
        setOrderId((current) => (open.some((o) => o.id === current) ? current : (open[0]?.id ?? '')))
      },
      () => !cancelled && setState({ kind: 'problem', text: 'Keine Verbindung. Lieferungen buchen geht nur mit Netz.' }),
    )
    return () => {
      cancelled = true
    }
  }, [locationId, onSignedOut, reload])

  function choose(id: string) {
    setOrderId(id)
    setLines({})
    setDeliveryNote('')
    setRead(null)
    setMessage(null)
  }

  if (state.kind === 'loading') {
    return (
      <p className="muted" role="status">
        Lade Bestellungen …
      </p>
    )
  }
  if (state.kind === 'problem') return <p role="alert">{state.text}</p>

  const order = state.orders.find((o) => o.id === orderId)
  const openLines = order?.lines.filter((l) => l.received < l.quantity) ?? []
  const line = (id: string) => lines[id] ?? emptyLine
  const change = (id: string, patch: Partial<LineInput>) => setLines((all) => ({ ...all, [id]: { ...(all[id] ?? emptyLine), ...patch } }))

  async function photo(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file || !order || reading) return
    setReading(true)
    setMessage(null)
    try {
      const { data, error, response } = await api.POST('/api/v1/stock/orders/{purchaseOrderId}/delivery-note-scan', {
        params: { path: { purchaseOrderId: order.id } },
        body: { file } as never,
        bodySerializer: () => {
          const form = new FormData()
          form.append('file', file)
          return form
        },
      })
      if (response.status === 401) return onSignedOut()
      if (!data) return setMessage({ text: problemText(error, 'Das Foto konnte nicht gelesen werden.'), error: true })
      setRead(data)
      if (!data.read) return
      // Der Vorschlag ersetzt die Felder; gebucht wird erst mit dem Knopf.
      setLines(
        Object.fromEntries(
          data.lines.map((l) => [
            l.lineId,
            { quantity: asInput(l.quantity), batch: l.batch ?? '', expiresOn: l.expiresOn ?? '', check: l.uncertain ? (l.reason ?? 'Unsicher gelesen.') : null },
          ]),
        ),
      )
      if (data.deliveryNote) setDeliveryNote(data.deliveryNote)
    } catch {
      setMessage({ text: 'Keine Verbindung. Das Foto wurde nicht gelesen.', error: true })
    } finally {
      setReading(false)
    }
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!order || busy) return
    const parsed = openLines.map((l) => ({ line: l, input: line(l.id), quantity: parseQuantity(line(l.id).quantity) }))
    if (parsed.some((p) => p.quantity === null)) {
      return setMessage({ text: 'Bitte Mengen ab 0 mit höchstens drei Nachkommastellen eingeben.', error: true })
    }
    const delivered = parsed.filter((p) => (p.quantity ?? 0) > 0)
    if (delivered.length === 0) return setMessage({ text: 'Bitte bei mindestens einer Position die gelieferte Menge eintragen.', error: true })
    setBusy(true)
    try {
      const { data, error, response } = await api.POST('/api/v1/stock/orders/{purchaseOrderId}/deliveries', {
        params: { path: { purchaseOrderId: order.id } },
        body: {
          lines: delivered.map((p) => ({ lineId: p.line.id, quantity: p.quantity!, batch: p.input.batch.trim() || null, expiresOn: p.input.expiresOn || null })),
          deliveryNote: deliveryNote.trim() || null,
        },
      })
      if (response.status === 401) return onSignedOut()
      if (!data) return setMessage({ text: problemText(error, 'Die Lieferung konnte nicht gebucht werden.'), error: true })
      const count = delivered.length
      choose(order.id)
      setMessage({ text: `Lieferung zu Bestellung ${order.number} gebucht: ${count === 1 ? '1 Position' : `${count} Positionen`}.`, error: false })
      setReload((n) => n + 1)
    } catch {
      setMessage({ text: 'Keine Verbindung. Die Lieferung ist noch nicht gebucht.', error: true })
    } finally {
      setBusy(false)
    }
  }

  return (
    <section aria-labelledby="delivery-heading">
      <h2 id="delivery-heading">Lieferung</h2>
      <p className="muted">Bestellung wählen, Lieferschein fotografieren, Mengen prüfen und buchen. Das Foto wird nur gelesen und nicht gespeichert.</p>

      {state.orders.length === 0 ? (
        <p>Keine offene Bestellung an diesem Standort.</p>
      ) : (
        <form className="panel" onSubmit={(e) => void submit(e)} noValidate>
          <div className="field">
            <label htmlFor="delivery-order">Bestellung</label>
            <select id="delivery-order" value={orderId} onChange={(e) => choose(e.target.value)}>
              {state.orders.map((o) => (
                <option key={o.id} value={o.id}>
                  Nr. {o.number} · {o.supplier}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="delivery-photo">Lieferschein fotografieren (optional)</label>
            <input id="delivery-photo" type="file" accept="image/jpeg,image/png,image/webp" capture="environment" onChange={(e) => void photo(e)} />
          </div>
          {reading && (
            <p className="muted" role="status">
              Lese den Lieferschein …
            </p>
          )}
          {read && (
            <div className="notice">
              {read.read ? (
                <p>
                  <strong>Von der KI gelesen.</strong> Bitte alle Mengen mit dem Lieferschein vergleichen; gebucht wird erst mit dem Knopf.
                </p>
              ) : (
                <p role="alert">{read.note}</p>
              )}
              {read.read && read.note && <p>{read.note}</p>}
              {read.unmatched.length > 0 && (
                <>
                  <p>Auf dem Lieferschein, aber nicht in dieser Bestellung:</p>
                  <ul>
                    {read.unmatched.map((text, i) => (
                      <li key={i}>{text}</li>
                    ))}
                  </ul>
                </>
              )}
            </div>
          )}

          {openLines.map((l) => {
            const input = line(l.id)
            return (
              <fieldset key={l.id} className="delivery-line">
                <legend>
                  {l.itemName} · offen {amountText(l.quantity - l.received)} {l.unit}
                </legend>
                {input.check && (
                  <p className="count-hint" id={`delivery-${l.id}-check`}>
                    Bitte prüfen: {input.check}
                  </p>
                )}
                <div className="field">
                  <label htmlFor={`delivery-${l.id}-quantity`}>
                    Geliefert ({l.unit}): {l.itemName}
                  </label>
                  <input
                    id={`delivery-${l.id}-quantity`}
                    type="text"
                    inputMode="decimal"
                    autoComplete="off"
                    value={input.quantity}
                    aria-describedby={input.check ? `delivery-${l.id}-check` : undefined}
                    onChange={(e) => change(l.id, { quantity: e.target.value, check: null })}
                  />
                </div>
                <div className="field">
                  <label htmlFor={`delivery-${l.id}-batch`}>Charge (optional): {l.itemName}</label>
                  <input id={`delivery-${l.id}-batch`} type="text" autoComplete="off" maxLength={60} value={input.batch} onChange={(e) => change(l.id, { batch: e.target.value })} />
                </div>
                <div className="field">
                  <label htmlFor={`delivery-${l.id}-expires`}>Ablaufdatum (optional): {l.itemName}</label>
                  <input id={`delivery-${l.id}-expires`} type="date" value={input.expiresOn} onChange={(e) => change(l.id, { expiresOn: e.target.value })} />
                </div>
              </fieldset>
            )
          })}

          <div className="field">
            <label htmlFor="delivery-note">Lieferscheinnummer (optional)</label>
            <input id="delivery-note" type="text" autoComplete="off" maxLength={100} value={deliveryNote} onChange={(e) => setDeliveryNote(e.target.value)} />
          </div>
          <div className="actions">
            <button type="submit" aria-disabled={busy || reading || undefined}>
              Lieferung buchen
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
