import { useId, type ReactNode } from 'react'

interface Props {
  children: (describedBy: string) => ReactNode
  label?: string
  side?: 'top' | 'bottom'
  className?: string
}

/** Wraps a download control: "Bientôt disponible" appears on hover and keyboard focus (and on tap, via focus). */
export function SoonTooltip({ children, label = 'Bientôt disponible', side = 'top', className = '' }: Props) {
  const id = useId()
  return (
    <span className={`group/soon relative inline-flex ${className}`}>
      {children(id)}
      <span
        id={id}
        role="tooltip"
        className={`ui pointer-events-none absolute left-1/2 z-[60] -translate-x-1/2 whitespace-nowrap rounded-full bg-foam px-3.5 py-1.5 text-[0.8rem] font-[700] text-abyss opacity-0 shadow-[0_12px_30px_-10px_rgba(3,23,42,0.6)] transition-[opacity,transform] duration-300 [transition-timing-function:cubic-bezier(0.22,1,0.36,1)] group-focus-within/soon:opacity-100 group-hover/soon:opacity-100 ${
          side === 'top'
            ? 'bottom-full mb-3 translate-y-1 group-focus-within/soon:translate-y-0 group-hover/soon:translate-y-0'
            : 'top-full mt-3 -translate-y-1 group-focus-within/soon:translate-y-0 group-hover/soon:translate-y-0'
        }`}
      >
        {label}
        <span className={`absolute left-1/2 h-2.5 w-2.5 -translate-x-1/2 rotate-45 bg-foam ${side === 'top' ? '-bottom-1' : '-top-1'}`} aria-hidden="true" />
      </span>
    </span>
  )
}
