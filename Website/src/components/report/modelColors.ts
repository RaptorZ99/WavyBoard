/* Validated categorical palette on the dark report surface (#03172a): one colour per model, always shown with its name. */
export const MODEL_COLORS: Record<string, string> = {
  'Fable 5.1': '#3987e5',
  'Opus 5': '#d95926',
  'Opus 4.8': '#199e70',
  'Opus 5.5': '#c98500',
  'Sonnet 5.5': '#d55181',
}

/** Longest name first, so "Opus 5.5" is never read as "Opus 5". */
export const modelKey = (s: string) =>
  Object.keys(MODEL_COLORS)
    .sort((a, b) => b.length - a.length)
    .find((k) => s.includes(k)) ?? ''
