import { SESSION } from '../../content/game'
import { MEDIA } from '../../content/media'
import { Glyph } from '../ui/Glyph'
import { MediaSlot } from '../ui/MediaSlot'

const PAD_GLYPH: Record<string, string> = { 'Stick G': 'L-stick', Croix: 'cross', R2: 'R2', L2: 'L2' }

function Inputs({ items, pad }: { items: readonly string[]; pad: boolean }) {
  return (
    <span className="flex flex-wrap items-center gap-1.5">
      {items.map((it) =>
        pad && PAD_GLYPH[it] ? (
          <Glyph key={it} g={PAD_GLYPH[it]} />
        ) : (
          <span key={it} className="ui inline-flex h-9 items-center rounded-lg bg-foam/[0.08] px-3 text-[0.82rem] font-[650] ring-1 ring-foam/15">
            {it}
          </span>
        ),
      )}
    </span>
  )
}

export function Session() {
  return (
    <section id="session" className="relative bg-abyss py-28 sm:py-36" aria-labelledby="session-titre">
      <div className="mx-auto grid max-w-[1400px] gap-14 px-4 sm:px-8 lg:grid-cols-[minmax(0,4fr)_minmax(0,8fr)]">
        <div className="lg:sticky lg:top-28 lg:self-start">
          <h2 id="session-titre" className="display text-[clamp(2.6rem,5.4vw,4.6rem)] font-[780] text-foam">
            Une vague, de la série à la sortie
          </h2>
          <p className="mt-5 max-w-[40ch] text-[1.15rem] leading-relaxed text-foam/75">
            Pas de niveaux, pas de menus : un pic, des séries, et toi. Tout le jeu tient dans ces six moments.
          </p>
          <p className="ui mt-8 max-w-[38ch] text-[0.9rem] leading-relaxed text-foam/60">
            La vitesse vient de la vague, pas d’un bouton : le rouleau déroule à son rythme et c’est à toi de te placer par rapport à lui.
          </p>
        </div>

        <ol className="space-y-20 sm:space-y-28">
          {SESSION.map((s, i) => (
            <li key={s.id} className="grid gap-6 md:grid-cols-[minmax(0,7fr)_minmax(0,5fr)] md:items-center md:gap-10">
              <MediaSlot
                kind={MEDIA.session[s.id]?.kind ?? 'image'}
                src={MEDIA.session[s.id]?.src}
                poster={MEDIA.session[s.id]?.poster}
                alt={s.title}
                brief={MEDIA.session[s.id]?.brief}
                ratio="16 / 10"
              />
              <div>
                <p className="display text-[4.5rem] font-[300] leading-none text-lagoon/80 tabular-nums" aria-hidden="true">
                  {i + 1}
                </p>
                <h3 className="display mt-2 text-[2rem] font-[760] text-foam">{s.title}</h3>
                <p className="mt-3 text-[1.08rem] leading-relaxed text-foam/75">{s.text}</p>
                <div className="mt-5 space-y-2.5">
                  <div className="flex items-center gap-3">
                    <span className="ui w-16 shrink-0 text-[0.78rem] text-foam/55">Manette</span>
                    <Inputs items={s.pad} pad />
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="ui w-16 shrink-0 text-[0.78rem] text-foam/55">Clavier</span>
                    <Inputs items={s.keys} pad={false} />
                  </div>
                </div>
                <p className="ui mt-4 text-[0.88rem] text-sun/85">{s.hint}</p>
              </div>
            </li>
          ))}
        </ol>
      </div>
    </section>
  )
}
