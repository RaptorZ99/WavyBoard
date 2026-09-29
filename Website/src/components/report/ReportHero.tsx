import { motion, useReducedMotion } from 'motion/react'
import { WaveCanvas } from '../hero/WaveCanvas'
import { scrollToId } from '../../lib/smoothScroll'

const WORDS = ['Faire', 'un', 'jeu', 'vidéo', 'Unity', 'avec', 'des', 'agents', 'IA']

export function ReportHero() {
  const reduce = useReducedMotion()
  const fade = (delay: number) => ({
    initial: reduce ? false : { opacity: 0, y: 18 },
    animate: { opacity: 1, y: 0 },
    transition: { duration: 1, delay, ease: [0.22, 1, 0.36, 1] as const },
  })
  return (
    <header className="relative isolate flex h-svh min-h-[700px] items-end overflow-hidden bg-trench">
      <WaveCanvas className="absolute inset-0 -z-10" view="tube" parallax={0.8} />
      <div className="pointer-events-none absolute inset-0 -z-10 bg-[linear-gradient(90deg,rgba(3,23,42,0.88)_0%,rgba(3,23,42,0.55)_38%,rgba(3,23,42,0)_66%)]" />
      <div className="pointer-events-none absolute inset-0 -z-10 bg-[linear-gradient(180deg,rgba(3,23,42,0)_55%,#041a30_100%)]" />

      <div className="mx-auto w-full max-w-[1400px] px-4 pb-16 sm:px-8 sm:pb-24">
        <motion.p {...fade(0.2)} className="ui mb-6 text-[0.95rem] text-lagoon">
          WavyBoard : rapport de projet
        </motion.p>
        <h1 className="display max-w-[13ch] text-[clamp(3rem,7.6vw,7.2rem)] font-[800] leading-[0.9] tracking-[-0.03em] text-foam">
          {WORDS.map((w, i) => (
            <span key={i}>
              <motion.span
                className="inline-block"
                initial={reduce ? false : { opacity: 0, y: '0.6em', filter: 'blur(8px)' }}
                animate={{ opacity: 1, y: 0, filter: 'blur(0px)' }}
                transition={{ duration: 1.1, delay: 0.35 + i * 0.07, ease: [0.22, 1, 0.36, 1] }}
              >
                {w}
              </motion.span>{' '}
            </span>
          ))}
        </h1>
        <motion.p {...fade(1.1)} className="mt-7 max-w-[40ch] font-serif text-[clamp(1.25rem,2vw,1.6rem)] leading-snug text-foam/90">
          Veille, spécification, implémentation, itérations et limites.
        </motion.p>
        <motion.div {...fade(1.35)} className="mt-10 flex flex-wrap items-end justify-between gap-6 border-t border-foam/15 pt-6">
          <p className="ui text-[0.92rem] leading-relaxed text-foam/70">
            Alexandre, Maxime, Louis. M2 EFREI, Culture &amp; Concepts Informatiques. Septembre 2026.
          </p>
          <button
            type="button"
            onClick={() => scrollToId('en-bref', -120)}
            className="ui group inline-flex items-center gap-3 rounded-full border border-foam/30 px-6 py-3 text-[0.95rem] font-[650] text-foam transition-colors hover:border-foam hover:bg-foam/10"
          >
            Entrer dans le rapport
            <svg viewBox="0 0 16 16" className="h-4 w-4 transition-transform group-hover:translate-y-0.5" aria-hidden="true">
              <path d="M8 3v10M3.5 8.5L8 13l4.5-4.5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </button>
        </motion.div>
      </div>
    </header>
  )
}
