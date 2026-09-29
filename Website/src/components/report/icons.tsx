/** Line icons drawn for the report (24px grid, 1.6 stroke). */
const S = { fill: 'none', stroke: 'currentColor', strokeWidth: 1.6, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const }

const ICONS = {
  editor: (
    <>
      <rect x="3" y="4" width="18" height="16" rx="2.5" {...S} />
      <path d="M3 8.5h18M7 6.3h.01M9.5 6.3h.01M8 12h4M8 15h8" {...S} />
    </>
  ),
  scene: (
    <>
      <circle cx="6" cy="6" r="2" {...S} />
      <circle cx="18" cy="12" r="2" {...S} />
      <circle cx="18" cy="19" r="2" {...S} />
      <path d="M6 8v8a3 3 0 0 0 3 3h7M6 12h10" {...S} />
    </>
  ),
  file: (
    <>
      <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z" {...S} />
      <path d="M14 3v5h5M10 12l-2 2 2 2M14 12l2 2-2 2" {...S} />
    </>
  ),
  render: (
    <>
      <path d="M12 3l8 14H4z" {...S} />
      <path d="M12 3v14M4 17l8-5 8 5" {...S} />
    </>
  ),
  burst: <path d="M13 2L5 13h6l-1 9 8-11h-6z" {...S} />,
  recompile: (
    <>
      <path d="M20 12a8 8 0 1 1-2.3-5.7M20 4v4h-4" {...S} />
      <path d="M12 8v4l2.5 1.5" {...S} />
    </>
  ),
  sets: <path d="M2 17c3 0 4-3 6-3s3 3 6 3 4-3 6-3M2 11c3 0 4-3 6-3s3 3 6 3 4-3 6-3" {...S} />,
  takeoff: <path d="M3 7c6 0 9 3 11 8 1 2.5 3 4 7 4M17 15l4 4-4 4" {...S} />,
  speed: (
    <>
      <path d="M4 16a8 8 0 1 1 16 0" {...S} />
      <path d="M12 16l4-5" {...S} />
    </>
  ),
  pump: <path d="M7 9l5-5 5 5M7 15l5 5 5-5M12 4v16" {...S} />,
  envol: <path d="M3 19c3-9 8-13 18-13M16 3l5 3-3 5" {...S} />,
  stick: (
    <>
      <circle cx="12" cy="12" r="9" {...S} />
      <circle cx="12" cy="12" r="3.5" {...S} />
      <path d="M12 16v4M12 4v4" {...S} />
    </>
  ),
  score: <path d="M12 3l2.6 5.6 6 .7-4.5 4.1 1.2 6L12 16.4 6.7 19.4l1.2-6-4.5-4.1 6-.7z" {...S} />,
  buoy: (
    <>
      <path d="M12 3v3M8 21l1.5-9h5L16 21M7 21h10M9.3 15h5.4" {...S} />
      <circle cx="12" cy="8.5" r="2.5" {...S} />
    </>
  ),
  check: <path d="M5 12.5l4.5 4.5L19 7.5" {...S} />,
  eye: (
    <>
      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z" {...S} />
      <circle cx="12" cy="12" r="3" {...S} />
    </>
  ),
  hand: <path d="M8 13V5.5a1.5 1.5 0 0 1 3 0V11m0-1V4.5a1.5 1.5 0 0 1 3 0V11m0-4.5a1.5 1.5 0 0 1 3 0V14a7 7 0 0 1-7 7h-.5A6.5 6.5 0 0 1 4 16.2L2.7 13a1.5 1.5 0 0 1 2.6-1.5L8 15" {...S} />,
  loop: <path d="M4 12a8 8 0 0 1 14-5.3L20 9M20 4v5h-5M20 12a8 8 0 0 1-14 5.3L4 15M4 20v-5h5" {...S} />,
  target: (
    <>
      <circle cx="12" cy="12" r="9" {...S} />
      <circle cx="12" cy="12" r="5" {...S} />
      <circle cx="12" cy="12" r="1.2" {...S} />
    </>
  ),
  broom: <path d="M14 3l-4 9M7 12h8l2 9H5zM9 16v5M13 16v5" {...S} />,
} as const

export type IconName = keyof typeof ICONS

export function Icon({ name, className = 'h-6 w-6' }: { name: IconName; className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} aria-hidden="true">
      {ICONS[name]}
    </svg>
  )
}
