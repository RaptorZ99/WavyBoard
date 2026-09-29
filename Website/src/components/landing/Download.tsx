import { SoonTooltip } from '../ui/SoonTooltip'

const BUILDS = [
  { os: 'Windows', detail: '10 et 11, 64 bits' },
  { os: 'macOS', detail: 'Apple Silicon (M1 et suivants)' },
]

export function Download() {
  return (
    <section id="telecharger" className="relative overflow-hidden bg-foam py-28 text-abyss sm:py-32" aria-labelledby="telecharger-titre">
      <div className="mx-auto grid max-w-[1400px] gap-12 px-4 sm:px-8 lg:grid-cols-[minmax(0,6fr)_minmax(0,6fr)] lg:items-center">
        <div>
          <h2 id="telecharger-titre" className="display text-[clamp(2.8rem,6vw,5.2rem)] font-[800] text-abyss">
            À l’eau.
          </h2>
          <p className="mt-5 max-w-[44ch] text-[1.15rem] leading-relaxed text-ink/75">
            WavyBoard sera gratuit. Branche ta manette en USB pour les vibrations, lance le jeu, rame vers le pic.
          </p>
          <p className="ui mt-6 text-[0.9rem] text-ink/70">Configuration conseillée : GPU dédié ou Apple M2, 8 Go de RAM. Cible : 60 images par seconde en 1080p.</p>
        </div>
        <ul className="grid gap-4 sm:grid-cols-2">
          {BUILDS.map((b) => (
            <li key={b.os} className="flex">
              <SoonTooltip className="w-full">
                {(tip) => (
                  <button
                    type="button"
                    aria-disabled="true"
                    aria-describedby={tip}
                    onClick={(e) => e.currentTarget.focus()}
                    className="flex h-full w-full cursor-default flex-col justify-between gap-10 rounded-[1.5rem] bg-abyss p-7 text-left text-foam transition-transform duration-300 hover:-translate-y-1"
                  >
                    <span className="flex items-start justify-between gap-4">
                      <span>
                        <span className="display block text-[2rem] font-[760]">{b.os}</span>
                        <span className="ui mt-1 block text-[0.92rem] text-foam/70">{b.detail}</span>
                      </span>
                      <span className="ui mt-2 shrink-0 rounded-full bg-lagoon/15 px-3 py-1 text-[0.75rem] font-[700] text-lagoon ring-1 ring-lagoon/40">Bientôt</span>
                    </span>
                    <span className="ui inline-flex w-fit items-center gap-2 rounded-full bg-sun/85 px-5 py-2.5 text-[0.95rem] font-[750] text-abyss">
                      <svg viewBox="0 0 16 16" className="h-4 w-4" aria-hidden="true">
                        <path d="M8 2v8M4.5 6.5L8 10l3.5-3.5M3 13h10" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                      Télécharger
                    </span>
                  </button>
                )}
              </SoonTooltip>
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}
