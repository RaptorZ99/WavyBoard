/**
 * Every capture the landing page shows, in one place. All of them come from the game, captured in the open Unity
 * Editor through the CLI: stills with WaveLab.Shot + capture_game_view, videos with the FrameRecorder while a
 * RiderBot rides (see Website/README.md). A slot without `src` renders a placeholder that says what to capture.
 */

import { asset } from '../lib/asset'

export interface Media {
  kind: 'image' | 'video'
  src?: string
  poster?: string
  brief: string
  alt?: string
}

const G = asset('/media/game')

export const MEDIA: {
  trailer: Media
  session: Record<string, Media>
  gallery: Media[]
} = {
  trailer: {
    kind: 'video',
    src: `${G}/trailer.mp4`,
    poster: `${G}/trailer-poster.webp`,
    brief: 'Bande-annonce : du line-up au tube et à l’envol.',
  },
  session: {
    serie: { kind: 'image', src: `${G}/serie.webp`, brief: 'Le rider allongé au pic, une série qui lève au large.' },
    takeoff: { kind: 'video', src: `${G}/takeoff.mp4`, poster: `${G}/takeoff-poster.webp`, brief: 'Le take-off : la face se lève derrière le rider, il bascule dans la pente.' },
    trim: { kind: 'image', src: `${G}/trim.webp`, brief: 'Le rider file le long de la face pendant que la lèvre pitche.' },
    tube: { kind: 'video', src: `${G}/tube.mp4`, poster: `${G}/tube-poster.webp`, brief: 'Dans le tube : la lèvre au-dessus, la sortie en ligne de mire.' },
    envol: { kind: 'video', src: `${G}/envol.mp4`, poster: `${G}/envol-poster.webp`, brief: 'Un air reverse au-dessus de la lèvre, réception sur la face.' },
    sortie: { kind: 'image', src: `${G}/sortie.webp`, brief: 'La sortie : le rider remonte par-dessus l’épaule quand la vague s’éteint.' },
  },
  gallery: [
    { kind: 'image', src: `${G}/lab-tube.webp`, brief: 'Le tube vu de l’intérieur, la lumière qui traverse la lèvre.' },
    { kind: 'image', src: `${G}/lab-channel.webp`, brief: 'Depuis le chenal : la vague qui déroule, horizon ouvert.' },
    { kind: 'image', src: `${G}/tube-close.webp`, brief: 'Réception dans le tube après un El Rollo.' },
    { kind: 'image', src: `${G}/lab-top.webp`, brief: 'Vue du ciel : le rouleau déroule dans un seul sens.' },
    { kind: 'image', src: `${G}/air.webp`, brief: 'Un air reverse au-dessus de la lèvre.' },
    { kind: 'image', src: `${G}/lab-whitewater.webp`, brief: 'L’impact : la lèvre retombe et explose en écume.' },
  ],
}
