/** URL of a file in public/, under the base the site is served from ("/" locally, "/WavyBoard/" on GitHub Pages). */
export const asset = (path: string) => import.meta.env.BASE_URL + path.replace(/^\//, '')
