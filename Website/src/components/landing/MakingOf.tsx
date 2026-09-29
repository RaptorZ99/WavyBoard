import { Link } from 'react-router'

const NUMBERS = [
  { v: '21 jours', l: 'du premier commit à la vague finale' },
  { v: '58 commits', l: 'sur 8 branches, trois auteurs humains' },
  { v: '5 vagues', l: 'réécrites avant la bonne' },
  { v: '−1 M lignes', l: 'supprimées au grand ménage' },
]

export function MakingOf() {
  return (
    <section className="relative overflow-hidden bg-trench py-28 sm:py-36" aria-labelledby="coulisses-titre">
      <div className="pointer-events-none absolute -right-60 -top-60 h-[760px] w-[760px] bg-[radial-gradient(closest-side,rgba(13,107,148,0.3),transparent)]" />
      <div className="relative mx-auto max-w-[1400px] px-4 sm:px-8">
        <div className="grid gap-12 lg:grid-cols-[minmax(0,7fr)_minmax(0,5fr)] lg:items-end">
          <h2 id="coulisses-titre" className="font-serif text-[clamp(2.2rem,4.6vw,4rem)] font-[340] leading-[1.06] tracking-[-0.01em] text-foam">
            Trois étudiants, aucun n’avait touché à Unity. Le jeu a été écrit par des agents IA.
          </h2>
          <div>
            <p className="text-[1.12rem] leading-relaxed text-foam/75">
              Une veille et une spécification rédigées par Fable 5.1, une implémentation pilotée en ligne de commande dans l’éditeur Unity, puis des dizaines d’itérations jusqu’à Opus 5.5. Ce qui a marché, ce qui a tourné en rond, et pourquoi Unity résiste plus aux IA qu’un site web.
            </p>
            <Link
              to="/rapport"
              className="ui mt-8 inline-flex items-center gap-3 rounded-full bg-lagoon px-7 py-4 text-[1.02rem] font-[750] text-abyss transition-transform duration-300 hover:-translate-y-0.5"
            >
              Lire le rapport de projet
            </Link>
          </div>
        </div>
        <dl className="mt-16 grid gap-8 border-t border-foam/15 pt-10 sm:grid-cols-2 lg:grid-cols-4">
          {NUMBERS.map((n) => (
            <div key={n.v}>
              <dt className="display text-[2.2rem] font-[760] text-foam">{n.v}</dt>
              <dd className="mt-1 text-[1rem] text-foam/60">{n.l}</dd>
            </div>
          ))}
        </dl>
      </div>
    </section>
  )
}
