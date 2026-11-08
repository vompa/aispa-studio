import { api, type StockItemDto } from '../api/client'

/** Material zu einem Code (Etikett, Handscanner, Kamera): `item` oder ein Satz für die Meldung. */
export async function findItemByCode(code: string): Promise<{ item: StockItemDto } | { problem: string }> {
  try {
    const { data, response } = await api.GET('/api/v1/stock/items/by-barcode', { params: { query: { code } } })
    if (data) return { item: data }
    if (response.status === 404) return { problem: 'Zu diesem Code gibt es kein Material.' }
    return { problem: 'Der Code konnte nicht gesucht werden.' }
  } catch {
    return { problem: 'Keine Verbindung. Der Code konnte nicht gesucht werden.' }
  }
}
