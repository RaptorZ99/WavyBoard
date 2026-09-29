import { useEffect } from 'react'
import { motion, useReducedMotion } from 'motion/react'
import { useLocation } from 'react-router'

const EDGE = 'M0 40 C 120 10, 240 70, 360 40 S 600 10, 720 40 S 960 70, 1080 40 S 1320 10, 1440 40 V 80 H 0 Z'

// the page the visitor landed on does not get the transition, only the pages reached by navigating
let shownPath: string | null = null

/** Between pages, three layers of water (foam, lagoon, abyss) wash up the screen and uncover the new page. */
export function RouteWave() {
  const { pathname } = useLocation()
  const reduce = useReducedMotion()
  const first = shownPath === null || shownPath === pathname
  useEffect(() => {
    shownPath = pathname
  }, [pathname])
  if (reduce) return null
  const layers = [
    { color: '#f0f7ff', delay: 0.16 },
    { color: '#33cce6', delay: 0.08 },
    { color: '#051f38', delay: 0 },
  ]
  return (
    <div className="pointer-events-none fixed inset-0 z-[70]" aria-hidden="true" key={pathname}>
      {layers.map((l, i) => (
        <motion.div
          key={i}
          className="absolute inset-x-0 top-0 h-[115vh] will-change-transform"
          initial={first ? false : { y: '0%' }}
          animate={{ y: '-118%' }}
          transition={{ duration: 1.05, delay: l.delay, ease: [0.76, 0, 0.24, 1] }}
        >
          <div className="absolute inset-x-0 top-0 bottom-[7vh]" style={{ background: l.color }} />
          <svg viewBox="0 0 1440 80" preserveAspectRatio="none" className="absolute inset-x-0 bottom-0 h-[7.2vh] w-full">
            <path d={EDGE} transform="scale(1 -1) translate(0 -80)" fill={l.color} />
          </svg>
        </motion.div>
      ))}
    </div>
  )
}
