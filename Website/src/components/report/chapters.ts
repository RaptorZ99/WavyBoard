import type { ChapterMeta } from './kit'

/** The report's chapters. Background deepens to the wall's blue, then turns to foam. */
export const CHAPTERS: ChapterMeta[] = [
  { id: 'en-bref', n: 1, title: 'En bref', short: 'En bref', bg: '#041a30', tau: -3.1 },
  { id: 'projet', n: 2, title: 'Le projet et le choix d’Unity', short: 'Le choix d’Unity', bg: '#051e36', tau: -2.6 },
  { id: 'cadre', n: 3, title: 'Notre cadre d’utilisation de l’IA', short: 'Notre cadre', bg: '#06213c', tau: -2.0 },
  { id: 'iagraphie', n: 4, title: 'IAgraphie : quels modèles, pour quoi faire', short: 'IAgraphie', bg: '#072541', tau: -1.5 },
  { id: 'veille', n: 5, title: 'La veille : ce qu’on a cherché, ce qu’on en a tiré', short: 'La veille', bg: '#082946', tau: -0.9 },
  { id: 'prompt', n: 6, title: 'Le prompt initial et le prompt engineering', short: 'Le prompt', bg: '#092d4b', tau: -0.3 },
  { id: 'cli', n: 7, title: 'Comment l’IA pilote Unity', short: 'L’IA pilote Unity', bg: '#0a3150', tau: 0.3 },
  { id: 'boucle', n: 8, title: 'Notre boucle de travail : tester sans jouer', short: 'Tester sans jouer', bg: '#0b3555', tau: 0.8 },
  { id: 'vague', n: 9, title: 'La vague : conception et validation', short: 'La vague', bg: '#0b385a', tau: 1.3 },
  { id: 'cheminement', n: 10, title: 'Le cheminement, commit par commit', short: 'Le cheminement', bg: '#0c3b5e', tau: 1.8 },
  { id: 'menage', n: 11, title: 'Le grand ménage', short: 'Le grand ménage', bg: '#0c3e62', tau: 2.3 },
  { id: 'technique', n: 12, title: 'Comment le jeu fonctionne (vue technique)', short: 'Vue technique', bg: '#0d4166', tau: 2.7 },
  { id: 'limites', n: 13, title: 'Les limites de l’exercice', short: 'Les limites', bg: '#0d436a', tau: 3.0 },
  { id: 'morale', n: 14, title: 'La morale', short: 'La morale', bg: '#f0f7ff', tau: 3.4, light: true },
  { id: 'annexes', n: 15, title: 'Annexes', short: 'Annexes', bg: '#dfedf3', tau: 5.6, lead: 0.5, light: true },
]
