import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { AnimatePresence, motion, useInView, useReducedMotion, useScroll, useTransform } from 'motion/react'
import { B, Body, C, CountUp, I, P, Part } from '../kit'
import { MODEL_COLORS, modelKey } from '../modelColors'

/* ============================================================== 10 Le cheminement */

const EVENTS: [string, string, string, string, ReactNode][] = [
  ['8 sept., matin', '—', 'Fable 5.1', 'Veille et spec', 'Recherche sur le web, cinq documents dont une spec de 708 lignes et un plan en neuf phases'],
  ['8 sept., 14 h 29 à 16 h 52', 'main', 'Fable 5.1', 'Prototype jouable', '9 commits en 2 h 23 : vague, rider, caméra, score, effets, son. Le premier commit ajoute 6 360 fichiers'],
  ['8 sept., 15 h 21', 'feat/planches-dgz', 'Opus 5', 'Planches DGZ', 'Cinq coloris importés ; orientation et flottaison « mesurées sur la géométrie plutôt que réglées à l’œil »'],
  ['8 sept., 20 h 49', 'feature/test-louis', 'Fable 5.1', 'Vague « phase 2 »', 'Découpe adoucie de l’océan, tube rond à lèvre fermée, six premiers tests'],
  ['8 sept., 23 h 47 à 3 h 16', 'feat/wave-v3', 'Fable 5.1, puis Opus 4.8', 'Run autonome de nuit', <>13 commits pendant que nous dormions : refonte « profile-loft » inspirée de <I>Surf’s Up</I>, 11 tests sur 11 au vert</>],
  ['9 sept., 8 h 15 à 13 h 41', 'feat/wave-v3', 'Opus 4.8, puis Fable 5.1', 'UX, HUD, figures', 'HUD « Ligne d’eau », figures Skate v1, panneau de réglage de la vague (45 curseurs, puis 8)'],
  ['10 et 11 sept.', 'feat/clean-v4', 'Opus 5', 'Reconstruction v4', 'Repartir de main, vague « designée », figures flick-it, 42 tests. Verdict : « un mur avec un parasol »'],
  ['12 au 27 sept.', '—', '—', 'Pause', '17 jours sans commit'],
  ['28 sept., 14 h 58', 'feat/clean-v4', 'Opus 5.5', 'Sauvegarde', 'La v4 est mise de côté : « gameplay restarts from main »'],
  ['28 sept., 15 h 39', 'feat/main-tricks-centered', 'Opus 5.5', 'Redémarrage', 'Le gameplay de main, les figures de la v4 et un monde centré sur le rider'],
  ['28 sept., 19 h 48', 'feat/main-tricks-centered', 'Opus 5.5', 'Vague Teahupoo', 'Un seul shader d’eau, le profil à 15 points, 51 533 lignes supprimées'],
  ['28 sept., 20 h 04', 'raptor/main', 'Opus 5.5', 'Renommage', 'Biscotte devient WavyBoard'],
  ['28 sept., 21 h 31', 'feat/gameplay-camera-v6', 'Opus 5.5', 'Gameplay et caméra', 'Toute la face devient surfable, envol depuis la lèvre, caméra procédurale'],
  ['28 sept., 22 h 42', 'feat/gameplay-camera-v6', 'Opus 5.5', 'Grand ménage', '1 013 998 lignes supprimées, dix paquets retirés'],
  ['29 sept., 0 h 38', 'main', 'Opus 5.5', 'Vitesse « wave power »', 'Course contre le rouleau, demi-tours pivotés, simulation de ride, 22 tests'],
  ['29 sept.', 'Website', 'Opus 5.5 et Sonnet 5.5', 'Site et rapport', 'Ce document, et les images et vidéos du site tournées dans l’éditeur par le CLI'],
]

function EventCard({ e, wide }: { e: (typeof EVENTS)[number]; wide?: boolean }) {
  const [date, branch, model, step, what] = e
  const k = modelKey(model)
  const pause = step === 'Pause'
  if (pause) {
    return (
      <div className={`flex flex-col justify-center ${wide ? 'w-[34rem]' : ''} rounded-[1.5rem] border border-dashed border-foam/25 p-6`}>
        <p className="ui text-[0.8rem] text-foam/70">{date}</p>
        <p className="display mt-2 text-[2.4rem] font-[800] leading-none text-foam/80">17 jours</p>
        <p className="mt-2 font-serif text-[1.1rem] italic text-foam/65">sans commit : mer plate.</p>
      </div>
    )
  }
  return (
    <article className={`${wide ? 'w-[21rem]' : ''} relative rounded-[1.5rem] bg-[#082a4a] p-6 ring-1 ring-foam/12`}>
      <span className="absolute inset-x-6 top-0 h-[3px] rounded-b" style={{ background: MODEL_COLORS[k] ?? '#33cce6' }} />
      <p className="ui text-[0.8rem] tabular-nums text-foam/65">{date}</p>
      <h4 className="display mt-2 text-[1.45rem] font-[780] leading-tight text-foam">{step}</h4>
      <p className="mt-2 text-[0.98rem] leading-snug text-foam/78">{what}</p>
      <p className="ui mt-4 flex flex-wrap items-center gap-2 text-[0.75rem]">
        {model !== '—' && (
          <span className="flex items-center gap-1.5 font-[700] text-foam/90">
            <span className="h-2 w-2 rounded-full" style={{ background: MODEL_COLORS[k] ?? '#33cce6' }} />
            {model}
          </span>
        )}
        {branch !== '—' && <span className="rounded bg-foam/10 px-1.5 py-0.5 font-mono text-[0.7rem] text-lagoon-soft">{branch}</span>}
      </p>
    </article>
  )
}

/**
 * Vertical scroll drives the commits past horizontally; the pause is a long flat stretch of sea. The strip is full
 * bleed and its travel is measured from the last card, so the story always ends with the last card fully in view.
 */
function HorizontalTimeline() {
  const outer = useRef<HTMLDivElement>(null)
  const view = useRef<HTMLDivElement>(null)
  const track = useRef<HTMLOListElement>(null)
  const [dist, setDist] = useState(0)
  useLayoutEffect(() => {
    const measure = () => {
      const t = track.current
      const v = view.current
      const last = t?.lastElementChild as HTMLElement | null
      if (!t || !v || !last) return
      const lead = parseFloat(getComputedStyle(t).paddingLeft) || 0
      setDist(Math.max(0, Math.ceil(last.offsetLeft + last.offsetWidth + lead - v.clientWidth)))
    }
    measure()
    const ro = new ResizeObserver(measure)
    if (track.current) ro.observe(track.current)
    if (view.current) ro.observe(view.current)
    document.fonts?.ready.then(measure)
    return () => ro.disconnect()
  }, [])
  const { scrollYProgress } = useScroll({ target: outer, offset: ['start start', 'end end'] })
  const x = useTransform(scrollYProgress, [0, 1], [0, -dist])
  const lineW = useTransform(scrollYProgress, [0, 1], ['0%', '100%'])
  return (
    <div ref={outer} className="relative ml-[calc(50%-50vw)] hidden w-screen md:block" style={{ height: `calc(100svh + ${dist}px)` }}>
      <div ref={view} className="sticky top-0 flex h-svh flex-col justify-center overflow-hidden">
        <div className="relative">
          <svg className="absolute left-0 right-0 top-1/2 h-16 w-full -translate-y-1/2" preserveAspectRatio="none" viewBox="0 0 1000 60" aria-hidden="true">
            <path d="M0 30 C 60 10, 120 50, 180 30 S 300 10, 360 30 S 420 30, 700 30 S 820 8, 880 30 S 960 50, 1000 30" fill="none" stroke="#f0f7ff" strokeOpacity="0.14" strokeWidth="2" vectorEffect="non-scaling-stroke" />
          </svg>
          <motion.ol ref={track} style={{ x }} className="relative flex w-max items-center gap-6 pl-[max(2rem,calc((100vw-1400px)/2+2rem))] will-change-transform">
            {EVENTS.map((e, i) => (
              <li key={i} className={i % 2 ? 'translate-y-14' : '-translate-y-14'}>
                <EventCard e={e} wide />
              </li>
            ))}
          </motion.ol>
        </div>
        <div className="mx-auto mt-10 w-full max-w-[1400px] px-8">
          <div className="h-px w-full bg-foam/15">
            <motion.div className="h-px bg-lagoon" style={{ width: lineW }} />
          </div>
          <p className="ui mt-3 flex justify-between text-[0.75rem] text-foam/65">
            <span>8 septembre</span>
            <span>29 septembre</span>
          </p>
        </div>
      </div>
    </div>
  )
}

function BeforeAfter() {
  const [pos, setPos] = useState(50)
  const box = useRef<HTMLDivElement>(null)
  const drag = (clientX: number) => {
    const r = box.current?.getBoundingClientRect()
    if (r) setPos(Math.min(100, Math.max(0, ((clientX - r.left) / r.width) * 100)))
  }
  return (
    <figure>
      <div
        ref={box}
        className="relative aspect-video cursor-ew-resize select-none overflow-hidden rounded-[1.8rem] ring-1 ring-foam/15"
        onPointerDown={(e) => {
          ;(e.target as HTMLElement).setPointerCapture?.(e.pointerId)
          drag(e.clientX)
        }}
        onPointerMove={(e) => e.buttons === 1 && drag(e.clientX)}
      >
        <img decoding="async" src="/media/evolution/09-tube-parfait.webp" alt="28 septembre : le tube de la vague Teahupoo, vu de l’intérieur" className="absolute inset-0 h-full w-full object-cover" draggable={false} />
        <div className="absolute inset-0" style={{ clipPath: `inset(0 ${100 - pos}% 0 0)` }}>
          <img decoding="async" src="/media/evolution/03-premier-tube.webp" alt="8 septembre : premier tube lisible, vu de l’intérieur" className="h-full w-full object-cover" draggable={false} />
        </div>
        <span className="ui absolute left-4 top-4 rounded-full bg-trench/80 px-3 py-1.5 text-[0.8rem] text-foam">8 septembre</span>
        <span className="ui absolute right-4 top-4 rounded-full bg-lagoon px-3 py-1.5 text-[0.8rem] font-[700] text-abyss">28 septembre</span>
        <div className="absolute inset-y-0 w-[2px] bg-foam shadow-[0_0_20px_rgba(240,247,255,0.8)]" style={{ left: `${pos}%` }}>
          <button
            type="button"
            role="slider"
            aria-label="Comparer le premier tube et le tube final"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={Math.round(pos)}
            onKeyDown={(e) => {
              if (e.key === 'ArrowLeft') setPos((p) => Math.max(0, p - 5))
              if (e.key === 'ArrowRight') setPos((p) => Math.min(100, p + 5))
            }}
            className="absolute left-1/2 top-1/2 flex h-12 w-12 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full bg-foam text-abyss shadow-xl"
          >
            <svg viewBox="0 0 24 24" className="h-5 w-5" aria-hidden="true">
              <path d="M9 6l-6 6 6 6M15 6l6 6-6 6" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </button>
        </div>
      </div>
      <figcaption className="ui mt-3 text-[0.85rem] text-foam/65">Premier tube lisible (8 septembre) et tube de la vague Teahupoo (28 septembre). Faites glisser pour comparer.</figcaption>
    </figure>
  )
}

const EVOLUTION: [string, string, string, string][] = [
  ['01-premier-lancement', 'Premier lancement : l’océan Storm Breakers, et pas encore de vague', '8 septembre', 'Premier lancement : l’océan est là, la vague non.'],
  ['02-vague-transparente', 'La vague apparaît, transparente, avec des bandes de sable', '8 septembre', 'La vague apparaît, transparente, traversée de bandes de sable.'],
  ['03-premier-tube', 'Premier tube lisible', '8 septembre', 'Premier tube lisible, vu de l’intérieur.'],
  ['04-premier-rider', 'Premier rider sur la vague', '8 septembre', 'Le premier rider sur la face.'],
  ['05-patch-rectangulaire', 'Une vague en forme de rectangle posée sur la plage', '9 septembre', 'Wave-v3 : un rectangle d’eau posé sur le sable.'],
  ['06-v4-hud', 'La v4 avec son HUD', '11 septembre', 'V4 : gameplay, HUD et figures fonctionnent, la vague reste plate.'],
  ['07-v5-levre', 'La lèvre ronde de la v5', '28 septembre, matin', 'Opus 5.5 reprend la vague : la lèvre ronde apparaît.'],
  ['08-face-turquoise', 'Le rider sur une face turquoise', '28 septembre, fin de matinée', 'Le rider sur une face qui s’illumine.'],
  ['09-tube-parfait', 'Le tube parfait photographié par WaveLab', '28 septembre, après-midi', 'WaveLab : le tube de la vague Teahupoo, vu de l’intérieur.'],
  ['10-teahupoo', 'La vague Teahupoo vue du chenal', '28 septembre, après-midi', 'Le chenal : la silhouette de Teahupoo.'],
  ['11-jeu-final', 'Le jeu final, dans le tube', '29 septembre', 'Le jeu final : dans le tube, jauge de course contre le rouleau à l’écran.'],
]

function Filmstrip() {
  const [open, setOpen] = useState<number | null>(null)
  useEffect(() => {
    if (open === null) return
    const k = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(null)
      if (e.key === 'ArrowRight') setOpen((o) => (o === null ? o : Math.min(EVOLUTION.length - 1, o + 1)))
      if (e.key === 'ArrowLeft') setOpen((o) => (o === null ? o : Math.max(0, o - 1)))
    }
    window.addEventListener('keydown', k)
    return () => window.removeEventListener('keydown', k)
  }, [open])
  const cur = open === null ? null : EVOLUTION[open]
  return (
    <>
      <ol className="-mx-4 flex snap-x snap-mandatory gap-4 overflow-x-auto px-4 pb-5 sm:-mx-8 sm:px-8" aria-label="L’évolution en images, du 8 au 29 septembre">
        {EVOLUTION.map(([f, alt, date, text], i) => (
          <li key={f} className="w-[80%] shrink-0 snap-start sm:w-[44%] lg:w-[30%]">
            <button type="button" onClick={() => setOpen(i)} className="group block w-full text-left" aria-label={`Agrandir : ${alt}`}>
              <span className="relative block overflow-hidden rounded-[1.2rem] ring-1 ring-foam/12">
                <img decoding="async" src={`/media/evolution/${f}.webp`} alt={alt} loading="lazy" className="aspect-video w-full object-cover transition-transform duration-700 group-hover:scale-[1.04]" />
                <span className="ui absolute left-3 top-3 rounded-full bg-trench/80 px-2.5 py-1 text-[0.72rem] tabular-nums text-foam">{String(i + 1).padStart(2, '0')}</span>
              </span>
              <span className="ui mt-3 block text-[0.85rem] font-[750] text-lagoon">{date}</span>
              <span className="mt-1 block text-[0.98rem] leading-snug text-foam/80">{text}</span>
            </button>
          </li>
        ))}
      </ol>
      <AnimatePresence>
        {cur && (
          <motion.div role="dialog" aria-modal="true" aria-label={cur[1]} className="fixed inset-0 z-[90] flex flex-col items-center justify-center bg-trench/97 p-4 sm:p-10" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} onClick={() => setOpen(null)}>
            <motion.img decoding="async" key={open} src={`/media/evolution/${cur[0]}.webp`} alt={cur[1]} className="max-h-[78vh] w-auto max-w-full rounded-xl shadow-2xl" initial={{ opacity: 0, scale: 0.97 }} animate={{ opacity: 1, scale: 1 }} onClick={(e) => e.stopPropagation()} />
            <p className="mt-5 max-w-[70ch] text-center text-[1.05rem] text-foam/85">
              <span className="ui mr-2 font-[750] text-lagoon">{cur[2]}</span>
              {cur[3]}
            </p>
            <p className="ui mt-3 text-[0.8rem] text-foam/65">Flèches pour naviguer, Échap pour fermer</p>
          </motion.div>
        )}
      </AnimatePresence>
    </>
  )
}

const STEPS: [string, string][] = [
  ['Le 8 septembre, tout va très vite.', 'En un après-midi, l’agent livre un prototype jouable : un océan, une vague qui déferle, un rider qui rame, part et surfe, une caméra, un score. Puis les problèmes arrivent, tous typiques d’Unity : la vague est invisible parce que des matrices n’existent qu’en mémoire et ne sont jamais sauvegardées ; un script de diagnostic laisse la scène enregistrée sans océan ; 20 tuiles d’océan sur 21 gardent un vieux matériau et dessinent une bande beige à travers l’eau.'],
  ['La nuit du 8 au 9, l’agent travaille seul.', 'Treize commits entre 23 h 47 et 3 h 16. Il lance une recherche avec 28 agents, reconstruit la vague sur la technique de Surf’s Up, écrit les tests, et termine en traquant un bug subtil : en mode « fast math » de Burst, une normalisation de vecteur presque nul produisait des NaN et projetait un sommet au loin, dessinant « un fin triangle turquoise ». Onze tests sur onze au vert à 3 h 16.'],
  ['Le 9 au matin, nos retours de joueurs entrent dans les commits', ': « je me fais éjecter de la vague et je n’arrive pas à prendre de la vitesse dans le tube ». Le HUD, les premières figures au stick droit et un panneau de réglage de la vague arrivent. Mais la branche grossit : 90 réglages sur le rider, trois HUD qui se superposent.'],
  ['Les 10 et 11 septembre, première reconstruction.', 'Opus 5 repart de la branche principale, plus petite, et construit une vague « designée » et le système de figures flick-it. Le code est propre et testé (42 tests), mais la vague ne convainc pas.'],
  ['Le 28 septembre, le déclic.', 'Après 17 jours de pause, Opus 5.5 reprend la v4 dès le matin et réécrit encore la vague. En début d’après-midi, nous sauvegardons cette branche et repartons de la base du 8 septembre. Nous ne reprenons que deux idées : les figures au stick droit et un monde centré sur le rider. Quatre heures plus tard, la vague Teahupoo est là. Dans la soirée suivent le renommage, la nouvelle caméra, le grand ménage et le modèle de vitesse qui donne enfin la sensation de courir contre le rouleau.'],
]

export function Journey() {
  return (
    <div className="space-y-24 sm:space-y-32">
      <HorizontalTimeline />
      <ol className="space-y-4 md:hidden">
        {EVENTS.map((e, i) => (
          <li key={i}>
            <EventCard e={e} />
          </li>
        ))}
      </ol>
      <Part title="Les grandes étapes">
        <ol className="space-y-10">
          {STEPS.map(([lead, rest], i) => (
            <li key={i} className="grid gap-3 border-l-2 border-foam/15 pl-6 transition-colors hover:border-lagoon sm:pl-8">
              <p className="max-w-[66ch] text-[1.12rem] leading-[1.7] text-foam/80">
                <span className="display mb-1 block text-[1.4rem] font-[760] leading-tight text-foam">{lead}</span>
                {rest.includes('Surf’s Up') ? (
                  <>
                    {rest.split('Surf’s Up')[0]}
                    <I>Surf’s Up</I>
                    {rest.split('Surf’s Up')[1]}
                  </>
                ) : (
                  rest
                )}
              </p>
            </li>
          ))}
        </ol>
      </Part>
      <div className="space-y-10">
        <h3 className="display text-[clamp(1.6rem,2.6vw,2.2rem)] font-[760] text-foam">L’évolution en images</h3>
        <BeforeAfter />
        <Filmstrip />
      </div>
    </div>
  )
}

/* ============================================================== 11 Le grand ménage */

const CLEANUP: [string, number, number][] = [
  ['Exemples de Shader Graph', 363, 413000],
  ['Storm Breakers (seul le son du vent reste)', 294, 307000],
  ['Packs Kenney (seule la particule d’écume reste)', 5252, 271000],
  ['Poly Haven (seul le ciel HDRI reste)', 208, 18000],
  ['Polices, scripts morts, outils obsolètes', 61, 1900],
  ['Quaternius (seul le mannequin reste)', 12, 1800],
]
const PACKAGES = ['Cinemachine', 'Splines', 'Timeline', 'uGUI et TextMeshPro', 'VFX Graph', 'Visual Scripting', 'AI Navigation', 'Animation Rigging', 'glTFast', 'AI Inference']

function CleanupChart() {
  const ref = useRef<HTMLDivElement>(null)
  const inView = useInView(ref, { once: true, margin: '-15% 0px' })
  const reduce = useReducedMotion()
  const max = CLEANUP[0][2]
  const fmt = (n: number) => n.toLocaleString('fr-FR')
  return (
    <div ref={ref} className="rounded-[1.8rem] bg-trench/70 p-6 ring-1 ring-foam/10 sm:p-10">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <p className="display text-[clamp(2.8rem,7vw,5.5rem)] font-[800] leading-none text-foam">
          <CountUp value="−1 013 998" />
        </p>
        <p className="ui max-w-[22rem] text-[0.88rem] text-foam/70">lignes supprimées par un seul commit (9332cf0), 6 213 fichiers. Ce qui a été supprimé, lignes et fichiers :</p>
      </div>
      <ul className="mt-10 space-y-5">
        {CLEANUP.map(([what, files, lines], i) => (
          <li key={what} className="grid gap-2 md:grid-cols-[minmax(0,19rem)_1fr] md:items-center md:gap-6">
            <span className="text-[0.98rem] leading-snug text-foam/80">{what}</span>
            <span className="flex items-center gap-3">
              <motion.span
                className="h-7 origin-left rounded-r-[4px] bg-lagoon"
                style={{ width: `max(4px, ${(lines / max) * 72}%)` }}
                initial={reduce ? false : { scaleX: 0 }}
                animate={inView ? { scaleX: 1 } : undefined}
                transition={{ duration: 1.1, delay: 0.2 + i * 0.12, ease: [0.22, 1, 0.36, 1] }}
                title={`${fmt(lines)} lignes, ${fmt(files)} fichiers`}
              />
              <span className="ui shrink-0 text-[0.85rem] tabular-nums text-foam">
                {fmt(lines)} <span className="text-foam/65">lignes, {fmt(files)} fichiers</span>
              </span>
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}

export function Cleanup() {
  const reduce = useReducedMotion()
  return (
    <div className="space-y-20">
      <Body>
        <P className="text-[1.2rem]">
          À force d’itérer, le projet avait accumulé des couches : des packs de ressources entiers pour une seule image, des paquets Unity que plus rien n’utilisait, des scripts d’outillage de versions abandonnées. Quand la logique du jeu nous a semblé stable, nous avons demandé à l’agent un audit simple : <B>ne livrer que ce que le jeu utilise</B>. Il a parcouru le graphe de dépendances de la scène, puis tout supprimé.
        </P>
      </Body>
      <CleanupChart />
      <Part title="Dix paquets Unity sont partis avec">
        <ul className="flex flex-wrap gap-2.5">
          {PACKAGES.map((p, i) => (
            <motion.li
              key={p}
              className="ui relative rounded-full bg-foam/[0.07] px-4 py-2 text-[0.92rem] text-foam/85 ring-1 ring-foam/15"
              initial={reduce ? false : { opacity: 1 }}
              whileInView={{ opacity: 0.45 }}
              viewport={{ once: true, margin: '-20% 0px' }}
              transition={{ delay: 0.3 + i * 0.1, duration: 0.6 }}
            >
              {p}
              <motion.span className="absolute left-3 right-3 top-1/2 h-px origin-left bg-coral" initial={reduce ? false : { scaleX: 0 }} whileInView={{ scaleX: 1 }} viewport={{ once: true, margin: '-20% 0px' }} transition={{ delay: 0.3 + i * 0.1, duration: 0.5 }} aria-hidden="true" />
            </motion.li>
          ))}
        </ul>
        <P>Cinemachine, Splines, Timeline, uGUI et TextMeshPro, VFX Graph, Visual Scripting, AI Navigation, Animation Rigging, glTFast et AI Inference, en plus de ProBuilder la veille.</P>
      </Part>
      <div className="grid items-center gap-10 rounded-[1.8rem] bg-foam/[0.05] p-6 ring-1 ring-foam/10 sm:p-10 lg:grid-cols-[minmax(0,5fr)_minmax(0,7fr)]">
        <p className="display flex flex-wrap items-baseline gap-4 text-[clamp(2.4rem,5.5vw,4.4rem)] font-[800] leading-none tabular-nums text-foam">
          7 726
          <svg viewBox="0 0 48 16" className="w-12 self-center text-lagoon" aria-hidden="true">
            <path d="M2 8h42M36 2l8 6-8 6" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          <span className="text-lagoon">7 680</span>
        </p>
        <p className="text-[1.1rem] leading-relaxed text-foam/82">
          Le chiffre le plus parlant : <B>notre propre code C# est passé de 7 726 à 7 680 lignes.</B> Le gonflement ne venait pas de nous mais des ressources et des paquets que les agents ajoutaient « au cas où » et ne retiraient jamais. Un agent ajoute volontiers ; il ne nettoie que si on le lui demande.
        </p>
      </div>
    </div>
  )
}

/* ============================================================== 12 Vue technique */

const MODULES: Record<string, ReactNode> = {
  Entrées: <><C>InputRouter</C> lit la manette ou le clavier et la souris (la souris devient un stick virtuel) et expose une interface <C>IRiderInput</C>. Le bot de test utilise la même interface.</>,
  Eau: <><C>WaterSurfaceComposite</C> répond à une seule question, « quelle est l’eau en ce point ? », en interrogeant la vague qui possède ce point ou, à défaut, la houle ambiante.</>,
  Vague: <><C>SurfWave</C> reconstruit à chaque image un maillage de 320 × 129 sommets avec des jobs Burst, à partir de <C>WaveProfile</C>. Le même profil analytique sert à la physique : on surfe exactement l’eau qu’on voit.</>,
  Océan: <><C>OceanSurface</C> est un maillage polaire centré sur la caméra, fin sous le joueur et grossier jusqu’à 6 km ; <C>OceanSwell</C> somme six vagues de Gerstner, avec les mêmes formules en C# et dans le shader.</>,
  Rider: <><C>RiderController</C> est une machine à états (rame, canard, take-off, ride, air, chute, sortie) intégrée à 50 Hz, dont la pose est extrapolée à chaque image pour rester fluide à 60 ou 144 images par seconde. Environ 100 réglages vivent dans un ScriptableObject.</>,
  Figures: <><C>FlickIt</C> reconnaît les gestes du stick (logique pure, testable), <C>TrickCatalog</C> les traduit selon la zone, <C>TrickRunner</C> superpose jusqu’à quatre rotations.</>,
  Caméra: <><C>CameraDirector</C> est entièrement procédural, sans Cinemachine. Plans de ride, de tube, d’air et de chute ; un bras à ressort teste le segment rider-caméra contre le volume analytique de l’eau pour ne jamais finir dans la vague.</>,
  Rendu: <>un seul shader <C>WavyBoard/Water</C> pour la mer et la vague : lumière turquoise à travers la lèvre, reflets, scintillement du soleil, écume en dentelle, ombre dans le tube.</>,
  'Interface et score': <><C>GameHud</C> (jauge de course contre le rouleau, indications contextuelles, légende des figures) et <C>RideScorer</C> (note sur 10, deux meilleures vagues).</>,
  Avatar: <>un mannequin humanoïde CC0 posé à chaque image par un solveur procédural, sans aucune animation enregistrée.</>,
}
const COLS: [string, string[]][] = [
  ['Ce qui entre', ['Entrées']],
  ['Ce qui est simulé, à 50 Hz', ['Rider', 'Figures', 'Eau', 'Vague', 'Océan']],
  ['Ce qui est montré', ['Caméra', 'Rendu', 'Interface et score', 'Avatar']],
]

function Flow() {
  return (
    <div className="flex items-center justify-center py-2 lg:px-1 lg:py-0" aria-hidden="true">
      <svg viewBox="0 0 40 80" className="h-10 w-20 rotate-90 lg:h-24 lg:w-10 lg:rotate-0">
        <path d="M6 40h28" stroke="#33cce6" strokeWidth="2.5" strokeDasharray="4 5" className="animate-[flow_1.2s_linear_infinite]" fill="none" />
        <path d="M26 32l8 8-8 8" stroke="#33cce6" strokeWidth="2.5" fill="none" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </div>
  )
}

const SPECS: [string, string][] = [
  ['Fichiers C#', '48 (40 pour le jeu, 4 pour l’éditeur, 4 pour les tests)'],
  ['Lignes de C#', '8 845 (7 758 jeu, 597 éditeur, 490 tests)'],
  ['Shader', '1 shader, 4 passes, 449 lignes'],
  ['Tests automatiques', '22 (49 cas, tous au vert)'],
  ['Commandes', '12 actions de jeu, 2 schémas (manette, clavier et souris)'],
  ['Figures', 'plus de 30, réparties sur 5 zones'],
  ['Ressources tierces', 'toutes CC0 : Quaternius, Poly Haven, Kenney, OpenGameArt, Storm Breakers'],
]

export function Tech() {
  return (
    <div className="space-y-16">
      <div className="grid gap-0 lg:grid-cols-[minmax(0,3fr)_auto_minmax(0,5fr)_auto_minmax(0,4fr)]">
        {COLS.map(([title, keys], ci) => (
          <div key={title} className="contents">
            {ci > 0 && <Flow />}
            <section className="rounded-[1.8rem] bg-trench/60 p-4 ring-1 ring-foam/10 sm:p-5">
              <h3 className="ui mb-4 px-1 text-[0.82rem] font-[700] text-foam/70">{title}</h3>
              <ul className="space-y-3">
                {keys.map((k) => (
                  <li key={k} className="rounded-[1.1rem] bg-foam/[0.06] p-4 ring-1 ring-foam/10 transition-colors hover:bg-foam/[0.1]">
                    <p className="ui text-[1.02rem] font-[780] text-lagoon">{k}</p>
                    <p className="mt-1.5 text-[0.93rem] leading-snug text-foam/78">{MODULES[k]}</p>
                  </li>
                ))}
              </ul>
            </section>
          </div>
        ))}
      </div>
      <dl className="grid overflow-hidden rounded-[1.8rem] ring-1 ring-foam/12 sm:grid-cols-2 xl:grid-cols-4">
        {SPECS.map(([k, v]) => (
          <div key={k} className="border-b border-r border-foam/10 bg-foam/[0.04] p-6">
            <dt className="ui text-[0.8rem] text-foam/70">{k}</dt>
            <dd className="display mt-2 text-[1.15rem] font-[720] leading-snug text-foam">{v}</dd>
          </div>
        ))}
      </dl>
    </div>
  )
}
