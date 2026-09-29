import { FEATURES, WAVE_SIZES } from '../../content/game'

export function Intro() {
  return (
    <section className="relative bg-abyss pb-24 pt-10 sm:pb-32" aria-labelledby="intro-titre">
      <div className="mx-auto max-w-[1400px] px-4 sm:px-8">
        <div className="grid gap-12 lg:grid-cols-[minmax(0,7fr)_minmax(0,5fr)]">
          <h2 id="intro-titre" className="font-serif text-[clamp(2.1rem,4.2vw,3.7rem)] font-[340] leading-[1.08] tracking-[-0.01em] text-foam">
            Un seul pic, taillé d’après Teahupoo. Une lèvre épaisse qui retombe une hauteur de vague devant toi. Un tube rond où l’on tient debout.
          </h2>
          <div className="space-y-6 text-[1.12rem] leading-relaxed text-foam/75 lg:pt-3">
            <p>
              WavyBoard ne simule pas l’océan entier : il soigne une vague. Elle déroule toujours dans le même sens, à la vitesse de sa section, et tout le jeu consiste à la lire.
            </p>
            <p>
              Tu rames, tu pars, tu te cales dans le tube, tu cours contre le rouleau, tu t’envoles de la lèvre. Et la série suivante arrive déjà.
            </p>
          </div>
        </div>

        <dl className="mt-20 grid gap-10 border-t border-foam/15 pt-10 sm:grid-cols-3">
          {FEATURES.map((f) => (
            <div key={f.value}>
              <dt className="display text-[clamp(2.4rem,4.4vw,3.6rem)] font-[760] text-lagoon">{f.value}</dt>
              <dd className="mt-2 max-w-[30ch] text-[1.02rem] text-foam/70">{f.label}</dd>
            </div>
          ))}
        </dl>

        <div className="mt-16" aria-label="Tailles de vagues">
          <div className="flex items-end gap-3 sm:gap-6">
            {WAVE_SIZES.map((w) => (
              <div key={w.name} className="flex-1">
                <div className="relative overflow-hidden rounded-t-[999px] rounded-b-md bg-gradient-to-t from-wall/40 to-lagoon/80" style={{ height: `${w.meters * 34}px` }}>
                  <div className="absolute inset-x-0 top-0 h-1/3 bg-gradient-to-b from-foam/35 to-transparent" />
                </div>
                <p className="ui mt-3 text-[0.95rem] font-[700] text-foam">{w.name}</p>
                <p className="ui text-[0.85rem] text-foam/55 tabular-nums">{w.meters.toLocaleString('fr-FR')} m</p>
              </div>
            ))}
          </div>
          <p className="ui mt-5 text-[0.85rem] text-foam/55">Une série toutes les 42 s, trois vagues à 11 s d’écart. La taille est tirée au sort à chaque série.</p>
        </div>
      </div>
    </section>
  )
}
