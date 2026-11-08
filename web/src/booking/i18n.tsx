import { createContext, useContext } from 'react'

// Texte des Kundenbereichs. Deutsch ist die Grundsprache; jede weitere Sprache ist ein weiteres Wörterbuch mit denselben Schlüsseln
// (TypeScript prüft, dass keiner fehlt).

/** Teile einer Schließzeit für den Hinweis bei der Buchung: Tage schon als Text, Uhrzeiten als „13:00“; `sameDay`, wenn sie an einem Tag beginnt und endet. */
export type ClosedParts = { from: string; until: string; fromTime?: string; untilTime?: string; sameDay: boolean }

const de = {
  appName: 'Online-Buchung',
  skipToContent: 'Zum Inhalt springen',
  language: 'Sprache',
  navBook: 'Termin buchen',
  navMine: 'Meine Termine',
  navSignIn: 'Anmelden',
  navSignOut: 'Abmelden',
  signedOut: 'Sie sind abgemeldet.',
  navAccessibility: 'Barrierefreiheit',
  loading: 'Wird geladen …',
  errorGeneric: 'Das hat leider nicht geklappt. Bitte versuchen Sie es noch einmal.',
  errorOffline: 'Keine Verbindung zum Server. Bitte prüfen Sie Ihre Internetverbindung.',
  back: 'Zurück',
  retry: 'Erneut versuchen',
  chooseOtherLocation: 'Anderen Standort wählen',
  next: 'Weiter',
  change: 'Ändern',
  price: 'Preis',
  planRoute: 'Route planen',
  planRouteNewTab: 'öffnet die Karten-App in einem neuen Fenster',
  directions: 'Anreise',

  bookTitle: 'Termin buchen',
  stepLocation: 'Standort',
  stepTreatment: 'Behandlung',
  stepTime: 'Tag und Uhrzeit',
  stepDetails: 'Ihre Angaben',
  stepOf: (n: number, total: number) => `Schritt ${n} von ${total}`,
  chooseLocation: 'Wo möchten Sie Ihre Behandlung genießen?',
  chooseTreatment: 'Welche Behandlung möchten Sie buchen?',
  noTreatments: 'An diesem Standort gibt es zurzeit keine online buchbaren Behandlungen.',
  noOnlineBooking: 'Termine sind zurzeit nicht online buchbar. Bitte rufen Sie uns an.',
  guestNote: 'Hinweis vom Spa:',
  cancelPolicy: (hours: number) => (hours === 0 ? 'Online stornierbar bis zum Beginn.' : `Online stornierbar bis ${hours} Stunden vor Beginn.`),
  wishTitle: 'Oder sagen Sie uns Ihren Wunsch',
  wishLabel: 'Ihr Wunsch in eigenen Worten',
  wishHint: 'Zum Beispiel: „Massage nächsten Freitag am Nachmittag“. Ein KI-Assistent schlägt freie Zeiten vor.',
  wishButton: 'Zeiten vorschlagen',
  wishSuggestions: 'Vorgeschlagene Zeiten',
  minutes: (n: number) => `${n} Minuten`,
  chooseDay: 'Tag',
  chooseTime: 'Uhrzeit',
  earlierDays: 'Frühere Tage',
  laterDays: 'Spätere Tage',
  noTimesThatDay: 'An diesem Tag ist nichts mehr frei.',
  noTimesAtAll: 'In diesen Tagen ist nichts frei. Bitte blättern Sie weiter.',
  closedNotice: (p: ClosedParts) =>
    p.sameDay
      ? `Am ${p.from}${p.fromTime && p.untilTime ? ` von ${p.fromTime} bis ${p.untilTime} Uhr` : p.fromTime ? ` ab ${p.fromTime} Uhr` : p.untilTime ? ` bis ${p.untilTime} Uhr` : ''} geschlossen`
      : `Vom ${p.from}${p.fromTime ? `, ${p.fromTime} Uhr` : ''} bis ${p.until}${p.untilTime ? `, ${p.untilTime} Uhr` : ''} geschlossen`,
  closureDefault: { Vacation: 'Betriebsurlaub.', SpecialClosure: 'Sonderschließung.', Outage: 'Vorübergehend geschlossen.' },
  timesFree: (n: number) => (n === 1 ? '1 freie Zeit' : `${n} freie Zeiten`),
  timeZoneHint: (zone: string) => `Alle Zeiten in Ortszeit des Standorts (${zone}).`,
  firstName: 'Vorname',
  lastName: 'Nachname',
  email: 'E-Mail-Adresse',
  emailHint: 'Hierhin schicken wir die Bestätigung mit dem Link zum Stornieren.',
  phone: 'Telefon (freiwillig)',
  phoneHint: 'Nur für Rückfragen zu Ihrem Termin.',
  note: 'Nachricht an uns (freiwillig)',
  voucher: 'Gutscheincode (freiwillig)',
  voucherHint: 'Wir prüfen den Gutschein jetzt; eingelöst wird er beim Bezahlen im Spa.',
  voucherInvalid: 'Diesen Gutschein können wir für diese Behandlung nicht annehmen. Bitte prüfen Sie den Code.',
  bookingRefused: 'Online ist diese Buchung leider nicht möglich. Bitte rufen Sie uns an, wir finden gern einen Termin.',
  privacy: 'Ich habe den Datenschutzhinweis gelesen. Meine Angaben werden nur für die Buchung und die Behandlung verwendet.',
  privacyLink: 'Datenschutzhinweis',
  required: 'Pflichtfeld',
  invalidEmail: 'Bitte eine gültige E-Mail-Adresse eingeben.',
  privacyRequired: 'Bitte bestätigen Sie den Datenschutzhinweis.',
  formErrors: (n: number) => (n === 1 ? 'Bitte prüfen Sie 1 Angabe.' : `Bitte prüfen Sie ${n} Angaben.`),
  signedInAs: (email: string) => `Angemeldet als ${email}`,
  guestOrSignIn: 'Sie können ohne Konto buchen. Mit Anmeldung sehen Sie später alle Ihre Termine.',
  summary: 'Ihre Buchung',
  bookNow: 'Verbindlich buchen',
  booking: 'Wird gebucht …',
  slotTaken: 'Diese Zeit ist leider gerade vergeben worden. Bitte wählen Sie eine andere.',

  waitlistOffer: 'Nichts Passendes dabei?',
  waitlistJoin: 'Auf die Warteliste',
  waitlistTitle: 'Warteliste',
  waitlistIntro: 'Wir schreiben Ihnen, sobald im gewünschten Zeitraum ein Termin frei wird. Wer zuerst bucht, bekommt ihn.',
  waitlistFrom: 'Frühester Tag',
  waitlistUntil: 'Spätester Tag',
  waitlistRangeHint: (n: number) => `Höchstens ${n} Tage.`,
  waitlistUntilInvalid: 'Bitte einen Tag ab dem frühesten Tag und innerhalb des Zeitraums wählen.',
  dayPart: 'Tageszeit',
  dayParts: { Any: 'Egal', Morning: 'Vormittags (bis 12 Uhr)', Afternoon: 'Nachmittags (12 bis 17 Uhr)', Evening: 'Abends (ab 17 Uhr)' },
  privacyWaitlist: 'Ich habe den Datenschutzhinweis gelesen. Meine Angaben werden nur für die Warteliste und die Buchung verwendet.',
  waitlistSubmit: 'Auf die Warteliste setzen',
  waitlistJoining: 'Wird eingetragen …',
  waitlistJoinedTitle: 'Sie stehen auf der Warteliste',
  waitlistJoinedText: (email: string) =>
    `Wir haben Ihnen eine Bestätigung an ${email} geschickt. Sobald etwas frei wird, bekommen Sie eine E-Mail mit den freien Zeiten.`,
  waitlistManage: 'Eintrag ansehen oder austragen',
  waitlistMine: 'Meine Warteliste',
  waitlistLeave: 'Austragen',
  waitlistLeft: 'Sie sind von der Warteliste ausgetragen.',
  consentTitle: 'Erinnerung an die nächste Behandlung',
  consentIntro: (spa: string) =>
    `${spa} erinnert Sie per Mail, wenn Ihre nächste Behandlung fällig sein könnte, mit einem Link zum Buchen. Sie können das jederzeit hier oder über den Link in jeder Erinnerung beenden.`,
  consentPending: 'Bitte bestätigen Sie, dass Sie diese Erinnerungen bekommen möchten.',
  consentActive: 'Sie bekommen diese Erinnerungen.',
  consentInactive: 'Sie bekommen keine solchen Erinnerungen.',
  consentConfirm: 'Ja, erinnern Sie mich',
  consentWithdraw: 'Keine Erinnerungen mehr',
  consentConfirmed: 'Danke, Ihre Einwilligung ist bestätigt.',
  consentWithdrawn: 'Sie bekommen keine Erinnerungen an die nächste Behandlung mehr.',
  waitlistStatus: { Waiting: 'Wartet auf einen freien Termin', Booked: 'Erledigt: Termin gebucht', Cancelled: 'Ausgetragen', Expired: 'Abgelaufen' },
  waitlistRange: (from: string, until: string) => `${from} bis ${until}`,
  waitlistLastOffer: (when: string) => `Zuletzt freie Termine gemeldet: ${when}`,

  navVoucher: 'Gutscheine',
  voucherTitle: 'Gutschein schenken',
  voucherIntro: 'Per E-Mail, sofort nach der Zahlung. Zum Ausdrucken oder Weiterleiten.',
  voucherLocation: 'Standort',
  voucherKind: 'Art des Gutscheins',
  voucherValue: 'Wertgutschein',
  voucherValueHint: 'Für alle Behandlungen und Produkte',
  voucherTreatment: 'Für eine Behandlung',
  voucherTreatmentHint: 'Gilt für genau diese Behandlung',
  voucherAmount: 'Betrag',
  voucherOtherAmount: 'Anderer Betrag',
  voucherAmountHint: (min: string, max: string) => `Von ${min} bis ${max}.`,
  voucherAmountInvalid: (min: string, max: string) => `Bitte einen Betrag von ${min} bis ${max} angeben.`,
  voucherChooseTreatment: 'Behandlung',
  voucherRecipient: 'Für (freiwillig)',
  voucherRecipientHint: 'Steht auf dem Gutschein, z. B. „Mama“.',
  voucherMessage: 'Persönliche Nachricht (freiwillig)',
  voucherBuyer: 'Ihre Angaben',
  voucherEmailHint: 'Hierhin schicken wir den Gutschein.',
  privacyVoucher: 'Ich habe den Datenschutzhinweis gelesen. Meine Angaben werden nur für den Gutschein verwendet.',
  voucherTotal: 'Zu bezahlen',
  voucherPay: 'Weiter zur Zahlung',
  voucherPaying: 'Zahlung wird vorbereitet …',
  voucherTestMode: 'Testbetrieb: Es wird kein Geld abgebucht.',
  voucherUnavailable: 'Gutscheine können hier gerade nicht online gekauft werden.',
  voucherOrderTitle: 'Ihr Gutschein',
  voucherTestPayment: 'Testzahlung',
  voucherTestPaymentText: 'Hier würde der Zahlungsanbieter die Karte abfragen. Im Testbetrieb bestätigen Sie die Zahlung selbst.',
  voucherTestPay: 'Zahlung bestätigen (Test)',
  voucherTestCancel: 'Zahlung abbrechen',
  voucherWaitingPayment: 'Wir warten auf die Bestätigung der Zahlung …',
  voucherPayNow: 'Jetzt bezahlen',
  voucherPayOpen: 'Die Zahlung ist noch offen. Sie können sie jetzt abschließen.',
  voucherIssuing: 'Zahlung erhalten. Der Gutschein wird ausgestellt …',
  voucherIssued: 'Fertig! Wir haben Ihnen den Gutschein per E-Mail geschickt.',
  voucherCancelled: 'Die Zahlung wurde abgebrochen. Es wurde nichts abgebucht.',
  voucherFailed: 'Die Zahlung ist angekommen, der Gutschein konnte aber nicht ausgestellt werden. Wir melden uns bei Ihnen.',
  voucherRefunded: 'Dieser Gutschein wurde storniert und der Betrag zurückerstattet.',
  voucherTryAgain: 'Neuen Gutschein auswählen',
  voucherCode: 'Gutscheincode',
  voucherFor: 'Für',
  voucherWorth: (amount: string) => `im Wert von ${amount}`,
  voucherRedeem: (location: string) => `Einlösbar in ${location}, beim Bezahlen an der Rezeption.`,
  voucherPrint: 'Drucken',

  feeTerms: (percent: number, amount: string, hours: number) =>
    `Ausfallgebühr: Wenn Sie nicht kommen oder ${hours === 0 ? 'nach Beginn' : `später als ${hours} Stunden vorher`} absagen, berechnen wir ${amount} (${percent} % des Preises). Dafür hinterlegen Sie nach dem Buchen eine Karte; abgebucht wird nur in diesem Fall.`,
  feeAccept: (amount: string) => `Ich stimme der Ausfallgebühr von ${amount} zu.`,
  feeRequired: 'Bitte stimmen Sie der Ausfallgebühr zu.',
  feeNewGuestsOnly: 'Die Gebühr gilt nur für Gäste, die zum ersten Mal bei uns sind; Stammgäste hinterlegen keine Karte.',
  cardTitle: 'Karte für die Ausfallgebühr',
  cardMissing: 'Bitte hinterlegen Sie noch eine Karte. Abgebucht wird nur bei Nichterscheinen oder später Absage.',
  cardPreparing: 'Die Seite zum Hinterlegen wird vorbereitet …',
  cardSave: 'Karte hinterlegen',
  cardSaved: (amount: string) => `Karte hinterlegt. ${amount} werden nur bei Nichterscheinen oder später Absage abgebucht.`,
  cardFailed: 'Das Hinterlegen wurde abgebrochen. Es wurde nichts abgebucht.',
  cardRetry: 'Erneut versuchen',
  cardAccept: 'Zustimmen und Karte hinterlegen',
  cardCharged: (amount: string) => `Die Ausfallgebühr von ${amount} wurde abgebucht. Den Beleg haben wir Ihnen per E-Mail geschickt.`,
  cardRefunded: (amount: string) => `Die Ausfallgebühr von ${amount} wurde zurückerstattet.`,
  cardHoldUntil: (when: string) => `Bitte bis ${when} hinterlegen, sonst geben wir den Termin für andere Gäste frei.`,
  cardReleased: 'Ohne hinterlegte Karte haben wir den Termin freigegeben. Abgebucht wurde nichts.',
  cardTestText: 'Hier würde der Zahlungsanbieter die Karte abfragen. Im Testbetrieb bestätigen Sie selbst.',
  cardTestSave: 'Karte hinterlegen (Test)',
  cardTestCancel: 'Abbrechen',

  depositTerms: (percent: number, amount: string, hours: number) =>
    `${percent >= 100 ? 'Vorauszahlung' : 'Anzahlung'}: Nach dem Buchen bezahlen Sie ${amount}${percent >= 100 ? '' : ` (${percent} % des Preises)`} online und erhalten einen Beleg${percent >= 100 ? '' : '; der Rest wird vor Ort bezahlt'}. Bei einer Absage ${hours === 0 ? 'vor Beginn' : `bis ${hours} Stunden vorher`} erstatten wir den Betrag. Wenn Sie nicht kommen oder später absagen, behalten wir ihn ein.`,
  depositAccept: (amount: string) => `Ich stimme den Bedingungen für die Zahlung von ${amount} zu.`,
  depositRequired: 'Bitte stimmen Sie den Bedingungen der Anzahlung zu.',
  depositNewGuestsOnly: 'Die Anzahlung gilt nur für Gäste, die zum ersten Mal bei uns sind.',
  depositTitle: (percent: number): string => (percent >= 100 ? 'Vorauszahlung' : 'Anzahlung'),
  depositMissing: (amount: string) => `Bitte bezahlen Sie noch ${amount}. Der Betrag wird beim Bezahlen vor Ort abgezogen.`,
  depositPreparing: 'Die Seite zum Bezahlen wird vorbereitet …',
  depositPay: (amount: string) => `${amount} bezahlen`,
  depositPayUntil: (when: string) => `Bitte bis ${when} bezahlen, sonst geben wir den Termin für andere Gäste frei.`,
  depositFailed: 'Die Zahlung wurde abgebrochen. Es wurde nichts abgebucht.',
  depositPaid: (amount: string) => `${amount} bezahlt. Der Beleg wird gerade ausgestellt.`,
  depositReceipted: (amount: string, receipt: string) => `${amount} bezahlt, Beleg ${receipt}. Der Betrag wird beim Bezahlen vor Ort abgezogen.`,
  depositRefunded: (amount: string) => `${amount} wurden zurückerstattet.`,
  depositReleased: 'Ohne Zahlung haben wir den Termin freigegeben. Abgebucht wurde nichts.',
  depositCancelled: 'Der Termin ist storniert. Abgebucht wurde nichts.',
  depositTestText: 'Hier würde der Zahlungsanbieter die Karte abfragen. Im Testbetrieb bestätigen Sie die Zahlung selbst.',
  depositTestPay: 'Zahlung bestätigen (Test)',

  confirmedTitle: 'Ihr Termin ist gebucht',
  confirmedText: (email: string) => `Wir haben Ihnen eine Bestätigung an ${email} geschickt.`,
  confirmedAnamnesis: 'Bitte füllen Sie vor Ihrem Besuch den kurzen Gesundheitsfragebogen aus.',
  fillAnamnesis: 'Gesundheitsfragebogen ausfüllen',
  manageBooking: 'Termin ansehen oder stornieren',
  bookAnother: 'Weiteren Termin buchen',

  signInTitle: 'Anmelden',
  signInIntro: 'Ohne Passwort: Wir schicken Ihnen einen Code per E-Mail, oder Sie nutzen einen Passkey.',
  sendCode: 'Code senden',
  sendingCode: 'Code wird gesendet …',
  codeSent: (email: string) => `Wir haben einen sechsstelligen Code an ${email} geschickt. Er gilt 10 Minuten.`,
  code: 'Code aus der E-Mail',
  confirmCode: 'Anmelden',
  otherEmail: 'Andere E-Mail-Adresse',
  codeInvalid: 'Der Code stimmt nicht oder ist abgelaufen.',
  or: 'oder',
  withPasskey: 'Mit Passkey anmelden',
  withProvider: (name: string) => `Mit ${name} anmelden`,
  passkeyFailed: 'Die Anmeldung mit Passkey hat nicht geklappt.',
  addPasskey: 'Passkey für schnellere Anmeldung anlegen',
  passkeyAdded: 'Passkey angelegt. Beim nächsten Mal genügt Fingerabdruck, Gesicht oder Geräte-PIN.',
  signInFailed: 'Die Anmeldung hat nicht geklappt. Bitte versuchen Sie es noch einmal.',

  mineTitle: 'Meine Termine',
  upcoming: 'Kommende Termine',
  past: 'Frühere und stornierte Termine',
  noAppointments: 'Sie haben noch keine Termine.',
  cancel: 'Termin stornieren',
  cancelConfirm: 'Wirklich stornieren?',
  cancelYes: 'Ja, stornieren',
  cancelNo: 'Nein, behalten',
  cancelled: 'Storniert',
  attend: 'Ich komme',
  attendDone: 'Danke, Sie haben den Termin bestätigt.',
  attendConfirmed: 'Sie haben zugesagt.',
  move: 'Verschieben',
  moveTitle: 'Termin verschieben',
  moveSubmit: 'Auf diese Zeit verschieben',
  moveDone: 'Der Termin ist verschoben. Sie bekommen eine Bestätigung per E-Mail.',
  moveTaken: 'Diese Zeit ist nicht mehr frei oder der Termin lässt sich online nicht mehr verschieben. Bitte wählen Sie eine andere Zeit oder rufen Sie uns an.',
  moveNotOnline: 'Diese Behandlung ist online gerade nicht buchbar. Bitte rufen Sie uns zum Verschieben an.',
  cancelDone: 'Der Termin ist storniert. Sie bekommen eine Bestätigung per E-Mail.',
  cancelUntil: (when: string) => `Online stornierbar bis ${when}`,
  nextAppointment: 'Ihr nächster Termin',
  addToCalendar: 'Zum Kalender hinzufügen',
  lateCancelHint: (fee: string) => `Die Stornofrist ist vorbei. Eine Absage kostet jetzt ${fee}.`,
  lateCancelConfirm: (fee: string) => `Die Absage kostet ${fee}. Wirklich stornieren?`,
  ownVouchers: 'Meine Gutscheine',
  ownVoucherBalance: (rest: string, of: string) => `Restwert ${rest} von ${of}`,
  ownVoucherUnits: (left: number, of: number) => `Noch ${left} von ${of} Behandlungen`,
  ownVoucherValidUntil: (date: string) => `Gültig bis ${date}`,
  ownVoucherRedeemed: 'Eingelöst',
  ownVoucherVoided: 'Ungültig',
  ownVoucherCode: 'Code',
  cancelTooLate: 'Online-Storno ist nicht mehr möglich. Bitte rufen Sie uns an.',
  anamnesisDone: 'Gesundheitsfragebogen liegt vor',
  anamnesisMissing: 'Gesundheitsfragebogen fehlt noch',
  profileTitle: 'Ihre Daten',
  saveProfile: 'Speichern',
  profileSaved: 'Gespeichert.',
  linkInvalid: 'Dieser Link ist ungültig oder abgelaufen.',

  anamnesisTitle: 'Gesundheitsfragebogen',
  anamnesisFor: (name: string, what: string) => `Für ${name}${what ? `, ${what}` : ''}`,
  anamnesisLast: (when: string) => `Zuletzt abgegeben am ${when}. Sie können ihn jederzeit neu ausfüllen.`,
  yes: 'Ja',
  no: 'Nein',
  consent: 'Einwilligung',
  signature: 'Vor- und Nachname als Bestätigung',
  submitAnamnesis: 'Fragebogen absenden',
  submitting: 'Wird gesendet …',
  anamnesisThanks: 'Vielen Dank! Ihr Fragebogen ist bei uns angekommen.',
  anamnesisTabletThanks: 'Vielen Dank! Bitte geben Sie das Tablet jetzt an der Rezeption zurück.',
  answerRequired: 'Bitte mit Ja oder Nein beantworten.',
  consentRequired: 'Ohne Ihre Einwilligung können wir die Angaben nicht speichern.',

  a11yTitle: 'Erklärung zur Barrierefreiheit',
  mainNav: 'Hauptnavigation',
  a11yIntro:
    'Diese Online-Buchung soll für alle Menschen nutzbar sein. Sie richtet sich nach den Anforderungen des Barrierefreiheitsstärkungsgesetzes (BFSG) und der europäischen Norm EN 301 549, die auf WCAG 2.1 Stufe AA verweist.',
  a11yWhatTitle: 'Was wir dafür tun',
  a11yWhat: [
    'Alle Schritte lassen sich vollständig mit der Tastatur und mit Screenreadern bedienen.',
    'Texte und Bedienelemente haben ausreichenden Kontrast, auch im dunklen Modus.',
    'Die Seite lässt sich auf 200 % vergrößern und funktioniert auf kleinen Bildschirmen ohne seitliches Scrollen.',
    'Formularfelder sind beschriftet, Fehler werden im Klartext beim Feld genannt und vorgelesen.',
    'Es gibt keine Zeitbegrenzung beim Ausfüllen; Bewegungen werden reduziert, wenn Ihr Gerät das wünscht.',
    'Die Anmeldung kommt ohne Passwort und ohne Bilderrätsel aus.',
  ] as readonly string[],
  a11yReportTitle: 'Barriere melden',
  a11yReport:
    'Wenn Ihnen etwas den Zugang erschwert, sagen Sie es uns bitte über die Kontaktdaten des Spas. Wir antworten innerhalb von zwei Wochen und bieten Ihnen bis zur Behebung die Buchung per Telefon oder E-Mail an.',
  a11yEscalate:
    'Erhalten Sie keine zufriedenstellende Antwort, können Sie sich an die zuständige Marktüberwachungsbehörde für Barrierefreiheit wenden.',
  navImprint: 'Impressum',
  imprintTitle: 'Impressum',
  imprintMissing: 'Das Spa vervollständigt diese Angaben gerade. Bei Fragen erreichen Sie es über die Kontaktdaten des Standorts.',
  imprintPhone: 'Telefon',
  imprintEmail: 'E-Mail',
  imprintWebsite: 'Website',
  imprintVatId: 'UID-Nummer',
  imprintRegister: 'Firmenbuch',
  imprintPurpose: 'Unternehmensgegenstand',
  imprintAuthority: 'Aufsichtsbehörde',
  imprintChamber: 'Kammer und Berufsrecht',
}

export type Texts = typeof de

/** Grundsprache, immer geladen. */
export const baseTexts: Texts = de

export const languageNames = { de: 'Deutsch', en: 'English', fr: 'Français', it: 'Italiano' } as const
export type Language = keyof typeof languageNames

const isLanguage = (value: string | null | undefined): value is Language => !!value && Object.hasOwn(languageNames, value)

// Deutsch ist im ersten Bundle; die anderen Sprachen lädt der Browser erst, wenn sie gebraucht werden.
const loaders: Record<Exclude<Language, 'de'>, () => Promise<{ default: Texts }>> = {
  en: () => import('./lang/en'),
  fr: () => import('./lang/fr'),
  it: () => import('./lang/it'),
}
const loaded = new Map<Language, Texts>([['de', de]])

/** Wörterbuch einer Sprache, sofern schon geladen. */
export function textsIfLoaded(lang: Language): Texts | undefined {
  return loaded.get(lang)
}

export async function loadTexts(lang: Language): Promise<Texts> {
  const cached = loaded.get(lang)
  if (cached) return cached
  const texts = (await loaders[lang as Exclude<Language, 'de'>]()).default
  loaded.set(lang, texts)
  return texts
}

export function detectLanguage(): Language {
  // Ein Link vom Empfang (Tablet, Papierbogen) bringt die Sprache mit, die dort gewählt wurde.
  const query = window.location.hash.split('?')[1]
  const fromLink = query ? new URLSearchParams(query).get('lang') : null
  if (isLanguage(fromLink)) return fromLink
  try {
    const saved = localStorage.getItem('aispa.sprache')
    if (isLanguage(saved)) return saved
  } catch {
    // Kein Speicher (privates Fenster): Sprache des Browsers verwenden.
  }
  // Rückfall ist Deutsch, wie bei allen Gästenachrichten (Auftraggeber, Runde 5b): Österreich ist der erste Markt.
  if (!navigator.language) return 'de'
  const browser = navigator.language.toLowerCase().slice(0, 2)
  return isLanguage(browser) ? browser : 'de'
}

export const LanguageContext = createContext<{ lang: Language; t: Texts }>({ lang: 'de', t: de })

export function useT() {
  return useContext(LanguageContext)
}

const locales: Record<Language, string> = { de: 'de-AT', en: 'en-GB', fr: 'fr-CH', it: 'it-CH' }

/** Datum und Uhrzeit immer in der Zeitzone des Standorts, nicht in der des Geräts. */
export function formatDateTime(iso: string, timeZone: string, lang: Language): string {
  return new Intl.DateTimeFormat(locales[lang], {
    timeZone,
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(iso))
}

export function formatTime(iso: string, timeZone: string, lang: Language): string {
  return new Intl.DateTimeFormat(locales[lang], { timeZone, hour: '2-digit', minute: '2-digit' }).format(new Date(iso))
}

/** Kalendertag (YYYY-MM-DD) als Text; 12 Uhr UTC, damit kein Zeitzonenwechsel den Tag verschiebt. */
export function formatDay(date: string, lang: Language, style: 'long' | 'short' = 'long'): string {
  return new Intl.DateTimeFormat(locales[lang], {
    timeZone: 'UTC',
    weekday: style === 'long' ? 'long' : 'short',
    day: 'numeric',
    month: style === 'long' ? 'long' : 'short',
  }).format(new Date(`${date}T12:00:00Z`))
}

/** Kalendertag (YYYY-MM-DD) eines Zeitpunkts in der Zeitzone des Standorts. */
export function localDate(iso: string, timeZone: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(iso))
}

export function formatPrice(price: number, currency: string, lang: Language): string {
  return new Intl.NumberFormat(locales[lang], { style: 'currency', currency }).format(price)
}

export function addDays(date: string, days: number): string {
  const d = new Date(`${date}T12:00:00Z`)
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}
