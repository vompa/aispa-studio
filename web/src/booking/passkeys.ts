import { api } from './api'

/** Anmeldung im Kundenbereich per Passkey (eigene Endpunkte, getrennt von den Konten des Teams). */
export async function signInWithPasskey(): Promise<boolean> {
  const options = await fetch('/api/online/auth/passkey/request-options', { method: 'POST' })
  if (!options.ok) return false

  const publicKey = PublicKeyCredential.parseRequestOptionsFromJSON(await options.json())
  const credential = await navigator.credentials.get({ publicKey })
  if (!credential) return false

  const { response } = await api.POST('/api/online/auth/passkey/login', {
    body: { credentialJson: JSON.stringify(credential) },
  })
  return response.ok
}

export async function addPasskey(): Promise<boolean> {
  const options = await fetch('/api/online/auth/passkey/creation-options', { method: 'POST' })
  if (!options.ok) return false

  const publicKey = PublicKeyCredential.parseCreationOptionsFromJSON(await options.json())
  const credential = await navigator.credentials.create({ publicKey })
  if (!credential) return false

  const { response } = await api.POST('/api/online/auth/passkey', {
    body: { credentialJson: JSON.stringify(credential) },
  })
  return response.ok
}

export function passkeysSupported(): boolean {
  return typeof window.PublicKeyCredential?.parseRequestOptionsFromJSON === 'function'
}
