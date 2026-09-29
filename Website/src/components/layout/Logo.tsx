interface Props {
  className?: string
}

/** The curl mark: a lip throwing over a tube, drawn from the game's barrel key. */
export function LogoMark({ className }: Props) {
  return (
    <svg viewBox="0 0 64 64" className={className} aria-hidden="true">
      <path
        d="M4 50c11 0 16-7 20-16 5-11 12-18 23-18 8 0 13 5 13 11 0 5-4 9-9 9-4 0-6-3-6-6 0 5-5 8-10 8 6 6 12 11 25 11v11H4z"
        fill="currentColor"
      />
      <path d="M4 55c15 0 24-2 32-6 7 4 15 6 24 6v5H4z" fill="currentColor" opacity=".45" />
    </svg>
  )
}

export function Wordmark({ className }: Props) {
  return (
    <span className={`display inline-flex items-center gap-2 ${className ?? ''}`}>
      <LogoMark className="h-7 w-7 text-lagoon" />
      <span className="text-[1.15rem] font-[800] tracking-tight" style={{ fontVariationSettings: "'wdth' 125" }}>
        WavyBoard
      </span>
    </span>
  )
}
