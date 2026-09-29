import { useRef, useState, type ReactNode } from 'react'
import { motion, useInView, useReducedMotion } from 'motion/react'
import { B, Body, C, Callout, I, Lead, P, Part, Pull } from '../kit'
import { Icon, type IconName } from '../icons'
import { MODEL_COLORS, modelKey } from '../modelColors'

/* ============================================================== 04 IAgraphie */

const MODELS = [
  ['Claude Fable 5.1', 'Max', '8 et 9 septembre', 'Veille sur le web, spécification et plan, premier prototype jouable, début du run de nuit, tricks Skate v1'],
  ['Claude Opus 5 (1M de contexte)', 'Élevé', '8 au 11 septembre', 'Import des planches DGZ, reconstruction v4, système de figures flick-it'],
  ['Claude Opus 4.8', 'Élevé', 'nuit et matinée du 9 septembre', 'Relais pendant le run de nuit : tests de la vague, HUD, caméra'],
  ['Claude Opus 5.5', 'Élevé', '28 et 29 septembre', 'Redémarrage, vague Teahupoo, gameplay et caméra, grand ménage, simulation de ride, site et rapport'],
  ['Claude Sonnet 5.5', 'Standard', '29 septembre', 'Agents d’exploration (historique git, code, captures) pour préparer ce rapport'],
] as const

const DAYS = 22
const at = (d: number, h = 0, m = 0) => (d - 8 + (h + m / 60) / 24) / DAYS
const SPANS: Record<string, [number, number, string][]> = {
  'Fable 5.1': [
    [at(8, 8), at(9, 2, 19), 'veille, spec, prototype, début du run de nuit'],
    [at(9, 11, 16), at(9, 13, 41), 'figures Skate v1, panneau de réglage'],
  ],
  'Opus 5': [
    [at(8, 15, 21), at(8, 15, 39), 'planches DGZ'],
    [at(9, 15, 24), at(11, 16, 31), 'reconstruction v4'],
  ],
  'Opus 4.8': [[at(9, 2, 27), at(9, 10, 30), 'relais du run de nuit, HUD']],
  'Opus 5.5': [
    [at(28, 9, 30), at(29, 0, 38), 'vague Teahupoo, gameplay, caméra, ménage'],
    [at(29, 9), at(29, 23), 'site et rapport'],
  ],
  'Sonnet 5.5': [[at(29, 9), at(29, 12), 'exploration pour le rapport']],
}
const TICKS = [8, 11, 14, 17, 20, 23, 26, 29]

function ModelLanes() {
  const [hover, setHover] = useState<string | null>(null)
  const ref = useRef<HTMLDivElement>(null)
  const inView = useInView(ref, { once: true, margin: '-15% 0px' })
  const reduce = useReducedMotion()
  return (
    <div ref={ref} className="rounded-[1.8rem] bg-trench/70 p-5 ring-1 ring-foam/10 sm:p-9">
      <div className="relative">
        <div className="absolute inset-y-0 rounded-lg bg-[repeating-linear-gradient(135deg,rgba(240,247,255,0.035)_0_8px,transparent_8px_16px)]" style={{ left: `calc(8rem + (100% - 8rem) * ${at(12)})`, width: `calc((100% - 8rem) * ${at(28) - at(12)})` }} aria-hidden="true" />
        <ul className="relative space-y-3.5">
          {MODELS.map(([name]) => {
            const k = modelKey(name)
            return (
              <li key={name} className="grid grid-cols-[7rem_1fr] items-center gap-4 sm:grid-cols-[8rem_1fr]">
                <span className="ui flex items-center gap-2 text-[0.85rem] font-[700] text-foam">
                  <span className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ background: MODEL_COLORS[k] }} />
                  {k}
                </span>
                <span className="relative h-8 rounded-full bg-foam/[0.05]">
                  {(SPANS[k] ?? []).map(([a, b, what], j) => (
                    <motion.span
                      key={what}
                      tabIndex={0}
                      onMouseEnter={() => setHover(`${k} : ${what}`)}
                      onMouseLeave={() => setHover(null)}
                      onFocus={() => setHover(`${k} : ${what}`)}
                      onBlur={() => setHover(null)}
                      aria-label={`${k} : ${what}`}
                      className="absolute top-1.5 h-5 origin-left rounded-[4px] outline-none focus-visible:ring-2 focus-visible:ring-foam"
                      style={{ left: `${a * 100}%`, width: `max(8px, ${(b - a) * 100}%)`, background: MODEL_COLORS[k], boxShadow: `0 0 18px ${MODEL_COLORS[k]}66` }}
                      initial={reduce ? false : { scaleX: 0 }}
                      animate={inView ? { scaleX: 1 } : undefined}
                      transition={{ duration: 0.9, delay: 0.15 + j * 0.2, ease: [0.22, 1, 0.36, 1] }}
                    />
                  ))}
                </span>
              </li>
            )
          })}
        </ul>
        <div className="relative ml-[8rem] mt-4 h-5 sm:ml-[9rem]" aria-hidden="true">
          {TICKS.map((d) => (
            <span key={d} className="ui absolute -translate-x-1/2 text-[0.72rem] tabular-nums text-foam/65" style={{ left: `${at(d) * 100}%` }}>
              {d} sept.
            </span>
          ))}
        </div>
      </div>
      <p className="ui mt-4 min-h-5 text-[0.85rem] text-foam/75" aria-live="polite">
        {hover ?? 'Survolez une barre. Les hachures marquent 17 jours sans commit.'}
      </p>
    </div>
  )
}

export function Models() {
  return (
    <div className="space-y-16">
      <Body>
        <Lead className="max-w-[36ch]">Chaque commit porte le nom du modèle qui l’a écrit. Voici ce que racontent ces signatures.</Lead>
      </Body>
      <ModelLanes />
      <ul className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
        {MODELS.map(([name, effort, period, role]) => {
          const k = modelKey(name)
          return (
            <li key={name} className="relative overflow-hidden rounded-[1.5rem] bg-foam/[0.05] p-6 ring-1 ring-foam/10">
              <span className="absolute inset-x-0 top-0 h-1" style={{ background: MODEL_COLORS[k] }} />
              <p className="display text-[1.35rem] font-[780] leading-tight text-foam">{name.replace(' (1M de contexte)', '')}</p>
              {name.includes('1M') && <p className="ui text-[0.78rem] text-foam/70">1M de contexte</p>}
              <p className="ui mt-3 flex flex-wrap gap-2 text-[0.75rem]">
                <span className="rounded-full bg-foam/10 px-2.5 py-1 text-foam/85">Effort : {effort}</span>
                <span className="rounded-full bg-foam/10 px-2.5 py-1 text-foam/85">{period}</span>
              </p>
              <p className="mt-4 text-[0.98rem] leading-snug text-foam/75">{role}</p>
            </li>
          )
        })}
      </ul>
      <Body>
        <P>
          Deux remarques. D’abord, la phase de veille et de planification a été confiée au modèle le plus lent et le plus réfléchi, en effort maximal : c’est là qu’une erreur coûte le plus cher. Ensuite, une fois le plan écrit, nous avons <B>compacté le contexte</B> et lancé un nouvel agent sur l’implémentation, avec la spec comme seule mémoire. C’est tout l’intérêt d’une spec autosuffisante.
        </P>
        <Callout className="!bg-[#c98500]/[0.12] !ring-[#c98500]/40">
          <p className="ui text-[0.85rem] font-[700] tabular-nums text-[#f3c35a]">28 sept., matin → 29 sept., 0 h 38</p>
          <p className="mt-3 font-serif text-[1.35rem] leading-snug text-foam">
            Le tournant est net : tout ce qui rend le jeu bon aujourd’hui (la vague, la sensation de vitesse, la caméra, le nettoyage) a été écrit en une journée avec Opus 5.5, entre le matin du 28 septembre et le 29 à 0 h 38. Nous y revenons dans les limites : le modèle n’explique pas tout, mais sans lui nous n’y serions pas arrivés.
          </p>
        </Callout>
      </Body>
    </div>
  )
}

/* ============================================================== 05 La veille */

const COVERED: { icon: IconName; title: string; text: ReactNode }[] = [
  { icon: 'editor', title: 'Unity 6.4 à 6.6', text: <>nouveautés, pièges de migration, pipeline URP, Render Graph, GPU Resident Drawer, Burst et <C>Mesh.MeshData</C>.</> },
  { icon: 'stick', title: 'Les manettes', text: <>Input System 1.20, DualSense.</> },
  { icon: 'render', title: 'Le rendu de l’eau', text: <>Storm Breakers (océan open source CC0), Crest, Boat Attack, océans FFT, tutoriels Catlike Coding (vagues de Gerstner) et Cyanilux, fils du forum Unity sur les vagues déferlantes, et la technique du film <I>Surf’s Up</I> (Sony Imageworks, SIGGRAPH 2007).</> },
  { icon: 'sets', title: 'La science du surf', text: <>angle de déroulement (peel angle), vitesse nécessaire au surfeur <I>Vs = c / sin α</I>, intensité de déferlement (« vortex ratio », Mead et Black 2001), revue de Scarfe et al. (2003).</> },
  { icon: 'envol', title: 'Le bodyboard', text: <>styles (allongé, drop-knee), vocabulaire des figures (El Rollo, ARS, invert, backflip), critères de jugement APB/IBC, jeux de référence (Barton Lynch Pro Surfing, YouRiding).</> },
  { icon: 'file', title: 'Les ressources gratuites', text: <>avec licence vérifiée : Storm Breakers, Poly Haven, Quaternius, Kenney et OpenGameArt (toutes CC0), polices sous licence OFL.</> },
]

const LESSONS: [ReactNode, ReactNode][] = [
  ['URP n’a pas de système d’eau, et aucun océan disponible ne sait faire un tube', 'La vague est entièrement faite maison ; l’océan autour aussi, à la fin'],
  ['Une seule fonction mathématique doit servir au rendu et à la physique', 'Le rider surfe exactement la surface qu’on voit, sans relire la carte graphique'],
  ['Une vague est surfable si son angle de déroulement dépasse environ 30°', 'Le rouleau déroule dans un seul sens, à une vitesse réglée section par section'],
  ['Unity 6.6 désactive le rechargement du domaine en Play mode', <>Toute variable statique doit être réinitialisée à la main (règle du <C>CLAUDE.md</C>)</>],
  ['Les vibrations de la DualSense ne passent que par USB', 'Documenté pour le joueur'],
  ['Les scans Poly Haven font 0,5 à 2 millions de triangles', 'Interdits tels quels dans une scène jouable'],
]

export function Research() {
  return (
    <div className="space-y-24 sm:space-y-32">
      <Part title="La méthode">
        <div className="grid items-start gap-10 lg:grid-cols-[minmax(0,7fr)_minmax(0,5fr)]">
          <div className="space-y-7">
            <P>
              Le premier agent (Fable 5.1, effort maximal) a mené la recherche seul sur Internet, à partir de notre prompt. Il a classé chaque fait en deux catégories : <B>vérifié</B> (lu sur une page officielle, avec l’URL) ou <B>observé</B> (constaté sur notre machine). Tout est consigné dans <C>Docs/RESEARCH_SOURCES.md</C>, avec des extraits texte des documents de référence pour que les agents suivants puissent les relire sans refaire la recherche.
            </P>
            <P>Le 9 septembre, une seconde recherche, menée cette fois par 28 agents en parallèle, a porté sur une seule question : comment faire un rouleau qui se referme, puisqu’aucun système d’océan du marché n’en est capable.</P>
          </div>
          <div className="space-y-4">
            <div className="flex gap-3">
              <span className="ui flex items-center gap-2 rounded-full bg-lagoon/15 px-4 py-2 text-[0.9rem] font-[700] text-lagoon ring-1 ring-lagoon/40">
                <Icon name="check" className="h-4 w-4" /> vérifié
              </span>
              <span className="ui flex items-center gap-2 rounded-full bg-sun/15 px-4 py-2 text-[0.9rem] font-[700] text-sun ring-1 ring-sun/40">
                <Icon name="eye" className="h-4 w-4" /> observé
              </span>
            </div>
            <div className="rounded-[1.4rem] bg-trench/60 p-6 ring-1 ring-foam/12">
              <div className="grid grid-cols-7 gap-1.5" aria-hidden="true">
                {Array.from({ length: 28 }, (_, i) => (
                  <motion.span
                    key={i}
                    className="aspect-square rounded-full bg-lagoon"
                    initial={{ opacity: 0.15, scale: 0.6 }}
                    whileInView={{ opacity: 1, scale: 1 }}
                    viewport={{ once: true }}
                    transition={{ delay: i * 0.03, duration: 0.4 }}
                  />
                ))}
              </div>
              <p className="display mt-5 text-[2.4rem] font-[800] leading-none text-foam">28 agents</p>
              <p className="ui mt-1 text-[0.88rem] text-foam/70">en parallèle, une seule question : le rouleau</p>
            </div>
          </div>
        </div>
      </Part>

      <Part title="Ce qui a été couvert">
        <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {COVERED.map((c) => (
            <li key={c.title} className="rounded-[1.4rem] bg-foam/[0.05] p-6 ring-1 ring-foam/10">
              <span className="text-lagoon">
                <Icon name={c.icon} className="h-7 w-7" />
              </span>
              <p className="mt-3 text-[1rem] leading-relaxed text-foam/78">
                <B>{c.title}</B> : {c.text}
              </p>
            </li>
          ))}
        </ul>
      </Part>

      <Part title="Ce que nous en avons tiré">
        <ol className="space-y-3">
          <li className="ui hidden grid-cols-[1fr_3.5rem_1fr] gap-3 px-6 text-[0.8rem] text-foam/65 md:grid">
            <span>Constat</span>
            <span />
            <span>Conséquence dans le jeu</span>
          </li>
          {LESSONS.map(([a, b], i) => (
            <li key={i} className="group grid items-center gap-3 rounded-[1.3rem] bg-foam/[0.05] p-5 ring-1 ring-foam/10 transition-colors duration-500 hover:bg-foam/[0.08] md:grid-cols-[1fr_3.5rem_1fr] md:p-6">
              <p className="text-[1.02rem] leading-snug text-foam/78">{a}</p>
              <span className="flex justify-center" aria-hidden="true">
                <span className="flex h-10 w-10 items-center justify-center rounded-full bg-lagoon/12 text-lagoon ring-1 ring-lagoon/35 transition-[transform,background-color] duration-500 [transition-timing-function:cubic-bezier(0.22,1,0.36,1)] group-hover:translate-x-1 group-hover:bg-lagoon/20 max-md:group-hover:translate-x-0 max-md:group-hover:translate-y-1">
                  {/* a little swell, then a straight run into a symmetric head */}
                  <svg viewBox="0 0 24 24" className="h-[22px] w-[22px] overflow-visible rotate-90 md:rotate-0">
                    <path d="M3 12c2-2.6 4-2.6 6 0s4 2.6 6 0H20.5M16 7.5 20.5 12 16 16.5" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                </span>
              </span>
              <p className="text-[1.02rem] font-[500] leading-snug text-foam">{b}</p>
            </li>
          ))}
        </ol>
      </Part>

      <Body>
        <Pull>Avec le recul, l’esprit de la spec a tenu, la lettre beaucoup moins.</Pull>
        <P>
          Une vague dessinée plutôt que simulée, une seule fonction pour la physique et le rendu, des maths testables sans lancer le jeu : tout cela est dans le jeu final. En revanche, Storm Breakers, Cinemachine et l’interface en UI Toolkit ont disparu, et le plan de 30 jours de travail s’est fait en deux grandes poussées. Une spec n’est pas une prophétie : c’est un point de départ commun.
        </P>
      </Body>
    </div>
  )
}

/* ============================================================== 06 Le prompt */

type Seg = string | { t: string; n: number }
const PROMPT: Seg[][] = [
  [{ t: 'Tu es un expert senior en création de jeux vidéo Unity. Nous utilisons Unity 6.6, et tu as accès au CLI Unity.', n: 1 }],
  [
    'Nous avons pour projet de faire un magnifique jeu de surf type bodyboard. Ce serait un jeu dans lequel nous aurions un personnage qui évoluerait dans un océan, avec des vagues de plusieurs intensités. ',
    { t: 'Le personnage serait par défaut en position bodyboard, pourrait se déplacer, et surfer en position allongé, aller dans les rouleaux, faire des tricks, etc. Nous avons pour but de pouvoir jouer soit en clavier souris, soit en manette de PS5.', n: 2 },
    ' Il faut être créatif et avoir un vrai gameplay attrayant et divertissant. ',
    { t: 'Le jeu doit être fluide, beau, avoir de belles textures, etc.', n: 3 },
  ],
  [{ t: 'J’aimerais que tu trouves par toi-même de belles assets Unity de bonne qualité, gratuites', n: 3 }, ', qu’on pourrait utiliser pour tous les assets qui seront nécessaires à ce jeu.'],
  [
    { t: 'Tu feras un gros travail de recherche en amont : documentation Unity 6.6 ; communauté autour d’Unity pour avoir des conseils sur la façon de coder notre système cible', n: 4 },
    ', si des gens se sont déjà cassé la tête à optimiser des choses, on pourra s’en inspirer ; assets magnifiques et gratuits pour notre jeu. Nous souhaitons vraiment avoir quelque chose de qualitatif et de fluide.',
  ],
  [{ t: '(suivait la liste complète des commandes du CLI Unity)', n: 5 }],
  [{ t: 'Tu as aussi accès au skill dédié dans ~/.claude/skills/unity-cli !', n: 5 }, ' Tu devras bien le consulter pour bien comprendre le fonctionnement, ne passe pas à côté d’informations essentielles !'],
  [
    { t: 'À l’issue de toutes tes recherches, tu vas préparer un plan d’implémentation complet, autosuffisant, qui sera la source de vérité pour notre projet.', n: 6 },
    ' L’implémentation sera entièrement déléguée aux agents IA, alors il faut que tu prépares la spec qui nous permettra de faire quelque chose de vraiment qualitatif.',
  ],
  [
    'Tu vas installer tout le nécessaire, récupérer les choses nécessaires (ou si tu ne peux pas du tout le faire, tu me guides sur quoi faire). ',
    { t: 'Tu prépares tout en amont avant de commencer. Et on implémentera tout ultérieurement.', n: 7 },
  ],
]

const NOTES: ReactNode[] = [
  <><B>Il donne un rôle</B> (« expert senior en création de jeux vidéo Unity ») et un contexte technique précis (version, accès au CLI).</>,
  <><B>Il décrit l’expérience voulue</B>, pas l’implémentation : allongé, rouleaux, tricks, manette PS5.</>,
  <><B>Il fixe une barre de qualité</B> (fluide, beau, gratuit mais qualitatif) et laisse l’IA choisir les moyens.</>,
  <><B>Il impose la recherche avant l’action</B>, avec des pistes concrètes : documentation officielle, communauté, ressources.</>,
  <><B>Il fournit les outils</B> : la liste des commandes et un skill officiel à lire en entier.</>,
  <><B>Il définit le livrable</B> : un plan autosuffisant, source de vérité, pensé pour être exécuté par d’autres agents.</>,
  <><B>Il sépare les phases</B> : « on implémentera tout ultérieurement ». Pas de code tant que le plan n’est pas prêt.</>,
]

function PromptWindow() {
  const [on, setOn] = useState<number | null>(null)
  const ref = useRef<HTMLDivElement>(null)
  const inView = useInView(ref, { once: true, margin: '-15% 0px' })
  const reduce = useReducedMotion()
  return (
    <div ref={ref} className="grid gap-8 lg:grid-cols-[minmax(0,7fr)_minmax(0,5fr)]">
      <figure className="overflow-hidden rounded-[1.8rem] bg-[#07182b] shadow-[0_40px_100px_-40px_rgba(0,0,0,0.8)] ring-1 ring-foam/12">
        <div className="flex items-center gap-2 border-b border-foam/10 px-5 py-3.5">
          <span className="h-3 w-3 rounded-full bg-[#ff5f57]" />
          <span className="h-3 w-3 rounded-full bg-[#febc2e]" />
          <span className="h-3 w-3 rounded-full bg-[#28c840]" />
          <span className="ui ml-3 text-[0.8rem] text-foam/70">Prompt envoyé à Claude Fable 5.1, effort maximal</span>
        </div>
        <div className="space-y-4 p-6 sm:p-8">
          {PROMPT.map((para, i) => (
            <motion.p
              key={i}
              className="font-serif text-[1.06rem] leading-[1.65] text-foam/80"
              initial={reduce ? false : { opacity: 0, y: 10 }}
              animate={inView ? { opacity: 1, y: 0 } : undefined}
              transition={{ duration: 0.6, delay: i * 0.18 }}
            >
              {para.map((s, j) =>
                typeof s === 'string' ? (
                  <span key={j}>{s}</span>
                ) : (
                  <mark
                    key={j}
                    onMouseEnter={() => setOn(s.n)}
                    onMouseLeave={() => setOn(null)}
                    className={`rounded-md bg-transparent px-0.5 text-foam transition-colors duration-300 [box-decoration-break:clone] ${
                      on === s.n ? 'bg-lagoon/30 ring-1 ring-lagoon' : on === null ? 'bg-lagoon/[0.1]' : ''
                    } ${s.t.startsWith('(') ? 'italic text-foam/70' : ''}`}
                  >
                    {s.t}
                    <sup className="ui ml-1 rounded-full bg-lagoon px-1.5 text-[0.62rem] font-[800] not-italic text-abyss">{s.n}</sup>
                  </mark>
                ),
              )}
            </motion.p>
          ))}
        </div>
      </figure>
      <div>
        <h3 className="display text-[1.6rem] font-[760] text-foam">Ce que ce prompt fait bien</h3>
        <ol className="mt-5 space-y-2">
          {NOTES.map((n, i) => (
            <li key={i}>
              <button
                type="button"
                onMouseEnter={() => setOn(i + 1)}
                onMouseLeave={() => setOn(null)}
                onFocus={() => setOn(i + 1)}
                onBlur={() => setOn(null)}
                className={`flex w-full gap-4 rounded-2xl px-4 py-3 text-left transition-colors ${on === i + 1 ? 'bg-foam/[0.1] ring-1 ring-lagoon/50' : 'hover:bg-foam/[0.05]'}`}
              >
                <span className="ui mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-lagoon text-[0.75rem] font-[800] text-abyss">{i + 1}</span>
                <span className="text-[0.98rem] leading-snug text-foam/80">{n}</span>
              </button>
            </li>
          ))}
        </ol>
      </div>
    </div>
  )
}

const FEEDBACK: ReactNode[] = [
  <>deux photos de Teahupoo envoyées comme cible, avec la consigne « une seule vague, un seul rouleau, une mer qui bouge un peu »</>,
  <>« je peux pas remonter la vague ni sauter en haut », « la caméra est bloquée à gauche », « la caméra est dans la texture du tube »</>,
  <>« deux vagues peuvent se chevaucher, je passe à travers la première »</>,
  <>« je suis aspiré vers le fond du rouleau : même le joystick vers la sortie, on recule »</>,
  <>« j’ai du mal à faire demi-tour dans les rouleaux, à rentrer et sortir des tubes »</>,
  <>et une contrainte ferme : <B>toutes les figures restent au stick droit</B>, comme dans Skate. Les figures sur boutons ont été refusées.</>,
]

function Bubble({ side, children, delay = 0 }: { side: 'us' | 'ai'; children: ReactNode; delay?: number }) {
  const reduce = useReducedMotion()
  return (
    <motion.div
      className={`flex ${side === 'us' ? 'justify-end' : 'justify-start'}`}
      initial={reduce ? false : { opacity: 0, y: 14, scale: 0.98 }}
      whileInView={{ opacity: 1, y: 0, scale: 1 }}
      viewport={{ once: true, margin: '-10% 0px' }}
      transition={{ duration: 0.5, delay }}
    >
      <div
        className={`max-w-[34rem] rounded-[1.4rem] px-5 py-3.5 text-[1.02rem] leading-snug shadow-[0_18px_40px_-24px_rgba(0,0,0,0.7)] ${
          side === 'us' ? 'rounded-br-md bg-foam text-abyss [&_strong]:text-wall' : 'rounded-bl-md bg-[#0b2b4a] text-foam ring-1 ring-lagoon/30'
        }`}
      >
        <span className={`ui mb-1 block text-[0.7rem] font-[700] ${side === 'us' ? 'text-wall' : 'text-lagoon'}`}>{side === 'us' ? 'Nous' : 'L’agent'}</span>
        {children}
      </div>
    </motion.div>
  )
}

export function Prompt() {
  return (
    <div className="space-y-24 sm:space-y-32">
      <Body>
        <Lead className="max-w-[40ch]">Voici le prompt qui a lancé le projet, tel que nous l’avons envoyé à Fable 5.1 :</Lead>
      </Body>
      <PromptWindow />
      <Part title="Comment nous avons itéré ensuite">
        <P>
          Les prompts suivants étaient courts et concrets. Ils décrivaient un <B>ressenti de joueur</B>, pas une solution technique, et s’appuyaient sur des images :
        </P>
        <div className="space-y-3 rounded-[1.8rem] bg-trench/50 p-5 ring-1 ring-foam/10 sm:p-8">
          {FEEDBACK.map((f, i) => (
            <Bubble key={i} side="us" delay={i * 0.05}>
              {f}
            </Bubble>
          ))}
          <Bubble side="ai">
            « l’ancienne physique plafonnait entre 4,5 et 7 m/s alors que le rouleau déroule entre 5 et 8 m/s ; un rider dans le tube était donc toujours rattrapé »
          </Bubble>
        </div>
        <P>
          L’agent traduisait ensuite ces phrases en quelque chose de mesurable. Par exemple, « aspiré vers le fond du rouleau » est devenu : « l’ancienne physique plafonnait entre 4,5 et 7 m/s alors que le rouleau déroule entre 5 et 8 m/s ; un rider dans le tube était donc toujours rattrapé ». La correction et le test automatique qui la vérifie ont suivi dans le même commit.
        </P>
      </Part>
    </div>
  )
}
