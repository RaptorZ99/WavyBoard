import { useEffect, useRef } from 'react'

interface Props {
  kind?: 'image' | 'video'
  src?: string
  poster?: string
  alt: string
  /** what should be captured here, shown while the slot is empty */
  brief?: string
  ratio?: string
  className?: string
  rounded?: string
  priority?: boolean
  /** shown over the bottom of a filled slot */
  caption?: string
  /** native video controls (the trailer) */
  controls?: boolean
}

/** Plays a muted looping video only while it is on screen (and loads it only then). */
function InViewVideo(props: { src: string; poster?: string; alt: string; controls?: boolean }) {
  const ref = useRef<HTMLVideoElement>(null)
  useEffect(() => {
    const v = ref.current
    if (!v) return
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    const io = new IntersectionObserver(
      ([e]) => {
        if (e.isIntersecting && !reduce) v.play().catch(() => {})
        else if (!e.isIntersecting) v.pause()
      },
      { threshold: 0.25 },
    )
    io.observe(v)
    return () => io.disconnect()
  }, [])
  return (
    <video
      ref={ref}
      src={props.src}
      poster={props.poster}
      className="absolute inset-0 h-full w-full object-cover"
      muted
      loop
      playsInline
      controls={props.controls}
      preload="none"
      aria-label={props.alt}
    />
  )
}

/**
 * An image or a looping muted video. Until the capture exists, an intentional placeholder
 * says what goes there, so the layout is final from day one.
 */
export function MediaSlot({ kind = 'image', src, poster, alt, brief, ratio = '16 / 9', className = '', rounded = 'rounded-[1.25rem]', priority, caption, controls }: Props) {
  return (
    <figure className={`relative overflow-hidden bg-trench ${rounded} ${className}`} style={{ aspectRatio: ratio }}>
      {src && kind === 'image' && (
        <img src={src} alt={alt} loading={priority ? 'eager' : 'lazy'} decoding="async" className="absolute inset-0 h-full w-full object-cover" />
      )}
      {src && kind === 'video' && (
        <InViewVideo src={src} poster={poster} alt={alt} controls={controls} />
      )}
      {src && caption && (
        <figcaption className="ui pointer-events-none absolute inset-x-0 bottom-0 bg-gradient-to-t from-trench/85 to-transparent px-5 pb-4 pt-12 text-[0.85rem] text-foam/90">
          {caption}
        </figcaption>
      )}
      {!src && (
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-3 p-6 text-center" role="img" aria-label={alt}>
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(90%_70%_at_30%_20%,rgba(51,204,230,0.18),transparent_60%),radial-gradient(70%_60%_at_80%_90%,rgba(13,107,148,0.35),transparent_70%)]" />
          <svg viewBox="0 0 48 48" className="relative h-10 w-10 text-lagoon/70" aria-hidden="true">
            {kind === 'video' ? (
              <path d="M18 14l18 10-18 10z" fill="currentColor" />
            ) : (
              <path d="M6 36c8 0 11-5 14-11 4-8 9-13 17-13 6 0 9 4 9 8" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
            )}
          </svg>
          <p className="ui relative max-w-[32ch] text-sm text-foam/70">{brief ?? alt}</p>
        </div>
      )}
    </figure>
  )
}
