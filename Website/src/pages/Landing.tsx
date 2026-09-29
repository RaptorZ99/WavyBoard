import { Hero } from '../components/landing/Hero'
import { Intro } from '../components/landing/Intro'
import { Trailer } from '../components/landing/Trailer'
import { WaveLife } from '../components/landing/WaveLife'
import { Session } from '../components/landing/Session'
import { Tricks } from '../components/landing/Tricks'
import { Controls } from '../components/landing/Controls'
import { Gallery } from '../components/landing/Gallery'
import { Download } from '../components/landing/Download'
import { MakingOf } from '../components/landing/MakingOf'

export function Landing() {
  return (
    <main>
      <Hero />
      <Intro />
      <Trailer />
      <WaveLife />
      <Session />
      <Tricks />
      <Controls />
      <Gallery />
      <Download />
      <MakingOf />
    </main>
  )
}
