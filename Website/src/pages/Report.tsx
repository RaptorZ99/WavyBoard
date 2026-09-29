import { useEffect, type ReactNode } from 'react'
import { ReportHero } from '../components/report/ReportHero'
import { SwellRail } from '../components/report/SwellRail'
import { CHAPTERS } from '../components/report/chapters'
import { Chapter } from '../components/report/kit'
import { Brief, Framework, Project } from '../components/report/chapters/Ch01to03'
import { Models, Prompt, Research } from '../components/report/chapters/Ch04to06'
import { Cli, Loop, Wave } from '../components/report/chapters/Ch07to09'
import { Cleanup, Journey, Tech } from '../components/report/chapters/Ch10to12'
import { Annex, Limits, Moral } from '../components/report/chapters/Ch13to15'

/**
 * The project report, laid out for the web (its text is written in the chapter components).
 * The page reads like the life of a wave: it opens inside the tube, each chapter begins with the game's wave a
 * little later in its life, and the last chapters end in the foam.
 */
const BODIES: Record<string, ReactNode> = {
  'en-bref': <Brief />,
  projet: <Project />,
  cadre: <Framework />,
  iagraphie: <Models />,
  veille: <Research />,
  prompt: <Prompt />,
  cli: <Cli />,
  boucle: <Loop />,
  vague: <Wave />,
  cheminement: <Journey />,
  menage: <Cleanup />,
  technique: <Tech />,
  limites: <Limits />,
  morale: <Moral />,
  annexes: <Annex />,
}

export function Report() {
  useEffect(() => {
    document.title = 'Rapport de projet, WavyBoard'
    return () => {
      document.title = 'WavyBoard — le bodyboard au cœur du tube'
    }
  }, [])
  return (
    <main className="overflow-x-clip bg-trench">
      <SwellRail />
      <ReportHero />
      {CHAPTERS.map((c, i) => (
        <Chapter key={c.id} meta={c} prev={CHAPTERS[i - 1]}>
          {BODIES[c.id]}
        </Chapter>
      ))}
    </main>
  )
}
