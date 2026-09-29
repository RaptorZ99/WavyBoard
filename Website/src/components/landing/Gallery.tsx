import { MEDIA } from '../../content/media'
import { MediaSlot } from '../ui/MediaSlot'

const LAYOUT = ['md:col-span-7 md:row-span-2', 'md:col-span-5', 'md:col-span-5', 'md:col-span-4', 'md:col-span-4', 'md:col-span-4']
const RATIO = ['16 / 11', '16 / 9', '16 / 9', '4 / 3', '4 / 3', '4 / 3']

export function Gallery() {
  return (
    <section id="galerie" className="relative bg-abyss-2 py-28 sm:py-36" aria-labelledby="galerie-titre">
      <div className="mx-auto max-w-[1400px] px-4 sm:px-8">
        <div className="flex flex-wrap items-end justify-between gap-6">
          <h2 id="galerie-titre" className="display text-[clamp(2.6rem,5.6vw,4.8rem)] font-[780] text-foam">
            Captures
          </h2>
          <p className="ui max-w-[44ch] text-[0.95rem] text-foam/55">Captures in-game, sans retouche, sur un MacBook Apple Silicon.</p>
        </div>
        <div className="mt-12 grid gap-4 md:grid-cols-12">
          {MEDIA.gallery.map((m, i) => (
            <MediaSlot
              key={i}
              kind={m.kind}
              src={m.src}
              alt={m.alt ?? m.brief}
              brief={m.brief}
              caption={m.src ? m.brief : undefined}
              ratio={RATIO[i]}
              className={`${LAYOUT[i]} md:!aspect-auto md:min-h-[220px]`}
            />
          ))}
        </div>
      </div>
    </section>
  )
}
