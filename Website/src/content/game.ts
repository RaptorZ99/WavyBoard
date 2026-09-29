/**
 * Everything the landing page says about the game. Sourced from the code on main (322108c):
 * WavyBoardActions.inputactions, GameHud.cs, FlickIt.cs, TrickCatalog.cs, RideScorer.cs, WaveSetScheduler.cs.
 */

export const REPO_URL = 'https://github.com/RaptorZ99/WavyBoard'

export const TEAM = [
  { name: 'Alexandre', role: 'Planches DGZ, intégration 3D' },
  { name: 'Maxime', role: 'Prototype, HUD, tuner de vague' },
  { name: 'Louis', role: 'Vague Teahupoo, gameplay, caméra' },
] as const

export const WAVE_SIZES = [
  { name: 'Petite', scale: 0.68, meters: 2.3 },
  { name: 'Moyenne', scale: 0.95, meters: 3.2 },
  { name: 'Grosse', scale: 1.2, meters: 4.1 },
  { name: 'Bombe', scale: 1.45, meters: 4.9 },
] as const

/** A ride, in the order it happens. */
export const SESSION = [
  {
    id: 'serie',
    title: 'Attendre la série',
    text: 'Allongé sur ta planche au pic, tu regardes le large. Une série arrive toutes les 42 secondes : trois vagues, onze secondes d’écart. Petite, moyenne, grosse ou bombe, tu ne sais jamais laquelle.',
    pad: ['Stick G'],
    keys: ['ZQSD'],
    hint: 'Tourne-toi vers la plage quand la vague arrive.',
  },
  {
    id: 'takeoff',
    title: 'Ramer et partir',
    text: 'Rame vers la plage au moment où la face se lève derrière toi. La vague te soulève, t’aligne, et tu bascules dans la pente : c’est le take-off.',
    pad: ['Stick G', 'Croix'],
    keys: ['Z', 'Espace'],
    hint: 'Croix maintenu pour sprinter en rame.',
  },
  {
    id: 'trim',
    title: 'Tenir la ligne',
    text: 'Ta vitesse, c’est celle de la vague. Stick en avant pour filer plus vite que le rouleau, en arrière pour le laisser revenir. R2 en rythme pour pomper, pas en martelant.',
    pad: ['Stick G', 'R2'],
    keys: ['Z / S', 'Maj'],
    hint: 'Le rouleau déroule entre 4,5 et 8 m/s selon la section.',
  },
  {
    id: 'tube',
    title: 'Entrer dans le tube',
    text: 'Cale-toi avec L2 : le rouleau te rattrape et la lèvre se referme au-dessus de toi. Le chrono du tube tourne. Quand ça ferme, stick en avant et pump pour sortir avant l’impact.',
    pad: ['L2', 'Stick G', 'R2'],
    keys: ['Ctrl', 'Z', 'Maj'],
    hint: 'L2 + tourne : demi-tour serré dans le tube.',
  },
  {
    id: 'envol',
    title: 'S’envoler de la lèvre',
    text: 'Monte la face vite. Arrivé en haut, la lèvre te catapulte : c’est l’envol. Tu as quelques dixièmes de seconde pour lancer ta figure au stick droit, puis viser la réception.',
    pad: ['Stick D ↓', 'Stick D ↑'],
    keys: ['Souris ↓', 'Souris ↑'],
    hint: 'Pose-toi à plat, nez ou tail devant : les deux passent.',
  },
  {
    id: 'sortie',
    title: 'Sortir proprement',
    text: 'Quand la vague s’éteint, cale-toi et passe par-dessus l’épaule. Ta vague reçoit une note sur 10 ; seules tes deux meilleures comptent, comme en compétition.',
    pad: ['L2', 'Stick G'],
    keys: ['Ctrl', 'Z'],
    hint: 'Une chute garde 80 % des points de la vague.',
  },
] as const

export type Zone = 'plat' | 'face' | 'levre' | 'tube' | 'air'

export const ZONES: { id: Zone; name: string; text: string }[] = [
  { id: 'plat', name: 'À plat', text: 'Dans le line-up, en attendant la série.' },
  { id: 'face', name: 'Sur la face', text: 'Pendant que tu dessines ta ligne.' },
  { id: 'levre', name: 'Sur la lèvre', text: 'Le tremplin : en haut de la face, près du rouleau.' },
  { id: 'tube', name: 'Dans le tube', text: 'Même vocabulaire que la lèvre, 40 % de points en plus.' },
  { id: 'air', name: 'En l’air', text: 'Les gestes s’empilent sur la rotation en cours.' },
]

/** Stick path in stick space ([-1, 1]², y up), sampled by the animation. */
export type Path = [number, number][]

const arc = (from: number, to: number, r = 0.92, steps = 10): Path => {
  const out: Path = []
  for (let i = 0; i <= steps; i++) {
    const a = ((from + ((to - from) * i) / steps) * Math.PI) / 180
    out.push([Math.cos(a) * r, Math.sin(a) * r])
  }
  return out
}

const DOWN: Path = [[0, 0], [0, -0.5], [0, -0.92], [0, -0.92]]
const UP: Path = [[0, -0.2], [0, 0.5], [0, 0.95]]

export type Motion = 'pop' | 'roll' | 'spin' | 'flip' | 'invert' | 'grab' | 'carve' | 'duck'

export interface Trick {
  name: string
  zone: Zone
  gesture: string
  points: number | string
  path: Path
  motion: Motion
  /** degrees of the board's rotation shown in the demo */
  turns?: number
}

export const TRICKS: Trick[] = [
  // flat
  { name: 'Hop', zone: 'plat', gesture: '↓ puis ↑', points: 25, path: [...DOWN, ...UP], motion: 'pop' },
  { name: 'Hop roll', zone: 'plat', gesture: '↓ puis ↗', points: 60, path: [...DOWN, [0.4, 0.2], [0.8, 0.7]], motion: 'roll', turns: 360 },
  { name: 'Rollo à plat', zone: 'plat', gesture: 'un tour complet du stick', points: 90, path: arc(-90, 270), motion: 'roll', turns: 360 },
  { name: 'Canard', zone: 'plat', gesture: '↑ puis ↓ sec (ou Rond)', points: '—', path: [[0, 0], [0, 0.6], [0, 0.95], [0, 0], [0, -0.95]], motion: 'duck' },
  // face
  { name: 'Snap', zone: 'face', gesture: '↓ puis ↗', points: 70, path: [...DOWN, [0.4, 0.2], [0.8, 0.7]], motion: 'carve', turns: 95 },
  { name: 'Cutback', zone: 'face', gesture: '↓, quart de tour, ↑', points: 110, path: [...DOWN, ...arc(-90, 0), [0.5, 0.6], [0, 0.95]], motion: 'carve', turns: 175 },
  { name: 'Spinner 360', zone: 'face', gesture: '↓, demi-tour, ↑', points: '90 +', path: [...DOWN, ...arc(-90, 90)], motion: 'spin', turns: 360 },
  { name: 'Bottom turn', zone: 'face', gesture: '↑ puis ↓', points: 25, path: [[0, 0], [0, 0.7], [0, 0.95], [0, 0], [0, -0.95]], motion: 'carve', turns: 60 },
  // lip
  { name: 'Air', zone: 'levre', gesture: '↓ puis ↑ en haut de la face', points: 140, path: [...DOWN, ...UP], motion: 'pop' },
  { name: 'El Rollo', zone: 'levre', gesture: '↓ puis ↗ ou ↖', points: 260, path: [...DOWN, [0.4, 0.2], [0.8, 0.7]], motion: 'roll', turns: 360 },
  { name: 'Air reverse 360', zone: 'levre', gesture: '↓, enroule vers la droite, ↑', points: '120 +', path: [...DOWN, ...arc(-90, 90)], motion: 'spin', turns: 360 },
  { name: 'ARS', zone: 'levre', gesture: 'tour complet rapide', points: 340, path: arc(-90, 270, 0.92, 14), motion: 'roll', turns: 360 },
  { name: 'Backflip', zone: 'levre', gesture: 'charge ↓, puis tour complet', points: 380, path: [...DOWN, [0, -0.92], ...arc(-90, 270, 0.92, 14)], motion: 'flip', turns: 360 },
  { name: 'Invert', zone: 'levre', gesture: '↑ puis ↓ sec', points: 300, path: [[0, 0], [0, 0.7], [0, 0.95], [0, 0], [0, -0.95]], motion: 'invert', turns: 150 },
  // tube
  { name: 'Sortie de tube', zone: 'tube', gesture: '↓ puis ↑ dans le tube', points: 200, path: [...DOWN, ...UP], motion: 'pop' },
  { name: 'Relance', zone: 'tube', gesture: '↑ puis ↓', points: 30, path: [[0, 0], [0, 0.7], [0, 0.95], [0, 0], [0, -0.95]], motion: 'carve', turns: 30 },
  // air
  { name: '+360', zone: 'air', gesture: 'enroule en vol', points: '50 +', path: arc(-90, 90), motion: 'spin', turns: 360 },
  { name: '+Rollo', zone: 'air', gesture: 'tour complet ou ↗ en vol', points: 120, path: [[0, 0], [0.4, 0.2], [0.8, 0.7]], motion: 'roll', turns: 360 },
  { name: 'Grab', zone: 'air', gesture: 'garde le stick tendu', points: '× 1,15', path: [[0, 0], [0.9, -0.3], [0.9, -0.3], [0.9, -0.3], [0.9, -0.3]], motion: 'grab' },
  { name: 'Viser la réception', zone: 'air', gesture: '↑ en l’air', points: '—', path: [[0, 0], [0, 0.95], [0, 0.95]], motion: 'pop' },
]

export type Glyph = string

export interface ControlRow {
  action: string
  detail?: string
  pad: Glyph[]
  keys: Glyph[]
}

export const CONTROL_GROUPS: { title: string; rows: ControlRow[] }[] = [
  {
    title: 'Ramer',
    rows: [
      { action: 'Ramer, tourner', pad: ['L-stick'], keys: ['Z', 'Q', 'S', 'D'], detail: 'ou WASD, ou les flèches' },
      { action: 'Sprint en rame', pad: ['cross'], keys: ['Espace'], detail: 'maintenu' },
      { action: 'Canard sous la mousse', pad: ['circle'], keys: ['C'] },
    ],
  },
  {
    title: 'Surfer',
    rows: [
      { action: 'Filer / freiner dans la ligne', pad: ['L-stick'], keys: ['Z', 'S'] },
      { action: 'Pump', pad: ['R2'], keys: ['Maj'], detail: 'en rythme, pas en martelant' },
      { action: 'Caler, le tube vient à toi', pad: ['L2'], keys: ['Ctrl'] },
      { action: 'Demi-tour serré (pivot)', pad: ['L2', 'L-stick'], keys: ['Ctrl', 'Q', 'D'] },
      { action: 'Sortir de la vague', pad: ['L2'], keys: ['Ctrl'], detail: 'et passe par-dessus l’épaule' },
      { action: 'Drop-knee / allongé', pad: ['L1'], keys: ['A'], detail: 'appui court (Q en QWERTY)' },
    ],
  },
  {
    title: 'Figures',
    rows: [
      { action: 'La planche', pad: ['R-stick'], keys: ['Souris'], detail: 'charge ↓, puis flick ↑' },
      { action: 'Pop à la souris', pad: [], keys: ['Clic G'], detail: 'maintenu = charge' },
      { action: 'Tourner en l’air', pad: ['L-stick'], keys: ['Q', 'D'] },
      { action: 'Regarder autour', pad: ['L1', 'R-stick'], keys: ['A', 'Souris'], detail: 'maintenu' },
    ],
  },
  {
    title: 'Session',
    rows: [
      { action: 'Revenir au line-up', pad: ['triangle'], keys: ['R'] },
      { action: 'Appeler une vague', pad: ['dpad-up'], keys: ['N'] },
      { action: 'Afficher les figures', pad: [], keys: ['H'] },
    ],
  },
]

export const FEATURES = [
  { value: '2,3 → 4,9 m', label: 'quatre tailles de série, de la petite à la bombe' },
  { value: '30 +', label: 'figures au stick droit, qui changent selon la zone' },
  { value: '50 Hz', label: 'une physique fixe, extrapolée à chaque image' },
] as const
