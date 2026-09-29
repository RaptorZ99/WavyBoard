import { useEffect, useRef, useState, type ReactNode } from 'react'
import { AnimatePresence, motion, useInView, useReducedMotion } from 'motion/react'
import { B, Body, C, Callout, Lead, P, Part, Pull } from '../kit'
import { Icon } from '../icons'
import { MediaSlot } from '../../ui/MediaSlot'
import { ProfileSvg } from '../../wave/ProfileSvg'
import { TAU_MAX, TAU_MIN, stageAt } from '../../../lib/profileGeometry'
import { ROLES, STAGES } from '../../../lib/waveProfile'
import { asset } from '../../../lib/asset'

/* ============================================================== 07 Comment l'IA pilote Unity */

interface Line {
  cmd?: string
  note?: string
  comment?: string
  out?: string
  eyes?: boolean
}
// the commands of the report; outputs are what the Editor answered when this site was made
const SCRIPT: Line[] = [
  { cmd: 'unity status', note: "l'éditeur est-il prêt ?", out: 'state: ready  6000.6.0f1' },
  { cmd: 'unity command recompile', note: 'après chaque changement de script' },
  { cmd: 'unity command recompile_status', note: 'attendre la fin de la compilation', out: 'completed failed=False' },
  { cmd: 'unity command get_console_logs', note: 'zéro erreur avant de continuer', out: 'total: 0' },
  { comment: "exécuter du C# dans l'éditeur : ici, photographier la vague sans lancer le jeu" },
  { cmd: `unity command eval --code 'return WavyBoard.EditorTools.WaveLab.Shot("tube", 40f, 1.2f);'`, out: 'tube: tw=22.2 peelS=40.0 H=4.1 c=7.5' },
  { cmd: 'unity command capture_game_view --save_path Assets/Screenshots~/lab/v6_tube.png', out: 'saved', eyes: true },
  { cmd: 'unity command run_tests --mode EditMode', note: '22 tests, quelques secondes', out: 'Total 49  Passed 49  Failed 0' },
]

function Terminal() {
  const ref = useRef<HTMLDivElement>(null)
  const inView = useInView(ref, { once: true, margin: '-20% 0px' })
  const reduce = useReducedMotion()
  const [line, setLine] = useState(reduce ? SCRIPT.length : 0)
  const [chars, setChars] = useState(0)
  useEffect(() => {
    if (!inView || reduce || line >= SCRIPT.length) return
    const l = SCRIPT[line]
    const full = (l.cmd ?? l.comment ?? '').length
    if (chars < full) {
      const t = window.setTimeout(() => setChars((c) => Math.min(full, c + 3)), 16)
      return () => window.clearTimeout(t)
    }
    const t = window.setTimeout(() => {
      setLine((n) => n + 1)
      setChars(0)
    }, l.out ? 520 : 220)
    return () => window.clearTimeout(t)
  }, [inView, reduce, line, chars])
  const seen = SCRIPT.slice(0, Math.min(line + 1, SCRIPT.length))
  const eyes = SCRIPT.findIndex((l) => l.eyes) < line
  return (
    <div ref={ref} className="grid gap-6 lg:grid-cols-[minmax(0,8fr)_minmax(0,4fr)]">
      <figure className="overflow-hidden rounded-[1.6rem] bg-[#02101f] shadow-[0_40px_100px_-40px_rgba(0,0,0,0.8)] ring-1 ring-foam/12">
        <div className="flex items-center gap-2 border-b border-foam/10 px-5 py-3">
          <span className="h-3 w-3 rounded-full bg-[#ff5f57]" />
          <span className="h-3 w-3 rounded-full bg-[#febc2e]" />
          <span className="h-3 w-3 rounded-full bg-[#28c840]" />
          <span className="ml-3 font-mono text-[0.75rem] text-foam/65">zsh, WavyBoard</span>
        </div>
        <pre className="min-h-[430px] overflow-x-auto p-5 font-mono text-[0.8rem] leading-[1.75] sm:p-7 sm:text-[0.84rem]" aria-label="Commandes du CLI Unity">
          {seen.map((l, i) => {
            const typing = i === line
            const text = l.cmd ?? l.comment ?? ''
            const shown = typing ? text.slice(0, chars) : text
            return (
              <div key={i}>
                {l.comment ? (
                  <span className="text-foam/65"># {shown}</span>
                ) : (
                  <>
                    <span className="text-lagoon">$ </span>
                    <span className="text-foam">{shown}</span>
                    {!typing && l.note && <span className="text-foam/40">{'  '}# {l.note}</span>}
                  </>
                )}
                {typing && <span className="inline-block h-[1.05em] w-[0.55em] translate-y-[0.18em] animate-pulse bg-lagoon" />}
                {!typing && l.out && <div className="pl-4 text-sun/90">→ {l.out}</div>}
              </div>
            )
          })}
        </pre>
      </figure>
      <div className="flex flex-col gap-4">
        <div className="relative aspect-video overflow-hidden rounded-[1.4rem] bg-trench ring-1 ring-foam/12">
          <AnimatePresence>
            {eyes && (
              <motion.img decoding="async"
                src={asset('/media/game/lab-tube.webp')}
                alt="La capture que l’agent relit : le tube photographié par WaveLab"
                className="absolute inset-0 h-full w-full object-cover"
                initial={{ opacity: 0, scale: 1.08, filter: 'blur(10px)' }}
                animate={{ opacity: 1, scale: 1, filter: 'blur(0px)' }}
                transition={{ duration: 0.9 }}
              />
            )}
          </AnimatePresence>
          {!eyes && (
            <div className="ui absolute inset-0 flex items-center justify-center text-[0.8rem] text-foam/65">
              <Icon name="eye" className="mr-2 h-5 w-5" /> en attente de capture
            </div>
          )}
        </div>
        <p className="ui text-[0.85rem] leading-snug text-foam/70">
          <span className="font-mono text-lagoon">capture_game_view</span> : ce que voit la caméra, relu par l’agent.
        </p>
      </div>
    </div>
  )
}

const THREE: { name: string; text: ReactNode }[] = [
  { name: 'eval', text: <>exécute du C# arbitraire dans l’éditeur. C’est la colonne vertébrale de tous nos outils : placer une caméra, construire une vague à un instant précis, lancer une simulation.</> },
  { name: 'capture_game_view', text: <>enregistre ce que voit la caméra. L’agent relit ensuite l’image : ce sont ses yeux. Il en a pris <B>721</B> en trois semaines.</> },
  { name: 'run_tests', text: <>lance les tests automatiques et renvoie le résultat.</> },
]

const PITFALLS: ReactNode[] = [
  <>le code passé à <C>eval</C> est un corps de méthode : pas de <C>using</C>, types entièrement qualifiés ;</>,
  <>une opération de plus de 5 secondes sur le fil principal expire côté CLI mais continue dans l’éditeur : il faut interroger l’état et relancer sans casse ;</>,
  <>une erreur de compilation fait démarrer l’éditeur en « Safe Mode », où le CLI ne répond plus ;</>,
  <>quand l’éditeur n’a pas le focus, Unity arrête de faire tourner le jeu : il a fallu écrire un « ticker » qui fait avancer les images à la main ;</>,
  <>un job Burst qui lit un tableau statique géré échoue silencieusement et tourne 25 fois plus lentement ;</>,
  <>certaines valeurs écrites par le jeu en cours d’exécution atterrissent dans les fichiers de matériaux, donc dans git (six « stashes » de bruit en témoignent).</>,
]

export function Cli() {
  return (
    <div className="space-y-24 sm:space-y-28">
      <Body>
        <Lead className="max-w-[36ch]">L’agent ne clique jamais dans l’éditeur. Il envoie des commandes à l’éditeur ouvert, via le CLI officiel, et lit les réponses.</Lead>
      </Body>
      <Terminal />
      <Part title="Trois commandes ont tout changé">
        <ul className="grid gap-4 md:grid-cols-3">
          {THREE.map((c) => (
            <li key={c.name} className="rounded-[1.5rem] bg-foam/[0.05] p-6 ring-1 ring-foam/10">
              <p className="font-mono text-[1.15rem] font-[600] text-lagoon">{c.name}</p>
              <p className="mt-3 text-[1rem] leading-relaxed text-foam/78">{c.text}</p>
            </li>
          ))}
        </ul>
      </Part>
      <Body>
        <P>L’agent a appris le CLI en lisant le skill officiel publié par Unity (414 lignes et 8 fichiers de référence), puis a consigné ses propres découvertes dans ses notes :</P>
        <ol className="grid gap-3 md:grid-cols-2">
          {PITFALLS.map((p, i) => (
            <li key={i} className="flex gap-4 rounded-[1.3rem] bg-[#ff7a5c]/[0.07] p-5 ring-1 ring-[#ff7a5c]/25">
              <span className="mt-0.5 text-[#ff9f86]">
                <Icon name="buoy" className="h-6 w-6" />
              </span>
              <p className="text-[0.99rem] leading-snug text-foam/80">{p}</p>
            </li>
          ))}
        </ol>
        <p className="display text-[clamp(1.6rem,3vw,2.4rem)] font-[760] leading-tight text-foam">Aucun de ces pièges n’apparaît dans un tutoriel. Chacun a coûté des heures.</p>
      </Body>
      <Callout>
        <div className="grid items-center gap-8 md:grid-cols-[minmax(0,7fr)_minmax(0,5fr)]">
          <p className="text-[1.08rem] leading-relaxed text-foam/85">
            Les images et les vidéos de ce site ont été tournées de la même façon, sans que personne ne touche à l’éditeur : les plans fixes avec <C>WaveLab</C>, les vidéos en laissant le bot rider pendant qu’un petit composant (<C>FrameRecorder</C>) enregistre chaque image à pas de temps fixe, assemblées ensuite avec ffmpeg.
          </p>
          <MediaSlot kind="video" src={asset('/media/game/tube.mp4')} poster={asset('/media/game/tube-poster.webp')} alt="Une ride dans le tube, enregistrée par FrameRecorder" rounded="rounded-2xl" className="ring-1 ring-foam/15" />
        </div>
      </Callout>
    </div>
  )
}

/* ============================================================== 08 Notre boucle de travail */

const LOOP = [
  ['Veille et spec', 'un modèle en effort maximal produit la source de vérité.'],
  ['Implémentation', 'un agent écrit le C# et les shaders, recompile, lit la console.'],
  ['Vérification automatique', 'tests, simulation, graphiques, photos de la vague.'],
  ['Retour humain', 'nous jouons, nous comparons aux photos de référence, nous décrivons ce qui cloche.'],
  ['Itération', 'l’agent corrige, ou nous décidons de repartir d’une base saine.'],
  ['Nettoyage', 'on retire tout ce que le jeu n’utilise plus.'],
] as const

function LoopDiagram() {
  const [active, setActive] = useState(0)
  const reduce = useReducedMotion()
  const box = useRef<HTMLDivElement>(null)
  const inView = useInView(box, { margin: '-15% 0px' })
  useEffect(() => {
    // the loop only turns while it is on screen
    if (reduce || !inView) return
    const t = window.setInterval(() => setActive((a) => (a + 1) % LOOP.length), 2600)
    return () => window.clearInterval(t)
  }, [reduce, inView])
  const R = 150
  const pos = (i: number) => {
    const a = -Math.PI / 2 + (i / LOOP.length) * Math.PI * 2
    return [200 + Math.cos(a) * R, 200 + Math.sin(a) * R] as const
  }
  return (
    <div ref={box} className="grid items-center gap-10 lg:grid-cols-[minmax(0,5fr)_minmax(0,7fr)]">
      <svg viewBox="0 0 400 400" className="mx-auto w-full max-w-[440px]" role="img" aria-label="La boucle de travail en six étapes">
        <defs>
          <radialGradient id="loop-core" cx="50%" cy="50%" r="50%">
            <stop offset="0" stopColor="#33cce6" stopOpacity="0.25" />
            <stop offset="1" stopColor="#33cce6" stopOpacity="0" />
          </radialGradient>
        </defs>
        <circle cx="200" cy="200" r="120" fill="url(#loop-core)" />
        <circle cx="200" cy="200" r={R} fill="none" stroke="#f0f7ff" strokeOpacity=".14" strokeWidth="2" />
        <motion.circle cx="200" cy="200" r={R} fill="none" stroke="#33cce6" strokeWidth="3" strokeLinecap="round" transform="rotate(-90 200 200)" animate={{ pathLength: (active + 1) / LOOP.length }} transition={{ duration: 0.8, ease: [0.22, 1, 0.36, 1] }} />
        <text x="200" y="194" textAnchor="middle" className="fill-foam font-display text-[26px] font-[800]">
          Itérer
        </text>
        <text x="200" y="220" textAnchor="middle" className="fill-foam/60 text-[12px]">
          jusqu’à ce que ce soit bon
        </text>
        {LOOP.map(([label], i) => {
          const [x, y] = pos(i)
          const on = i === active
          return (
            <g key={label} onMouseEnter={() => setActive(i)} className="cursor-pointer">
              <circle cx={x} cy={y} r={on ? 27 : 20} fill={on ? '#33cce6' : '#062a48'} stroke="#33cce6" strokeOpacity={on ? 1 : 0.45} strokeWidth="2" style={{ transition: 'all .4s' }} />
              <text x={x} y={y + 5} textAnchor="middle" className={`font-display text-[15px] font-[800] ${on ? 'fill-abyss' : 'fill-foam'}`}>
                {i + 1}
              </text>
            </g>
          )
        })}
      </svg>
      <ol className="space-y-2">
        {LOOP.map(([label, text], i) => (
          <li key={label}>
            <button type="button" onClick={() => setActive(i)} onMouseEnter={() => setActive(i)} className={`w-full rounded-2xl px-5 py-3.5 text-left transition-colors ${i === active ? 'bg-foam/[0.1] ring-1 ring-lagoon/50' : 'hover:bg-foam/[0.05]'}`}>
              <span className="ui block text-[1.02rem] font-[750] text-foam">
                {i + 1}. {label}
              </span>
              <span className="block text-[0.98rem] leading-snug text-foam/75">{text.charAt(0).toUpperCase() + text.slice(1)}</span>
            </button>
          </li>
        ))}
      </ol>
    </div>
  )
}

const TOOLS: [string, string, string][] = [
  ['Captures en Play mode', 'Lancer le jeu, capturer l’écran, relire l’image', 'Les yeux de l’agent, mais lents et dépendants de l’éditeur'],
  ['HeadlessPlayTicker', 'Fait avancer le jeu quand l’éditeur n’a pas le focus', 'Les tests tournent même quand nous utilisons l’ordinateur'],
  ['WaveLab', 'Construit la vague à un instant précis de sa vie et place la caméra sur un plan nommé (chenal, tube, embouchure, dos, vue aérienne, rider, mousse, line-up)', 'Photographier la vague sans lancer le jeu, toujours sous le même angle, pour comparer avant et après'],
  ['wave_profile_lab.py', 'Reproduit en Python le profil de la vague et trace ses graphiques', 'Concevoir la forme sur un graphique avant de la voir en 3D, et vérifier qu’elle ne se croise jamais elle-même'],
  ['RiderBot', 'Un bot qui joue avec exactement les mêmes commandes qu’un joueur, selon un plan : Pocket (tube), InAndOut, Exit, Cruise, Stall, Airs, Carve', 'Tester le gameplay comme un joueur, pas en trichant'],
  ['RideSim', 'Ride une vague entière dans l’éditeur, sans Play mode, au pas de la physique (50 Hz) : une ride de 40 s prend une fraction de seconde', 'Des tests de gameplay déterministes et quasi instantanés'],
  ['playtest.py + PlaytestRecorder', 'Lance le vrai jeu avec le bot aux commandes, capture l’écran et mesure : tubes, envols, réceptions, chutes, caméra dans l’eau', 'Vérifier ce que seule la vraie partie montre : la caméra, le HUD, la fluidité'],
]

const PROMISES = [
  'attrape la vague et la surfe',
  'sort d’un tube profond, avec ou sans pump',
  'pumper bat le simple trim',
  'caler fait entrer dans le tube',
  's’envole de la lèvre et se pose sur la face',
  'sort de la vague par l’épaule',
  'pivote serré et repart',
  'rentre et sort du tube encore et encore',
  'la caméra suit chaque virage sans clignoter',
]

export function Loop() {
  const reduce = useReducedMotion()
  return (
    <div className="space-y-24 sm:space-y-32">
      <Body>
        <p className="max-w-[38ch] font-serif text-[clamp(1.5rem,2.5vw,2.1rem)] font-[340] leading-[1.3] text-foam">
          Le plus gros enseignement du projet : <strong className="font-[600] text-lagoon">une IA avance aussi vite que sa boucle de retour.</strong> Au début, l’agent ne pouvait vérifier son travail qu’en lançant le jeu et en regardant une capture. À la fin, il faisait rider des vagues entières en une fraction de seconde, sans rien lancer.
        </p>
      </Body>
      <LoopDiagram />
      <Part title="Les outils sont apparus dans cet ordre, chacun raccourcissant la boucle">
        <div className="ui hidden grid-cols-[minmax(0,13rem)_1fr_1fr] gap-6 pl-16 pr-5 text-[0.8rem] text-foam/65 md:grid" aria-hidden="true">
          <span>Outil</span>
          <span>Ce qu’il fait</span>
          <span>Pourquoi c’est important</span>
        </div>
        <ol className="relative space-y-3 before:absolute before:bottom-6 before:left-[1.35rem] before:top-6 before:w-px before:bg-gradient-to-b before:from-lagoon before:to-lagoon/10">
          {TOOLS.map(([name, what, why], i) => (
            <motion.li
              key={i}
              className="relative grid gap-3 rounded-[1.3rem] bg-foam/[0.05] p-5 pl-16 ring-1 ring-foam/10 md:grid-cols-[minmax(0,13rem)_1fr_1fr] md:gap-6"
              initial={reduce ? false : { opacity: 0, x: -16 }}
              whileInView={{ opacity: 1, x: 0 }}
              viewport={{ once: true, margin: '-10% 0px' }}
              transition={{ duration: 0.5, delay: i * 0.05 }}
            >
              <span className="ui absolute left-3 top-5 flex h-7 w-7 items-center justify-center rounded-full bg-lagoon text-[0.75rem] font-[800] text-abyss">{i + 1}</span>
              <p className="text-[1rem] font-[600] text-foam">
                {i === 0
                  ? name
                  : name.split(' + ').map((n, j) => (
                      <span key={n}>
                        {j > 0 && ' + '}
                        <C>{n}</C>
                      </span>
                    ))}
              </p>
              <p className="text-[0.96rem] leading-snug text-foam/75">{what}</p>
              <p className="text-[0.96rem] leading-snug text-lagoon-soft">{why}</p>
            </motion.li>
          ))}
        </ol>
      </Part>
      <Part title="Les 22 tests finaux">
        <P>Les 22 tests finaux se lisent comme une liste de promesses faites au joueur :</P>
        <ul className="flex flex-wrap gap-2.5">
          {PROMISES.map((p, i) => (
            <motion.li
              key={p}
              className="ui flex items-center gap-2 rounded-full bg-[#28c840]/10 py-2 pl-2.5 pr-4 text-[0.92rem] text-foam ring-1 ring-[#28c840]/35"
              initial={reduce ? false : { opacity: 0, scale: 0.9 }}
              whileInView={{ opacity: 1, scale: 1 }}
              viewport={{ once: true }}
              transition={{ delay: i * 0.09, duration: 0.35 }}
            >
              <span className="flex h-5 w-5 items-center justify-center rounded-full bg-[#28c840] text-abyss">
                <Icon name="check" className="h-3.5 w-3.5" />
              </span>
              « {p} »
            </motion.li>
          ))}
        </ul>
        <P>La plupart sont vérifiés sur trois tailles de vague, soit 49 cas, tous au vert.</P>
        <Callout>
          <p className="text-[1.08rem] leading-relaxed text-foam/85">
            Une leçon au passage : <B>il faut tester le testeur.</B> Trois fois, l’agent a cru le jeu cassé alors que c’était son pilote automatique : il tenait le stick « comme une direction alors que c’est un cap », ou rapportait zéro figure parce que ses gestes étaient mal lus. Et un bot qui se crashe en boucle devant nous donne l’impression que le jeu est cassé, même quand les chiffres sont bons. Le bot final surfe proprement : zéro chute sur tous les plans et toutes les tailles.
          </p>
        </Callout>
      </Part>
    </div>
  )
}

/* ============================================================== 09 La vague */

const APPROACHES = [
  ['Storm Breakers + shader graph', '8 septembre', 'Océan open source, vague rendue avec une copie générée de son shader graph', 'Vague invisible (matrices jamais sauvegardées), faces à l’envers'],
  ['Trou dans l’océan', '8 septembre', 'Découpe en transparence de l’océan sous la vague', 'Bandes de sable, plaques grises, dos de la vague transparent'],
  ['« Phase 2 » adoucie', '8 septembre', 'Découpe progressive sur 3 m, tube rond à lèvre fermée', 'Tuiles d’océan oubliées, scène sauvegardée dans un état cassé'],
  ['Profile-loft (v3)', '9 septembre', 'Bibliothèque de profils à la Surf’s Up, 11 formes × 3 intensités, jobs Burst', 'Complexité : un rider à 90 réglages, trois HUD superposés, un panneau de 45 curseurs'],
  ['Vague « designée » (v4)', '10 et 11 septembre', 'Profil à 4 formes sur un axe d’ouverture', 'Jamais vraiment jouée ; verdict : « un mur avec un parasol au-dessus »'],
  ['Teahupoo (v5)', '28 septembre', 'Une courbe de 15 points de contrôle, 10 formes clés dans le temps, un seul shader pour toute l’eau', 'C’est la vague du jeu'],
] as const

function Quiver() {
  return (
    <ol className="-mx-4 flex snap-x snap-mandatory gap-4 overflow-x-auto px-4 pb-6 sm:-mx-8 sm:px-8 xl:mx-0 xl:grid xl:grid-cols-6 xl:overflow-visible xl:px-0">
      {APPROACHES.map(([name, date, tech, why], i) => {
        const final = i === APPROACHES.length - 1
        return (
          <motion.li
            key={name}
            className={`relative flex min-h-[27rem] w-[16rem] shrink-0 snap-start flex-col rounded-b-[1.6rem] rounded-t-[999px] px-6 pb-6 pt-16 xl:w-auto ${
              final ? 'bg-[linear-gradient(180deg,#33cce6,#0d6b94)] text-abyss shadow-[0_30px_80px_-20px_rgba(51,204,230,0.6)]' : 'bg-foam/[0.06] text-foam ring-1 ring-foam/12'
            }`}
            initial={{ opacity: 0, y: 30 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true, margin: '-10% 0px' }}
            transition={{ duration: 0.6, delay: i * 0.08, ease: [0.22, 1, 0.36, 1] }}
          >
            <span className={`absolute left-1/2 top-4 h-8 w-[3px] -translate-x-1/2 rounded-full ${final ? 'bg-abyss/40' : 'bg-foam/20'}`} aria-hidden="true" />
            <p className={`ui text-[0.78rem] font-[700] tabular-nums ${final ? 'text-abyss/70' : 'text-foam/65'}`}>
              {String(i + 1).padStart(2, '0')} · {date}
            </p>
            <p className="display mt-2 text-[1.45rem] font-[790] leading-tight">{name}</p>
            <p className={`mt-3 text-[0.94rem] leading-snug ${final ? 'text-abyss/85' : 'text-foam/70'}`}>{tech}</p>
            <p className={`mt-auto border-t pt-4 text-[0.94rem] font-[500] leading-snug ${final ? 'border-abyss/20' : 'border-foam/10 text-[#ff9f86]'}`}>{final ? why : `Quittée : ${why.charAt(0).toLowerCase()}${why.slice(1)}`}</p>
          </motion.li>
        )
      })}
    </ol>
  )
}

function ProfileLab() {
  const [tau, setTau] = useState(2.4)
  const [playing, setPlaying] = useState(false)
  useEffect(() => {
    if (!playing) return
    let last = performance.now()
    let raf = 0
    const tick = (now: number) => {
      const dt = (now - last) / 1000
      last = now
      setTau((t) => (t + dt * 1.4 > TAU_MAX ? TAU_MIN : t + dt * 1.4))
      raf = requestAnimationFrame(tick)
    }
    raf = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf)
  }, [playing])
  const stage = STAGES[stageAt(tau)]
  return (
    <div className="overflow-hidden rounded-[1.8rem] bg-trench/80 ring-1 ring-foam/12">
      <div className="px-2 pt-10 sm:px-8">
        <ProfileSvg tau={tau} labels="all" className="h-auto w-full overflow-visible" title={`Coupe de la vague : ${stage.name}`} />
      </div>
      <div className="grid gap-4 border-t border-foam/10 p-5 sm:grid-cols-[auto_1fr] sm:items-center sm:gap-6 sm:p-7">
        <button type="button" onClick={() => setPlaying((p) => !p)} aria-pressed={playing} className="ui inline-flex h-11 items-center justify-center rounded-full bg-lagoon px-6 text-[0.92rem] font-[750] text-abyss">
          {playing ? 'Pause' : 'Faire vivre la vague'}
        </button>
        <label className="block">
          <span className="ui mb-1 flex justify-between text-[0.82rem] text-foam/70">
            <span>
              <span className="font-[750] text-lagoon">{stage.name}</span> ({tau >= 0 ? '+' : ''}
              {tau.toFixed(1)} s depuis la casse)
            </span>
          </span>
          <input type="range" min={TAU_MIN} max={TAU_MAX} step={0.01} value={tau} onChange={(e) => { setPlaying(false); setTau(+e.target.value) }} className="w-full accent-[#33cce6]" aria-label="Temps depuis la casse" />
        </label>
      </div>
    </div>
  )
}

function LabFigure({ src, alt, w, h }: { src: string; alt: string; w: number; h: number }) {
  const [open, setOpen] = useState(false)
  return (
    <figure>
      <button type="button" onClick={() => setOpen(true)} className="block w-full overflow-hidden rounded-[1.3rem] bg-white p-3 ring-1 ring-foam/15 transition-transform duration-500 hover:-translate-y-1" aria-label={`Agrandir : ${alt}`}>
        <img decoding="async" src={src} alt={alt} width={w} height={h} loading="lazy" className="h-auto w-full" />
      </button>
      <figcaption className="ui mt-3 text-[0.85rem] text-foam/65">{alt}</figcaption>
      <AnimatePresence>
        {open && (
          <motion.div role="dialog" aria-modal="true" aria-label={alt} className="fixed inset-0 z-[90] flex items-center justify-center bg-trench/95 p-4 sm:p-10" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} onClick={() => setOpen(false)}>
            <motion.img decoding="async" src={src} alt={alt} className="max-h-[88vh] w-auto max-w-full rounded-xl bg-white p-3" initial={{ scale: 0.96 }} animate={{ scale: 1 }} />
          </motion.div>
        )}
      </AnimatePresence>
    </figure>
  )
}

export function Wave() {
  return (
    <div className="space-y-24 sm:space-y-32">
      <Body>
        <Pull>La spec le disait dès le premier jour : « la vague est le personnage principal ». C’est aussi ce qui nous a coûté le plus cher.</Pull>
      </Body>
      <div>
        <h3 className="display mb-8 text-[clamp(1.6rem,2.6vw,2.2rem)] font-[760] text-foam">Six approches pour une vague</h3>
        <Quiver />
      </div>
      <Part title="Comment fonctionne la vague finale">
        <P>
          La coupe de la vague est <B>une seule courbe continue</B> : l’eau plate devant, la face, le fond du tube, le plafond, la pointe de la lèvre, le dessus de la lèvre, la crête et le dos. C’est une spline Catmull-Rom centripète qui passe par 15 points de contrôle ({ROLES.join(', ')}).
        </P>
        <ul className="flex flex-wrap gap-2" aria-hidden="true">
          {ROLES.map((r) => (
            <li key={r} className="rounded-full bg-sun/10 px-3 py-1 font-mono text-[0.75rem] text-sun ring-1 ring-sun/30">
              {r}
            </li>
          ))}
        </ul>
        <P>
          Ces 15 points bougent au fil de la vie de la vague, entre 10 formes clés dessinées à la main d’après les photos : houle, face raide, lèvre qui pitche, lèvre lancée, tube, fin du tube, impact, dôme de mousse, barre, retour au plat. Toutes les unités sont en « hauteurs de vague » : la même table sert pour une vague de 2,3 m comme de 4,9 m.
        </P>
        <p className="max-w-[40ch] font-serif text-[1.45rem] leading-snug text-foam">
          Le secret du déroulement est simple : <strong className="font-[600] text-lagoon">chaque point de la crête vit la même vie, mais décalée dans le temps.</strong> Le point qui casse maintenant est suivi, un peu plus loin, par un point qui cassera dans une seconde. Ce décalage fait courir le rouleau le long de la vague.
        </p>
      </Part>
      <ProfileLab />
      <LabFigure w={1650} h={660} src={asset('/media/lab/stages.webp')} alt="Les coupes de la vague au fil du temps, tracées par wave_profile_lab.py" />
      <Part title="Des graphiques avant la 3D">
        <P>
          Avant de toucher au jeu, l’agent a écrit un double de la vague en Python (<C>Tools/wave_profile_lab.py</C>) qui trace :
        </P>
        <ul className="space-y-2.5">
          {[
            'les coupes successives d’un même point de crête, colorées par le temps ;',
            'les 10 formes clés avec leurs 15 points annotés ;',
            'le tube à l’échelle, avec un bodyboarder allongé et un rider en drop-knee pour vérifier qu’on y tient ;',
            'les quatre tailles de vague, avant et après l’agrandissement du tube ;',
            'le déroulement en 3D le long de la crête.',
          ].map((t) => (
            <li key={t} className="flex gap-3 text-[1.06rem] leading-snug text-foam/80">
              <span className="mt-2.5 h-1.5 w-1.5 shrink-0 rounded-full bg-lagoon" />
              {t}
            </li>
          ))}
        </ul>
        <P>Le script vérifie aussi, sur 90 instants, que la courbe ne se croise jamais elle-même. Le code C# du jeu est contrôlé contre les chiffres du script : les deux doivent rester identiques.</P>
        <div className="grid gap-6 lg:grid-cols-2">
          <LabFigure w={1500} h={1200} src={asset('/media/lab/keys.webp')} alt="Les dix formes clés et leurs points de contrôle" />
          <LabFigure w={1800} h={720} src={asset('/media/lab/tube_sizes.webp')} alt="Le tube de chaque taille de vague, avant et après agrandissement" />
        </div>
      </Part>
      <Part title="Des photos, pas des chiffres">
        <P>
          La différence entre la v4 et la v5 tient moins au code qu’à la méthode. La v4 était réglée par des chiffres dans les messages de commit et n’a jamais été vraiment jouée. La v5 a été dessinée <B>contre deux photos de Teahupoo</B>, avec un graphique vérifiable et des photos <C>WaveLab</C> prises sous les mêmes angles à chaque itération. Une cible visuelle précise transforme un avis (« c’est moche ») en écart mesurable.
        </P>
      </Part>
    </div>
  )
}
