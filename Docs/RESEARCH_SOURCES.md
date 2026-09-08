# RESEARCH SOURCES — Ce que nous savons et d'où ça vient (vérifié le 2026-09-08)

Chaque ligne = un fait exploité par la spec + la source. « VERIFIED » = lu sur la page/l'API/le code ; « OBS » = observé sur cette machine/ce projet.

## 1. Unity 6.4 → 6.6 (plateforme)

| Fait | Source |
|---|---|
| Unity 6.6 (6000.6.0f1, sept. 2026) est une « Supported/Update release » : même qualité/support qu'une LTS jusqu'à la sortie de la suivante. 6.3 = LTS (déc. 2025, support jusqu'à déc. 2027). Cadence trimestrielle jusqu'à 6.7 LTS. | https://unity.com/releases/unity-6/support ; https://docs.unity3d.com/6000.6/Documentation/Manual/WhatsNewUnity66.html |
| 6.6 : **Cinemachine, Timeline, Animation Rigging, Performance Testing = core packages** (version = version de l'Editor, 6.6.0). **Burst = module intégré** (2.0.0). Dynamic batching **supprimé**. **Domain reload désactivé par défaut** à l'entrée en Play Mode (« Reload Scene only »). Sérialisation native des `Dictionary<,>`. DXC pour DX12 (Shader Model 6). Project Auditor avec module URP. GPU Resident Drawer visible dans le Profiler. Read/Write des meshes imposé à l'authoring. | WhatsNewUnity66 (VERIFIED) ; https://docs.unity3d.com/6000.6/Documentation/Manual/UpgradeGuideUnity66.html |
| 6.5 : Built-in RP **déprécié** (supporté jusqu'à 6.7 LTS) ; **Mathematics = module intégré** ; Unity Physics (ECS) core ; Shader Graph : nœuds Expression/Switch, génération de nœuds depuis fonctions HLSL ; on-tile post-processing ; logs Editor par projet (`<Project>/Logs/Editor.log`) ; allocateur mimalloc ; EntityId remplace InstanceID. | https://docs.unity3d.com/6000.5/Documentation/Manual/WhatsNewUnity65.html (VERIFIED) |
| 6.4 : **URP Compatibility Mode supprimé → toute passe custom doit utiliser Render Graph** ; ECS en core packages ; DirectStorage Windows ; Project Auditor intégré. | https://docs.unity3d.com/6000.5/Documentation/Manual/WhatsNewUnity64.html (VERIFIED) |
| Depuis Unity 6.1, **DirectX 12 est l'API par défaut des nouveaux projets Windows** ; DX11 recommandé en repli. Sur cette machine l'Editor tourne en **Direct3D11** (OBS, `SystemInfo.graphicsDeviceType`). | https://unity.com/blog/directx-12-improvements-in-unity-6 ; OBS |
| URP n'a **pas** de système d'eau intégré (le Water System est HDRP). | https://discussions.unity.com/t/water-on-urp/918749 ; recherche Asset Store/registre |
| Shader Graph 17.6 « Production Ready Shaders » contient 4 shaders d'eau (WaterLake, WaterSimple_FoamMask avec 3 sous-graphes Gerstner, WaterStream, WaterStreamFalls) : réflexion, réfraction, normales défilantes, brouillard de profondeur, écume. | https://docs.unity3d.com/Packages/com.unity.shadergraph@17.6/manual/Shader-Graph-Sample-Production-Ready-Water.html (VERIFIED) ; liste des samples via `Sample.FindByPackage` (OBS) |
| STP (Spatial-Temporal Post-processing) : upscaler temporel d'URP, compute + TAA, réglé via URP Asset > Quality > Upscaling Filter ; permet Render Scale 0.5–0.75. | https://docs.unity3d.com/6000.6/Documentation/Manual/urp/stp/stp-enable.html |
| GPU Resident Drawer : SRP Batcher + Forward+ + « Instanced Drawing » ; GPU Occlusion Culling s'active ensuite dans le renderer. Shader Stripping : BatchRendererGroup Variants = Keep All. | https://docs.unity3d.com/6000.6/Documentation/Manual/urp/gpu-resident-drawer.html ; https://docs.unity3d.com/6000.6/Documentation/Manual/urp/gpu-culling.html |
| Forward+ : pas de limite de lumières par objet ; se règle dans l'Universal Renderer (Rendering Path). Le template a déjà `PC_Renderer` en Forward+ (`m_RenderingMode: 2`) et GRD activé (OBS). | https://docs.unity3d.com/6000.6/Documentation/Manual/urp/rendering/forward-rendering-paths.html ; https://docs.unity3d.com/6000.6/Documentation/Manual/urp/rendering-paths-comparison.html |
| Render Graph (custom passes) : https://docs.unity3d.com/6000.6/Documentation/Manual/urp/render-graph.html ; URP samples « URP RenderGraph Samples » disponibles (OBS). | docs |
| Rigidbody interpolation + physique dans FixedUpdate + caméra en LateUpdate = anti-jitter standard. | https://docs.unity3d.com/6000.6/Documentation/Manual/rigidbody-interpolation.html |
| Job System / Burst / Mesh.MeshData (génération de maillage sans GC). | https://docs.unity3d.com/6000.6/Documentation/Manual/job-system.html ; https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Mesh.MeshData.html |
| IL2CPP Windows : Visual Studio 2022+ avec compilateurs C++ et Windows 10/11 SDK. Module `windows-il2cpp` non installé ici (OBS). | https://docs.unity3d.com/6000.6/Documentation/Manual/scripting-backends-il2cpp.html ; https://discussions.unity.com/t/installing-visual-studio-2022-with-il2cpp-support-on-unity/899685 |
| Packages compatibles 6000.6 (registre, OBS via `package_search`) : Splines 2.9.0, ProBuilder 6.1.2, Terrain Tools 5.3.3, Recorder 5.1.7, Addressables 4.0.1, glTFast 6.20.0 ; VFX Graph 17.6.0 (built-in). | OBS |

## 2. Input System 1.20 / DualSense

| Fait | Source |
|---|---|
| Input System 1.20.0 (2026-07-21) : correctif « périphériques perdus après upgrade avec fast enter playmode », `GetBindingDisplayString` composites, HID hat switch 8 bits. 1.15–1.16 : `RebindingUISample` enrichi (mode jeu/menu, slider sensibilité souris, `SwapBinding`, `WithSuppressedActionPropagation`). 1.18 : support PS5 sur Linux. | https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/changelog/CHANGELOG.html ; `Docs/research/InputSystem_CHANGELOG_1.20.md` |
| **DualSense (PS5)** : supporté Windows/macOS/Linux via USB HID (`DualSenseGamepadHID`, namespace `UnityEngine.InputSystem.DualShock`) ; vibration (`SetMotorSpeeds`) et lightbar (`SetLightBarColor`) **non supportées en Bluetooth**. Gâchettes adaptatives : non exposées par l'Input System. | https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/supported-devices.html ; https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/api/UnityEngine.InputSystem.DualShock.DualSenseGamepadHID.html |
| Gâchettes adaptatives sur PC : nécessitent d'écrire le rapport HID de sortie ; référence open source **UniSense** (MIT, archivé 2021, `https://github.com/nullkal/UniSense`) et DualSense-Windows (Ohjurot). Option « stretch » seulement. | GitHub API (OBS : archived=true, MIT) |
| Pages manuel utiles : `control-schemes.html`, `about-project-wide-actions.html`, `about-player-input-component.html`, `rebind-action-runtime.html`, `save-load-rebinds.html`, `gamepad-haptics.html`, `gamepads-p.html`, `devices-gamepads.html`. | https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/ |
| Icônes : **Kenney Input Prompts 1.5** (CC0) couvre PS5, Xbox, clavier/souris, Steam Deck… (1 280+ sprites, sheets + XML). | https://kenney.nl/assets/input-prompts (VERIFIED, importé) |

## 3. Océan, vagues, rendu de l'eau

| Fait | Source |
|---|---|
| **Storm Breakers** (Lysandre / The Storm Rider) : package d'océan URP + VFX Graph pour Unity 2021.3+, **open-sourcé sous CC0** en 2024. Modèle de vague par **groupes (sets)** (4 systèmes, amplitude max au centre du groupe, vitesse de groupe = ½ célérité, phase avancée au centre → forme en fer à cheval), **crête pointue 120°, hauteur 0,17 λ**, déformation horizontale (compression) → déferlement déclenché par la compression (seuil 0,85), effets rivage via Terrain (hauteur réduite avec la profondeur), API `Ocean.GetHeight/GetNormal/GetVelocity` (algorithme itératif « undeformed position »), flottabilité par pression sur triangles, VFX GPU (bursts/écume/spray, grille primaire/secondaire), audio procédural (bruit rose filtré). Limites : pas de lèvre/tube maillé, pas de wake wave visible, ambiance skybox non supportée pour l'éclairage des particules, pas mobile. Démo « Epic Surfing » (speedboat sur vagues de 10 m). | https://github.com/Stormrider31/Storm-Breakers (LICENSE CC0-1.0, dernier commit 2024-04-10) ; manuel (extrait dans `Docs/research/StormBreakers_manual_text.txt`) ; `Ocean.cs` lu intégralement ; https://the-storm-rider.itch.io/epic-surfing |
| **Boat Attack Water** (Unity Technologies) : package `com.unity.urp-water-system` 2.0.0-preview.5, cible Unity 2021.3 / URP 12.1, « pas supporté officiellement ». Passes custom pré-Render Graph → non portable tel quel sur URP 17. Référence seulement (Gerstner GPU/CPU, foam, depth). | https://github.com/Unity-Technologies/boat-attack-water (package.json lu) |
| **Crest** (wave-harmonic/crest) : MIT sur GitHub mais **Built-in RP uniquement** ; versions URP/HDRP payantes sur l'Asset Store. Non retenu. | https://github.com/wave-harmonic/crest (README) |
| FFT open source Unity : gasgiant/FFT-Ocean (MIT, 2022) et Ocean-URP (MIT, 2023) ; DanielAskerov/URP-Ocean-System (pas de licence) ; Mozobo/Ocean-Simulation (MIT, 2026). Trop coûteux pour un iGPU et non nécessaires (les vagues de surf sont « designées »). | GitHub API (OBS) |
| Gerstner : formules exactes (position, tangente, binormale, normale, contrainte Σ steepness ≤ 1). | https://catlikecoding.com/unity/tutorials/flow/waves/ (VERIFIED) |
| Shader d'eau URP Shader Graph : Scene Color (réfraction), Scene Depth (couleur/écume par profondeur), reconstruction de position monde pour caustiques ; nécessite Opaque Texture + Depth Texture ; transparents absents de la depth texture. | https://www.cyanilux.com/tutorials/water-shader-breakdown/ (VERIFIED) |
| Approche « Swell » (FinSaltSwell) : **depthmap de bathymétrie** (canal R) → la vague grandit/déferle quand elle atteint un seuil de hauteur ; une pente diagonale produit un peel gauche/droite ; autres formes → closeout, A-frame. | https://jettelly.com/blog/simulating-surf-breaks-in-unity-with-bathymetry-depthmaps (VERIFIED) |
| Fils Unity : vague déferlante réalisée avec **courbes de Bézier + mise à jour des sommets** ; conseils : « si tu peux extraire vitesse/altitude en un point, tu peux faire un jeu de surf » ; références Guerrilla (Horizon FW), Surf's Up (Siggraph 2007). | https://discussions.unity.com/t/realistic-breaking-wave/748291 ; https://discussions.unity.com/t/how-would-i-go-into-making-a-surf-game-with-wave-physics/904926 |

## 4. Science du surf (paramètres de design)

| Fait | Source |
|---|---|
| **Peel angle α** = angle entre la ligne d'écume et la crête non déferlée ; 0° = closeout, 90° = vague « molle » ; minimum surfable ≈ 30° (Walker 1974) ; la plupart des vagues surfées entre **45° et 66°** ; au-delà de ~70° la vague ne « peel » plus utilement. Vitesse requise du surfeur **Vs = c / sin α** (c = célérité). Le peel angle contrôle le type de manœuvre (Scarfe 2002). | https://www.scienceofsurfing.com/p/peel-angle ; https://escholarship.org/uc/item/6h72j1fz (Scarfe et al. 2003, texte dans `Docs/research/`) |
| **Intensité de déferlement** : le gradient orthogonal du fond est la variable dominante ; forme du tube approchée par une cubique, **vortex ratio** (hauteur/largeur) = indicateur d'intensité ; régression Y = 0,065 X + 0,821 (Mead & Black 2001). Types : spilling, plunging (tube), collapsing, surging. Les surfeurs préfèrent les faces raides/plunging. | Scarfe et al. 2003 |
| Sections : une variation de hauteur, peel angle, intensité ou longueur de section crée une nouvelle section ; une section rapide (petit α) suivie d'une section lente appelle un cutback ; plages planes à contours parallèles = peel angle trop faible (closeouts). Composantes de spot : ramp, platform, focus, wedge, ledge, pinnacle, ridge, bowl… | Scarfe et al. 2003 |
| Bodyboard : styles **prone**, **drop-knee** (pied avant sur le deck, genou opposé au tail), stand-up ; planche 100–110 cm, noyau PE/PP, tails crescent (accroche) vs bat (liberté) ; palmes, leash. Tricks : El Rollo (rotation avec la lèvre), ARS (Air-Roll-Spin = rollo + 360), invert, backflip, reverse/forward spin 360, air forward/reverse, tube ride, cutback, bottom turn, re-entry, floater, drop-knee 360. | https://en.wikipedia.org/wiki/Bodyboarding ; https://en.wikipedia.org/wiki/El_Rollo ; https://en.wikipedia.org/wiki/ARS_(bodyboard) ; https://www.surfertoday.com/bodyboarding/the-best-bodyboarding-tricks-in-the-world |
| Jugement (APB/IBC) : manœuvres **radicales dans la section critique** (sous/derrière la lèvre), **enchaînements fluides**, **vitesse et puissance**, **meilleures vagues** ; une manœuvre compte si le rider **reprend un contrôle** après ; les deux meilleures vagues s'additionnent ; manœuvres incomplètes = 1–3 pts. | https://www.sixty40.co.za/tutorials.php?i=4 ; https://www.surfertoday.com/bodyboarding/professional-bodyboarding-has-new-rule-book |
| Jeux de référence : Barton Lynch Pro Surfing (Bungarra, 2023, Unity ; « pump control », météo temps réel, sticks pour guider, X puis boutons pour les airs) ; YouRiding (surf + **bodyboard** : El Rollo, ARS, backflip ; 300 vagues, manette PS/Xbox) ; Surf World Series (2017). | https://store.steampowered.com/app/1776170/ ; https://store.steampowered.com/app/1725680/ ; reviews (WayTooManyGames, Digitally Downloaded) |

## 5. Assets gratuits (licences vérifiées)

| Asset | Licence | Source |
|---|---|---|
| Poly Haven HDRIs/textures/modèles (API publique sans clé ; User-Agent requis) | CC0 | https://polyhaven.com/our-api |
| Kenney Input Prompts 1.5, Particle Pack, Smoke Particles, UI Pack, Impact/Interface/UI Audio | CC0 | https://kenney.nl |
| Quaternius Universal Animation Library 1 (45 clips « Standard ») et 2 (+ Female Mannequin), Universal Base Characters (itch) | CC0 | https://quaternius.com ; https://opengameart.org/content/universal-animation-library ; https://opengameart.org/content/universal-animation-library-2 |
| KayKit Character Animations / Adventurers | CC0 | https://kaylousberg.itch.io |
| OpenGameArt « Beach Ocean Waves » | CC0 | https://opengameart.org/content/beach-ocean-waves |
| Google Fonts Bebas Neue, Righteous, Nunito, Inter | SIL OFL 1.1 | https://github.com/google/fonts |
| Mixamo (personnages + animations) | Gratuit, usage commercial dans un jeu, pas de redistribution brute | https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html |
| Unity Asset Store : Starter Assets ThirdPerson URP, Particle Pack, Terrain Sample Asset Pack, AllSky Free, Skybox Series Free, Cartoon FX Remaster Free, POLYGON Starter Pack | Standard Unity Asset Store EULA (gratuits) | liens dans `Docs/USER_ACTIONS.md` |
| Sonniss GDC bundles | royalty-free, pas d'attribution | https://gdc.sonniss.com/ |

## 6. Machine et projet (observé)

- CPU Intel Core i5-1235U (10 c/12 t), **GPU Intel Iris Xe intégré**, 15,7 Go RAM, ~130 Go libres, Windows 11. Pas de Visual Studio. Editor 6000.6.0f1 (module Web seulement + Mono Windows par défaut).
- Projet : template Universal 3D ; `PC_RPAsset` : Depth/Opaque Texture ON, HDR ON, MSAA off, GRD Instanced, SRP Batcher ON ; qualité « PC » active ; Linear ; backend Mono ; Fast Enter Play Mode ON (domain+scene reload désactivés).
- Unity CLI 1.0.0-beta.6 ; Pipeline 0.6.0-exp.1 ; ~517 commandes exposées.
