/**
 * TypeScript port of the game's wave cross-section (Assets/_Project/Scripts/Wave/WaveProfile.cs,
 * prototyped in Tools/wave_profile_lab.py).
 *
 * The cross-section is ONE continuous curve: flat water in front (beach side) -> up the face -> round the back
 * wall of the tube -> along the ceiling -> round the lip tip -> over the crest -> down the back. It is a
 * centripetal Catmull-Rom spline through 15 control points whose positions are keyframed over the life of the
 * wave. Units are normalised by the wave height H; x points toward the beach, x = 0 is the back wall of the tube.
 */

export type Vec2 = readonly [number, number]
export type Shape = Vec2[]

export const ROLES = [
  'F2', 'F1', 'FOOT', 'FACE_LO', 'FACE_MID', 'WALL', 'CEIL', 'LIPIN', 'TIP', 'LIPOUT', 'LIPTOP', 'CREST', 'BACK', 'B1', 'B2',
] as const
const M = ROLES.length
/** Samples per spline segment (sum = vertex count - 1), identical to the game. */
export const SEG = [10, 8, 8, 8, 8, 10, 10, 9, 9, 10, 10, 10, 8, 10]
export const NU = SEG.reduce((a, b) => a + b, 0) + 1

/** Index of the first sample of each control point in the sampled curve. */
export const SAMPLE_OF_CONTROL: number[] = (() => {
  const out = [0]
  for (const s of SEG) out.push(out[out.length - 1] + s)
  return out
})()

function bump(xs: number[], h: number, wf: number, wb: number, xc = 0): Shape {
  return xs.map((x) => {
    const u = (x - xc) / (x >= xc ? wf : wb)
    return [x, h * (0.5 + 0.5 * Math.cos(Math.PI * Math.min(1, Math.abs(u))))] as const
  })
}

const pts = (a: number[][]): Shape => a.map(([x, y]) => [x, y] as const)

const SWELL_X = [7.0, 3.6, 2.4, 1.75, 1.25, 0.85, 0.55, 0.32, 0.14, -0.02, -0.2, -0.5, -1.4, -3.6, -7.0]

export const KEYS: Record<string, Shape> = {
  swell: bump(SWELL_X, 0.42, 3.0, 3.2),
  steep: pts([[7, 0], [3.4, 0], [2.1, 0.03], [1.45, 0.19], [0.95, 0.45], [0.58, 0.67], [0.36, 0.78], [0.2, 0.83], [0.07, 0.855],
    [-0.05, 0.86], [-0.2, 0.845], [-0.5, 0.78], [-1.45, 0.44], [-3.6, 0], [-7, 0]]),
  pitch: pts([[7, 0], [3.2, 0], [1.7, 0.02], [0.95, 0.18], [0.42, 0.47], [0.17, 0.72], [0.16, 0.9], [0.3, 0.99], [0.46, 1.01],
    [0.4, 1.08], [0.18, 1.1], [-0.25, 1.02], [-1.25, 0.55], [-3.6, 0], [-7, 0]]),
  throw: pts([[7, 0], [3.1, 0], [1.55, 0.015], [0.85, 0.14], [0.32, 0.4], [0.04, 0.66], [0.2, 0.92], [0.62, 0.87], [0.9, 0.62],
    [1.02, 0.83], [0.66, 1.05], [0.12, 1.08], [-1.1, 0.6], [-3.6, 0], [-7, 0]]),
  barrel: pts([[7, 0], [3.0, 0], [1.55, 0.005], [0.8, 0.08], [0.24, 0.31], [0.0, 0.6], [0.24, 0.9], [0.92, 0.68], [1.16, 0.03],
    [1.4, 0.4], [1.06, 0.93], [0.3, 1.1], [-1.0, 0.64], [-3.6, 0], [-7, 0]]),
  barrel2: pts([[7, 0], [3.0, 0], [1.6, 0.0], [0.82, 0.075], [0.26, 0.3], [0.02, 0.58], [0.26, 0.88], [0.98, 0.64], [1.24, -0.07],
    [1.47, 0.37], [1.1, 0.9], [0.33, 1.08], [-1.0, 0.63], [-3.6, 0], [-7, 0]]),
  impact: pts([[7, 0], [3.0, 0], [1.62, 0.0], [0.85, 0.07], [0.32, 0.26], [0.12, 0.44], [0.36, 0.63], [0.92, 0.47], [1.38, -0.12],
    [1.62, 0.3], [1.12, 0.76], [0.42, 0.9], [-0.9, 0.55], [-3.6, 0], [-7, 0]]),
  mound: bump([7, 3.6, 2.5, 2.05, 1.72, 1.45, 1.22, 1.03, 0.86, 0.68, 0.48, 0.18, -0.75, -3.5, -7], 0.6, 1.9, 2.6, 0.9),
  bore: bump([7, 4.2, 3.2, 2.7, 2.35, 2.05, 1.8, 1.58, 1.38, 1.16, 0.9, 0.5, -0.5, -3.4, -7], 0.34, 2.1, 2.8, 1.35),
  flat: bump([7, 4.4, 3.4, 2.9, 2.55, 2.25, 2.0, 1.78, 1.58, 1.36, 1.1, 0.7, -0.3, -3.3, -7], 0.03, 2.3, 3.0, 1.55),
}

/** Life of one point of the crest: key and time relative to its break (s). */
export const TIMELINE: [string, number][] = [
  ['steep', -1.6], ['pitch', 0.0], ['throw', 0.75], ['barrel', 1.5], ['barrel2', 3.3], ['impact', 4.1], ['mound', 5.2],
  ['bore', 7.5], ['flat', 16.0],
]

export const T_BARREL = 1.5
export const T_IMPACT = 4.1
export const T_MOUND = 5.2

function catmull(p0: Vec2, p1: Vec2, p2: Vec2, p3: Vec2, t: number): Vec2 {
  const tj = (ti: number, a: Vec2, b: Vec2) => ti + Math.max(Math.hypot(b[0] - a[0], b[1] - a[1]), 1e-4) ** 0.5
  const t0 = 0
  const t1 = tj(t0, p0, p1)
  const t2 = tj(t1, p1, p2)
  const t3 = tj(t2, p2, p3)
  const tt = t1 + (t2 - t1) * t
  const lerp = (a: Vec2, b: Vec2, ta: number, tb: number): Vec2 => {
    const w = (tt - ta) / (tb - ta)
    return [a[0] + (b[0] - a[0]) * w, a[1] + (b[1] - a[1]) * w]
  }
  const a1 = lerp(p0, p1, t0, t1)
  const a2 = lerp(p1, p2, t1, t2)
  const a3 = lerp(p2, p3, t2, t3)
  const b1 = lerp(a1, a2, t0, t2)
  const b2 = lerp(a2, a3, t1, t3)
  return lerp(b1, b2, t1, t2)
}

function hermiteKeys(keys: Shape[], times: number[], t: number): Shape {
  const n = keys.length
  if (t <= times[0]) return keys[0]
  if (t >= times[n - 1]) return keys[n - 1]
  let i = 0
  while (times[i + 1] < t) i++
  const u = (t - times[i]) / (times[i + 1] - times[i])
  const k0 = keys[Math.max(0, i - 1)]
  const k1 = keys[i]
  const k2 = keys[i + 1]
  const k3 = keys[Math.min(n - 1, i + 2)]
  const u2 = u * u
  const u3 = u2 * u
  const out: Vec2[] = []
  for (let j = 0; j < M; j++) {
    const c = [0, 1].map((a) => {
      const p0 = k0[j][a], p1 = k1[j][a], p2 = k2[j][a], p3 = k3[j][a]
      return 0.5 * (2 * p1 + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u2 + (-p0 + 3 * p1 - 3 * p2 + p3) * u3)
    })
    out.push([c[0], c[1]])
  }
  return out
}

const smoothstep = (a: number, b: number, x: number) => {
  const t = Math.min(1, Math.max(0, (x - a) / (b - a)))
  return t * t * (3 - 2 * t)
}

/** WaveProfile.ScaleCurl: the curl grows about the foot of the back wall, so the tube opens. */
function scaleCurl(p: Shape, tau: number, tubeScale: number): Shape {
  if (tubeScale <= 1.0001) return p
  const life = smoothstep(-0.4, 1.4, tau) * (1 - smoothstep(T_IMPACT, T_MOUND, tau))
  return p.map(([x, y], j) => {
    const w = j === 4 ? 0.3 : j === 12 ? 0.25 : j >= 5 && j <= 11 ? 1 : 0
    const k = 1 + (tubeScale - 1) * life * w
    return [x * k, 0.22 + (y - 0.22) * k] as const
  })
}

const PRE_KEYS = (shoal: number): Shape =>
  KEYS.swell.map((a, j) => [a[0] * (1 - shoal) + KEYS.steep[j][0] * shoal, a[1] * (1 - shoal) + KEYS.steep[j][1] * shoal] as const)

const TL_KEYS = TIMELINE.slice(1).map(([n]) => KEYS[n])
const TL_TIMES = TIMELINE.map(([, t]) => t)

/** Control points at time tau (s) relative to the local break; shoal 0..1 grows the swell into the steep face. */
export function shapeAt(tau: number, shoal = 1, heavy = 1, tubeScale = 1): Shape {
  let p = hermiteKeys([PRE_KEYS(shoal), ...TL_KEYS], TL_TIMES, tau)
  if (heavy < 1) p = p.map(([x, y], j) => [j >= 5 && j <= 11 && x > 0 ? x * (0.55 + 0.45 * heavy) : x, y] as const)
  return scaleCurl(p, tau, tubeScale)
}

/** Samples the spline through the control points: NU points, written into `out` as x0,y0,x1,y1,... */
export function sampleCurve(p: Shape, out: Float32Array = new Float32Array(NU * 2)): Float32Array {
  const ext: Vec2[] = [
    [2 * p[0][0] - p[1][0], 2 * p[0][1] - p[1][1]],
    ...p,
    [2 * p[M - 1][0] - p[M - 2][0], 2 * p[M - 1][1] - p[M - 2][1]],
  ]
  let o = 0
  for (let i = 0; i < M - 1; i++) {
    for (let k = 0; k < SEG[i]; k++) {
      const q = catmull(ext[i], ext[i + 1], ext[i + 2], ext[i + 3], k / SEG[i])
      out[o++] = q[0]
      out[o++] = q[1]
    }
  }
  out[o++] = p[M - 1][0]
  out[o++] = p[M - 1][1]
  return out
}

/** Life stages shown on the site, in the order a point of the crest lives them. */
export const STAGES = [
  { key: 'swell', tau: -2.4, shoal: 0, name: 'La houle', text: 'Une bosse de 40 cm qui arrive du large. Rien ne laisse deviner ce qui va suivre.' },
  { key: 'steep', tau: -1.6, shoal: 1, name: 'Elle se dresse', text: 'Le fond remonte, la vague ralentit et se creuse : la face devient raide.' },
  { key: 'pitch', tau: 0.0, shoal: 1, name: 'La lèvre pitche', text: 'La crête dépasse la verticale. C’est l’instant du take-off.' },
  { key: 'throw', tau: 0.75, shoal: 1, name: 'Elle lance', text: 'Une lèvre épaisse est projetée vers la plage, une hauteur de vague devant la paroi.' },
  { key: 'barrel', tau: 2.4, shoal: 1, name: 'Le tube', text: 'Un tube rond et spacieux. C’est ici que l’on veut vivre.' },
  { key: 'impact', tau: 4.1, shoal: 1, name: 'L’impact', text: 'La lèvre retombe et explose. Il fallait être sorti une seconde plus tôt.' },
  { key: 'mound', tau: 5.2, shoal: 1, name: 'La mousse', text: 'Le rouleau s’effondre en un dôme d’écume.' },
  { key: 'bore', tau: 7.5, shoal: 1, name: 'La barre', text: 'Un mur de mousse roule vers la plage et s’éteint.' },
] as const
