import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { AccessibilityPage } from './AccessibilityPage'
import { ImprintPage } from './ImprintPage'
import { AnamnesisPage } from './AnamnesisPage'
import { BookPage } from './BookPage'
import { ConsentPage } from './ConsentPage'
import { baseTexts, detectLanguage, languageNames, LanguageContext, loadTexts, textsIfLoaded, useT, type Language, type Texts } from './i18n'
import { ManagePage } from './ManagePage'
import { MyAppointmentsPage } from './MyAppointmentsPage'
import { href, useRoute, type Route } from './router'
import { SessionProvider, useSession } from './session'
import { SignInPage } from './SignInPage'
import { VoucherOrderPage, VoucherShopPage } from './Vouchers'
import { WaitlistPage } from './Waitlist'

/** Kundenbereich: schlanke, eigene Oberfläche unter /buchen/, getrennt vom Bundle der Rezeption. */
export function BookingApp() {
  const [lang, setLang] = useState<Language>(detectLanguage)
  // Angezeigt wird die gewählte Sprache, sobald ihr Wörterbuch geladen ist; bis dahin bleibt die bisherige stehen.
  const [active, setActive] = useState<{ lang: Language; t: Texts } | null>(() => {
    const t = textsIfLoaded(lang)
    return t ? { lang, t } : null
  })

  useEffect(() => {
    let current = true
    loadTexts(lang)
      .then((t) => current && setActive({ lang, t }))
      .catch(() => current && setActive((a) => a ?? { lang: 'de', t: baseTexts }))
    try {
      localStorage.setItem('aispa.sprache', lang)
    } catch {
      // Ohne Speicher gilt die Wahl nur für diesen Besuch.
    }
    return () => {
      current = false
    }
  }, [lang])

  // Vor dem Zeichnen setzen, damit Vorleseprogramme die neuen Texte gleich in der richtigen Sprache lesen.
  useLayoutEffect(() => {
    if (active) document.documentElement.lang = active.lang
  }, [active])

  if (!active) return null
  return (
    <LanguageContext.Provider value={active}>
      <SessionProvider>
        <Shell lang={lang} onLanguage={setLang} />
      </SessionProvider>
    </LanguageContext.Provider>
  )
}

function Shell({ lang, onLanguage }: { lang: Language; onLanguage: (l: Language) => void }) {
  const { t } = useT()
  const route = useRoute()
  const { session, signOut } = useSession()
  // Nach dem Abmelden ersetzt der Link „Anmelden“ den Knopf: Fokus dorthin und Abmeldung ansagen.
  const [signedOut, setSignedOut] = useState(false)
  const signInRef = useRef<HTMLAnchorElement>(null)
  useEffect(() => {
    if (signedOut && !session) signInRef.current?.focus()
  }, [signedOut, session])

  // Beim ersten Aufruf bleibt der Fokus am Seitenanfang; erst nach einem Wechsel springt er zur neuen Überschrift.
  const first = useRef(true)
  const [navigated, setNavigated] = useState(false)
  useEffect(() => {
    if (first.current) {
      first.current = false
      return
    }
    setNavigated(true)
  }, [route])

  useEffect(() => {
    document.title = `${titleOf(route, t)} · ${t.appName}`
  }, [route, t])

  return (
    <>
      <a
        className="skip"
        href="#inhalt"
        onClick={(e) => {
          // Der Hash gehört den Routen; deshalb hier nur den Fokus setzen.
          e.preventDefault()
          document.getElementById('inhalt')?.focus()
        }}
      >
        {t.skipToContent}
      </a>
      <header className="top">
        <a className="brand" href={href({ page: 'book' })}>
          {t.appName}
        </a>
        <nav aria-label={t.mainNav}>
          <ul>
            <li>
              <a href={href({ page: 'book' })} aria-current={route.page === 'book' ? 'page' : undefined}>
                {t.navBook}
              </a>
            </li>
            <li>
              <a href={href({ page: 'voucher' })} aria-current={route.page === 'voucher' || route.page === 'voucherOrder' ? 'page' : undefined}>
                {t.navVoucher}
              </a>
            </li>
            <li>
              <a href={href({ page: 'mine' })} aria-current={route.page === 'mine' ? 'page' : undefined}>
                {t.navMine}
              </a>
            </li>
            <li>
              {session ? (
                <button
                  type="button"
                  className="link"
                  onClick={() => {
                    setSignedOut(true)
                    void signOut()
                  }}
                >
                  {t.navSignOut}
                </button>
              ) : (
                <a ref={signInRef} href={href({ page: 'signin' })} aria-current={route.page === 'signin' ? 'page' : undefined}>
                  {t.navSignIn}
                </a>
              )}
            </li>
          </ul>
        </nav>
        <p className="visually-hidden" role="status">
          {signedOut && !session ? t.signedOut : ''}
        </p>
        <label className="language">
          <span className="visually-hidden">{t.language}</span>
          <select value={lang} onChange={(e) => onLanguage(e.target.value as Language)}>
            {(Object.keys(languageNames) as Language[]).map((l) => (
              <option key={l} value={l} lang={l}>
                {languageNames[l]}
              </option>
            ))}
          </select>
        </label>
      </header>
      <main id="inhalt" tabIndex={-1}>
        <Page route={route} navigated={navigated} />
      </main>
      <footer className="bottom">
        <a href={href({ page: 'imprint' })}>{t.navImprint}</a>
        <a href={href({ page: 'accessibility' })}>{t.navAccessibility}</a>
      </footer>
    </>
  )
}

function Page({ route, navigated }: { route: Route; navigated: boolean }) {
  switch (route.page) {
    case 'book':
      return <BookPage key={route.prefill ? JSON.stringify(route.prefill) : 'book'} prefill={route.prefill} navigatedHere={navigated} />
    case 'mine':
      return <MyAppointmentsPage navigatedHere={navigated} />
    case 'signin':
      return <SignInPage navigatedHere={navigated} />
    case 'manage':
      return <ManagePage token={route.token} navigatedHere={navigated} />
    case 'waitlist':
      return <WaitlistPage token={route.token} navigatedHere={navigated} />
    case 'consent':
      return <ConsentPage token={route.token} navigatedHere={navigated} />
    case 'voucher':
      return <VoucherShopPage navigatedHere={navigated} />
    case 'voucherOrder':
      return <VoucherOrderPage token={route.token} navigatedHere={navigated} />
    case 'anamnesis':
      return <AnamnesisPage token={route.token} navigatedHere={navigated} />
    case 'accessibility':
      return <AccessibilityPage navigatedHere={navigated} />
    case 'imprint':
      return <ImprintPage navigatedHere={navigated} />
  }
}

function titleOf(route: Route, t: Texts): string {
  switch (route.page) {
    case 'book':
      return t.bookTitle
    case 'mine':
      return t.mineTitle
    case 'signin':
      return t.signInTitle
    case 'manage':
      return t.manageBooking
    case 'waitlist':
      return t.waitlistTitle
    case 'consent':
      return t.consentTitle
    case 'voucher':
      return t.voucherTitle
    case 'voucherOrder':
      return t.voucherOrderTitle
    case 'anamnesis':
      return t.anamnesisTitle
    case 'accessibility':
      return t.a11yTitle
    case 'imprint':
      return t.imprintTitle
  }
}
