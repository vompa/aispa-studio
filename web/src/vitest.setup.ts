// jsdom kennt showModal() nicht. Der Ersatz öffnet den Dialog und setzt den Fokus wie der Browser (Fokussierschritte
// des Dialogs): auf das Element mit autofocus, sonst auf den ersten fokussierbaren Nachfahren. Esc löst `cancel` aus
// und schließt, wenn niemand es verhindert; `close()` entfernt `open` und meldet `close`. Er ist eine Näherung:
// kein inerter Hintergrund, keine Sichtbarkeitsprüfung, kein Fokus auf den Dialog selbst, keine Rückgabe des Fokus
// beim Schließen und kein `InvalidStateError` beim zweiten Aufruf.
const focusable = 'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
if (typeof HTMLDialogElement !== 'undefined' && typeof HTMLDialogElement.prototype.showModal !== 'function') {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.setAttribute('open', '')
    const target = this.querySelector<HTMLElement>('[autofocus]') ?? this.querySelector<HTMLElement>(focusable)
    target?.focus()
  }
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    if (!this.hasAttribute('open')) return
    this.removeAttribute('open')
    this.dispatchEvent(new Event('close'))
  }
  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape') return
    const dialog = [...document.querySelectorAll<HTMLDialogElement>('dialog[open]')].at(-1)
    if (dialog && dialog.dispatchEvent(new Event('cancel', { cancelable: true }))) dialog.close()
  })
}

// jsdom kennt kein Popover (`showPopover()`, `popovertarget`, `:popover-open`); seine Standardvorlage blendet jedes
// `[popover]` aus. Der Ersatz bildet das Verhalten des Browsers für `popover="auto"` nach: Klick auf den Auslöser
// mit `popovertarget` schaltet um, Esc schließt das zuletzt geöffnete Popover und gibt den Fokus an den Auslöser
// zurück, `toggle` meldet `newState`, `matches(':popover-open')` gilt nur für offene. Offen heißt hier sichtbar per
// Inline-`display`. Er ist eine Näherung: kein Top-Layer, kein Schließen per Klick daneben, keine verschachtelten
// Popover, kein `InvalidStateError`, Fokus zurück nur bei Esc.
if (typeof HTMLElement !== 'undefined' && typeof HTMLElement.prototype.showPopover !== 'function') {
  const open: HTMLElement[] = []
  const invokers = new WeakMap<HTMLElement, HTMLElement | null>()
  const toggle = (popover: HTMLElement, newState: 'open' | 'closed') =>
    popover.dispatchEvent(Object.assign(new Event('toggle'), { oldState: newState === 'open' ? 'closed' : 'open', newState }))
  const show = (popover: HTMLElement, invoker: HTMLElement | null) => {
    if (open.includes(popover)) return
    open.push(popover)
    invokers.set(popover, invoker)
    popover.style.setProperty('display', 'block')
    toggle(popover, 'open')
  }
  const hide = (popover: HTMLElement, returnFocus: boolean) => {
    const index = open.indexOf(popover)
    if (index < 0) return
    open.splice(index, 1)
    popover.style.removeProperty('display')
    toggle(popover, 'closed')
    if (returnFocus) invokers.get(popover)?.focus()
  }
  HTMLElement.prototype.showPopover = function showPopover(this: HTMLElement) {
    show(this, null)
  }
  HTMLElement.prototype.hidePopover = function hidePopover(this: HTMLElement) {
    hide(this, false)
  }
  HTMLElement.prototype.togglePopover = function togglePopover(this: HTMLElement) {
    if (open.includes(this)) hide(this, false)
    else show(this, null)
    return open.includes(this)
  }
  const matches = Element.prototype.matches
  Element.prototype.matches = function matchesPopover(this: Element, selectors: string) {
    return selectors === ':popover-open' ? open.includes(this as HTMLElement) : matches.call(this, selectors)
  } as typeof Element.prototype.matches
  document.addEventListener('click', (e) => {
    const invoker = (e.target as Element | null)?.closest<HTMLElement>('[popovertarget]')
    const popover = invoker && document.getElementById(invoker.getAttribute('popovertarget') ?? '')
    if (!invoker || !popover) return
    if (open.includes(popover)) hide(popover, false)
    else show(popover, invoker)
  })
  document.addEventListener('keydown', (e) => {
    const top = open.at(-1)
    if (e.key === 'Escape' && top) hide(top, true)
  })
}
