import { useEffect, useId, useMemo, useRef, type ReactNode } from 'react'
import { animate, useInView, useReducedMotion } from 'motion/react'
import { NU, sampleCurve, shapeAt } from '../../lib/waveProfile'

/* ------------------------------------------------------------------ inline */

export const B = ({ children }: { children: ReactNode }) => <strong className="font-[650] text-foam">{children}</strong>
export const I = ({ children }: { children: ReactNode }) => <em className="italic">{children}</em>
export const C = ({ children }: { children: ReactNode }) => (
  <code className="rounded-md bg-foam/[0.09] px-1.5 py-0.5 font-mono text-[0.84em] text-lagoon-soft ring-1 ring-foam/10">{children}</code>
)

/* ------------------------------------------------------------------ blocks */

export function P({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <p className={`max-w-[66ch] text-[1.14rem] leading-[1.72] text-foam/82 ${className}`}>{children}</p>
}

export function Lead({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <p className={`max-w-[34ch] font-serif text-[clamp(1.55rem,2.6vw,2.2rem)] font-[340] leading-[1.25] text-foam ${className}`}>{children}</p>
}

export function H3({ children, id }: { children: ReactNode; id?: string }) {
  return (
    <h3 id={id} className="display scroll-mt-32 text-[clamp(1.6rem,2.6vw,2.2rem)] font-[760] leading-[1.05] text-foam">
      {children}
    </h3>
  )
}

/** A statement pulled out of the text: big serif, a lagoon rule on its left. */
export function Pull({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <blockquote className={`relative border-l-2 border-lagoon pl-6 sm:pl-10 ${className}`}>
      <p className="font-serif text-[clamp(1.7rem,3.4vw,3rem)] font-[330] leading-[1.18] tracking-[-0.01em] text-foam">{children}</p>
    </blockquote>
  )
}

export function Callout({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`relative overflow-hidden rounded-[1.6rem] bg-foam/[0.06] p-6 ring-1 ring-foam/12 sm:p-9 ${className}`}>
      <div className="pointer-events-none absolute -right-32 -top-32 h-80 w-80 bg-[radial-gradient(closest-side,rgba(51,204,230,0.16),transparent)]" />
      <div className="relative">{children}</div>
    </div>
  )
}

/** File or command names shown as small chips. */
export function Chip({ children, tone = 'dark' }: { children: ReactNode; tone?: 'dark' | 'light' }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-3 py-1 font-mono text-[0.78rem] ring-1 ${
        tone === 'dark' ? 'bg-foam/[0.07] text-lagoon-soft ring-foam/15' : 'bg-ink/[0.06] text-ink ring-ink/15'
      }`}
    >
      {children}
    </span>
  )
}

/* ------------------------------------------------------------------ numbers */

/** "−1 013 998 lignes" -> animated count, French grouping, sign and unit kept. The digits are written straight into
 * the DOM (no React render per frame) and the final value reserves the width, so nothing around it moves. */
export function CountUp({ value, className = '' }: { value: string; className?: string }) {
  const m = /^([−+-]?)(\d[\d\s  ]*\d|\d)(.*)$/.exec(value)
  const sign = m?.[1] ?? ''
  const unit = m?.[3] ?? ''
  const target = m ? Number(m[2].replace(/[\s  ]/g, '')) : 0
  const ref = useRef<HTMLSpanElement>(null)
  const digits = useRef<HTMLSpanElement>(null)
  const inView = useInView(ref, { once: true, margin: '0px 0px -12% 0px' })
  const reduce = useReducedMotion()
  useEffect(() => {
    const el = digits.current
    if (!m || !el) return
    if (reduce) {
      el.textContent = target.toLocaleString('fr-FR')
      return
    }
    if (!inView) return
    const c = animate(0, target, {
      duration: target > 10000 ? 2.4 : 1.8,
      ease: [0.16, 1, 0.3, 1],
      onUpdate: (v) => {
        el.textContent = Math.round(v).toLocaleString('fr-FR')
      },
    })
    return () => c.stop()
    // m is derived from value; target covers it
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [inView, reduce, target])
  if (!m) return <span className={className}>{value}</span>
  const final = `${sign}${target.toLocaleString('fr-FR')}${unit}`
  return (
    <span ref={ref} className={`inline-grid tabular-nums ${className}`} aria-label={final}>
      <span className="invisible col-start-1 row-start-1" aria-hidden="true">
        {final}
      </span>
      <span className="col-start-1 row-start-1" aria-hidden="true">
        {sign}
        <span ref={digits}>0</span>
        {unit}
      </span>
    </span>
  )
}

/* ------------------------------------------------------------------ chapter */

export interface ChapterMeta {
  id: string
  n: number
  title: string
  short: string
  bg: string
  /** time since the break of the wave drawn at the top of the chapter */
  tau: number
  /** how much of the wave's life the divider plays as it comes into view (s) */
  lead?: number
  light?: boolean
}

const smooth = (a: number, b: number, x: number) => {
  const t = Math.min(1, Math.max(0, (x - a) / (b - a)))
  return t * t * (3 - 2 * t)
}

// wide enough that the whole curl, crest included, always fits: the frame only ever crops the flat sides
const VX0 = -4.6
const VX1 = 5.0
const VY0 = -0.3
const VY1 = 1.5
const VW = 1600
const VH = (VW * (VY1 - VY0)) / (VX1 - VX0)

const vx = (x: number) => ((x - VX0) / (VX1 - VX0)) * VW
const vy = (y: number) => VH - ((y - VY0) / (VY1 - VY0)) * VH
// the thrown lip lands on the water: it never dips under the flat, where it would cut a hole in the next section
const onWater = (y: number) => Math.max(0.004, y)

function wavePaths(t: number) {
  const c = sampleCurve(shapeAt(t, smooth(-3.1, -1.7, t), 1, 1.15))
  let line = `M${vx(VX0 - 3)} ${vy(onWater(c[1])).toFixed(1)}`
  for (let k = 0; k < NU; k++) line += `L${vx(c[k * 2]).toFixed(1)} ${vy(onWater(c[k * 2 + 1])).toFixed(1)}`
  line += `L${vx(VX1 + 3)} ${vy(onWater(c[(NU - 1) * 2 + 1])).toFixed(1)}`
  // the curve runs beach -> sea (x decreasing), so close the shape along the bottom
  const fill = `${line}L${vx(VX1 + 3)} ${VH + 4}L${vx(VX0 - 3)} ${VH + 4}Z`
  return { fill, line }
}

/**
 * The top of each chapter is the game's wave at a later moment of its life: the report is read as the wave breaks.
 * The wave plays its next second of life while the divider rises from the bottom of the screen to a third of the way
 * down; the path is written straight into the DOM, and only while the divider is on screen.
 */
export function WaveDivider({
  from,
  to,
  tau,
  label,
  light,
  fromLight,
  lead = 0.9,
}: {
  from: string
  to: string
  tau: number
  label?: string
  light?: boolean
  fromLight?: boolean
  /** seconds of the wave's life played while the divider rises into view (it ends 0.35 s after tau) */
  lead?: number
}) {
  const ref = useRef<HTMLDivElement>(null)
  const fillRef = useRef<SVGPathElement>(null)
  const lineRef = useRef<SVGPathElement>(null)
  const gid = `wd-${useId().replace(/:/g, '')}`
  const reduce = useReducedMotion()
  const start = reduce ? tau : tau - lead
  const initial = useMemo(() => wavePaths(start), [start])

  useEffect(() => {
    const el = ref.current
    if (!el || reduce) return
    let visible = false
    let raf = 0
    let last = NaN
    const update = () => {
      raf = 0
      const r = el.getBoundingClientRect()
      const vh = window.innerHeight
      const p = Math.min(1, Math.max(0, (vh - r.top) / (vh * 0.68)))
      const e = 1 - Math.pow(1 - p, 2)
      const t = Math.round((tau - lead + (lead + 0.35) * e) * 80) / 80
      if (t === last) return
      last = t
      const { fill, line } = wavePaths(t)
      fillRef.current?.setAttribute('d', fill)
      lineRef.current?.setAttribute('d', line)
    }
    const onScroll = () => {
      if (visible && !raf) raf = requestAnimationFrame(update)
    }
    const io = new IntersectionObserver(
      ([entry]) => {
        visible = entry.isIntersecting
        onScroll()
      },
      { rootMargin: '15% 0px' },
    )
    io.observe(el)
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => {
      io.disconnect()
      window.removeEventListener('scroll', onScroll)
      cancelAnimationFrame(raf)
    }
  }, [tau, lead, reduce])

  // dark -> dark: deep water with a lit crest; dark -> white: the wave glows turquoise and breaks into the foam;
  // white -> white: the last of the foam, a soft turquoise mound
  const stops = !light ? ['#33cce6', '#0d6b94'] : fromLight ? ['#33cce6', '#a9dfea'] : ['#33cce6', '#9be7f2']
  return (
    <div ref={ref} className="relative overflow-hidden" style={{ background: from }} aria-hidden="true">
      <svg viewBox={`0 0 ${VW} ${VH}`} preserveAspectRatio="xMidYMax slice" className="block h-[max(104px,17.8vw)] w-full">
        <defs>
          <linearGradient id={gid} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stopColor={stops[0]} stopOpacity={light ? 1 : 0.9} />
            <stop offset="0.32" stopColor={stops[1]} />
            <stop offset="0.74" stopColor={to} />
            <stop offset="1" stopColor={to} />
          </linearGradient>
        </defs>
        <path ref={fillRef} d={initial.fill} fill={`url(#${gid})`} />
        <path
          ref={lineRef}
          d={initial.line}
          fill="none"
          stroke={fromLight ? '#0d6b94' : '#f0f7ff'}
          strokeOpacity={fromLight ? 0.3 : 0.55}
          strokeWidth="1.8"
          vectorEffect="non-scaling-stroke"
        />
      </svg>
      {label && <span className={`ui absolute bottom-3 right-4 hidden text-[0.7rem] tabular-nums sm:right-8 sm:block ${fromLight || light ? 'text-ink/70' : 'text-foam/65'}`}>{label}</span>}
    </div>
  )
}

export function Chapter({ meta, prev, children }: { meta: ChapterMeta; prev?: ChapterMeta; children: ReactNode }) {
  const light = meta.light
  const secs = `${Math.abs(meta.tau).toFixed(1).replace('.', ',')} s`
  const tauLabel = `la vague, ${secs} ${meta.tau >= 0 ? 'après' : 'avant'} la casse`
  return (
    <section id={meta.id} aria-labelledby={`${meta.id}-t`} className={light ? 'text-ink' : 'text-foam'}>
      {prev && <WaveDivider from={prev.bg} to={meta.bg} tau={meta.tau} lead={meta.lead} light={light} fromLight={prev.light} label={tauLabel} />}
      <div style={{ background: meta.bg }} className="relative">
        <div className="mx-auto max-w-[1400px] px-4 pb-28 pt-10 sm:px-8 sm:pb-36">
          <header className="grid gap-4 md:grid-cols-[minmax(0,3fr)_minmax(0,9fr)] md:gap-10">
            <p
              className={`display select-none text-[clamp(4.5rem,11vw,9.5rem)] font-[800] leading-[0.8] tabular-nums ${light ? 'text-ink/12' : 'text-foam/10'}`}
              style={{ fontVariationSettings: "'wdth' 125" }}
              aria-hidden="true"
            >
              {String(meta.n).padStart(2, '0')}
            </p>
            <h2
              id={`${meta.id}-t`}
              className={`display self-end scroll-mt-32 text-[clamp(2.4rem,5.2vw,4.6rem)] font-[790] leading-[0.98] ${light ? 'text-ink' : 'text-foam'}`}
            >
              {meta.title}
            </h2>
          </header>
          <div className="mt-14 sm:mt-20">{children}</div>
        </div>
      </div>
    </section>
  )
}

/** Main column aligned with the chapter title (9 of 12 columns, right). */
export function Body({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`grid md:grid-cols-[minmax(0,3fr)_minmax(0,9fr)] md:gap-10 ${className}`}>
      <div className="hidden md:block" />
      <div className="min-w-0 space-y-7">{children}</div>
    </div>
  )
}

/** A sub-part of a chapter: its title sits in the left gutter on wide screens. */
export function Part({ title, children, className = '' }: { title: ReactNode; children: ReactNode; className?: string }) {
  return (
    <div className={`grid gap-6 md:grid-cols-[minmax(0,3fr)_minmax(0,9fr)] md:gap-10 ${className}`}>
      <div className="md:pt-1">
        <div className="md:sticky md:top-32">
          <H3>{title}</H3>
        </div>
      </div>
      <div className="min-w-0 space-y-7">{children}</div>
    </div>
  )
}
