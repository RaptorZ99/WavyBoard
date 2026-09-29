import { useId, useMemo } from 'react'
import { NU, ROLES, sampleCurve, shapeAt } from '../../lib/waveProfile'
import { PROFILE_H, PROFILE_W, X0, X1, faceHeight, smooth, sx, sy } from '../../lib/profileGeometry'

interface Props {
  tau: number
  theme?: 'dark' | 'light'
  /** which control points get their role written next to them */
  labels?: 'some' | 'all' | 'none'
  className?: string
  title?: string
  /** profile-space x range to show (zooms on the curl on narrow screens) */
  crop?: readonly [number, number]
}

const SOME = new Set(['WALL', 'CEIL', 'TIP', 'CREST', 'FOOT', 'LIPTOP'])

/** The game's wave cross-section at time tau since the break, with its 15 control points. */
export function ProfileSvg({ tau, theme = 'dark', labels = 'some', className, title, crop }: Props) {
  const uid = useId().replace(/:/g, '')
  const shoal = smooth(-3.1, -1.7, tau)
  const { d, fill, controls, rider } = useMemo(() => {
    const pts = shapeAt(tau, shoal, 1, 1.15)
    const c = sampleCurve(pts)
    let d = ''
    for (let k = 0; k < NU; k++) d += `${k ? 'L' : 'M'}${sx(c[k * 2]).toFixed(1)} ${sy(c[k * 2 + 1]).toFixed(1)}`
    const fill = `${d}L${sx(X0 - 1)} ${sy(c[(NU - 1) * 2 + 1])}L${sx(X0 - 1)} ${PROFILE_H + 10}L${sx(X1 + 1)} ${PROFILE_H + 10}L${sx(X1 + 1)} ${sy(c[1])}Z`
    const controls = pts.map(([x, y], j) => ({ x: sx(x), y: sy(y), role: ROLES[j] }))
    let rider: { x: number; y: number; a: number } | null = null
    if (tau > 1.2 && tau < 4.0) {
      const f = faceHeight(c, 0.42)
      if (f) rider = { x: sx(0.42), y: sy(f.y), a: (-Math.atan(f.slope) * 180) / Math.PI }
    }
    return { d, fill, controls, rider }
  }, [tau, shoal])

  const dark = theme === 'dark'
  const ink = dark ? '#f0f7ff' : '#062640'
  const pointStroke = dark ? '#ffc56b' : '#c2410c'
  return (
    <svg
      viewBox={crop ? `${sx(crop[0])} 0 ${sx(crop[1]) - sx(crop[0])} ${PROFILE_H}` : `0 0 ${PROFILE_W} ${PROFILE_H}`}
      className={className}
      role="img"
      aria-label={title ?? 'Coupe de la vague'}
    >
      <defs>
        <linearGradient id={`w-${uid}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#33cce6" stopOpacity="0.95" />
          <stop offset="0.35" stopColor="#0d6b94" />
          <stop offset="1" stopColor={dark ? '#051f38' : '#0a2c4a'} />
        </linearGradient>
      </defs>
      <line x1="0" x2={PROFILE_W} y1={sy(0)} y2={sy(0)} stroke={ink} strokeOpacity="0.14" strokeDasharray="3 6" />
      <text x={PROFILE_W - 4} y={sy(0) - 6} textAnchor="end" fill={ink} fillOpacity="0.45" className="font-mono text-[9px]">
        niveau moyen
      </text>
      <line x1={sx(-2.9)} x2={sx(-2.9)} y1={sy(0)} y2={sy(1)} stroke={pointStroke} strokeOpacity="0.7" />
      <line x1={sx(-2.95)} x2={sx(-2.85)} y1={sy(1)} y2={sy(1)} stroke={pointStroke} strokeOpacity="0.7" />
      <text x={sx(-2.84)} y={sy(1) + 4} fill={pointStroke} className="font-mono text-[10px]">
        H
      </text>
      <path d={fill} fill={`url(#w-${uid})`} />
      <path d={d} fill="none" stroke={dark ? '#f0f7ff' : '#9be7f2'} strokeOpacity={dark ? 0.85 : 1} strokeWidth="2.2" strokeLinejoin="round" />
      {rider && (
        <g transform={`translate(${rider.x} ${rider.y}) rotate(${rider.a})`}>
          <rect x="-16" y="-5" width="32" height="5" rx="2.5" fill="#ffc56b" />
          <path d="M-12 -5 q6 -9 18 -8 q8 1 10 5" fill="none" stroke="#031727" strokeWidth="5" strokeLinecap="round" />
          <circle cx="14" cy="-12" r="4.2" fill="#031727" />
        </g>
      )}
      {controls.map((c, j) => {
        const named = labels === 'all' || (labels === 'some' && SOME.has(c.role))
        return (
          <g key={j}>
            <circle cx={c.x} cy={c.y} r={named ? 3.6 : 2.4} fill={dark ? '#051f38' : '#eef6f8'} stroke={pointStroke} strokeWidth="1.4" />
            {named && (
              <text x={c.x + 6} y={c.y - 6} fill={pointStroke} className="font-mono text-[9px]">
                {c.role}
              </text>
            )}
          </g>
        )
      })}
    </svg>
  )
}
