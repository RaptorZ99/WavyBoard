import type { JSX } from 'react'

/** Button and key glyphs: PlayStation symbols, triggers, sticks and keyboard keys. */

const PS: Record<string, { label: string; draw?: JSX.Element }> = {
  cross: { label: 'Croix', draw: <path d="M7 7l10 10M17 7L7 17" stroke="#8fb3ff" strokeWidth="2.4" strokeLinecap="round" /> },
  circle: { label: 'Rond', draw: <circle cx="12" cy="12" r="5.6" fill="none" stroke="#ff8a8a" strokeWidth="2.4" /> },
  triangle: { label: 'Triangle', draw: <path d="M12 6.5l6 10.5H6z" fill="none" stroke="#6fe0b8" strokeWidth="2.2" strokeLinejoin="round" /> },
  square: { label: 'Carré', draw: <rect x="6.8" y="6.8" width="10.4" height="10.4" fill="none" stroke="#f59ad9" strokeWidth="2.2" /> },
  'dpad-up': {
    label: 'Croix directionnelle haut',
    draw: <path d="M9 4h6v6h-6zM4 9h5v6H4zM15 9h5v6h-5zM9 15h6v5H9z" fill="currentColor" opacity=".35" />,
  },
}


export function Glyph({ g }: { g: string }) {
  const ps = PS[g]
  if (ps) {
    return (
      <span className="inline-flex h-9 w-9 items-center justify-center rounded-full bg-foam/[0.08] ring-1 ring-foam/15" title={ps.label}>
        <svg viewBox="0 0 24 24" className="h-6 w-6" role="img" aria-label={ps.label}>
          {ps.draw}
          {g === 'dpad-up' && <path d="M9 4h6v6H9z" fill="#33cce6" />}
        </svg>
      </span>
    )
  }
  if (g === 'L-stick' || g === 'R-stick') {
    const left = g === 'L-stick'
    return (
      <span className="ui inline-flex h-9 items-center gap-1.5 rounded-full bg-foam/[0.08] pl-1 pr-3 text-[0.8rem] font-[650] ring-1 ring-foam/15" title={left ? 'Stick gauche' : 'Stick droit'}>
        <svg viewBox="0 0 24 24" className="h-7 w-7" aria-hidden="true">
          <circle cx="12" cy="12" r="10" fill="none" stroke="currentColor" strokeOpacity=".35" strokeWidth="1.5" />
          <circle cx="12" cy="12" r="5.5" fill={left ? '#f0f7ff' : '#33cce6'} />
        </svg>
        {left ? 'Stick G' : 'Stick D'}
      </span>
    )
  }
  if (/^[LR][12]$/.test(g)) {
    const trigger = g.endsWith('2')
    return (
      <span
        className={`ui inline-flex h-9 min-w-11 items-center justify-center bg-foam/[0.08] px-2.5 text-[0.82rem] font-[750] ring-1 ring-foam/15 ${
          trigger ? 'rounded-b-md rounded-t-[1.1rem]' : 'rounded-md'
        }`}
      >
        {g}
      </span>
    )
  }
  const wide = g.length > 2
  return (
    <kbd
      className={`ui inline-flex h-9 items-center justify-center rounded-lg border-b-[3px] border-foam/25 bg-foam/[0.1] text-[0.82rem] font-[650] text-foam ring-1 ring-foam/15 ${
        wide ? 'px-3' : 'w-9'
      }`}
    >
      {g}
    </kbd>
  )
}
