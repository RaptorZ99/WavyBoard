import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router'
import { scrollToId } from '../../lib/smoothScroll'

interface Props {
  to: string
  className?: string
  children: ReactNode
  onClick?: () => void
  'aria-label'?: string
}

/** Link to "/#id": scrolls smoothly when already on the page, navigates otherwise. */
export function HashLink({ to, className, children, onClick, ...rest }: Props) {
  const { pathname } = useLocation()
  const [path, id] = to.split('#')
  return (
    <Link
      to={to}
      className={className}
      aria-label={rest['aria-label']}
      onClick={(e) => {
        onClick?.()
        if (id && (path || '/') === pathname) {
          e.preventDefault()
          window.history.replaceState(null, '', `#${id}`)
          scrollToId(id)
        }
      }}
    >
      {children}
    </Link>
  )
}
