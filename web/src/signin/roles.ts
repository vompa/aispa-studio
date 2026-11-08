import type { StaffRole } from '../api/client'

// `GeneralManager` ist der Manager (ADR 0070); `Manager` ist seit jeher die Standortleitung.
export const ROLE_LABEL: Record<StaffRole, string> = {
  Owner: 'Inhaber',
  GeneralManager: 'Manager',
  Manager: 'Standortleitung',
  Reception: 'Rezeption',
  Backoffice: 'Backoffice',
  Therapist: 'Therapeut:in',
}

export const ROLE_HINT: Record<StaffRole, string> = {
  Owner: 'Alles an allen Standorten, auch Standorte anlegen und Inhaber ernennen. Der Inhaber bleibt Eigentümer des Kontos.',
  GeneralManager:
    'Darf wie der Inhaber alles, legt aber keine Inhaber- und Manager-Zugänge an, ändert oder entfernt sie nicht und kündigt weder Lizenz noch Abrechnung.',
  Manager: 'Plant, pflegt Stammdaten und lädt Personen für die eigenen Standorte ein. Umsätze nur mit dem Recht „Berichte mit Beträgen“.',
  Reception: 'Bucht, verschiebt, storniert, checkt ein und legt Kunden an, liest die Anamnese. Umsätze nur mit dem Recht „Berichte mit Beträgen“.',
  Backoffice:
    'Für Buchhaltung und Lohnverrechnung: liest Berichte mit Beträgen, Belege, Tagesabschlüsse, Dienstplan und Arbeitszeiten und exportiert sie. Bucht und kassiert nicht, sieht keine Kunden und ändert keine Einstellungen.',
  Therapist: 'Sieht den Kalender, liest die Anamnese und setzt den Status der eigenen Termine.',
}
