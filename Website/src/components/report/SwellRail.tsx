import { useEffect, useState } from 'react'
import { motion, useScroll, useSpring, useTransform } from 'motion/react'
import { scrollToId } from '../../lib/smoothScroll'
import { CHAPTERS } from './chapters'

const W = 1000
const H = 34
const y = (x: number) => H / 2 + Math.sin((x / W) * Math.PI * 2 * 7.5) * 6
const PATH = (() => {
  let d = ''
  for (let x = 0; x <= W; x += 4) d += `${x ? 'L' : 'M'}${x} ${y(x).toFixed(2)}`
  return d
})()

/** Progress through the report drawn as a swell line, one buoy per chapter. */
export function SwellRail() {
  const { scrollYProgress } = useScroll()
  const progress = useSpring(scrollYProgress, { stiffness: 140, damping: 30, restDelta: 0.0005 })
  // the lit part of the swell line is an HTML layer clipped from the right: paint only, never a layout
  const clip = useTransform(progress, (v) => `inset(-10px ${(100 - v * 100).toFixed(2)}% -10px 0)`)
  const [active, setActive] = useState(-1)
  const [hover, setHover] = useState<number | null>(null)
  const [past, setPast] = useState(false)

  useEffect(() => {
    let raf = 0
    const update = () => {
      raf = 0
      const line = window.innerHeight * 0.35
      let cur = -1
      CHAPTERS.forEach((c, i) => {
        const el = document.getElementById(c.id)
        if (el && el.getBoundingClientRect().top <= line) cur = i
      })
      setActive(cur)
      setPast(window.scrollY > window.innerHeight * 0.6)
    }
    const on = () => {
      if (!raf) raf = requestAnimationFrame(update)
    }
    update()
    window.addEventListener('scroll', on, { passive: true })
    return () => {
      window.removeEventListener('scroll', on)
      cancelAnimationFrame(raf)
    }
  }, [])

  const shown = hover ?? active
  return (
    <nav
      aria-label="Chapitres du rapport"
      className={`fixed inset-x-0 top-16 z-40 transition-[opacity,transform] duration-500 ${past ? 'opacity-100' : 'pointer-events-none -translate-y-2 opacity-0'}`}
    >
      <motion.div className="h-[3px] origin-left bg-lagoon md:hidden" style={{ scaleX: progress }} />
      <div className="hidden border-b border-foam/10 bg-[#041a30]/95 md:block">
        <div className="relative mx-auto flex h-11 max-w-[1400px] items-center gap-5 px-8">
          <span className="ui w-44 shrink-0 truncate text-[0.78rem] text-foam/70" aria-live="polite">
            {shown >= 0 ? (
              <>
                <span className="tabular-nums text-lagoon">{String(CHAPTERS[shown].n).padStart(2, '0')}</span> {CHAPTERS[shown].short}
              </>
            ) : (
              'Rapport de projet'
            )}
          </span>
          <div className="relative h-[34px] flex-1">
            <svg viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="none" className="absolute inset-0 h-full w-full overflow-visible" aria-hidden="true">
              <path d={PATH} fill="none" stroke="#f0f7ff" strokeOpacity="0.16" strokeWidth="1.5" vectorEffect="non-scaling-stroke" />
            </svg>
            <motion.div className="absolute inset-0" style={{ clipPath: clip }} aria-hidden="true">
              <svg viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="none" className="h-full w-full overflow-visible">
                <path d={PATH} fill="none" stroke="#33cce6" strokeWidth="2" vectorEffect="non-scaling-stroke" />
              </svg>
            </motion.div>
            <ol className="absolute inset-0">
              {CHAPTERS.map((c, i) => {
                const x = ((i + 0.5) / CHAPTERS.length) * W
                const on = i === active
                return (
                  <li key={c.id} className="absolute -translate-x-1/2 -translate-y-1/2" style={{ left: `${(x / W) * 100}%`, top: `${(y(x) / H) * 100}%` }}>
                    <a
                      href={`#${c.id}`}
                      onMouseEnter={() => setHover(i)}
                      onMouseLeave={() => setHover(null)}
                      onFocus={() => setHover(i)}
                      onBlur={() => setHover(null)}
                      onClick={(e) => {
                        e.preventDefault()
                        window.history.replaceState(null, '', `#${c.id}`)
                        scrollToId(c.id, -120)
                      }}
                      className="group flex h-6 w-6 items-center justify-center rounded-full"
                      aria-label={`${c.n}. ${c.title}`}
                      aria-current={on ? 'true' : undefined}
                    >
                      <span
                        className={`block rounded-full transition-all duration-300 ${
                          on ? 'h-3 w-3 bg-lagoon shadow-[0_0_14px_rgba(51,204,230,0.9)]' : i < active ? 'h-2 w-2 bg-lagoon/70' : 'h-2 w-2 bg-foam/30 group-hover:bg-foam'
                        }`}
                      />
                    </a>
                  </li>
                )
              })}
            </ol>
          </div>
        </div>
      </div>
    </nav>
  )
}
