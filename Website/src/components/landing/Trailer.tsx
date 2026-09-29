import { MEDIA } from '../../content/media'
import { MediaSlot } from '../ui/MediaSlot'

export function Trailer() {
  return (
    <section id="bande-annonce" className="relative bg-abyss pb-8" aria-label="Bande-annonce">
      <div className="mx-auto max-w-[1400px] px-4 sm:px-8">
        <MediaSlot
          kind="video"
          src={MEDIA.trailer.src}
          poster={MEDIA.trailer.poster}
          alt="Bande-annonce de WavyBoard"
          brief={MEDIA.trailer.brief}
          controls
          rounded="rounded-[2rem]"
          className="shadow-[0_40px_120px_-40px_rgba(51,204,230,0.35)] ring-1 ring-foam/10"
        />
      </div>
    </section>
  )
}
