import type { ReactNode } from 'react'
import { motion } from 'motion/react'
import { B, C, P, Part } from '../kit'

/* ============================================================== 13 Les limites */

const MATRIX_HEAD = ['Critère', 'Site web en TypeScript', '3D en WebGL', 'Algorithme en Python', 'Jeu sous Unity']
const MATRIX: [string, string, string, string, string][] = [
  ['Présence dans les données d’entraînement', 'Très forte', 'Forte', 'Très forte', 'Moyenne, et Unity 6.6 est tout récent'],
  ['Boucle de retour', 'Quelques secondes', 'Quelques secondes', 'Quelques secondes', 'Des dizaines de secondes à quelques minutes'],
  ['Vérifiable par l’IA seule', 'Oui', 'En grande partie', 'Oui', 'Difficilement'],
  ['État caché hors du code', 'Faible', 'Faible', 'Quasi nul', 'Fort : scènes, matériaux, état de l’éditeur'],
  ['Maturité de l’outillage IA', 'Mûr', 'Mûr', 'Mûr', 'Récent : CLI en bêta, Pipeline expérimental'],
]
// how favourable each cell is to an AI (4 = very), read from the words of the table
const SCORES = [
  [4, 3, 4, 2],
  [4, 4, 4, 1],
  [4, 3, 4, 1],
  [4, 4, 4, 1],
  [4, 4, 4, 2],
]

function TerrainMatrix() {
  return (
    <div className="overflow-x-auto rounded-[1.8rem] bg-trench/70 ring-1 ring-foam/12">
      <table className="w-full min-w-[760px] border-collapse text-left">
        <caption className="ui px-6 pt-6 text-left text-[0.85rem] text-foam/65">Plus il y a de points pleins, plus le terrain est favorable à l’IA.</caption>
        <thead>
          <tr>
            {MATRIX_HEAD.map((h, i) => (
              <th key={h} scope="col" className={`ui px-5 py-4 text-[0.85rem] font-[750] ${i === 4 ? 'bg-coral/15 text-foam' : 'text-foam/70'}`}>
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {MATRIX.map((r, ri) => (
            <tr key={r[0]} className="border-t border-foam/10">
              <th scope="row" className="px-5 py-4 align-top text-[0.95rem] font-normal text-foam">
                {r[0]}
              </th>
              {r.slice(1).map((cell, ci) => {
                const s = SCORES[ri][ci]
                return (
                  <td key={ci} className={`px-5 py-4 align-top ${ci === 3 ? 'bg-coral/15' : ''}`}>
                    <span className="flex gap-1" role="img" aria-label={`${s} sur 4`}>
                      {[1, 2, 3, 4].map((d) => (
                        <span key={d} className={`h-2.5 w-2.5 rounded-full ${d <= s ? (ci === 3 ? 'bg-coral' : 'bg-lagoon') : 'bg-foam/15'}`} />
                      ))}
                    </span>
                    <span className="mt-2 block text-[0.9rem] leading-snug text-foam/80">{cell}</span>
                  </td>
                )
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function Limits() {
  return (
    <div className="space-y-24 sm:space-y-28">
      <Part title="L’IA prototype vite, la dernière marche est longue">
        <p className="max-w-[40ch] font-serif text-[clamp(1.4rem,2.3vw,1.9rem)] font-[340] leading-[1.35] text-foam">
          Un prototype jouable en un après-midi, c’est réel. Une vague qui nous plaise et une prise en main agréable, il a fallu trois semaines, six vagues et quatre modèles. Plus on s’approche du résultat voulu, plus chaque progrès coûte cher, parce que ce qui reste à corriger est affaire de goût et de sensation.
        </p>
      </Part>
      <Part title="Elle ne voit pas et elle ne sent pas">
        <P>
          Une capture d’écran permet à l’agent de voir une forme, pas de juger si une vague est belle ou si une manette répond bien. Tout ce qui relève du ressenti est passé par nous : « un mur avec un parasol », « la caméra est dans le tube », « je recule même stick en avant ». Sans humain qui joue, l’agent optimise des chiffres qui ne disent pas si c’est amusant.
        </P>
      </Part>
      <Part title="Itérer trop longtemps fait tourner en rond">
        <P>
          Sur la branche wave-v3, chaque correction ajoutait un réglage ou une exception : 90 paramètres sur le rider, trois HUD superposés, un panneau de 45 curseurs. Le code n’était ni simple ni générique, et chaque nouvelle demande devenait plus risquée. Deux fois, la meilleure décision a été de <B>repartir d’une base saine</B>, en gardant les idées et pas le code. Deux agents travaillant dans le même dossier ont même mélangé leurs changements dans un commit (« j’ai lancé <C>git add -A</C> sans regarder les fichiers de la vague ») : l’erreur est documentée dans le message lui-même.
        </P>
      </Part>
      <Part title="Unity est un terrain difficile pour une IA">
        <TerrainMatrix />
        <P>
          Ce site en est la démonstration : la vague 3D de sa page d’accueil est un portage en WebGL du profil du jeu, écrit et réglé en une soirée. Le même travail sous Unity nous a pris trois semaines. Ce n’est pas que la vague web soit plus simple ; c’est que tout, autour, aide l’IA : un rechargement instantané, du code qui est toute la vérité, et des technologies qu’elle a vues des millions de fois.
        </P>
      </Part>
      <Part title="Le modèle compte, mais pas seul">
        <P>
          Nous n’étions pas arrivés à un résultat satisfaisant avant Opus 5.5. Mais la journée du 28 septembre combine trois changements à la fois : un meilleur modèle, un redémarrage depuis une base saine, et des outils de test enfin rapides (photos sans Play mode, graphiques, simulation). Git ne permet pas de séparer leur part. Notre conviction : sans Opus 5.5, pas de journée du 28 ; sans les deux premières semaines d’erreurs, pas de journée du 28 non plus.
        </P>
      </Part>
    </div>
  )
}

/* ============================================================== 14 La morale (in the foam) */

const Ci = ({ children }: { children: ReactNode }) => <code className="rounded-md bg-ink/[0.07] px-1.5 py-0.5 font-mono text-[0.84em] text-wall">{children}</code>
const Bi = ({ children }: { children: ReactNode }) => <strong className="font-[650] text-ink">{children}</strong>

const LESSONS: [ReactNode, ReactNode][] = [
  ['Planifier avec le meilleur modèle, en effort maximal, avant d’écrire du code.', 'Une spec autosuffisante permet de repartir d’un contexte vierge.'],
  [<>Écrire les règles une fois pour toutes</>, <>(un <Ci>CLAUDE.md</Ci> court) plutôt que de les répéter.</>],
  ['Donner des yeux à l’IA, puis un simulateur.', 'Captures, graphiques, photos sans lancer le jeu, tests qui jouent à notre place : chaque outil a divisé le temps de réaction.'],
  ['Viser avec des images, pas avec des adjectifs.', 'Deux photos de Teahupoo ont fait plus que cent messages.'],
  ['Oser repartir de zéro,', 'avec un commit de sauvegarde, dès que chaque correction en appelle deux autres.'],
  ['Nettoyer régulièrement :', 'l’IA accumule, elle ne range pas d’elle-même.'],
]

function Foam() {
  const bubbles = Array.from({ length: 22 }, (_, i) => ({
    left: (i * 37) % 100,
    size: 6 + ((i * 13) % 26),
    delay: (i * 0.9) % 9,
    dur: 12 + ((i * 7) % 10),
  }))
  return (
    <div className="pointer-events-none absolute inset-0 overflow-hidden" aria-hidden="true">
      {bubbles.map((b, i) => (
        <span
          key={i}
          className="absolute bottom-0 rounded-full border border-lagoon/30 bg-white/40 opacity-0 will-change-transform motion-safe:animate-[bubble_linear_infinite]"
          style={{ left: `${b.left}%`, width: b.size, height: b.size, animationDuration: `${b.dur}s`, animationDelay: `${b.delay}s` }}
        />
      ))}
    </div>
  )
}

export function Moral() {
  return (
    <div className="relative">
      <Foam />
      <div className="relative space-y-20">
        <p className="max-w-[18ch] font-serif text-[clamp(2.4rem,5.6vw,5rem)] font-[330] leading-[1.04] tracking-[-0.015em] text-ink">Une IA ne remplace pas la compréhension, elle la rend plus urgente.</p>
        <div className="grid md:grid-cols-[minmax(0,3fr)_minmax(0,9fr)] md:gap-10">
          <div />
          <p className="max-w-[62ch] text-[1.18rem] leading-[1.72] text-ink/85">
            Nous avons fait un jeu dans une technologie que nous ne connaissions pas, et nous l’avons appris en chemin : en lisant les commits, en comprenant pourquoi une vague était invisible ou pourquoi un rider reculait. Les agents ont écrit le code ; les décisions, les références et le goût sont restés les nôtres.
          </p>
        </div>
        <div>
          <h3 className="display text-[clamp(1.6rem,2.6vw,2.2rem)] font-[780] text-ink">Ce que nous referions à l’identique :</h3>
          <ol className="mt-8 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {LESSONS.map(([a, b], i) => (
              <motion.li
                key={i}
                className="rounded-[1.6rem] bg-white/80 p-7 shadow-[0_24px_60px_-40px_rgba(6,38,64,0.5)] ring-1 ring-ink/10"
                initial={{ opacity: 0, y: 24 }}
                whileInView={{ opacity: 1, y: 0 }}
                viewport={{ once: true, margin: '-10% 0px' }}
                transition={{ duration: 0.6, delay: i * 0.07, ease: [0.22, 1, 0.36, 1] }}
              >
                <span className="display text-[3rem] font-[800] leading-none text-wall" style={{ fontVariationSettings: "'wdth' 125" }}>
                  {i + 1}
                </span>
                <p className="mt-4 text-[1.06rem] leading-relaxed text-ink/80">
                  <Bi>{a}</Bi> {b}
                </p>
              </motion.li>
            ))}
          </ol>
        </div>
        <div className="grid md:grid-cols-[minmax(0,3fr)_minmax(0,9fr)] md:gap-10">
          <div />
          <div className="space-y-8">
            <p className="max-w-[40ch] font-serif text-[clamp(1.4rem,2.3vw,1.9rem)] font-[360] leading-[1.35] text-ink">
              Ce que ce projet nous a appris sur l’IA en général : elle excelle là où le monde est fait de texte, de tests rapides et de technologies très répandues. Dès qu’on sort de ce terrain (un éditeur plein d’état, une sensation à juger, un domaine récent), elle reste d’une aide énorme, à condition qu’un humain tienne la barre.
            </p>
            <p className="display max-w-[30ch] text-[clamp(1.3rem,2vw,1.6rem)] font-[720] leading-snug text-wall">Pour la suite, il reste beaucoup à faire : un menu, l’anglais, le son réactivé, d’autres spots. Nous savons maintenant comment nous y prendre.</p>
          </div>
        </div>
      </div>
    </div>
  )
}

/* ============================================================== 15 Annexes */

const SOURCES: [ReactNode, string][] = [
  [<>Documentation Unity 6.4 à 6.6 (nouveautés, guide de migration, URP, Render Graph, GPU Resident Drawer, Job System, <Ci>Mesh.MeshData</Ci>)</>, 'docs.unity3d.com'],
  ['Input System 1.20 et DualSense', 'docs.unity3d.com/Packages/com.unity.inputsystem@1.20'],
  ['Storm Breakers, océan open source CC0', 'github.com/Stormrider31/Storm-Breakers'],
  ['Vagues de Gerstner', 'catlikecoding.com/unity/tutorials/flow/waves'],
  ['Anatomie d’un shader d’eau', 'cyanilux.com/tutorials/water-shader-breakdown'],
  ['Simuler des spots de surf avec la bathymétrie', 'jettelly.com'],
  ['Angle de déroulement d’une vague', 'scienceofsurfing.com'],
  [<>Scarfe, Elwany, Mead et Black (2003), <em>The Science of Surfing Waves and Surfing Breaks</em></>, 'escholarship.org/uc/item/6h72j1fz'],
  ['Figures de bodyboard et règles de jugement', 'surfertoday.com, Wikipédia (El Rollo, ARS)'],
  ['Ressources', 'polyhaven.com, kenney.nl, quaternius.com, opengameart.org'],
]
const DOCS: [string, string][] = [
  ['Docs/WAVYBOARD_SPEC.md', 'la spécification initiale (708 lignes)'],
  ['Docs/AGENT_PLAYBOOK.md', 'le guide de pilotage de l’éditeur par les agents'],
  ['Docs/RESEARCH_SOURCES.md', 'la veille, source par source'],
  ['Docs/ASSETS_MANIFEST.md', 'les ressources et leurs licences'],
  ['Docs/USER_ACTIONS.md', 'ce qui était réservé aux humains'],
  ['CLAUDE.md', 'les règles lues par chaque agent'],
]

export function Annex() {
  return (
    <div className="grid gap-16 lg:grid-cols-[minmax(0,7fr)_minmax(0,5fr)]">
      <section>
        <h3 className="display text-[1.6rem] font-[780] text-ink">Sources principales de la veille</h3>
        <ul className="mt-6 divide-y divide-ink/10 border-y border-ink/10">
          {SOURCES.map(([what, where], i) => (
            <li key={i} className="grid gap-1 py-4 sm:grid-cols-[1fr_auto] sm:gap-6">
              <span className="text-[1rem] leading-snug text-ink/85">{what}</span>
              <span className="font-mono text-[0.78rem] text-wall sm:text-right">{where}</span>
            </li>
          ))}
        </ul>
      </section>
      <div className="space-y-14">
        <section>
          <h3 className="display text-[1.6rem] font-[780] text-ink">Documents du projet</h3>
          <ul className="mt-6 space-y-3">
            {DOCS.map(([f, what]) => (
              <li key={f} className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
                <Ci>{f}</Ci>
                <span className="text-[0.98rem] text-ink/75">{what}</span>
              </li>
            ))}
          </ul>
        </section>
        <section>
          <h3 className="display text-[1.6rem] font-[780] text-ink">Crédits</h3>
          <p className="mt-6 text-[1rem] leading-relaxed text-ink/80">
            Mannequin : Quaternius (CC0). Ciel : Poly Haven, <em>spiaggia di mondello</em> (CC0). Particule d’écume : Kenney (CC0). Sons de vagues : OpenGameArt (CC0). Son du vent : Storm Breakers (CC0). Tout le reste (vague, océan, shader, rider, caméra, interface, planche) a été écrit pour le jeu.
          </p>
        </section>
      </div>
    </div>
  )
}
