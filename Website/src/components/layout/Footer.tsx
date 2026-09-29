import { Link } from 'react-router'
import { REPO_URL, TEAM } from '../../content/game'
import { Wordmark } from './Logo'
import { HashLink } from './HashLink'

export function Footer() {
  return (
    <footer className="relative border-t border-foam/10 bg-trench text-foam">
      <div className="mx-auto grid max-w-[1400px] gap-12 px-4 py-16 sm:px-8 md:grid-cols-[1.2fr_1fr_1fr]">
        <div className="space-y-4">
          <Wordmark />
          <p className="max-w-[42ch] text-[1.05rem] leading-relaxed text-foam/70">
            Un jeu de bodyboard sous Unity 6, conçu en septembre 2026 par trois étudiants du M2 EFREI et une équipe d’agents IA.
          </p>
        </div>
        <div>
          <h2 className="ui mb-4 text-sm font-[700] text-foam/60">Le jeu</h2>
          <ul className="ui space-y-2 text-[0.95rem]">
            <li><HashLink to="/#vague" className="text-foam/80 hover:text-lagoon">La vague</HashLink></li>
            <li><HashLink to="/#figures" className="text-foam/80 hover:text-lagoon">Les figures</HashLink></li>
            <li><HashLink to="/#commandes" className="text-foam/80 hover:text-lagoon">Commandes</HashLink></li>
            <li><HashLink to="/#telecharger" className="text-foam/80 hover:text-lagoon">Télécharger</HashLink></li>
            <li><Link to="/rapport" className="text-foam/80 hover:text-lagoon">Rapport de projet</Link></li>
            <li><a href={REPO_URL} className="text-foam/80 hover:text-lagoon" target="_blank" rel="noreferrer">Code source</a></li>
          </ul>
        </div>
        <div>
          <h2 className="ui mb-4 text-sm font-[700] text-foam/60">L’équipe</h2>
          <ul className="space-y-3">
            {TEAM.map((m) => (
              <li key={m.name}>
                <span className="ui block font-[700]">{m.name}</span>
                <span className="text-[0.95rem] text-foam/60">{m.role}</span>
              </li>
            ))}
          </ul>
        </div>
      </div>
      <div className="mx-auto flex max-w-[1400px] flex-col gap-2 border-t border-foam/10 px-4 py-6 text-sm text-foam/55 sm:flex-row sm:justify-between sm:px-8">
        <p className="ui">M2 EFREI, Culture &amp; Concepts Informatiques, 2026</p>
        <p className="ui">Contenus tiers CC0 : Quaternius, Poly Haven, Kenney, OpenGameArt, Storm Breakers</p>
      </div>
    </footer>
  )
}
