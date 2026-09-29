import { useState } from 'react'
import { AnimatePresence, motion } from 'motion/react'
import { CONTROL_GROUPS } from '../../content/game'
import { Glyph } from '../ui/Glyph'

type Device = 'pad' | 'keys'

export function Controls() {
  const [device, setDevice] = useState<Device>('pad')
  return (
    <section id="commandes" className="relative bg-abyss py-28 sm:py-36" aria-labelledby="commandes-titre">
      <div className="mx-auto max-w-[1400px] px-4 sm:px-8">
        <div className="flex flex-wrap items-end justify-between gap-8">
          <div>
            <h2 id="commandes-titre" className="display text-[clamp(2.6rem,5.6vw,4.8rem)] font-[780] text-foam">
              Commandes
            </h2>
            <p className="mt-4 max-w-[52ch] text-[1.1rem] leading-relaxed text-foam/75">
              Pensé pour la DualSense (vibrations comprises), jouable au clavier et à la souris : la souris devient un stick virtuel, les mêmes gestes marchent partout.
            </p>
          </div>
          <div role="radiogroup" aria-label="Périphérique" className="ui inline-flex rounded-full bg-foam/[0.06] p-1 ring-1 ring-foam/15">
            {(
              [
                ['pad', 'Manette'],
                ['keys', 'Clavier et souris'],
              ] as const
            ).map(([id, label]) => (
              <button
                key={id}
                type="button"
                role="radio"
                aria-checked={device === id}
                onClick={() => setDevice(id)}
                className="relative rounded-full px-5 py-2.5 text-[0.95rem] font-[650]"
              >
                {device === id && (
                  <motion.span layoutId="device-pill" className="absolute inset-0 rounded-full bg-foam" transition={{ type: 'spring', bounce: 0.2, duration: 0.5 }} />
                )}
                <span className={`relative ${device === id ? 'text-abyss' : 'text-foam/75'}`}>{label}</span>
              </button>
            ))}
          </div>
        </div>

        <div className="mt-14 grid gap-x-10 gap-y-12 md:grid-cols-2 xl:grid-cols-4">
          {CONTROL_GROUPS.map((g) => (
            <div key={g.title}>
              <h3 className="display border-b border-foam/15 pb-3 text-[1.5rem] font-[740] text-foam">{g.title}</h3>
              <ul className="mt-2">
                {g.rows
                  .filter((r) => (device === 'pad' ? r.pad.length : r.keys.length))
                  .map((r) => (
                    <li key={r.action} className="flex items-center justify-between gap-4 border-b border-foam/[0.07] py-3.5">
                      <span>
                        <span className="block text-[1.02rem] leading-snug text-foam/90">{r.action}</span>
                        {r.detail && <span className="ui block text-[0.8rem] text-foam/55">{r.detail}</span>}
                      </span>
                      <AnimatePresence mode="wait" initial={false}>
                        <motion.span
                          key={device}
                          initial={{ opacity: 0, x: 8 }}
                          animate={{ opacity: 1, x: 0 }}
                          exit={{ opacity: 0, x: -8 }}
                          transition={{ duration: 0.22 }}
                          className="flex shrink-0 flex-wrap justify-end gap-1.5"
                        >
                          {(device === 'pad' ? r.pad : r.keys).map((gl, i) => (
                            <Glyph key={gl + i} g={gl} />
                          ))}
                        </motion.span>
                      </AnimatePresence>
                    </li>
                  ))}
              </ul>
            </div>
          ))}
        </div>
        <p className="ui mt-10 max-w-[80ch] text-[0.85rem] leading-relaxed text-foam/55">
          Les touches suivent leur position physique : ZQSD et A sur un clavier AZERTY, WASD et Q en QWERTY. Manette Xbox : mêmes positions que la DualSense. Les vibrations de la DualSense passent par USB.
        </p>
      </div>
    </section>
  )
}
