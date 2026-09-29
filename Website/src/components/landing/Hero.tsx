import { motion, useReducedMotion } from 'motion/react'
import { WaveCanvas } from '../hero/WaveCanvas'
import { HashLink } from '../layout/HashLink'
import { SwellTitle } from './SwellTitle'
import { SoonTooltip } from '../ui/SoonTooltip'

export function Hero() {
  const reduce = useReducedMotion()
  const fade = (delay: number) => ({
    initial: reduce ? false : { opacity: 0, y: 16 },
    animate: { opacity: 1, y: 0 },
    transition: { duration: 0.9, delay, ease: [0.22, 1, 0.36, 1] as const },
  })
  return (
    <section className="relative isolate flex min-h-[680px] h-svh items-end overflow-hidden bg-abyss" aria-labelledby="hero-title">
      <WaveCanvas className="absolute inset-0 -z-10" />
      {/* legibility: the sea darkens toward the title and fades into the page */}
      <div className="pointer-events-none absolute inset-0 -z-10 bg-[linear-gradient(180deg,rgba(5,31,56,0.35)_0%,rgba(5,31,56,0)_22%,rgba(5,31,56,0)_52%,rgba(5,31,56,0.78)_82%,#051f38_100%)]" />
      <div className="pointer-events-none absolute inset-0 -z-10 bg-[radial-gradient(70%_60%_at_0%_100%,rgba(3,23,42,0.7),transparent_70%)]" />
      <div className="pointer-events-none absolute inset-0 -z-10 bg-[linear-gradient(180deg,transparent_38%,rgba(5,31,56,0.82)_62%)] sm:hidden" />

      <div className="mx-auto w-full max-w-[1400px] px-4 pb-14 sm:px-8 sm:pb-20">
        <div id="hero-title">
          <SwellTitle
            text="WavyBoard"
            className="display select-none whitespace-nowrap text-[clamp(2.6rem,10.4vw,10rem)] font-[800] leading-[0.86] tracking-[-0.03em] text-foam [font-variation-settings:'wdth'_122,'wght'_820]"
          />
        </div>
        <div className="mt-6 grid items-end gap-8 md:grid-cols-[minmax(0,1fr)_auto]">
          <motion.p {...fade(1.0)} className="max-w-[36ch] text-[clamp(1.2rem,2.1vw,1.6rem)] leading-snug text-foam/90">
            Une vague lourde qui déroule, un tube qui se referme derrière toi et trente figures au bout du pouce. Du bodyboard, pour de vrai.
          </motion.p>
          <motion.div {...fade(1.2)} className="flex flex-wrap items-center gap-3">
            <SoonTooltip>
              {() => (
                <HashLink
                  to="/#telecharger"
                  className="ui inline-flex items-center gap-2 rounded-full bg-sun px-7 py-4 text-[1.02rem] font-[750] text-abyss shadow-[0_12px_40px_-12px_rgba(255,197,107,0.7)] transition-transform duration-300 hover:-translate-y-0.5"
                >
                  Télécharger le jeu
                </HashLink>
              )}
            </SoonTooltip>
            <HashLink
              to="/#bande-annonce"
              className="ui inline-flex items-center gap-2 rounded-full border border-foam/35 px-6 py-4 text-[1.02rem] font-[650] text-foam transition-colors duration-300 hover:border-foam hover:bg-foam/10"
            >
              <svg viewBox="0 0 16 16" className="h-3.5 w-3.5" aria-hidden="true">
                <path d="M4 2.5l9 5.5-9 5.5z" fill="currentColor" />
              </svg>
              Bande-annonce
            </HashLink>
          </motion.div>
        </div>
        <motion.div {...fade(1.45)} className="ui mt-10 flex flex-wrap items-center gap-x-6 gap-y-2 border-t border-foam/15 pt-5 text-[0.85rem] text-foam/60">
          <span>Windows et macOS</span>
          <span>Manette PS5 ou clavier et souris</span>
          <span>Unity 6.6, URP</span>
          <span className="ml-auto hidden md:inline">Vague rendue en direct dans ton navigateur, avec le profil du jeu</span>
        </motion.div>
      </div>
    </section>
  )
}
