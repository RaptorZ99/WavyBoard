import { B, Body, C, Callout, Chip, CountUp, Lead, P, Part, Pull } from '../kit'
import { Icon, type IconName } from '../icons'

/* ============================================================== 01 En bref */

const KPIS = [
  ['21 jours', 'du premier commit (8 septembre) à la version finale (29 septembre)'],
  ['58 commits', 'sur 8 branches, dont 56 co-signés par un modèle Claude'],
  ['4 modèles', 'Fable 5.1, Opus 4.8, Opus 5 et Opus 5.5'],
  ['6 vagues', 'cinq approches abandonnées avant la vague finale'],
  ['721 captures', 'prises par les agents pour « voir » le jeu'],
  ['8 845 lignes', 'de C# dans le jeu final (48 fichiers), plus un shader de 449 lignes'],
  ['22 tests', 'automatiques qui rident des vagues entières sans lancer le jeu'],
  ['−1 013 998 lignes', 'supprimées lors du grand ménage final'],
] as const

export function Brief() {
  return (
    <>
      <div className="grid gap-12 lg:grid-cols-[minmax(0,6fr)_minmax(0,6fr)] lg:gap-16">
        <div className="space-y-7">
          <p className="max-w-[40ch] font-serif text-[clamp(1.35rem,2.1vw,1.75rem)] font-[340] leading-[1.4] text-foam">
            WavyBoard est un jeu de bodyboard réalisé sous Unity 6.6. On y rame vers une vague lourde inspirée de Teahupoo, on se cale dans le tube, on court contre le rouleau et on s’envole de la lèvre pour enchaîner des figures au stick droit. Il se joue à la manette PS5 ou au clavier et à la souris.
          </p>
          <P>
            Nous n’avions jamais utilisé Unity. Nous avons fait écrire la quasi-totalité du jeu par des agents IA (Claude Fable 5.1, Opus 4.8, Opus 5 puis Opus 5.5), qui pilotaient l’éditeur Unity ouvert en ligne de commande. Notre rôle : cadrer, fournir les références, juger, jouer, et décider quand repartir de zéro.
          </P>
          <P>Ce rapport raconte ce test grandeur nature : ce que nous avons demandé, comment l’IA s’y est prise, les cinq vagues jetées avant la bonne, et ce que nous en retenons.</P>
        </div>
        <figure className="relative lg:-mr-8 lg:mt-2">
          <div className="absolute -inset-10 bg-[radial-gradient(closest-side,rgba(51,204,230,0.14),transparent)]" aria-hidden="true" />
          <img decoding="async"
            width={1600}
            height={885}
            src="/media/evolution/11-jeu-final.webp"
            alt="Le jeu final : dans le tube, avec le HUD"
            className="relative h-auto w-full rounded-[1.6rem] shadow-[0_40px_90px_-30px_rgba(0,0,0,0.7)] ring-1 ring-foam/15"
            loading="lazy"
          />
          <figcaption className="ui relative mt-3 text-[0.85rem] text-foam/70">Le jeu final : dans le tube, avec le HUD</figcaption>
        </figure>
      </div>

      <div className="mt-20 overflow-hidden rounded-[1.8rem] bg-[linear-gradient(135deg,rgba(51,204,230,0.14),rgba(3,23,42,0.5))] p-px">
        <dl className="grid grid-cols-2 gap-px overflow-hidden rounded-[1.75rem] bg-foam/10 lg:grid-cols-4">
          {KPIS.map(([v, l]) => (
            <div key={v} className="group relative bg-trench/90 p-6 transition-colors duration-500 hover:bg-[#062a48] sm:p-8">
              <dt className="display text-[clamp(1.55rem,2.8vw,2.5rem)] font-[790] leading-none text-foam">
                <CountUp value={v} />
              </dt>
              <dd className="mt-3 text-[0.98rem] leading-snug text-foam/65">{l}</dd>
              <span className="absolute inset-x-6 bottom-0 h-px origin-left scale-x-0 bg-lagoon transition-transform duration-500 group-hover:scale-x-100 sm:inset-x-8" />
            </div>
          ))}
        </dl>
      </div>
    </>
  )
}

/* ============================================================== 02 Le projet et le choix d'Unity */

const PRIMER: { icon: IconName; title: string; text: React.ReactNode }[] = [
  { icon: 'editor', title: 'L’éditeur', text: <>est une application lourde qui garde un état : la scène ouverte, les objets sélectionnés, les ressources importées. Le jeu se lance dans l’éditeur (« Play mode »).</> },
  { icon: 'scene', title: 'Une scène', text: <>contient des <B>GameObjects</B>, auxquels on attache des <B>composants</B> : un script C#, un maillage, une caméra, une lumière.</> },
  { icon: 'file', title: 'Les ressources', text: <>(scènes, prefabs, matériaux) sont des fichiers YAML pleins d’identifiants internes. On ne les édite pas à la main : on passe par l’éditeur.</> },
  { icon: 'render', title: 'Le rendu', text: <>passe par un pipeline (ici URP) et des <B>shaders</B> écrits en HLSL, qui s’exécutent sur la carte graphique.</> },
  { icon: 'burst', title: 'Burst et le Job System', text: <>compilent du C# spécialisé en code natif parallèle : indispensable pour recalculer une vague à chaque image.</> },
  { icon: 'recompile', title: 'Chaque modification de script', text: <>déclenche une recompilation et un rechargement : on compte en dizaines de secondes, pas en millisecondes comme en web.</> },
]

const MECHANICS: { icon: IconName; title: string; text: React.ReactNode }[] = [
  { icon: 'sets', title: 'Les séries', text: <>une série toutes les 42 secondes, trois vagues à 11 secondes d’écart, en quatre tailles (2,3 m, 3,2 m, 4,1 m et 4,9 m).</> },
  { icon: 'takeoff', title: 'Le take-off', text: <>on rame vers la plage quand la face se lève derrière soi ; la vague soulève et aligne le rider.</> },
  { icon: 'speed', title: 'La vitesse', text: <>vient de la vague : le rouleau déroule entre 4,5 et 8 m/s selon la section, et le stick gauche règle le placement par rapport à lui (+32 % en avant, −22 % en arrière).</> },
  { icon: 'pump', title: 'Le pump', text: <>(R2) donne de la vitesse s’il est fait en rythme ; le <B>stall</B> (L2) freine pour laisser le tube se refermer au-dessus de soi ; stall et direction ensemble font pivoter la planche pour un demi-tour serré.</> },
  { icon: 'envol', title: 'L’envol', text: <>en montant la face assez vite, la lèvre catapulte le rider en l’air.</> },
  { icon: 'stick', title: 'Les figures', text: <>se font au stick droit, façon Skate : on charge vers le bas, on relance vers le haut, et le chemin du pouce choisit la figure (Air, El Rollo, Air reverse, ARS, Backflip, Invert, Grab…). Le même geste change de sens selon la zone : à plat, sur la face, sur la lèvre, dans le tube ou en l’air.</> },
  { icon: 'score', title: 'Le score', text: <>chaque vague reçoit une note sur 10, et seules les deux meilleures comptent, comme en compétition.</> },
]

export function Project() {
  return (
    <div className="space-y-24 sm:space-y-32">
      <Part title="Ce que nous voulions">
        <Lead className="max-w-[40ch]">
          Le cahier des charges tenait en quelques phrases : un personnage en position bodyboard dans un océan, des vagues de plusieurs tailles, la possibilité de ramer, de surfer allongé, d’aller dans les rouleaux et de faire des figures. Jouable au clavier et à la souris comme à la manette PS5. Fluide, beau, et surtout amusant.
        </Lead>
      </Part>

      <Part title="Pourquoi Unity">
        <P>
          Nous avons choisi Unity précisément parce qu’aucun de nous trois ne le connaissait. Faire un site web ou un script Python avec une IA, nous savions que ça marchait. La question intéressante était :
        </P>
        <Pull className="my-12">
          que se passe-t-il quand on confie à des agents une technologie que l’on ne maîtrise pas soi-même, et sur laquelle les IA sont elles-mêmes moins à l’aise ?
        </Pull>
        <div className="grid items-start gap-8 lg:grid-cols-[minmax(0,7fr)_minmax(0,5fr)]">
          <P>
            Le moment s’y prêtait. Unity 6.6 venait d’ouvrir son éditeur aux agents : une ligne de commande (<C>unity</C>, version 1.0 bêta) et un paquet « Pipeline » (version 0.6, expérimental) qui exposent environ 517 commandes pour piloter un éditeur ouvert. Nous voulions savoir jusqu’où cela tenait.
          </P>
          <div className="rounded-[1.4rem] bg-trench/60 p-6 ring-1 ring-foam/12">
            <p className="display text-[3.2rem] font-[800] leading-none text-lagoon">
              <CountUp value="517" />
            </p>
            <p className="ui mt-2 text-[0.9rem] text-foam/70">commandes pour piloter un éditeur ouvert</p>
            <div className="mt-4 flex flex-wrap gap-2">
              <Chip>unity 1.0 bêta</Chip>
              <Chip>Pipeline 0.6</Chip>
            </div>
          </div>
        </div>
      </Part>

      <Part title="Unity en deux minutes (pour comprendre la suite)">
        <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {PRIMER.map((c) => (
            <li key={c.title} className="group rounded-[1.4rem] bg-foam/[0.05] p-6 ring-1 ring-foam/10 transition-[transform,background-color] duration-500 hover:-translate-y-1 hover:bg-foam/[0.08]">
              <span className="inline-flex h-11 w-11 items-center justify-center rounded-xl bg-lagoon/15 text-lagoon ring-1 ring-lagoon/30">
                <Icon name={c.icon} />
              </span>
              <p className="mt-4 text-[1.02rem] leading-relaxed text-foam/78">
                <B>{c.title}</B> {c.text}
              </p>
            </li>
          ))}
        </ul>
        <Callout>
          <p className="font-serif text-[1.3rem] leading-snug text-foam">
            C’est tout ce qui rend Unity difficile pour une IA : beaucoup d’état caché hors du code, des fichiers qu’on ne peut pas écrire directement, et un résultat qui ne se juge qu’en le regardant et en le jouant.
          </p>
        </Callout>
      </Part>

      <Part title="Le jeu, en une phrase par mécanique">
        <ol className="divide-y divide-foam/10 border-y border-foam/10">
          {MECHANICS.map((m) => (
            <li key={m.title} className="grid gap-3 py-6 sm:grid-cols-[minmax(0,15rem)_1fr] sm:gap-8">
              <p className="flex items-center gap-3">
                <span className="text-lagoon">
                  <Icon name={m.icon} className="h-7 w-7" />
                </span>
                <span className="display text-[1.35rem] font-[760] text-foam">{m.title}</span>
              </p>
              <p className="text-[1.06rem] leading-relaxed text-foam/78">{m.text}</p>
            </li>
          ))}
        </ol>
      </Part>
    </div>
  )
}

/* ============================================================== 03 Notre cadre */

const RULES: { title: React.ReactNode; text: React.ReactNode; files?: string[] }[] = [
  {
    title: 'Une spécification écrite avant la première ligne de code.',
    text: <>Le document <C>Docs/WAVYBOARD_SPEC.md</C> (708 lignes, 23 sections) était la source de vérité : design, architecture, plan en neuf phases (P0 à P8), critères d’acceptation, risques.</>,
  },
  {
    title: 'Un fichier de règles lu par chaque agent au démarrage',
    text: <>(<C>CLAUDE.md</C>, 14 lignes) : lire la spec et le guide avant toute action, ne jamais éditer une scène ou un prefab à la main, recompiler puis lire la console après chaque changement de script, ranger le code du jeu dans <C>Assets/_Project/</C>.</>,
  },
  {
    title: 'Un guide de pilotage de l’éditeur',
    text: <>(<C>Docs/AGENT_PLAYBOOK.md</C>) : les commandes vérifiées, les pièges connus, et une règle forte, « revue visuelle obligatoire en fin de tâche graphique ».</>,
  },
  {
    title: 'Ce qui revient à l’humain',
    text: <>était listé à part (<C>Docs/USER_ACTIONS.md</C>) : téléchargements sur l’Asset Store, modules à installer, branchement de la manette, décisions produit. Chaque point indiquait la phase où il devenait nécessaire et le repli prévu, pour que l’humain ne bloque jamais les agents.</>,
  },
  {
    title: 'Git comme mémoire partagée.',
    text: <>Une branche par tentative, un commit par étape vérifiée, le modèle utilisé en signature de chaque commit (<C>Co-Authored-By</C>). Avant chaque redémarrage, un commit de sauvegarde : rien n’a jamais été perdu.</>,
  },
  {
    title: 'Une mémoire entre les sessions.',
    text: <>L’agent tenait des notes persistantes (pièges du CLI, direction artistique de la vague, choix de gameplay, « étiquette » des tests) relues à chaque nouvelle session.</>,
  },
]

const QUOTES = ['« je peux pas remonter la vague »', '« la caméra est dans la texture du tube »', '« un mur avec un parasol au-dessus »']

export function Framework() {
  return (
    <div className="space-y-20">
      <Body>
        <Lead className="max-w-[30ch]">Nous n’avons pas « demandé un jeu » à une IA. Nous avons posé des règles, et ce sont elles qui ont rendu le projet possible.</Lead>
      </Body>
      <ol className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {RULES.map((r, i) => (
          <li key={i} className="relative overflow-hidden rounded-[1.6rem] bg-foam/[0.05] p-7 ring-1 ring-foam/10">
            <span
              className="display pointer-events-none absolute -right-2 -top-6 text-[7rem] font-[800] leading-none text-foam/[0.05]"
              style={{ fontVariationSettings: "'wdth' 125" }}
              aria-hidden="true"
            >
              {i + 1}
            </span>
            <span className="display text-[1rem] font-[800] tabular-nums text-lagoon">{String(i + 1).padStart(2, '0')}</span>
            <p className="mt-3 text-[1.04rem] leading-relaxed text-foam/78">
              <B>{r.title}</B> {r.text}
            </p>
          </li>
        ))}
      </ol>
      <div className="grid items-center gap-12 lg:grid-cols-[minmax(0,7fr)_minmax(0,5fr)]">
        <P className="text-[1.2rem]">
          Notre rôle à nous : <B>directeurs artistiques, testeurs manette en main et arbitres.</B> Nous fournissions les références (deux photos de Teahupoo ont tout changé), nous jouions, et nous décrivions ce qui n’allait pas avec nos mots : « je peux pas remonter la vague », « la caméra est dans la texture du tube », « un mur avec un parasol au-dessus ».
        </P>
        <div className="relative h-[260px]" aria-hidden="true">
          {QUOTES.map((q, i) => (
            <p
              key={q}
              className={`absolute max-w-[18rem] rounded-[1.3rem] px-5 py-3.5 font-serif text-[1.08rem] italic shadow-[0_20px_40px_-20px_rgba(0,0,0,0.6)] motion-safe:animate-[bob_ease-in-out_infinite] ${
                i === 1 ? 'bg-sun text-abyss' : 'bg-foam text-abyss'
              }`}
              style={{ left: `${[2, 30, 8][i]}%`, top: `${[4, 38, 72][i]}%`, animationDuration: `${5 + i}s`, animationDelay: `${i * 0.8}s` }}
            >
              {q}
            </p>
          ))}
        </div>
      </div>
    </div>
  )
}
