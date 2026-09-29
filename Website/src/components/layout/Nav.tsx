import { useEffect, useState } from 'react'
import { Link, NavLink, useLocation } from 'react-router'
import { AnimatePresence, motion } from 'motion/react'
import { Wordmark } from './Logo'
import { HashLink } from './HashLink'
import { SoonTooltip } from '../ui/SoonTooltip'

const LANDING_LINKS = [
  { to: '/#vague', label: 'La vague' },
  { to: '/#session', label: 'Une session' },
  { to: '/#figures', label: 'Figures' },
  { to: '/#commandes', label: 'Commandes' },
  { to: '/#galerie', label: 'Galerie' },
]

export function Nav() {
  const { pathname } = useLocation()
  const [scrolled, setScrolled] = useState(false)
  const [open, setOpen] = useState(false)
  const onReport = pathname.startsWith('/rapport')

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 40)
    onScroll()
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])


  const solid = scrolled || onReport || open
  return (
    <header
      className={`fixed inset-x-0 top-0 z-50 transition-[background-color,backdrop-filter,border-color] duration-500 ${
        solid ? 'border-b border-foam/10 bg-abyss/95' : 'border-b border-transparent'
      }`}
    >
      <nav className="mx-auto flex h-16 max-w-[1400px] items-center justify-between gap-6 px-4 sm:px-8" aria-label="Navigation principale">
        <Link to="/" className="shrink-0 text-foam" aria-label="WavyBoard, accueil">
          <Wordmark />
        </Link>

        <ul className="ui hidden items-center gap-1 text-[0.92rem] lg:flex">
          {LANDING_LINKS.map((l) => (
            <li key={l.to}>
              <HashLink to={l.to} className="rounded-full px-3 py-2 text-foam/75 transition-colors hover:text-foam">
                {l.label}
              </HashLink>
            </li>
          ))}
          <li>
            <NavLink
              to="/rapport"
              className={({ isActive }) =>
                `rounded-full px-3 py-2 transition-colors ${isActive ? 'text-lagoon' : 'text-foam/75 hover:text-foam'}`
              }
            >
              Rapport de projet
            </NavLink>
          </li>
        </ul>

        <div className="flex items-center gap-2">
          <span className="hidden sm:inline-flex">
          <SoonTooltip side="bottom">
            {() => (
              <HashLink
                to="/#telecharger"
                className="ui inline-flex rounded-full bg-sun px-5 py-2.5 text-[0.9rem] font-[700] text-abyss transition-transform duration-300 hover:-translate-y-0.5"
              >
                Télécharger
              </HashLink>
            )}
          </SoonTooltip>
          </span>
          <button
            type="button"
            className="ui inline-flex h-10 w-10 items-center justify-center rounded-full border border-foam/20 text-foam lg:hidden"
            aria-expanded={open}
            aria-controls="menu-mobile"
            aria-label={open ? 'Fermer le menu' : 'Ouvrir le menu'}
            onClick={() => setOpen((v) => !v)}
          >
            <svg viewBox="0 0 24 24" className="h-5 w-5" aria-hidden="true">
              {open ? (
                <path d="M6 6l12 12M18 6L6 18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              ) : (
                <path d="M4 8h16M4 16h16" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              )}
            </svg>
          </button>
        </div>
      </nav>

      <AnimatePresence>
        {open && (
          <motion.div
            id="menu-mobile"
            initial={{ opacity: 0, y: -12 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -12 }}
            transition={{ duration: 0.3, ease: [0.22, 1, 0.36, 1] }}
            className="border-t border-foam/10 bg-abyss lg:hidden"
          >
            <ul className="display flex flex-col gap-1 px-4 py-6 text-3xl font-[750]">
              {[...LANDING_LINKS, { to: '/rapport', label: 'Rapport de projet' }].map((l) => (
                <li key={l.to}>
                  <HashLink to={l.to} onClick={() => setOpen(false)} className="block py-2 text-foam">
                    {l.label}
                  </HashLink>
                </li>
              ))}
              <li className="pt-4">
                <HashLink
                  to="/#telecharger"
                  onClick={() => setOpen(false)}
                  className="ui inline-flex rounded-full bg-sun px-6 py-3 text-base font-[700] text-abyss"
                >
                  Télécharger
                </HashLink>
              </li>
            </ul>
          </motion.div>
        )}
      </AnimatePresence>
    </header>
  )
}
