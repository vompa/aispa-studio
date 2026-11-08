// Verbindung zum Service Worker (public/buchen/sw.js). Er hält nur die App selbst vor, keine Termine und keine Gesundheitsdaten.

export function registerServiceWorker() {
  // Eingebettet auf der Website eines Spas (ADR 0080) bleibt es eine Seite, keine App.
  if (!import.meta.env.PROD || !('serviceWorker' in navigator) || window.top !== window.self) return
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/buchen/sw.js', { scope: '/buchen/' }).catch(() => {
      // Ohne Worker geht alles, nur der Start ohne Netz nicht. Kein Grund für eine Meldung.
    })
  })
}
