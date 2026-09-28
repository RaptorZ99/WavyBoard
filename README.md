# WavyBoard — jeu de bodyboard (Unity 6.6, URP)

Prototype jouable : océan, vagues qui déferlent (tube), rider en bodyboard (rame, take-off, ride, pump, air, wipeout), caméra dynamique.
Clavier/souris et manette (PS5 DualSense ou Xbox).

## Ouvrir le projet

1. Installer **Git LFS** (https://git-lfs.com) puis `git lfs install` **avant** de cloner (les textures/sons/FBX sont en LFS).
2. `git clone https://github.com/ForgeOfficial/WavyBoard.git`
3. Installer **Unity 6000.6.0f1** (Unity Hub > Installs > 6000.6.0f1, module *Windows Build Support (Mono)* suffit).
4. Ouvrir le dossier du projet dans Unity Hub. Le premier import prend quelques minutes.
5. (Optionnel, décor) Télécharger les scans Poly Haven (~850 Mo, CC0), exclus du dépôt :
   `powershell -ExecutionPolicy Bypass -File Tools/fetch_polyhaven.ps1` (Python 3 requis). Les fichiers `.meta` sont versionnés, donc les références restent valides.
6. Ouvrir `Assets/_Project/Scenes/Playground.unity` et appuyer sur **Play**.

## Contrôles (prototype)

| Action | Manette | Clavier / souris |
|---|---|---|
| Ramer / diriger | Stick gauche | ZQSD ou WASD / flèches |
| Sprint (rame) | Croix / A maintenu | Espace maintenu |
| Pump (vitesse, au bon rythme en descendant la face) | R2 / RT | Shift |
| Stall (freiner, se caler dans le tube) | L2 / LT | Ctrl |
| Pop / air sur la lèvre, kick-out sur l'épaule | Croix / A | Espace |
| El Rollo (en l'air, juste après avoir quitté la lèvre) | R1 / RB | E |
| Rotation en l'air | Stick droit | Souris |
| Grab | Carré / X | F |
| Drop-knee ↔ prone | L1 / LB | Q (A en AZERTY) |
| Duck dive (rame) | Rond / B | C |
| Reset au lineup | Triangle / Y | R |
| Lancer une vague tout de suite | D-pad haut | N |
| Overlay debug | Select | F1 |

Attends la série (une vague toutes les ~12 s, séries toutes les ~40 s), rame vers la plage quand la vague arrive derrière toi, laisse-toi porter, puis reste près de la lèvre.

## Structure

- `Docs/WAVYBOARD_SPEC.md` : spécification complète (design, architecture, plan). `Docs/AGENT_PLAYBOOK.md` : pilotage par agents IA.
- `Assets/_Project` : code du jeu (`WavyBoard.Runtime`), shader d'eau, scènes, données.
- `Assets/ThirdParty` : contenus gratuits (Storm Breakers CC0, Kenney CC0, Quaternius CC0, Poly Haven CC0, polices OFL) — voir `Docs/ASSETS_MANIFEST.md`.
- `Tools/` : scripts de construction de scène (`unity command run_script`) et de téléchargement.

## Tests automatisés sans joueur

Dans une scène en Play, ajouter le composant `RiderAutoPilot` au GameObject `Rider` : il rame, prend la vague et se cale dans la pocket tout seul (utile pour les captures et les réglages).

## État / limites connues

- Personnage provisoire (capsule) et planche procédurale : la planche définitive (Blender) se branche via `BoardSpec.boardModel`.
- Pas encore de HUD final, de score, ni d'audio ; VFX d'écume/spray à venir.
- Réglages de perf par défaut pensés pour un GPU intégré ; sur un PC puissant, monter la qualité URP (`Assets/Settings/PC_RPAsset`).
