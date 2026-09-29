# WavyBoard — site web

Landing page du jeu et rapport de projet. React 19, TypeScript, Vite, Tailwind CSS 4, three.js, Motion.

```bash
npm install
npm run dev      # http://localhost:5173
npm run build    # site statique dans dist/
npm run preview
```

## Pages

- `/` : la landing page. La vague 3D du hero est un portage WebGL du profil de vague du jeu
  (`src/lib/waveProfile.ts` reprend `Assets/_Project/Scripts/Wave/WaveProfile.cs` et `Tools/wave_profile_lab.py`).
- `/rapport` : le rapport de projet, mis en page pour le web. Tout le texte est écrit en dur dans
  `src/components/report/chapters/` (le site ne lit aucun fichier externe). Le rapport se lit
  comme la vie d’une vague : il s’ouvre dans le tube (WebGL), chaque chapitre commence par le profil de la vague du jeu un
  peu plus tard dans sa vie, et les derniers chapitres finissent dans l’écume.

## Médias

Toutes les captures de la landing sont déclarées dans `src/content/media.ts`. Un emplacement sans `src` affiche un
placeholder qui décrit la capture attendue ; déposer le fichier dans `public/media/` et renseigner `src`.

Les captures du jeu ont été prises dans l’éditeur Unity ouvert, par le CLI :

- images fixes : `WavyBoard.EditorTools.WaveLab.Shot(...)` puis `unity command capture_game_view` ;
- vidéos : `WavyBoard.Debugging.FrameRecorder` (images JPEG à pas de temps fixe pendant qu’un `RiderBot` ride),
  assemblées avec ffmpeg.
