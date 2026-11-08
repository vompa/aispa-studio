import { useEffect, useState } from 'react'
import { api, type OnlineOwnedVoucher } from './api'
import { formatDay, formatPrice, useT } from './i18n'

/** Eigene Gutscheine mit Restwert; ohne Gutschein bleibt der Abschnitt weg. */
export function OwnVouchers() {
  const { t, lang } = useT()
  const [vouchers, setVouchers] = useState<OnlineOwnedVoucher[]>([])

  useEffect(() => {
    api.GET('/api/online/me/vouchers').then(
      ({ data }) => setVouchers(data ?? []),
      () => setVouchers([]),
    )
  }, [])

  if (vouchers.length === 0) return null
  return (
    <section aria-labelledby="own-vouchers">
      <h2 id="own-vouchers">{t.ownVouchers}</h2>
      <ul className="cards">
        {vouchers.map((v) => (
          <li key={v.code}>
            <article className="card" aria-label={v.treatmentName ?? t.ownVouchers}>
              {v.treatmentName && <p>{v.treatmentName}</p>}
              <p>
                {v.units != null
                  ? t.ownVoucherUnits(v.unitsLeft ?? 0, v.units)
                  : t.ownVoucherBalance(formatPrice(v.balance, v.currency, lang), formatPrice(v.initialValue, v.currency, lang))}
              </p>
              <p className="hint voucher-code">
                {t.ownVoucherCode}: {v.code}
              </p>
              {v.status === 'Redeemed' && <p className="badge">{t.ownVoucherRedeemed}</p>}
              {v.status === 'Voided' && <p className="badge">{t.ownVoucherVoided}</p>}
              {v.status === 'Active' && v.validUntil && <p className="hint">{t.ownVoucherValidUntil(formatDay(v.validUntil, lang))}</p>}
            </article>
          </li>
        ))}
      </ul>
    </section>
  )
}
