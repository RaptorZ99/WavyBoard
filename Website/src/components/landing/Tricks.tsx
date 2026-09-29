import { useEffect, useMemo, useRef, useState } from 'react'
import { useInView, useReducedMotion } from 'motion/react'
import { TRICKS, ZONES, type Path, type Trick, type Zone } from '../../content/game'

const CYCLE = 3.2 // s
const STICK_END = 0.5
const MOVE_START = 0.46
const MOVE_END = 0.86

const ease = (t: number) => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2)
const clamp01 = (x: number) => Math.min(1, Math.max(0, x))

/** Point at fraction u of a polyline, by arc length. */
function along(path: Path, u: number): [number, number] {
  const seg: number[] = []
  let total = 0
  for (let i = 1; i < path.length; i++) {
    const l = Math.hypot(path[i][0] - path[i - 1][0], path[i][1] - path[i - 1][1]) + 0.12 // pauses count a little
    seg.push(l)
    total += l
  }
  let d = u * total
  for (let i = 0; i < seg.length; i++) {
    if (d <= seg[i]) {
      const t = d / seg[i]
      return [path[i][0] + (path[i + 1][0] - path[i][0]) * t, path[i][1] + (path[i + 1][1] - path[i][1]) * t]
    }
    d -= seg[i]
  }
  return path[path.length - 1]
}

function boardTransform(trick: Trick, m: number) {
  const k = ease(m)
  const hop = Math.sin(Math.PI * k)
  const turns = trick.turns ?? 360
  switch (trick.motion) {
    case 'pop':
      return { lift: hop * 46, rx: -hop * 18, ry: 0, rz: 0 }
    case 'roll':
      return { lift: hop * 52, rx: 0, ry: k * turns, rz: 0 }
    case 'spin':
      return { lift: hop * 44, rx: 0, ry: 0, rz: k * turns }
    case 'flip':
      return { lift: hop * 62, rx: -k * turns, ry: 0, rz: 0 }
    case 'invert':
      return { lift: hop * 56, rx: -hop * turns, ry: hop * 40, rz: 0 }
    case 'grab':
      return { lift: hop * 50, rx: -hop * 10, ry: hop * 35, rz: hop * 12 }
    case 'carve':
      return { lift: 0, rx: 0, ry: -hop * 22, rz: k * turns * (m < 1 ? 1 : 0) }
    case 'duck':
      return { lift: -hop * 26, rx: hop * 30, ry: 0, rz: 0 }
  }
}

/** Frame of the demo at cycle time t (0..1): stick trail, knob position, board transform. */
function frameAt(trick: Trick, t: number) {
  const u = clamp01(t / STICK_END)
  const pts: string[] = []
  for (let i = 0; i <= 40; i++) {
    const [px, py] = along(trick.path, (i / 40) * u)
    pts.push(`${(50 + px * 38).toFixed(2)},${(50 - py * 38).toFixed(2)}`)
  }
  const back = ease(clamp01((t - STICK_END) / 0.2))
  const [x, y] = t < STICK_END ? along(trick.path, u) : (along(trick.path, 1).map((v) => v * (1 - back)) as [number, number])
  const b = boardTransform(trick, clamp01((t - MOVE_START) / (MOVE_END - MOVE_START)))
  return { trail: pts.join(' '), cx: 50 + x * 38, cy: 50 - y * 38, trailOpacity: t < STICK_END + 0.25 ? 0.9 : 0.25, b }
}

/** The stick gesture and the board's move, animated by writing straight into the DOM (no React render per frame). */
function StickDemo({ trick, running }: { trick: Trick; running: boolean }) {
  const trailRef = useRef<SVGPolylineElement>(null)
  const knobRef = useRef<SVGGElement>(null)
  const liftRef = useRef<HTMLDivElement>(null)
  const spinRef = useRef<HTMLDivElement>(null)
  const still = useMemo(() => frameAt(trick, 0.72), [trick])

  useEffect(() => {
    const apply = (f: ReturnType<typeof frameAt>) => {
      trailRef.current?.setAttribute('points', f.trail)
      trailRef.current?.setAttribute('opacity', String(f.trailOpacity))
      knobRef.current?.setAttribute('transform', `translate(${f.cx.toFixed(2)} ${f.cy.toFixed(2)})`)
      if (liftRef.current) liftRef.current.style.transform = `translate3d(0, ${(-f.b.lift).toFixed(1)}px, 0) rotateX(40deg)`
      if (spinRef.current) spinRef.current.style.transform = `rotateZ(${f.b.rz.toFixed(1)}deg) rotateX(${f.b.rx.toFixed(1)}deg) rotateY(${f.b.ry.toFixed(1)}deg)`
    }
    apply(frameAt(trick, 0.72))
    if (!running) return
    const start = performance.now()
    let raf = 0
    const loop = (now: number) => {
      apply(frameAt(trick, ((now - start) / 1000 / CYCLE) % 1))
      raf = requestAnimationFrame(loop)
    }
    raf = requestAnimationFrame(loop)
    return () => cancelAnimationFrame(raf)
  }, [trick, running])

  return (
    <div className="grid items-center gap-8 sm:grid-cols-2">
      <div className="relative mx-auto aspect-square w-full max-w-[300px]">
        <svg viewBox="0 0 100 100" className="h-full w-full" aria-hidden="true">
          <defs>
            <radialGradient id="stick-well" cx="50%" cy="45%" r="60%">
              <stop offset="0" stopColor="#0a2c4a" />
              <stop offset="1" stopColor="#03172a" />
            </radialGradient>
          </defs>
          <circle cx="50" cy="50" r="46" fill="url(#stick-well)" stroke="#f0f7ff" strokeOpacity=".14" />
          <circle cx="50" cy="50" r="38" fill="none" stroke="#f0f7ff" strokeOpacity=".08" strokeDasharray="1 3" />
          <line x1="50" y1="8" x2="50" y2="92" stroke="#f0f7ff" strokeOpacity=".06" />
          <line x1="8" y1="50" x2="92" y2="50" stroke="#f0f7ff" strokeOpacity=".06" />
          <polyline ref={trailRef} points={still.trail} opacity={still.trailOpacity} fill="none" stroke="#33cce6" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" />
          <g ref={knobRef} transform={`translate(${still.cx} ${still.cy})`}>
            <circle r="12" fill="#f0f7ff" opacity=".96" />
            <circle r="7.5" fill="none" stroke="#051f38" strokeOpacity=".25" strokeWidth="1.2" />
          </g>
        </svg>
        <span className="ui absolute bottom-1 left-1/2 -translate-x-1/2 text-[0.72rem] text-foam/65">stick droit, ou souris</span>
      </div>

      <div className="relative mx-auto flex aspect-square w-full max-w-[300px] items-center justify-center" style={{ perspective: '700px' }}>
        <div className="absolute inset-x-6 bottom-[22%] h-12 rounded-[50%] bg-[radial-gradient(closest-side,rgba(51,204,230,0.35),transparent)]" />
        <div ref={liftRef} className="will-change-transform" style={{ transform: `translate3d(0, ${-still.b.lift}px, 0) rotateX(40deg)`, transformStyle: 'preserve-3d' }}>
          <div ref={spinRef} style={{ transform: `rotateZ(${still.b.rz}deg) rotateX(${still.b.rx}deg) rotateY(${still.b.ry}deg)`, transformStyle: 'preserve-3d' }}>
            <svg viewBox="0 0 60 110" className="h-[210px] w-[114px]" aria-hidden="true">
              <path d="M8 16 Q30 -2 52 16 L54 88 Q46 96 38 92 Q30 100 22 92 Q14 96 6 88 Z" fill="#ffc56b" />
              <path d="M8 16 Q30 -2 52 16" fill="none" stroke="#051f38" strokeWidth="3" />
              <path d="M14 30 L46 30 M14 70 L46 70" stroke="#051f38" strokeOpacity=".25" strokeWidth="2" />
              <rect x="27" y="22" width="6" height="62" rx="3" fill="#0d6b94" />
            </svg>
          </div>
        </div>
      </div>
    </div>
  )
}

export function Tricks() {
  const [zone, setZone] = useState<Zone>('levre')
  const list = TRICKS.filter((t) => t.zone === zone)
  const [name, setName] = useState('El Rollo')
  const trick = TRICKS.find((t) => t.name === name && t.zone === zone) ?? list[0]
  const ref = useRef<HTMLDivElement>(null)
  const inView = useInView(ref, { margin: '-20% 0px' })
  const reduce = useReducedMotion()

  return (
    <section id="figures" className="relative overflow-hidden bg-abyss-2 py-28 sm:py-36" aria-labelledby="figures-titre">
      <div className="mx-auto max-w-[1400px] px-4 sm:px-8">
        <div className="grid gap-8 lg:grid-cols-[minmax(0,6fr)_minmax(0,5fr)] lg:items-end">
          <h2 id="figures-titre" className="display text-[clamp(2.6rem,5.8vw,5rem)] font-[780] text-foam">
            Le pouce droit, c’est la planche
          </h2>
          <p className="max-w-[48ch] text-[1.12rem] leading-relaxed text-foam/75">
            Comme dans Skate : tu charges le stick vers le bas, tu le relances vers le haut, et le chemin que trace ton pouce choisit la figure. Le même geste ne veut pas dire la même chose à plat, sur la face, sur la lèvre ou dans le tube.
          </p>
        </div>

        <div role="tablist" aria-label="Zones de la vague" className="ui mt-12 flex flex-wrap gap-2">
          {ZONES.map((z) => (
            <button
              key={z.id}
              role="tab"
              type="button"
              aria-selected={zone === z.id}
              onClick={() => {
                setZone(z.id)
                setName(TRICKS.find((t) => t.zone === z.id)?.name ?? '')
              }}
              className={`rounded-full px-5 py-2.5 text-[0.95rem] font-[650] transition-colors ${
                zone === z.id ? 'bg-foam text-abyss' : 'bg-foam/[0.06] text-foam/75 ring-1 ring-foam/15 hover:text-foam'
              }`}
            >
              {z.name}
            </button>
          ))}
        </div>
        <p className="ui mt-4 text-[0.9rem] text-foam/60">{ZONES.find((z) => z.id === zone)?.text}</p>

        <div ref={ref} className="mt-10 grid gap-8 lg:grid-cols-[minmax(0,4fr)_minmax(0,8fr)]">
          <ul className="space-y-2" aria-label="Figures de la zone">
            {list.map((t) => (
              <li key={t.name}>
                <button
                  type="button"
                  onClick={() => setName(t.name)}
                  aria-pressed={t.name === trick.name}
                  className={`group flex w-full items-baseline justify-between gap-4 rounded-2xl px-5 py-4 text-left transition-colors ${
                    t.name === trick.name ? 'bg-foam/[0.1] ring-1 ring-lagoon/50' : 'hover:bg-foam/[0.05]'
                  }`}
                >
                  <span>
                    <span className="display block text-[1.35rem] font-[720] text-foam">{t.name}</span>
                    <span className="ui block text-[0.88rem] text-foam/55">{t.gesture}</span>
                  </span>
                  <span className="ui shrink-0 text-[0.9rem] tabular-nums text-sun/90">{t.points}</span>
                </button>
              </li>
            ))}
          </ul>

          <div className="rounded-[1.75rem] bg-trench/70 p-6 ring-1 ring-foam/10 sm:p-10">
            <StickDemo trick={trick} running={inView && !reduce} />
            <div className="mt-8 flex flex-wrap items-baseline justify-between gap-4 border-t border-foam/10 pt-6">
              <p className="display text-[clamp(2rem,4vw,3.2rem)] font-[780] text-foam" aria-live="polite">
                {trick.name}
              </p>
              <p className="ui text-[1rem] text-foam/70">
                {trick.gesture}
                {typeof trick.points === 'number' && <span className="ml-3 text-sun">{trick.points} pts</span>}
              </p>
            </div>
            <p className="ui mt-4 text-[0.85rem] leading-relaxed text-foam/55">
              Plus tu charges longtemps, plus le pop est haut. Continue d’enrouler pour passer de 360 à 540, 720… Répéter la même figure dans une vague rapporte 30 % de moins à chaque fois ; enchaîner en moins de 2,5 s fait grimper le multiplicateur jusqu’à ×2.
            </p>
          </div>
        </div>
      </div>
    </section>
  )
}
