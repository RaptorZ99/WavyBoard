/**
 * Scrolling is left to the browser: native scrolling runs on the compositor thread, so it stays smooth whatever
 * the page is doing (a JavaScript smooth-scroll library makes every scrolled frame wait for the main thread).
 * Only programmatic jumps (anchors, chapter buoys) are animated, with the native smooth behaviour.
 */
const reduced = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

/** Scrolls to an element id (or the top); if the page moved under the scroll (lazy content), lands again. */
export function scrollToId(id: string | null, offset = -72) {
  const el = id ? document.getElementById(id) : null
  if (id && !el) return
  const target = () => (el ? el.getBoundingClientRect().top + window.scrollY + offset : 0)
  window.scrollTo({ top: target(), behavior: reduced() ? 'auto' : 'smooth' })
  if (!el) return
  const settle = () => {
    const miss = el.getBoundingClientRect().top + offset
    if (Math.abs(miss) > 4) window.scrollTo({ top: target(), behavior: 'auto' })
  }
  if ('onscrollend' in window) window.addEventListener('scrollend', settle, { once: true })
  else setTimeout(settle, 1400)
}

export function jumpToTop() {
  window.scrollTo({ top: 0, behavior: 'auto' })
}

/** Scrolls to an absolute position. */
export function scrollToY(y: number) {
  window.scrollTo({ top: y, behavior: reduced() ? 'auto' : 'smooth' })
}
