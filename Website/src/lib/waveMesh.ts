import { NU, SAMPLE_OF_CONTROL, SEG, sampleCurve, shapeAt } from './waveProfile'

/**
 * Builds the lofted surf wave the way the game does (SurfWave + SurfWaveMath.Attributes): one cross-section per
 * crest coordinate s, each at its own time since the break, so the curl peels along the crest.
 * The result is static in the frame that moves with the curl; the shader animates the water flowing through it.
 */

const TIP = 8
const smoothstep = (a: number, b: number, x: number) => {
  const t = Math.min(1, Math.max(0, (x - a) / (b - a)))
  return t * t * (3 - 2 * t)
}
const saturate = (x: number) => Math.min(1, Math.max(0, x))

/** WaveProfile.HoldBarrel: the open tube lasts `hold` s longer, everything after is shifted. */
function holdBarrel(tau: number, hold: number) {
  const tBarrel = 1.5
  const open = 3.3 - tBarrel
  if (hold <= 0 || tau <= tBarrel) return tau
  const end = tBarrel + open + hold
  return tau < end ? tBarrel + ((tau - tBarrel) * open) / (open + hold) : tau - hold
}

interface Landmarks {
  curl: boolean
  xRef: number
  xTip: number
}

/** WaveProfile.Landmarks.Find on a sampled section (x decreasing along the face, then the ceiling runs forward). */
function findLandmarks(c: Float32Array): Landmarks {
  const n = NU
  let j = 0
  while (j < n - 1 && c[(j + 1) * 2] <= c[j * 2] + 1e-4) j++
  const curl = j < n - 1
  let yTop = -Infinity
  let xTop = 0
  for (let k = 0; k < n; k++) {
    if (c[k * 2 + 1] > yTop) {
      yTop = c[k * 2 + 1]
      xTop = c[k * 2]
    }
  }
  if (!curl) return { curl, xRef: xTop, xTip: xTop }
  let k2 = j
  let t = j
  let yMin = Infinity
  while (k2 < n - 1 && c[(k2 + 1) * 2] >= c[k2 * 2] - 1e-4) {
    k2++
    if (c[k2 * 2 + 1] < yMin) {
      yMin = c[k2 * 2 + 1]
      t = k2
    }
  }
  return { curl, xRef: Math.min(c[j * 2], xTop), xTip: c[t * 2] }
}

/** Control-space coordinate (0 front flat .. 14 back flat) of each sample of the curve. */
const R_OF_SAMPLE = (() => {
  const r = new Float32Array(NU)
  for (let i = 0; i < SEG.length; i++) for (let k = 0; k < SEG[i]; k++) r[SAMPLE_OF_CONTROL[i] + k] = i + k / SEG[i]
  r[NU - 1] = SEG.length
  return r
})()

export interface WaveMeshOptions {
  height: number
  sMin: number
  sMax: number
  rows: number
  peelSpeed: number
  celerity: number
  tubeScale: number
  hold: number
}

export interface WaveMeshData {
  positions: Float32Array
  normals: Float32Array
  /** thin water, foam, tube occlusion, face (SurfWaveMath.Attributes) */
  attributes: Float32Array
  /** s (m along the crest), arc length along the section (m): world-anchored flow coordinates for the shader */
  flow: Float32Array
  index: Uint32Array
  rows: number
  cols: number
  /** where the lip lands, per row, for spray: [s, xTip, tau] */
  lipLanding: { s: number; x: number; tau: number }[]
}

export function buildWaveMesh(o: WaveMeshOptions): WaveMeshData {
  const cols = NU
  const { rows, height: H } = o
  const positions = new Float32Array(rows * cols * 3)
  const normals = new Float32Array(rows * cols * 3)
  const attributes = new Float32Array(rows * cols * 4)
  const flow = new Float32Array(rows * cols * 2)
  const curve = new Float32Array(NU * 2)
  const lipLanding: WaveMeshData['lipLanding'] = []

  for (let i = 0; i < rows; i++) {
    const u = i / (rows - 1)
    const s = o.sMin + (o.sMax - o.sMin) * u
    // behind the peel point the crest broke |s| / vp seconds ago; ahead of it, it has not broken yet
    const rawTau = s < 0 ? -s / o.peelSpeed : (-s / o.peelSpeed) * 0.4
    const tau = holdBarrel(rawTau, o.hold)
    const shoal = 1 - smoothstep(6, 70, s)
    const amp = smoothstep(o.sMin, o.sMin + 25, s) * (1 - smoothstep(o.sMax - 30, o.sMax, s))
    sampleCurve(shapeAt(tau, shoal, 1, o.tubeScale), curve)
    const L = findLandmarks(curve)
    const xRef = L.xRef * H
    const xTip = L.xTip * H
    if (L.curl && tau > 0.9 && tau < 3.6) lipLanding.push({ s, x: xTip, tau })

    // SurfWaveMath.Attributes, per sample
    const throwK = smoothstep(-0.3, 1.2, tau)
    const barrelK = smoothstep(0.8, 1.7, tau) * (1 - smoothstep(3.5, 4.5, tau))
    const collapseK = smoothstep(3.3, 4.9, tau)
    const foamLife = Math.exp(-Math.max(0, tau - 7) / 4.5)
    const yScale = H * amp
    const ampK = saturate(yScale / Math.max(0.3, 0.3 * H))
    const xiMin = -7 * H
    const xiMax = 7 * H
    let arc = 0

    for (let k = 0; k < cols; k++) {
      const x = curve[k * 2] * H
      const y = curve[k * 2 + 1] * yScale
      const v = i * cols + k
      positions[v * 3] = s
      positions[v * 3 + 1] = y
      positions[v * 3 + 2] = x
      if (k > 0) arc += Math.hypot(x - curve[(k - 1) * 2] * H, y - curve[(k - 1) * 2 + 1] * yScale)
      flow[v * 2] = s
      flow[v * 2 + 1] = arc

      const r = R_OF_SAMPLE[k]
      const yN = y / Math.max(yScale, 0.05)
      const onFront = r < 10.6 ? 1 : 0.3
      const thinFace = smoothstep(0.25, 0.95, yN) * onFront * (0.3 + 0.7 * shoal)
      const lipRole = saturate(1 - Math.abs(r - 8.3) / 3.3)
      const thin = saturate(Math.max(thinFace, lipRole * throwK * 1.1)) * (1 - 0.85 * collapseK) * ampK

      const feather = smoothstep(-1.6, -0.3, tau) * (1 - smoothstep(0.9, 2.0, tau)) * saturate(1 - Math.abs(r - 10.4) / 1.1) * 0.45
      const lipReach = TIP + 0.2 + 2.4 * saturate((tau - 1) / 3)
      const lipOuter = smoothstep(TIP + 0.05, TIP + 0.6, r) * (1 - smoothstep(lipReach - 0.6, lipReach, r))
      const lipFoam = smoothstep(0.9, 3.4, tau) * lipOuter * 0.85
      const dxT = (x - xTip) / (0.3 * H + 0.8)
      const impact = smoothstep(1.2, 1.9, tau) * Math.exp(-dxT * dxT) * (r < 4.5 ? 1 : 0)
      const standing = smoothstep(0.03, 0.22, yN)
      const whitewater = collapseK * standing
      const behind = Math.max(0, xRef - x)
      const trail = collapseK * Math.exp(-behind / (o.celerity * 3.5)) * (r > 9 ? 0.85 : 0.55)
      let foam = Math.max(feather, lipFoam, impact, whitewater, trail)
      foam *= 1 + (foamLife - 1) * saturate(tau - 5)
      foam *= ampK
      foam *= smoothstep(xiMin, xiMin + 12, x) * (1 - smoothstep(xiMax - 8, xiMax, x))

      let ao = 0
      if (L.curl && xTip > xRef + 0.1) {
        const under = saturate((xTip - x) / (xTip - xRef))
        if (r < 5.5) ao = under * smoothstep(1.5, 3.5, r)
        else if (r < TIP) ao = 0.85 * saturate((TIP - r) / 1.2)
        ao *= barrelK
      }
      const a = v * 4
      attributes[a] = thin
      attributes[a + 1] = saturate(foam)
      attributes[a + 2] = saturate(ao)
      attributes[a + 3] = saturate(yN * 2.5)
    }
  }

  // normals: cross of the along-crest and along-section tangents (points out of the water, into the tube)
  const p = positions
  for (let i = 0; i < rows; i++) {
    const i0 = Math.max(0, i - 1)
    const i1 = Math.min(rows - 1, i + 1)
    for (let k = 0; k < cols; k++) {
      const k0 = Math.max(0, k - 1)
      const k1 = Math.min(cols - 1, k + 1)
      const a = (i1 * cols + k) * 3
      const b = (i0 * cols + k) * 3
      const c = (i * cols + k1) * 3
      const d = (i * cols + k0) * 3
      const tsx = p[a] - p[b], tsy = p[a + 1] - p[b + 1], tsz = p[a + 2] - p[b + 2]
      const tux = p[c] - p[d], tuy = p[c + 1] - p[d + 1], tuz = p[c + 2] - p[d + 2]
      let nx = tsy * tuz - tsz * tuy
      let ny = tsz * tux - tsx * tuz
      let nz = tsx * tuy - tsy * tux
      const len = Math.hypot(nx, ny, nz) || 1
      nx /= len
      ny /= len
      nz /= len
      const v = (i * cols + k) * 3
      normals[v] = nx
      normals[v + 1] = ny
      normals[v + 2] = nz
    }
  }

  const index = new Uint32Array((rows - 1) * (cols - 1) * 6)
  let t = 0
  for (let i = 0; i < rows - 1; i++) {
    for (let k = 0; k < cols - 1; k++) {
      const a = i * cols + k
      const b = a + 1
      const c = a + cols
      const d = c + 1
      index[t++] = a
      index[t++] = c
      index[t++] = b
      index[t++] = b
      index[t++] = c
      index[t++] = d
    }
  }
  return { positions, normals, attributes, flow, index, rows, cols, lipLanding }
}

