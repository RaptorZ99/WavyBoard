import { useEffect, useRef, useState } from 'react'
import { useMotionValueEvent, useReducedMotion, useScroll } from 'motion/react'
import { STAGES } from '../../lib/waveProfile'
import { scrollToY } from '../../lib/smoothScroll'
import { ProfileSvg } from '../wave/ProfileSvg'
import { TAU_MAX, TAU_MIN, stageAt } from '../../lib/profileGeometry'

export function WaveLife() {
  const ref = useRef<HTMLDivElement>(null)
  const reduce = useReducedMotion()
  const [p, setP] = useState(0)
  const [narrow, setNarrow] = useState(() => window.matchMedia('(max-width: 640px)').matches)
  useEffect(() => {
    const mq = window.matchMedia('(max-width: 640px)')
    const on = () => setNarrow(mq.matches)
    mq.addEventListener('change', on)
    return () => mq.removeEventListener('change', on)
  }, [])
  const { scrollYProgress } = useScroll({ target: ref, offset: ['start start', 'end end'] })
  useMotionValueEvent(scrollYProgress, 'change', (v) => {
    // one render per 0.25 % of progress, not one per scroll event
    const q = Math.round(v * 400) / 400
    setP((old) => (old === q ? old : q))
  })

  const tau = TAU_MIN + (TAU_MAX - TAU_MIN) * Math.min(1, Math.max(0, p))
  const goToStage = (i: number) => {
    const target = (STAGES[i].tau - TAU_MIN) / (TAU_MAX - TAU_MIN)
    const el = ref.current
    if (reduce || !el) return setP(target)
    const top = el.getBoundingClientRect().top + window.scrollY
    scrollToY(top + target * (el.offsetHeight - window.innerHeight))
  }
  const stageIndex = stageAt(tau)
  const stage = STAGES[stageIndex]

  return (
    <section id="vague" ref={ref} className="relative bg-abyss" style={{ height: reduce ? 'auto' : '360vh' }} aria-labelledby="vague-titre">
      <div className={`${reduce ? 'min-h-svh' : 'sticky top-0 h-svh'} flex flex-col overflow-hidden pb-8 pt-24`}>
        <div className="mx-auto grid w-full max-w-[1400px] gap-6 px-4 sm:px-8 lg:grid-cols-[minmax(0,7fr)_minmax(0,5fr)] lg:items-end lg:gap-12">
          <h2 id="vague-titre" className="display text-[clamp(2.3rem,4.6vw,4.2rem)] font-[780] text-foam">
            La vague est le personnage principal
          </h2>
          <p className="max-w-[46ch] text-[1.08rem] leading-relaxed text-foam/75">
            Chaque point de la crête vit la même vie, décalée dans le temps : c’est ce décalage qui fait dérouler le rouleau. Fais défiler pour la regarder naître, se creuser et casser.
          </p>
        </div>

        <div className="relative mt-4 min-h-[38vh] flex-1 px-2 sm:px-6">
          <ProfileSvg
            tau={tau}
            crop={narrow ? [-1.4, 2.3] : undefined}
            className="absolute inset-0 h-full w-full overflow-visible"
            title={`Coupe de la vague : ${stage.name}`}
          />
        </div>

        <div className="mx-auto mt-4 grid w-full max-w-[1400px] gap-6 px-4 sm:px-8 lg:grid-cols-[minmax(0,5fr)_minmax(0,7fr)] lg:items-end lg:gap-12">
          <div aria-live="polite">
            <p className="display text-[clamp(1.7rem,3vw,2.5rem)] font-[760] text-lagoon">{stage.name}</p>
            <p className="mt-2 max-w-[52ch] text-[1.05rem] leading-relaxed text-foam/80">{stage.text}</p>
          </div>
          <div>
            <ol className="ui grid grid-cols-4 gap-x-3 gap-y-2 text-[0.8rem] sm:grid-cols-8" aria-label="Étapes de la vie d’une vague">
              {STAGES.map((s, i) => (
                <li key={s.key}>
                  <button
                    type="button"
                    onClick={() => goToStage(i)}
                    aria-current={i === stageIndex ? 'step' : undefined}
                    className={`block w-full border-t-2 pt-2 text-left transition-colors duration-500 hover:text-foam ${i === stageIndex ? 'border-lagoon text-foam' : i < stageIndex ? 'border-foam/40 text-foam/65' : 'border-foam/15 text-foam/60'}`}
                  >
                    <span className="block tabular-nums">{String(i + 1).padStart(2, '0')}</span>
                    <span className="block leading-tight">{s.name}</span>
                  </button>
                </li>
              ))}
            </ol>
            <p className="ui mt-4 text-[0.78rem] text-foam/55">
              15 points de contrôle, 10 formes clés, une spline Catmull-Rom centripète : le même profil que dans le jeu, recalculé en direct. Temps depuis la casse : {tau >= 0 ? '+' : ''}
              {tau.toFixed(1)} s
            </p>
          </div>
        </div>
      </div>
    </section>
  )
}
