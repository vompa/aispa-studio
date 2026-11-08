import { api } from '../api/client'

/** Passkeys brauchen die JSON-Helfer des Browsers; ohne sie bleibt die Anmeldung mit Passwort. */
export function passkeysSupported(): boolean {
  return typeof window.PublicKeyCredential?.parseRequestOptionsFromJSON === 'function'
}

/**
 * Anmeldung per Passkey: Optionen vom Server holen, Browser-Dialog öffnen, Ergebnis zurückschicken.
 * Die Optionen-Endpunkte liefern JSON im WebAuthn-Format, das der Browser direkt einlesen kann.
 */
export async function signInWithPasskey(): Promise<boolean> {
  const options = await fetch('/api/v1/auth/passkey/request-options', { method: 'POST' })
  if (!options.ok) return false

  const publicKey = PublicKeyCredential.parseRequestOptionsFromJSON(await options.json())
  const credential = await navigator.credentials.get({ publicKey })
  if (!credential) return false

  const { response } = await api.POST('/api/v1/auth/passkey/login', {
    body: { credentialJson: JSON.stringify(credential) },
  })
  return response.ok
}

/** Fügt dem angemeldeten Konto einen weiteren Passkey hinzu (z. B. für ein zweites Gerät). */
export async function addPasskey(): Promise<boolean> {
  const options = await fetch('/api/v1/auth/passkey/creation-options', { method: 'POST' })
  if (!options.ok) return false

  const publicKey = PublicKeyCredential.parseCreationOptionsFromJSON(await options.json())
  const credential = await navigator.credentials.create({ publicKey })
  if (!credential) return false

  const { response } = await api.POST('/api/v1/auth/passkey', {
    body: { credentialJson: JSON.stringify(credential) },
  })
  return response.ok
}
