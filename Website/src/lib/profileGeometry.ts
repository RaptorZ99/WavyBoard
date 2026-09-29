import { NU, STAGES } from './waveProfile'

export const X0 = -3.3
export const X1 = 3.5
const Y0 = -0.45
const Y1 = 1.45
export const PROFILE_W = 1000
export const PROFILE_H = (PROFILE_W * (Y1 - Y0)) / (X1 - X0)
export const sx = (x: number) => ((x - X0) / (X1 - X0)) * PROFILE_W
export const sy = (y: number) => PROFILE_H - ((y - Y0) / (Y1 - Y0)) * PROFILE_H

export const TAU_MIN = -3.2
export const TAU_MAX = 8.4

export const smooth = (a: number, b: number, x: number) => {
  const t = Math.min(1, Math.max(0, (x - a) / (b - a)))
  return t * t * (3 - 2 * t)
}

/** Stage of the wave's life nearest to tau. */
export function stageAt(tau: number) {
  let best = 0
  for (let i = 1; i < STAGES.length; i++) if (tau >= (STAGES[i - 1].tau + STAGES[i].tau) / 2) best = i
  return best
}

/** Height and slope of the face (the front branch of the curve) at x. */
export function faceHeight(c: Float32Array, x: number): { y: number; slope: number } | null {
  for (let k = 0; k < NU - 1; k++) {
    const xa = c[k * 2]
    const xb = c[(k + 1) * 2]
    if (xb > xa + 1e-4) break
    if (x <= xa && x >= xb) {
      const t = (xa - x) / Math.max(1e-5, xa - xb)
      const ya = c[k * 2 + 1]
      const yb = c[(k + 1) * 2 + 1]
      return { y: ya + (yb - ya) * t, slope: (yb - ya) / Math.max(1e-5, xa - xb) }
    }
  }
  return null
}

