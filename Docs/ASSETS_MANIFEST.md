# ASSETS MANIFEST — Inventaire des contenus tiers (état au 2026-09-08)

Tout ce qui suit est **déjà dans le projet** sous `Assets/ThirdParty/` (sauf mention « à importer par l'humain »). Licences : CC0 (aucune obligation) ou OFL (polices, garder `OFL.txt`). Le fichier `Assets/ThirdParty/ATTRIBUTIONS.txt` résume les crédits ; le garder à jour.

## 1. Storm Breakers — océan, flottabilité, VFX, audio procédural (CC0)

- Chemin : `Assets/ThirdParty/StormBreakers/` (asmdef `StormBreakers.Runtime`, namespace `StormBreakers`).
- Source : https://github.com/Stormrider31/Storm-Breakers (clone du 2026-09-08 ; le manuel PDF de 100 Mo n'est pas dans le projet → texte dans `Docs/research/StormBreakers_manual_text.txt`).
- Contenu utile :
  - `3-Scripts/Ocean.cs` : modèle de vague statique (4 systèmes, groupes, déferlement par compression) + API `GetHeight / GetNormal / GetVelocity / GetWindVelocity`.
  - `3-Scripts/OceanController.cs` : paramètres (wavelength0..3, intensity0..3, direction0..3, waveDensity0..3, setNumber0..3, breakers factors, particle lighting, terrain), `UpdateWaves/UpdateWind/UpdateLighting`.
  - `WaterInteraction.cs`, `SphereWaterInteraction.cs`, `WaterEffects.cs`, `BoatController.cs`, `Foil.cs`, `HullGenerator.cs`, `BreakersAudio.cs` (audio procédural des déferlantes), `UnderwaterEffect.cs`, `ReflectionProbeController.cs`, `WindController.cs`, `CameraController.cs`.
  - `7-Shaders/*.shadergraph` : `ocean` (vertex = même maths que le CPU), `skybox`, `underwaterEffect`, `waterClippingMask`, `floatingParticlesOpaque/Transparent`, `lateOpaqueAlphaClip`, `hullGeneratorHelper` + sous-graphes.
  - `8-VFX/*.vfx` : `oceanVFX` (bursts/écume/spray des déferlantes, grille primaire/secondaire), `waterInteractionVFX` (splash/écume d'objets), `boatControllerVFX`.
  - `6-Models/Ocean Mesh/*.obj` (maillage océan à densité variable, centre 128×254), `Quad prefabs` (particules flottantes), `Default Hull`.
  - `5-Textures` : `WaveNormals.png` (ripples, source blenderartists), textures d'écume/splash procédurales, `defaultTerrainHeightmap.png`.
  - `9-Audio` : `splash.wav`, `wind.wav`.
  - `2-Prefabs & templates` : `Ocean.prefab`, `Reflection Probe.prefab`, `wind.prefab`, `WaterEffectsEmitter.prefab`, `SphereInteraction.prefab`, `playable boat.prefab`, `NPC boat.prefab`, scene template `oceanTemplate.unity`.
  - `1-Examples` : scènes `surfing.unity` (Epic Surfing), `storm.unity`, `sailingAtSunset.unity`, `fishing.unity`.
- Usage prévu : océan « ambiant » (lineup, houle, horizon), flottabilité en phase de rame, effets de rivage, spray/écume VFX (réutilisés et adaptés pour la SurfWave), audio procédural. Voir spec §4.
- Compatibilité : écrit pour Unity 2021.3 / URP 12 ; à valider/porter sur 6000.6 / URP 17.6 (Shader Graph et VFX s'upgradent à l'import ; `FindObjectOfType` obsolète mais compilable). Consigner tout patch dans `Assets/ThirdParty/StormBreakers/PATCHES.txt`.

## 2. Poly Haven (CC0) — HDRI, textures PBR, modèles photogrammétriques

Chemin : `Assets/ThirdParty/PolyHaven/{hdris,textures,models}/<id>/`. Téléchargés via l'API publique (`https://api.polyhaven.com/files/<id>`) ; réglages d'import à appliquer (spec Phase 1) :

| Type | Id | Usage prévu | Import |
|---|---|---|---|
| HDRI 4k `.hdr` | `secluded_beach` (matin, plage rocheuse) | Skybox/ambiance « Matin » par défaut | Texture Shape = Cube, Mapping = Latitude-Longitude, sRGB off (HDR), Max Size 4096 ; matériau `Skybox/Cubemap` |
| HDRI 4k | `umhlanga_sunrise` | Preset « Aube » | idem |
| HDRI 4k | `fish_hoek_beach` | Preset « Couvert » | idem |
| HDRI 4k | `spiaggia_di_mondello` | Preset « Midi clair » | idem |
| HDRI 4k | `venice_sunset` | Preset « Coucher de soleil » | idem |
| HDRI 4k | `the_sky_is_on_fire` | Preset « Golden hour dramatique » | idem |
| Texture 2k | `coast_sand_02`, `coast_sand_01`, `coast_sand_05`, `aerial_beach_01` | Couches de terrain : sable mouillé/sec, plage vue aérienne | `_diff` sRGB ; `_nor_gl` = Normal map (OpenGL, ne pas inverser Y) ; `_rough`, `_ao`, `_arm`, `_disp` = Linear (sRGB off). URP Terrain Layer : Diffuse + Normal + Mask map (créer la mask map depuis `_arm` : R=AO, G=Roughness→Smoothness inversé, B=Metallic) |
| Texture 2k | `coast_sand_rocks_02`, `coast_land_rocks_01` | Zones rocheuses/mixtes du terrain | idem |
| Texture 2k | `rock_boulder_dry`, `rock_face_03` | Falaises/rochers (matériaux Lit) | idem |
| Texture 2k | `ganges_river_pebbles` | Galets près des rochers | idem |
| Modèle glTF 2k | `coast_rocks_03`, `coast_rocks_05`, `coast_rocks_01`, `coast_land_rocks_04`, `coast_land_rocks_02`, `sand_rocks_small_01`, `boulder_01` | Rochers de plage et de récif (LOD via Mesh LOD Unity 6 si besoin) | Import par **glTFast 6.20.0** (`com.unity.cloud.gltfast`) : matériaux URP Lit générés automatiquement ; vérifier l'échelle (mètres) et activer Read/Write si utilisés par ProBuilder/colliders |
| Modèle glTF 2k | `coastal_cliff_04`, `coastal_cliff_02`, `coast_line_01` | Falaises/lignes de côte en arrière-plan | idem |
| Modèle glTF 2k | `island_tree_01/02/03` | Arbres tropicaux de bord de plage | idem (vérifier alpha cutout des feuilles) |
| Modèle glTF 2k | `grass_bermuda_01` | Touffes d'herbe de dune (Terrain detail ou instancing) | idem |
| Modèle glTF 2k | `ocean_buoy`, `lifebuoy` | Props (bouée de lineup, décor) | idem |

**Attention — densité des modèles Poly Haven (mesurée après import glTFast, 2026-09-08)** : ce sont des scans photogrammétriques très lourds : `boulder_01` 66 k tris, `lifebuoy` 11 k, `ocean_buoy` 12 k, `grass_bermuda_01` 0,9 k, mais `coast_rocks_01/03/05` 680–813 k, `sand_rocks_small_01` 739 k, `coast_line_01` 539 k, `coast_land_rocks_02/04` 1,09–1,29 M, `coastal_cliff_02/04` 0,94–1,54 M, `island_tree_01/02/03` 1,07–2,09 M triangles. **Interdiction de les placer tels quels dans une scène jouable.** Étape obligatoire (spec P1-T5) : `Tools/OptimizePolyHavenModels.cs` (run_script) copie chaque mesh dans `Assets/_Project/Art/Models/PolyHaven/`, génère des Mesh LODs (`UnityEditor.MeshLodUtility.GenerateMeshLods`, API Unity 6.6 vérifiée ; `Mesh.lodCount`, `Mesh.lodSelectionCurve`) et produit un prefab avec matériau URP/Lit (textures diff/nor_gl/arm) ; cible : base ≤ 60 k tris pour rochers/falaises, ≤ 40 k pour arbres, ≤ 20 k pour petits props, et LODs 4 niveaux. Si l'extraction d'un niveau décimé comme mesh de base n'est pas possible avec l'API Mesh LOD, repli : package **UnityMeshSimplifier** (MIT, `https://github.com/Whinarn/UnityMeshSimplifier.git`, décimation quadric) ajouté par git URL. Les arbres (`island_tree_*`) sont candidats au remplacement par des arbres plus légers si le budget ne tient pas (Terrain Sample Asset Pack, action humaine).
Le shader généré par glTFast est `Shader Graphs/glTF-pbrMetallicRoughness` (fonctionnel en URP) ; les prefabs optimisés utilisent `Universal Render Pipeline/Lit`.
API Mesh LOD disponible dans 6000.6 (vérifiée par réflexion) : `UnityEditor.MeshLodUtility.GenerateMeshLods(Mesh mesh, int meshLodLimit = -1)` et `GenerateMeshLods(Mesh mesh, LodGenerationFlags flags, int meshLodLimit = -1)` ; côté runtime `Mesh.lodCount`, `Mesh.lodSelectionCurve`, `Mesh.GetLod(subMesh, level)`, `Mesh.GetLods(subMesh)`, `Mesh.SetLod(subMesh, level, MeshLodRange, MeshUpdateFlags)`, `Mesh.SetLods(...)` (les niveaux sont des plages d'indices `MeshLodRange` dans le même index buffer : pour obtenir un mesh de base décimé, copier la plage d'indices du niveau choisi dans un nouveau Mesh et compacter les sommets). `ModelImporter.generateMeshLods / meshLodGenerationFlags / maximumMeshLod` existent pour les FBX (Poly Haven fournit aussi des FBX via l'API si l'on préfère cette voie).

Licence : `Assets/ThirdParty/PolyHaven/LICENSE-CC0.txt`.

## 3. Quaternius (CC0) — animations humanoïdes et mannequin

- `Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/Unity/AnimationLibrary_Unity_Standard.fbx` : 45 clips « Standard » (locomotion 8 directions, jog, sprint, crawl, swim, idle, sit, death…) sur le **Universal Rig**. Import : Rig = Humanoid (créer l'avatar depuis ce modèle), découper les clips (Animation tab), Loop Time sur les cycles.
- `Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/Unity/UAL2_Standard.fbx` : clips supplémentaires (UAL 2, version Standard).
- `Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/Mannequin_F/Mannequin_F.fbx` : mannequin féminin riggé (Universal Rig) → **personnage rider par défaut** (Humanoid), en attendant les Universal Base Characters (itch, action humaine).
- Licence : `License.txt` dans chaque dossier (CC0).

## 4. Kenney (CC0) — UI, icônes d'entrée, particules, sons

- `Kenney/InputPrompts/{PlayStation,Xbox,KeyboardMouse,Generic,Flairs}/Default/*.png` + sheets `*_sheet_default.png/.xml` + polices d'icônes `Fonts/*.ttf` + `Vector/*.svg` (SVG non importés par Unity sans package Vector Graphics ; conserver pour référence). Usage : glyphes de touches dans le HUD/menus selon la manette détectée (spec §11). Import : Sprite (2D and UI), Filter Bilinear, compression None/High Quality, Mip Maps off ; découper les sheets avec le XML si atlas souhaité.
- `Kenney/ParticlePack/Textures/*.png` (97) et `Kenney/SmokeParticles/Textures/*.png` (77) : sprites de particules (fumée, éclats, cercles, étoiles, traînées) pour le spray/foam/splash et les feedbacks UI. Import : Alpha Is Transparency, sRGB.
- `Kenney/UIPack/PNG/...` (871 fichiers, plusieurs thèmes) : boutons, panneaux, sliders, checkbox pour l'UI Toolkit (9-slice). N'utiliser qu'un thème cohérent (proposition : « Blue »/« Grey » à valider).
- `Kenney/ImpactSounds/Audio`, `InterfaceSounds/Audio`, `UIAudio/Audio` : impacts (bois/verre/métal) et clics UI. Usage : UI + base de « thud » de planche.

## 5. OpenGameArt (CC0) — audio océan

- `OpenGameArt/Audio/beach_wave_01..04_cc0_jasinski.wav` : 4 déferlantes (2–4 s) ; `ocean_ambience_loop_57s_generated.wav` : boucle d'ambiance **générée** (placeholder) ; `ATTRIBUTION.txt`.
- Import : Load Type Streaming pour la boucle, Decompress On Load pour les one-shots ; Force To Mono non (stéréo).

## 6. Polices (OFL)

- `Fonts/BebasNeue-Regular.ttf` (titres/score), `Righteous-Regular.ttf` (logo/trick popups), `Nunito-Variable.ttf` (UI), `Inter-Variable.ttf` (texte/debug) + `OFL-*.txt`. Créer des Font Assets UI Toolkit (TextCore) SDF.

## 7. Samples Unity importés

- `Assets/Samples/Shader Graph/17.6.0/Production Ready Shaders/` : shaders d'eau de référence (`WaterLake`, `WaterSimple_FoamMask` avec sous-graphes Gerstner, `WaterStream`), lit/décal/fullscreen. Utilisation : référence pour le shader `SurfWaveWater` (réfraction Scene Color, brouillard de profondeur, écume, normales défilantes).

## 8. En attente (action humaine, voir `Docs/USER_ACTIONS.md`)

Starter Assets ThirdPerson URP, Unity Particle Pack, Cartoon FX Remaster Free, Terrain Sample Asset Pack, AllSky Free, Skybox Series Free, POLYGON Starter Pack (Asset Store) ; Quaternius Universal Base Characters, KayKit (itch.io) ; Mixamo ; Sonniss.

## 9. Disponibles en staging mais non importés

- `Quaternius/lowpoly_rpg_characters_nov_2020.zip` (CC0, personnages stylisés) — importable si DA cartoon.
- Poly Haven : d'autres ids listés dans `Docs/RESEARCH_SOURCES.md` peuvent être ajoutés avec `tools/polyhaven_dl.py` (copie dans `Tools/polyhaven_dl.py`).

## 10. Règles d'usage

- Aucun asset payant, aucun asset sans licence explicite. Toute nouvelle source → ligne dans ce manifeste + `ATTRIBUTIONS.txt`.
- Les textures 2k suffisent (iGPU) ; ne pas passer en 4k/8k sans preset « Ultra ».
- Les modèles glTF importés par glTFast génèrent des matériaux : les **dupliquer** dans `Assets/_Project/Art/Materials` avant modification (les fichiers générés sont regénérés à l'import).
