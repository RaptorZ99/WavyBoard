/**
 * While the page scrolls, elements slide under a still cursor and each one would start its hover transition:
 * dozens of repaints per second for nothing. During a scroll, an invisible fixed shield takes the pointer; it is
 * one element switched on and off (switching pointer-events on the page instead restyles every node).
 */
export function pauseHoverWhileScrolling() {
  const shield = document.createElement('div')
  shield.setAttribute('aria-hidden', 'true')
  shield.style.cssText = 'position:fixed;inset:0;z-index:2147483647;display:none;contain:strict'
  document.body.appendChild(shield)
  let timer = 0
  const onScroll = () => {
    if (!timer) shield.style.display = 'block'
    else window.clearTimeout(timer)
    timer = window.setTimeout(() => {
      shield.style.display = 'none'
      timer = 0
    }, 140)
  }
  window.addEventListener('scroll', onScroll, { passive: true })
  return () => {
    window.removeEventListener('scroll', onScroll)
    window.clearTimeout(timer)
    shield.remove()
  }
}
