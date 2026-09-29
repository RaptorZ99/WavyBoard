import { Suspense, lazy, useEffect } from 'react'
import { Route, Routes, useLocation } from 'react-router'
import { Nav } from './components/layout/Nav'
import { Footer } from './components/layout/Footer'
import { RouteWave } from './components/layout/RouteWave'
import { Landing } from './pages/Landing'
import { jumpToTop, scrollToId } from './lib/smoothScroll'
import { pauseHoverWhileScrolling } from './lib/scrollHover'

const Report = lazy(() => import('./pages/Report').then((m) => ({ default: m.Report })))

function ScrollManager() {
  const { pathname, hash } = useLocation()
  useEffect(() => {
    if (hash) {
      const id = decodeURIComponent(hash.slice(1))
      const t = window.setTimeout(() => scrollToId(id), 60)
      return () => window.clearTimeout(t)
    }
    jumpToTop()
  }, [pathname, hash])
  return null
}

export default function App() {
  useEffect(() => pauseHoverWhileScrolling(), [])
  return (
    <>
      <a
        href="#contenu"
        className="ui sr-only z-[100] rounded-full bg-foam px-4 py-2 text-abyss focus:not-sr-only focus:fixed focus:left-4 focus:top-4"
      >
        Aller au contenu
      </a>
      <ScrollManager />
      <RouteWave />
      <Nav />
      <div id="contenu">
        <Routes>
          <Route path="/" element={<Landing />} />
          <Route
            path="/rapport"
            element={
              <Suspense fallback={<main className="min-h-svh bg-paper" />}>
                <Report />
              </Suspense>
            }
          />
          <Route path="*" element={<Landing />} />
        </Routes>
      </div>
      <Footer />
    </>
  )
}
