# BISCOTTE — Spécification d'implémentation (source de vérité)

Version 1.0 — 2026-09-08 — Unity 6000.6.0f1, URP 17.6.0, Windows 64 bits (clavier/souris + manette PS5 DualSense).
Documents compagnons : `Docs/AGENT_PLAYBOOK.md` (mécanique CLI), `Docs/ASSETS_MANIFEST.md` (assets), `Docs/USER_ACTIONS.md` (actions humaines), `Docs/RESEARCH_SOURCES.md` (faits et sources), `Docs/research/` (extraits).

---

## 0. Résumé exécutif

**Biscotte** est un jeu de **bodyboard** en 3D, arcade-sim : le joueur rame dans le lineup, lit les séries, choisit sa vague, décolle en position allongée (prone), génère de la vitesse en pompant sur la face, carve, rentre dans le tube, s'envoie en l'air depuis la lèvre pour des **El Rollo, ARS, backflips, inverts, 360** et enchaîne des manœuvres notées comme en compétition (section critique, engagement, variété, fluidité). Les conditions (taille de houle, période, direction, vent, marée) changent la forme des vagues sur trois spots (beach break, slab de récif, pointe).

Piliers : **(1) la vague est le personnage principal** (lisible, prévisible, belle) ; **(2) le feel** (vitesse, rail, pumping, tube) ; **(3) l'expression** (tricks, style, score) ; **(4) fluide et beau sur PC modeste** (60 fps 1080p sur GPU intégré, 120+ fps sur GPU dédié).

Choix techniques structurants :
- **Océan ambiant** : port de **Storm Breakers** (CC0, modèle de vague par groupes, flottabilité, VFX GPU, audio procédural) sur Unity 6.6 / URP 17.6.
- **SurfWave** : notre système de **vague déferlante « designée »** (spline de ligne de déferlement + profil de section paramétrique : houle → face raide → lèvre/tube → écume), maillage généré sur CPU par jobs Burst (`Mesh.MeshData`), **une seule fonction mathématique** pour le rendu et la physique.
- **Rider** : contrôleur « surface-locked » à états (rame, duck dive, take-off, ride, tube, air, atterrissage, wipeout), physique analytique (gravité projetée, poussée de vague, traînée, rail), tricks par combinaisons stick/boutons, notation inspirée des critères APB/IBC.
- **Rendu** : URP Forward+, GPU Resident Drawer, Shader Graph (eau, écume, SSS de lèvre), VFX Graph (spray, écume, splash), HDRI Poly Haven, terrain PBR CC0 ; presets Low→Ultra, STP/render scale.
- **Input System 1.20** (project-wide actions, control schemes Keyboard&Mouse / Gamepad, glyphes Kenney, vibrations et lightbar DualSense en USB).
- **UI Toolkit** pour HUD/menus ; **Cinemachine 3** pour la caméra (follow, tube, air, wipeout, replay).

Le plan (§19) est découpé en 9 phases (P0→P8) avec tâches identifiées, critères d'acceptation et commandes de vérification. Le jalon « **Playable 1** » (P3) donne une vague surfable avec capsule ; « **Vertical slice** » (P5) ajoute tube, airs, tricks, score, HUD ; « **Beta** » (P7) ajoute personnage, VFX, audio, modes, menus ; « **Release candidate** » (P8) optimise et packages.

---

## 1. Contexte, contraintes et hypothèses

### 1.1 Environnement de développement (observé)
| Élément | Valeur |
|---|---|
| Machine | Intel Core i5-1235U (10 cœurs/12 threads), **Intel Iris Xe intégré**, 15,7 Go RAM, Windows 11, ~130 Go libres |
| Unity | 6000.6.0f1 (Update release, support jusqu'à la prochaine ; 6.7 LTS attendue fin 2026), Editor ouvert et piloté par `unity command` (Pipeline 0.6.0-exp.1) |
| Pipeline | URP 17.6.0, template Universal 3D ; `PC_RPAsset` actif (Depth/Opaque Texture ON, HDR ON, MSAA off, SRP Batcher ON, GPU Resident Drawer Instanced) ; `PC_Renderer` en **Forward+** ; couleur linéaire ; API graphique Editor DX11 (DX12 par défaut des nouveaux projets depuis 6.1) |
| Packages | Input System 1.20.0, Cinemachine 6.6.0 (core), Animation Rigging 6.6.0 (core), Timeline 6.6.0 (core), Burst 2.0.0 (module), Mathematics 1.4.0 (module), Collections 6.6.0, Splines 2.9.0, VFX Graph 17.6.0, Shader Graph 17.6.0, ProBuilder 6.1.2, glTFast 6.20.0, Test Framework 1.8.0, UGUI 2.6.0 (TextMeshPro), AI Navigation 2.0.14, Visual Scripting 1.9.12, AI Assistant/Inference (template) |
| Backend | Mono (dev). IL2CPP Windows non installé (voir `USER_ACTIONS.md` §C) |
| Editor | **Fast Enter Play Mode actif** (domain reload et scene reload désactivés) → hygiène des statiques obligatoire |

### 1.2 Contraintes produit
- **Plateforme** : Windows 64 (Steam-ready mais pas requis en v1). Pas de mobile, pas de Web (VFX Graph et compute requis).
- **Entrées** : clavier/souris **et** DualSense (PS5) ; toute manette XInput doit aussi marcher (Xbox) via le même schéma « Gamepad ».
- **Performances** : 60 fps stables à 1080p preset « Low » sur Iris Xe (render scale 0,75) ; 120 fps 1440p « High » sur RTX 3060 (référence). Budget frame détaillé §16.
- **Licences** : uniquement contenus gratuits à licence claire (CC0, OFL, Asset Store EULA gratuit, Mixamo). Aucune ressource « free download » de site pirate.
- **Langue** : UI FR + EN ; code/commentaires/logs EN ; docs FR.

### 1.3 Hypothèses par défaut (modifiables par l'humain, voir `USER_ACTIONS.md` §F)
- Direction artistique **réaliste stylisée** : eau et lumière réalistes (HDRI, PBR), personnage stylisé-propre (mannequin Quaternius/Universal Base Characters), UI moderne épurée (Kenney UI + polices Bebas Neue/Righteous/Nunito).
- Vue **troisième personne** dynamique (caméra derrière/latérale selon la trajectoire) ; pas de vue FPS en v1.
- Solo, hors ligne. Pas de multijoueur, pas de sauvegarde cloud.
- Nom de code « Biscotte » conservé pour la v1.

---

## 2. Game design

### 2.1 Fantasme et boucle de jeu
> « Je suis dans l'eau, je sens la houle me soulever, je vois la série arriver, je choisis la deuxième vague, je rame fort, je bascule, la face se dresse, je pompe pour aller plus vite, je rentre dans le tube… et je sors pour lancer un El Rollo sur la lèvre. »

Boucle de **session** (3–12 min) :
1. **Lineup** : flotter/ramer dans l'océan ambiant (Storm Breakers), lire l'arrivée d'une **série** (annonce visuelle : lignes de houle à l'horizon, ombre sur l'eau, HUD discret « série dans 12 s »), se positionner près du **peak** (marqueur diégétique : bouée/repère de récif + indicateur de « zone de take-off » optionnel).
2. **Take-off** : ramer vers le bord, dans la direction de la vague, au bon moment → bascule.
3. **Ride** : bottom turn / top turn / cutback pour rester dans la **pocket** (zone d'énergie près de la lèvre) ; pumping pour la vitesse ; **stall** pour ralentir et se caler dans le **tube** ; **sections** (mur, tube, rampe/closeout) à négocier.
4. **Tricks** : airs depuis la lèvre (El Rollo, ARS, backflip, invert, spins), manœuvres sur la face (snap, cutback, floater, re-entry), drop-knee (stance alternative).
5. **Fin de vague** : kick-out propre (score validé), ou **wipeout** (ragdoll, apnée, retour à la surface).
6. **Retour au lineup** : rame, **duck dive** sous les mousses des vagues suivantes (jouable et lisible, pas un temps mort : 15–40 s, avec caméra large qui montre la série suivante).

Boucle **méta** (v1 minimale) : meilleurs scores par spot/conditions, défis (tube ≥ 3 s, ARS atterri, 3 vagues > 7,0), déblocage de presets de conditions et de planches (cosmétique + réglages de handling).

### 2.2 Modes (v1)
| Mode | Description | Fin |
|---|---|---|
| **Free Surf** | Spot + conditions au choix (éditeur de conditions), sans limite de temps ; défis contextuels | Retour menu |
| **Heat** | 15 min de « temps de heat » (accéléré ×1,5 hors ride), max 10 vagues, **les 2 meilleures notes (0–10) s'additionnent** (max 20). Palier bronze/argent/or par spot | Timer |
| **Trick Challenge** (P7, optionnel) | Objectifs de tricks sur vagues scriptées identiques (seed fixe) | Objectifs |

### 2.3 Spots (contenu v1)
| Spot | Type | Vague | Difficulté | Spécificités |
|---|---|---|---|---|
| **Baie Biscotte** | Beach break, fond sableux | A-frame gauche/droite, 1–2 m, peel angle 55–70°, sections molles + une section tube courte | Débutant | Apprentissage, vagues fréquentes, mousses faciles à duck-diver |
| **La Dalle** | Slab de récif | Droite creuse, 1,5–3 m, peel angle 35–50°, intensité de tube élevée (vortex ratio 2,5–3), ledge | Expert | Tubes longs, lèvre épaisse, wipeouts punitifs (rochers = respawn) |
| **Pointe Longue** | Point break | Gauche longue 200 m, 1–2,5 m, sections alternées (mur rapide → rampe → épaule) | Intermédiaire | Cutbacks, airs sur rampe, enchaînements |
| **Le Shorebreak** (P7, si temps) | Shore break (wedge) | Vagues courtes très creuses 1–2 m qui cassent sur le sable | Expert | Signature bodyboard : tubes ultra courts, inverts/backflips |

Chaque spot = un `SurfSpot` (ScriptableObject) + une scène additive de décor + un terrain sous-marin (bathymétrie) + une **BreakLine** (spline) + un profil de sections.

### 2.4 Conditions
`ConditionsPreset` : hauteur de houle H (0,5–4 m), période T (6–16 s), direction (décalage −20°…+20° vs direction idéale du spot), vent (offshore/onshore/cross, 0–12 m/s), marée (basse/mi/haute), intervalle entre séries (40–120 s), vagues par série (2–6), heure (aube/matin/midi/soir → HDRI et soleil). Effets :
- H et T → hauteur de la SurfWave, célérité c, longueur d'onde λ ; H élevé → sections plus rapides (peel angle réduit de 5–10°) et tube plus intense.
- Vent offshore → lèvre plus fine/plus tenue (φ_pitch prolongé, spray), onshore → sections qui s'écroulent plus tôt (closeouts), cross → asymétrie.
- Marée basse → break line plus au large / plus creuse (offset et vortex ratio +), haute → plus molle.
- Direction → rotation de la crête vs BreakLine → peel angle modifié (la même BreakLine devient plus/moins rapide).

### 2.5 Contrôles (résumé ; détail §11)
| Action | Gamepad (PS5 / Xbox) | Clavier-souris |
|---|---|---|
| Diriger / lean (carve) | Stick gauche X | A/D (Q/D en AZERTY via bindings) |
| Trim avant/arrière (vitesse vs contrôle) | Stick gauche Y | W/S |
| Pump (rythme) | R2 / RT | Shift |
| Stall (freiner, se caler dans le tube) | L2 / LT | Ctrl |
| Air / pop sur la lèvre ; kick-out sur l'épaule | Croix / A | Espace |
| El Rollo (roll avec la lèvre) | R1 / RB | E |
| Rotation en l'air (spin, flip, invert) | Stick droit (flick/hold) | Souris (X = spin, Y = flip/invert) |
| Grab (style, ralentit la rotation) | Carré / X | F |
| Drop-knee ↔ prone (stance) | L1 / LB | Q (A en AZERTY) |
| Duck dive (rame) / bail (ride) | Rond / B | C |
| Reset vague (respawn au lineup) | Triangle / Y | R |
| Caméra (nudge) | Stick droit (hors air) | Souris |
| Pause | Options / Menu | Échap |
| Debug overlay (dev) | — | F1 |

Principe : **stick gauche = corps**, **stick droit = rotation aérienne / caméra**, **gâchettes = énergie (pump/stall)**, **face = actions ponctuelles**. Toutes les entrées sont rebindables (§11).

### 2.6 Tricks (catalogue v1)
| Trick | Catégorie | Entrée | Conditions | Score base | Difficulté |
|---|---|---|---|---|---|
| Bottom turn | Face | Stick vers la vague en bas de face | Sur face, vitesse > 4 m/s | 20 | 1 |
| Top turn / Snap | Face | Stick vers le bas en haut de face (près de la lèvre) | Dans la pocket | 60 | 2 |
| Cutback | Face | Demi-tour sur l'épaule puis retour vers l'écume | Sur épaule, vitesse > 6 m/s | 90 | 3 |
| Floater | Face/lèvre | Passer sur la lèvre écroulée (section closeout) | Section broken | 80 | 3 |
| Re-entry | Lèvre | Monter dans la lèvre et redescendre avec elle | Pocket, lèvre en φ 0,8–1,4 | 110 | 3 |
| Tube ride | Tube | Rester dans le tube (score par ¼ s) | Dans le vortex | 40/s ×profondeur | 3–5 |
| Air forward / Air reverse (180/360/540/720) | Air | Pop + stick droit gauche/droite | Vitesse > 7 m/s à la lèvre | 120 + 80 par 180° | 3–5 |
| **El Rollo** | Air (signature) | R1 dans la fenêtre de contact lèvre (0,4 s) | Lèvre en φ 1,0–1,8, vitesse > 6 m/s | 200 | 3 |
| **ARS** (Air Roll Spin) | Air | El Rollo + stick droit latéral pendant le roll | Idem + vitesse > 8 m/s | 340 | 5 |
| Backflip | Air | Pop + stick droit haut (tuck) | Vitesse > 8 m/s, lèvre verticale | 320 | 5 |
| Invert | Air | Pop + stick droit bas | Vitesse > 6 m/s | 220 | 4 |
| Drop-knee snap / DK 360 | Face (DK) | En stance DK : stick + rotation | Stance DK | 90 / 180 | 3 / 4 |
| Grab modifiers | Style | Carré pendant un air | — | ×1,15 | — |

Règle de **validation** : un trick n'est comptabilisé que si le rider **atterrit** (angle planche/normale < 35°, vitesse angulaire résiduelle < seuil) **et conserve le contrôle ≥ 1,0 s** (pas de wipeout) — transcription de la règle de jugement « complete the manoeuvre and regain controlled momentum ».

### 2.7 Notation (0–10 par vague)
Points bruts `R` = Σ sur les tricks validés de `base × exécution × critique × variété` + `tube` + `bonus_flow` + `engagement`.
- **exécution** ∈ [0,6 ; 1,5] : qualité d'atterrissage (alignement, absence de sur-rotation), amplitude (hauteur d'air normalisée par H), grab.
- **critique** ∈ [0,8 ; 1,6] : proximité de la pocket/lèvre au démarrage du trick (`energy` du sample de surface).
- **variété** : chaque répétition d'un même trick dans la vague ×0,7 (cumulatif).
- **tube** : 40 pts/s × (1 + 2·profondeur) ; profondeur ∈ [0,1] = position derrière la ligne de lèvre normalisée.
- **flow** : enchaîner un trick < 2,5 s après le précédent → chaîne ×1,1 par maillon (plafond ×2,0), interrompue par un stall long ou un cutback raté.
- **engagement** : + (H/2 m) × 15 pts par manœuvre exécutée sur une section « critique » (φ ≥ 0,9).
- **wipeout** : les tricks non validés sont perdus ; la vague garde ce qui a été validé, −20 % sur le score de vague.
Note vague `N = 10 × R / (R + 900)` (courbe logistique douce ; 900 pts ≈ 5,0 ; 2 700 pts ≈ 7,5 ; 8 100 ≈ 9,0). Affichage façon jury : 3 « juges » virtuels = même formule avec bruits ±0,3 (cosmétique) et médiane.

### 2.8 Feel — cibles chiffrées
- Vitesse de ride confortable 6–10 m/s ; max 16 m/s (grosse vague/pump).
- Temps de réponse direction : yaw rate max 140°/s à 8 m/s ; rail : 0,25 s pour engager un carve complet.
- Pump : gain +8 % de vitesse par pompe bien synchronisée (fenêtre 0,35 s), −3 % si à contretemps ; jamais plus de 3 pompes efficaces d'affilée sans redescendre.
- Air : pop = +2,5 m/s vertical (à 8 m/s) ; hauteur typique 1–3 m au-dessus de la lèvre ; temps de vol 0,8–1,6 s ; rotation 360° en 0,9 s (flick) / 1,3 s (hold).
- Tube : entrée lisible (obscurcissement, son, ralenti optionnel ×0,85 pendant 0,5 s à l'entrée), sortie « doggy door » (éclat de lumière) ou foam ball.
- Caméra : FOV 55° → 70° avec la vitesse ; dutch ±6° en carve ; look-ahead 0,35 s.

### 2.9 Ce que le jeu n'est pas (v1)
Pas de multijoueur, pas de personnalisation poussée, pas de carrière scénarisée, pas de photo mode (nice-to-have P8), pas de surf debout (stand-up) ni de SUP.

---

## 3. Architecture technique

### 3.1 Vue d'ensemble
```
                +---------------------------+
                |  Session / GameState      |  (Free/Heat, timer, scores, spot, conditions)
                +------------+--------------+
                             |
   +----------------+  +-----v------------------+   +------------------+
   | OceanAmbient   |  | WaveSetScheduler        |   | ConditionsPreset |
   | (StormBreakers)|  |  -> SurfWave x N        |<--| SurfSpot         |
   +-------+--------+  +-----+------------------+   +------------------+
           |                 |
     +-----v-----------------v------+
     | WaterSurfaceComposite        |  IWaterSurface.Sample(pos, t) -> WaterSample
     +-----+--------------+---------+
           |              |
   +-------v------+  +----v-----------+      +----------------+
   | RiderController| | SurfWaveRenderer |     | VFX / Audio    |
   | (états, phys) | | (mesh jobs, mat, |---->| Directors      |
   +-------+-------+ |  VFX buffers)    |     +----------------+
           |         +------------------+
   +-------v-------+   +-------------+   +-----------+   +---------+
   | TrickSystem   |-->| Scoring     |-->| HUD (UITK)|   | Camera  |
   +---------------+   +-------------+   +-----------+   | Director|
                                                         +---------+
```
- **Vérité temporelle** : `WaveClock` (double) : `FixedTime` pour la physique, `RenderTime` pour le maillage/VFX ; les deux dérivent du même `Time.timeAsDouble` (pas de `Time.time` float dans les maths de vague).
- **Vérité géométrique** : `SurfWaveMath.EvaluateSurface(...)` (static, Burst) est appelée par le job de maillage **et** par `WaterSurfaceComposite.Sample`. Aucun readback GPU.
- **Composition** : `WaterSample` = ambiant (OceanMath, port Burst de Storm Breakers) + Σ SurfWaves actives (chaque SurfWave a une empreinte 2D ; à l'intérieur, la SurfWave **remplace** l'ambiant avec fondu sur 6 m aux bords).

### 3.2 Modules, namespaces, responsabilités
| Namespace (`Biscotte.*`) | Classes clés | Responsabilité |
|---|---|---|
| `Core` | `GameBootstrap`, `GameStateMachine`, `WaveClock`, `ServiceRegistry`, `SaveSystem`, `Localization`, `DebugSettings` | Cycle de vie, services, temps, sauvegarde JSON, localisation |
| `Ocean` | `OceanAmbient` (wrapper de `StormBreakers.OceanController`), `OceanMath` (port Burst de `Ocean.OceanDeformation`), `WaterSurfaceComposite : IWaterSurface`, `WaterSample` | Océan ambiant et point d'entrée unique d'échantillonnage |
| `Wave` | `SurfSpot`, `SurfBreakProfile`, `ConditionsPreset`, `WaveSetScheduler`, `SurfWave`, `SurfWaveParams`, `SurfWaveMath`, `SurfWaveMeshJob`, `SurfWaveRenderer`, `SurfWaveDebug` | La vague déferlante jouable |
| `Rider` | `RiderController`, `RiderState*`, `RiderTuning`, `BoardSpec`, `SurfaceProbe`, `RiderRagdoll`, `RiderAnimationDriver`, `RiderFeedback` | Contrôleur joueur |
| `Tricks` | `TrickCatalog`, `TrickDef`, `TrickDetector`, `AirRotationController`, `LandingEvaluator` | Détection/exécution/validation des tricks |
| `Scoring` | `WaveScorer`, `JudgePanel`, `SessionScore`, `ScoreEvents` | Notation |
| `Session` | `SessionManager`, `HeatRules`, `FreeSurfRules`, `ChallengeTracker`, `ResultsData` | Modes |
| `CameraRig` | `CameraDirector`, `RideCameraTuning`, `CameraFX` | Cinemachine 3 |
| `InputSys` | `BiscotteActions` (généré), `InputRouter`, `GlyphProvider`, `Haptics`, `RebindService` | Entrées, glyphes, vibrations |
| `UI` | `HudController`, `MenuFlow`, `OptionsController`, `ScorePopup`, `ConditionsEditorView`, `ResultsView` | UI Toolkit |
| `Audio` | `AudioDirector`, `OceanAudio`, `RiderAudio`, `UiAudio`, `MixerSnapshots` | Audio |
| `VFX` | `WaveVfxBinder`, `RiderVfx`, `VfxBudget` | Liaison VFX Graph ↔ données de vague |
| `Debugging` | `DebugOverlay`, `CheatMenu`, `GizmoToggles` | Outils dev |
| `EditorTools` (asmdef Editor) | `SurfSpotEditor`, `BreakLineTool`, `WaveProfilePreview`, `SceneBuilders` | Outils d'authoring |

### 3.3 Dossiers
```
Assets/_Project/
  Scripts/            (asmdef Biscotte.Runtime) — un sous-dossier par namespace
  Editor/             (asmdef Biscotte.Editor)
  Tests/EditMode/     (asmdef Biscotte.Tests.EditMode)   Tests/PlayMode/ (Biscotte.Tests.PlayMode)
  Shaders/            SurfWaveWater.shadergraph, Foam.shadersubgraph, WaveSSS.shadersubgraph, Underwater.shadergraph, HLSL/ (SurfWaveMath.hlsl si port GPU)
  VFX/                LipSpray.vfx, ImpactFoam.vfx, FaceFoam.vfx, RiderSpray.vfx, Splash.vfx, Whitewater.vfx
  Prefabs/            Rider.prefab, Board_*.prefab, SurfWave.prefab, Ocean.prefab, Cameras.prefab, Managers.prefab
  Scenes/             Boot.unity, MainMenu.unity, Playground.unity (dev), Spot_Baie.unity, Spot_Dalle.unity, Spot_Pointe.unity (+ _Env additives)
  Settings/           BuildProfiles/, Quality/, Volumes/, Input/BiscotteActions.inputactions, Audio/BiscotteMixer.mixer
  Data/               Spots/, Conditions/, Tuning/, Tricks/, Boards/, Localization/
  Art/                Materials/, Textures/, Models/, Animations/, Characters/, Terrain/
  Audio/              Ambience/, Rider/, UI/
  UI/                 UXML/, USS/, Sprites/, Fonts/
Assets/ThirdParty/    (voir ASSETS_MANIFEST.md)
Assets/Samples/       (samples Unity)
Tools/                scripts run_script + polyhaven_dl.py
Build/                (ignoré) builds, rapports, captures
Docs/                 cette spec et compagnons
```

### 3.4 Interfaces et structures partagées (contrat)
```csharp
namespace Biscotte.Ocean
{
    public struct WaterSample
    {
        public float  Height;        // y de la surface en world (face inférieure si sous une lèvre)
        public float3 Normal;        // normale unitaire de la surface
        public float3 Velocity;      // vitesse de l'eau à la surface (m/s), inclut translation de la vague
        public float  BreakPhase;    // 0 = houle, 1 = pitch/lèvre, 2 = tube plein, 3 = écume ; <0 = pas de SurfWave ici
        public float  Energy;        // 0..1, "pocket" : 1 près de la lèvre en section critique
        public float3 TravelDir;     // direction de propagation D (horizontale, unitaire)
        public float3 CrestDir;      // direction le long de la crête T (horizontale, unitaire)
        public float  CrestDistance; // ξ : distance signée à la crête (+ vers la plage)
        public float  PeelDistance;  // distance le long de la crête jusqu'au point de peel (+ = côté non déferlé/épaule)
        public bool   InTube;        // point sous la lèvre et au-dessus de la face
        public float  TubeDepth;     // 0..1 profondeur dans le tube
        public float  WhitewaterAmount; // 0..1 (mousse)
        public float  SeabedDepth;   // profondeur d'eau locale (bathymétrie)
        public int    WaveId;        // -1 si ambiant
    }
    public interface IWaterSurface
    {
        WaterSample Sample(float3 worldPos, double time);
        void SampleBatch(NativeArray<float3> positions, double time, NativeArray<WaterSample> results); // Burst job
        bool TryGetSurfWave(int waveId, out SurfWave wave);
        IReadOnlyList<SurfWave> ActiveSurfWaves { get; }
    }
}
```
Toutes les autres couches (rider, caméra, VFX, audio, HUD) ne connaissent l'eau **que** par `IWaterSurface`.

---

## 4. Océan ambiant — port de Storm Breakers

### 4.1 Ce qu'on garde / ce qu'on change
| Élément Storm Breakers | Décision |
|---|---|
| `Ocean` (modèle statique 4 systèmes, groupes) | **Gardé** tel quel comme source des paramètres et vérité GPU. Port Burst (`OceanMath`) pour nos jobs (mêmes formules, mêmes constantes : `groupSpeed = 0.5·sqrt(1.5613·λ)`, `pulsation = sqrt(20π/λ)`, `verticalAmplitude = 0.085·λ`, `deltah = −0.12·λ·sin(phase)`, seuil de déferlement 0,85). Test de parité obligatoire. |
| `OceanController` | Gardé, piloté par `OceanAmbient` (nos presets → ses 4 systèmes). Patch P1-T5 : éclairage des particules via `RenderSettings.ambientProbe` (permet Ambient = Skybox). |
| `ocean.shadergraph` + maillage océan | Gardés pour l'océan ambiant. Patch : « wave mask » (4 boîtes d'empreinte SurfWave passées en globals) qui abaisse et estompe l'océan ambiant sous nos SurfWaves (évite z-fight et doubles crêtes). Couleurs/ripples réglés pour les HDRI Poly Haven. |
| `oceanVFX.vfx` (déferlantes) | Gardé pour l'horizon/lineup ; **désactivé dans l'empreinte** des SurfWaves (même mask). |
| `WaterInteraction` (flottabilité par triangles) | **Non utilisé** pour le rider (la planche est plate → non supporté, manuel §« Special buoyancy »). Utilisé pour props flottants (bouée) si besoin. |
| `BreakersAudio` (audio procédural) | Réutilisé pour l'ambiance des déferlantes ambiantes ; notre `OceanAudio` étend l'idée pour la SurfWave. |
| `Terrain` (shoreline) | Utilisé : le terrain sous-marin du spot est passé au contrôleur (hauteur réduite près du rivage, couleur de fond). Note : jobs Storm Breakers désactivés avec terrain (nous n'utilisons pas ses jobs). |
| `CameraController`, `BoatController`, `Foil`, `HullGenerator`, exemples | Non utilisés (référence). |
| Skybox de SB | Remplacée par HDRI + `Skybox/Cubemap` (le manuel recommande un ciel dont le bas est de couleur cohérente : nos HDRI de plage conviennent ; vérifier les reflets). |
| Transparence | Océan ambiant **opaque** (min transparency = 1) comme recommandé quand des VFX bursts existent ; la SurfWave gère la couleur par profondeur bathymétrique (pas de Scene Color sur Low). |

### 4.2 Statiques et Fast Enter Play Mode
`StormBreakers.Ocean` est une classe statique alimentée par `OceanController.Start`. Ajouter dans `OceanAmbient` :
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetStatics() { StormBreakers.Ocean.ConstructStaticData(); /* + nos statiques */ }
```
et vérifier que `OceanController` se ré-exécute à chaque Play (il le fait dans `Start`). Consigner dans `PATCHES.txt` si une modification de SB est nécessaire.

### 4.3 Résultat du smoke test (2026-09-08)
Import sur 6000.6 : compilation OK (asmdef `StormBreakers.Runtime`, 33 types), 8 Shader Graphs sans erreur, 3 VFX importés, aucun log d'erreur à l'import. La scène `Epic Surfing` a été ouverte et jouée (capture `Build/shots/smoke_stormbreakers_surfing.png`) : océan, houle et déferlantes rendus correctement dans URP 17.6. Seules exceptions : les **scripts d'exemple** de SB (`CameraController`, `InputRotation`, `GameControl`) utilisent l'ancien `UnityEngine.Input` alors que le projet est en « Input System package » seul → `InvalidOperationException` dans les scènes d'exemple uniquement (non utilisées par Biscotte ; consigné dans `PATCHES.txt`). Voir P1 pour les vérifications visuelles restantes (ripples, reflets, particules avec nos HDRI).

---

## 5. SurfWave — la vague déferlante jouable

### 5.1 Modèle
Une **SurfWave** est une crête rectiligne (localement) qui se propage vers la plage à célérité `c` le long de `D` (direction de propagation, horizontale). La **BreakLine** du spot (spline Unity, plan XZ) définit **où** chaque point de la crête commence à déferler. Dans le repère de la vague :
- `s` = abscisse curviligne le long de la crête (m), `s ∈ [0, L]` (L = longueur de la BreakLine projetée sur la crête, 80–300 m).
- `ξ` = distance signée à la crête le long de `D` (+ vers la plage). La **face** est du côté `ξ > 0` (vers la plage), le **dos** `ξ < 0`.
- `y_c(t) = y_0 + c·(t − t_0)` position de la crête le long de `D`.
- `f(s)` = offset de la BreakLine le long de `D` au point `s` (échantillonné depuis la spline dans ce repère). **Instant de déferlement** : `t_break(s) = t_0 + (f(s) − y_0)/c`.
- **Peel angle** : `tan α(s) = f'(s)` ; **vitesse de peel** `v_peel = c / f'(s)` ; **vitesse requise du rider** `V_s = c / sin α`. Outils d'édition : afficher α(s) (couleur : rouge < 30°, vert 45–66°, bleu > 75°) et `V_s`.
- **Phase de déferlement** `φ(s,t)` :
  - `τ = t − t_break(s)`
  - `τ < −T_shoal` : φ = 0 (houle) ; `−T_shoal ≤ τ < 0` : φ = (τ + T_shoal)/T_shoal ∈ [0,1) (shoaling : la face se dresse) ;
  - `0 ≤ τ < T_pitch` : φ = 1 + τ/T_pitch ∈ [1,2) (la lèvre est projetée, le tube s'ouvre) ;
  - `T_pitch ≤ τ < T_pitch + T_collapse` : φ = 2 + (τ − T_pitch)/T_collapse ∈ [2,3) (le tube se referme, la lèvre frappe, écume) ;
  - `τ ≥ T_pitch + T_collapse` : φ = 3 (écume), la hauteur décroît `H_w(τ) = H·(0,55·exp(−(τ−τ_3)/T_decay) + 0,15)`.
  - Valeurs par défaut : `T_shoal = 3,0 s`, `T_pitch = 1,4 s`, `T_collapse = 1,2 s`, `T_decay = 9 s`. Sections « barrel » longues : `T_pitch` ↑ (2,2 s) ; « closeout » : φ passe 1→3 en 0,6 s.
- **Paramètres le long de s** (courbes du `SurfBreakProfile`, échantillonnées en NativeArray) : `H(s)` hauteur (m), `λ(s)` longueur d'onde effective (m, 25–60), `I(s)` intensité de tube (vortex ratio 1,6–3,0), `closeout(s)` (0/1 doux), `ramp(s)` (0..1 : lèvre « rampe » propice aux airs), `sectionType(s)` (enum pour le scoring : Wall, Barrel, Ramp, Closeout, Shoulder).
- **Continuité** : `f`, `H`, `λ`, `I` sont C¹ en `s` (splines) → maillage sans plis.

### 5.2 Profil de section (2D, par `s` et `t`)
Le profil est défini dans le plan `(ξ, y)`. Deux courbes :
1. **Face (height field)** `h_face(ξ; H, λ, φ, I)` — univoque, utilisée pour la physique et le maillage principal :
   - Base houle : `h_0(ξ) = (H/2)·[cos θ + κ·cos 2θ]`, `θ = 2π ξ/λ`, `κ = 0,12·min(1, φ)` (asymétrie type Stokes : crête pointue, creux plat).
   - Redressement : la crête se déplace vers la plage `ξ_c(φ) = 0,08·λ·smoothstep(0,1,φ) + 0,10·λ·max(0, φ−1)` et la face se raidit : `h_face(ξ) = h_0(ξ − ξ_c)` puis **compression horizontale** de la face côté plage : `ξ' = ξ_c + (ξ − ξ_c)·(1 − 0,45·min(1, φ))` pour `ξ > ξ_c` (la face devient concave/verticale).
   - Pente maximale garantie < 80° (φ capé à 1,8 pour la face) ; la partie plus raide est représentée par la **lèvre**.
   - Écume (φ ≥ 2) : `h_face += H_w(τ)·bump((ξ − ξ_c)/(0,35·λ))` avec `bump(x) = exp(−x²)`; le reste s'aplatit vers la houle résiduelle `0,3·h_0`.
   - Lissage le long de `s` : un filtre `[1,2,1]/4` sur `H(s)`/`φ(s)` dans le job avant évaluation (évite les marches).
2. **Lèvre (courbe paramétrique)** `L(v; s, t)`, `v ∈ [0,1]`, présente si `1 < φ < 2,4` :
   - Départ `P_a = (ξ_c, H)` (crête), tangente initiale vers la plage et le haut ;
   - Extrémité `P_b = (ξ_c + w_v(φ), H − h_v(φ))` où `h_v = 0,7·H·min(1, φ−1)` (chute), `w_v = h_v / I` (largeur du vortex = hauteur / vortex ratio) ;
   - Courbe de Bézier cubique `P_a, P_a + (0,45 w_v, 0,25 H), P_b + (0,25 w_v, 0,45 h_v), P_b` (« cubic fit » du vortex, réf. Mead & Black) ;
   - Épaisseur `e(v) = 0,12·H·(1 − 0,6·v)` (lèvre plus fine vers la pointe) → le maillage de lèvre est un ruban à deux faces (dessus/dessous) fermé à la pointe.
   - **Point d'impact** `P_i = (ξ_c + w_v, h_face(ξ_c + w_v))` : source de l'écume/spray, et zone de « foam ball » (rayon `0,25·H`).
3. **Test « dans le tube »** : point `p` est dans le tube si `ξ_c < ξ_p < ξ_c + w_v` **et** `h_face(ξ_p) < y_p < y_lip(ξ_p)` où `y_lip` = ordonnée de la lèvre à `ξ_p` (échantillonnée sur la Bézier). `TubeDepth = 1 − (ξ_p − ξ_c)/w_v`.
4. **Vitesse de surface** `V(ξ, s, t)` : translation `c·D` pondérée + composante orbitale : `V = c·D·(0,35 + 0,65·clamp(h/H,0,1))·min(1,φ+0,3) + U·∂h/∂t` ; dans l'écume : `V = c·D·0,8 + turbulence (bruit 1D, ±1,5 m/s)`.
5. **Énergie/pocket** `E(ξ, s, t) = clamp(1 − |ξ − ξ_c − 0,12λ| / (0,35λ), 0, 1) · smoothstep(0,4, 1,0, φ) · (1 − whitewater) · gate(PeelDistance)` où `gate` = 1 côté épaule proche du peel (0–15 m), décroît jusqu'à 0,25 à 40 m de l'épaule.

Tous les coefficients ci-dessus sont des champs de `SurfWaveTuning` (ScriptableObject) avec les valeurs par défaut indiquées ; les agents doivent les exposer, pas les figer.

### 5.3 Maillage
- **Grille face** : `Ns = 192` échantillons le long de `s`, `Nξ = 44` de `ξ = −0,6λ` à `+0,9λ` (densifié près de `ξ_c` : distribution `ξ = ξ_c + sign·λ·(u²)` ) → 8 448 sommets, mis à jour chaque frame par `SurfWaveMeshJob : IJobParallelFor` (Burst), écrit dans `Mesh.MeshData` (positions, normales analytiques par différences finies centrées `±0,15 m`, uv0 = (s/L, (ξ−ξ_min)/(ξ_max−ξ_min)), uv1 = (φ, foam), couleur = (E, whitewater, tubeAO, 0)).
- **Ruban de lèvre** : `Ns × Nv = 192 × 14` sommets ×2 faces (dessus/dessous) + fermeture ; sous-maillage 1 (matériau eau-lèvre avec SSS).
- **Jupe** : les 3 dernières colonnes aux bords `s=0`, `s=L` et `ξ_min`, `ξ_max` fondent vers la hauteur ambiante (`OceanMath`) — poids `w = smoothstep` sur 6 m.
- Bounds calculées analytiquement ; `MeshUpdateFlags.DontRecalculateBounds | DontValidateIndices | DontResetBoneWeights`. Indices statiques (créés une fois). Read/Write activé (Unity 6.6 exige la déclaration au authoring pour les meshes lus CPU).
- **LOD** : si la caméra est à > 120 m, `Ns` → 96 (deux jeux d'index pré-construits).
- Coût cible : job < 0,6 ms sur 4 threads (8 448 + 5 376 sommets, ~40 flops/sommet).

### 5.4 Rendu
- Matériau **`SurfWaveWater`** (Shader Graph, URP Lit, Opaque avec « fake transparency » par couleur de profondeur bathymétrique ; variante Transparent + Scene Color sur presets High/Ultra pour la réfraction) :
  - Couleur = lerp(`shallowColor` (turquoise), `deepColor` (bleu profond), `exp(−SeabedDepth·k_absorb)`), teinte de fond de sable via `Terrain` (échantillonnage de la couleur du terrain par heightmap → texture de couleur de fond 512² générée au chargement du spot).
  - Normales : normale géométrique du maillage + `WaveNormals.png` (Storm Breakers) défilant ×2 (échelle 3 m et 0,6 m) atténué par le vent et annulé dans l'écume.
  - **Écume** : masque = `uv1.y` (foam analytique : φ ≥ 1,9, impact, jupe de whitewater) + courbure (dérivée seconde estimée via `ddx/ddy` de la hauteur) + texture de bruit (Kenney/SB `seaFoamFloatsOpaque`) ; couleur écume blanche légèrement bleutée, rugosité 0,9.
  - **SSS de lèvre** : terme `sss = pow(saturate(dot(V, −L_sun)), 4) · thickness` où `thickness` = 1 sur la lèvre (uv1.x ∈ [1,2]) et `smoothstep` près de la crête sur la face ; couleur `sssColor` vert-turquoise ×intensité.
  - **AO de tube** : `tubeAO` (couleur vertex) assombrit l'intérieur du tube (×0,55) et ajoute un léger brouillard d'embruns.
  - Réflexions : reflection probe (skybox) ; sur High : Screen Space Reflections **non** (URP n'en a pas nativement) → probe temps réel de Storm Breakers (miroir caméra, 128², refresh 2 Hz) en option Ultra.
  - Spécularité solaire : Lit standard (smoothness 0,92 eau, 0,3 écume).
- **Whitewater** : même matériau, écume à 1, plus un maillage « nuage » bas-poly (sphères instanciées ou strip VFX) pour le volume, sur High.
- **VFX Graph** (voir §13) alimentés par un `GraphicsBuffer` de `Ns` `CrestSample { float3 lipTip; float3 impact; float3 dir; float phase; float H; float speed; }` écrit chaque frame par `SurfWaveRenderer` (upload de 192×48 octets).

### 5.5 Cycle de vie et ordonnancement
- `WaveSetScheduler` (par spot) génère des **séries** : intervalle `[40, 120] s` (preset), `n` vagues espacées de `T` (période) avec hauteurs `H_i = H·(0,7 + 0,3·sin(π·(i+0,5)/n))·(1 + bruit ±10 %)` (la vague du milieu est la plus grosse). Il instancie (pool de 6) des `SurfWave` positionnées au large (`y_0` = 120 m avant `min f(s)`), en synchronisant l'océan ambiant : le plan de houle Storm Breakers a la même direction `D` et une longueur d'onde/intensité dérivées de (H, T) ; la SurfWave apparaît par fondu (jupe et `H` en rampe sur 4 s) quand la houle ambiante est sous elle → pas de « pop ».
- Une SurfWave meurt quand toute sa crête est en φ=3 depuis > `T_decay` ou a atteint le rivage (profondeur < 0,3 m) ; l'écume résiduelle est fondue vers l'ambiant.
- Ordre d'exécution (Script Execution Order) : `WaveClock` (−300) → `WaveSetScheduler` (−200) → `SurfWave.ScheduleJobs` (−100, Update) → … → `SurfWaveRenderer.CompleteAndUpload` (LateUpdate, avant Cinemachine) ; en `FixedUpdate` : `SurfWave.EvaluateFixed` (paramètres à `FixedTime`) → `RiderController`.

### 5.6 Authoring (Editor)
- `SurfSpot` : référence de scène, `Terrain` bathymétrique, `BreakLine` (SplineContainer), `SurfBreakProfile`, `D` (direction), points de lineup/respawn, limites de jeu, presets de conditions autorisés, caméra de présentation.
- `BreakLineTool` (Editor) : dessine la spline, affiche α(s), `V_s(s)`, `H(s)`, `I(s)` en couleurs, et un **aperçu figé** de la SurfWave à un `t` choisi (slider) directement dans la Scene View (utilise `SurfWaveMath`, pas de Play).
- Import bathymétrique optionnel : `SurfBreakProfile.FromTerrain(terrain, D, H)` calcule `f(s)` par la règle de déferlement `H/d = 0,78` (McCowan) le long de rayons parallèles à `D` → génère la BreakLine automatiquement (réalisme), que le designer peut ensuite retoucher.

---

## 6. Rider — contrôleur et physique

### 6.1 Représentation
- GameObject `Rider` : `Rigidbody` (mass 75 kg, `interpolation = Interpolate`, `isKinematic = true` sauf en Wipeout), `CapsuleCollider` (couché, r = 0,25, h = 1,2 m, layer `Rider`), `RiderController` (state machine), `SurfaceProbe` (échantillonne 5 points : centre, nez, tail, rails), enfant `BoardRoot` (planche visuelle + points d'IK), enfant `CharacterRoot` (mesh humanoïde + Animator + Rigs).
- **Repère de la planche** : `forward` = nez, `up` = normale de la planche, `right` = rail droit. Position du rider = point de contact (centre de la planche) contraint à la surface : `y = Height + draft` (`draft` = 0,05 m en ride, 0,18 m en rame).
- **Physique en FixedUpdate (1/60 s)** : intégration semi-implicite maison (pas de forces PhysX en ride : déterminisme et contrôle du feel), PhysX seulement pour la collision avec rochers/bouées et le ragdoll.

### 6.2 Machine à états
```
Paddle ⇄ DuckDive
Paddle → TakeOff (conditions) → Ride
Ride ⇄ Tube (sous-état) ; Ride → Air (pop/lèvre) → Land → Ride | Wipeout
Ride → KickOut → Paddle ; Ride/Air/Tube → Wipeout → Recover → Paddle
Any → Reset (respawn lineup)
```
| État | Entrée | Sortie | Comportement |
|---|---|---|---|
| **Paddle** | défaut, Recover, KickOut | TakeOff, DuckDive | Flotte (ressort `k = 40 N/kg`, amort. 8) sur `Height`, avance stick (`v_max` 2,0 m/s ; sprint = maintenir Croix : 2,8 m/s, fatigue 6 s puis 1,6 m/s), tourne (90°/s), suit la houle (`Velocity` ambiante ×0,6). Mousse (`WhitewaterAmount > 0,5`) : poussée `Velocity·1,2` vers la plage + roulis caméra. |
| **DuckDive** | Rond en Paddle, face à la vague (angle < 60°) | auto (1,3 s) | Plonge (`draft` → 1,3 m sur 0,4 s), ignore 85 % de la poussée de mousse, vitesse conservée ; caméra passe sous l'eau (post-process). Cooldown 0,8 s. |
| **TakeOff** | Paddle et : `BreakPhase ∈ [0,55 ; 1,6]`, pente face `> 0,25` (≈14°), `E > 0,35`, vitesse rider selon `D` > 1,4 m/s, `PeelDistance ∈ [−5, 45] m` | Ride (0,5 s) ou retour Paddle si la vague passe (φ > 2 ou pente < 0,1) | Interpolation vers l'état Ride : `v ← c·D·0,55 + v_paddle`, `draft` → 0,05 ; animation de bascule. |
| **Ride** | TakeOff, Land | Air, Tube, KickOut, Wipeout | §6.3 |
| **Tube** (sous-état de Ride) | `InTube` vrai 0,2 s | `InTube` faux 0,3 s | Physique Ride + traînée −10 % (aspiration) ; score tube ; foam ball : si `dist(P_i) < 0,25 H` → turbulence (bruit 3 m/s) et 60 % de wipeout/s ; **lèvre** : si la capsule intersecte la lèvre (test analytique lèvre vs capsule) → Wipeout « lipped ». |
| **Air** | Croix (pop) avec `ξ` près de la crête (`|ξ − ξ_c| < 0,25λ`) et vitesse verticale de la face > 0, ou éjection naturelle (rider quitte la surface avec `v·U > 1,5` m/s) | Land (contact surface), Wipeout (impact lèvre/rocher, ou chute > 3 s) | Balistique `v += g·dt` (g = 9,81), traînée air 0,02·v², contrôle de rotation (§7), grab. |
| **Land** | Air quand `y ≤ Height + 0,05` | Ride (succès) / Wipeout | Évaluation §7.4 ; sur succès : `v` projetée sur la surface, pénalité de vitesse `×(0,85 + 0,15·alignement)` ; caméra impulse. |
| **KickOut** | Croix sur épaule (`E < 0,3`, φ < 1) ou fin de vague (φ = 3 partout autour) | Paddle (1,0 s) | Score de vague validé ; passe par-dessus le dos de la vague. |
| **Wipeout** | conditions ci-dessus, pente trop forte sans vitesse (`slope > 1,2` et `v < 3`), collision rocher, foam ball | Recover (3–5 s) | Ragdoll (§12) : `Rigidbody` dynamique, joints actifs, poussée `Velocity` de l'eau, flottabilité simple par os ; caméra sous-marine, apnée (HUD), sons étouffés. Score : tricks non validés perdus. |
| **Recover** | timer | Paddle | Rider remonte à la surface (blend ragdoll → animation « pull up »), planche revient (leash). |

### 6.3 Équations du ride (FixedUpdate, `dt = 1/60`)
Notations : `p` position, `v` vitesse (3D, tangentielle), `n` normale de surface, `g = 9,81`, `ŷ` up monde, `F` avant planche (horizontal projeté sur le plan tangent), `R` rail droit = `F × n`.
1. **Échantillonnage** : `S = water.Sample(p, FixedTime)`.
2. **Gravité projetée** : `a_g = −g·(ŷ − (n·ŷ)·n)` (vecteur dans le plan tangent, vers le bas de la pente).
3. **Poussée de vague** : `a_push = (S.Velocity − v)·k_push·S.Energy / τ_push` avec `k_push = 0,8`, `τ_push = 0,9 s` (l'eau « emmène » le rider dans la pocket).
4. **Traînée** : `a_drag = −C_d·|v|·v` avec `C_d = 0,012` (prone, board) ; stall : `C_d ×3,2` ; trim avant (stick Y > 0) : `C_d ×0,85` et contrôle latéral −25 % ; trim arrière : `C_d ×1,3`, contrôle +20 %.
5. **Direction et rail** : entrée `x ∈ [−1,1]`. Angle de lean `β = x·β_max` (`β_max = 55°`), yaw rate `ω = x·ω_max·f_v` avec `ω_max = 140°/s`, `f_v = clamp(|v|/8, 0,35, 1,2)`. `F` tourne autour de `n` de `ω·dt`. **Slip** : décomposer `v = v_F·F + v_R·R` ; glissement latéral amorti `v_R ← v_R·exp(−dt/τ_grip)` avec `τ_grip = 0,22 s·(1 + 0,8·(1 − |x|))` (rail engagé = accroche), sur écume `τ_grip ×2,5` ; force centripète implicite → **carve** = rotation de `v` vers `F` : `v ← |v|·normalize(lerp(v̂, F, 1 − exp(−dt/τ_align)))`, `τ_align = 0,35 s`.
6. **Pump** : `PumpMeter` : chaque pression R2 crée une impulsion si `|dt_since_last| ∈ [0,35 ; 1,2] s` et si la planche descend la face (`v·a_g > 0`) : `v += F·(0,08·|v| + 0,4)·E_local` ; sinon −3 %. Trois pompes efficaces max par « descente ». Feedback : animation, léger FOV, son.
7. **Intégration** : `v += (a_g + a_push + a_drag)·dt` ; `p += v·dt` ; **contrainte** : `p.y = S'.Height + draft` (re-sample à `p.xz`), projeter `v` sur le plan tangent ; si `v·U_face > v_eject` et `ξ` proche crête → transition Air (éjection).
8. **Limites** : `|v| ≤ 16 m/s` ; pente trop raide sans vitesse → glissade (a_g domine) et wipeout si `n·ŷ < 0,35` (≈70°) et `|v| < 3`.
9. **Wall clipping** : si `p` passe derrière la crête (`ξ < ξ_c − 0,05λ`) en Ride → KickOut forcé (il passe le dos).

Tous les coefficients sont dans `RiderTuning` (SO) ; `BoardSpec` (SO) module `C_d`, `τ_grip`, `ω_max`, `β_max`, `pop` (tail crescent : +15 % grip, −10 % ω ; bat tail : inverse).

### 6.4 Take-off et lecture de vague — aide au joueur
- **Indicateur de zone** (option « Assist » ON par défaut en Baie, OFF en Dalle) : décalcomanie (URP Decal) sur l'eau montrant la zone de take-off recommandée (`E` de la prochaine SurfWave projetée) et une flèche de direction de rame.
- **Auto-orientation légère** en Paddle : si le joueur pousse vers la plage à moins de 25° de `D`, on aligne progressivement (+30°/s).
- **Bascule assistée** : en TakeOff, la vitesse initiale est garantie ≥ 0,55 c pour éviter le « raté » frustrant ; le raté existe si le joueur est trop sur l'épaule (E faible) → la vague passe.

---

## 7. Tricks

### 7.1 Détection d'entrée
`TrickDetector` consomme les actions (Input System) avec horodatage et un buffer de 0,5 s :
- **Pop** : Croix pressée (buffer 0,2 s) quand la fenêtre de lèvre est ouverte (`|ξ − ξ_c| < 0,25λ` et φ ∈ [0,7 ; 2,2]) → Air avec `v_up = pop·(0,6 + 0,4·|v|/10)·(0,7 + 0,3·ramp(s))`, `pop = 2,5 m/s`. Sans pression, l'éjection naturelle donne un air plus faible (`v_up` naturel).
- **Rotations** : stick droit ; **flick** (magnitude > 0,8 atteinte en < 0,12 s puis relâché) = rotation rapide déclenchée ; **hold** = rotation continue tant que maintenu (plus lente, plus contrôlée). Axe X → spin (autour de `ŷ`), Y+ → backflip (autour de `R`), Y− → invert (rotation partielle autour de `R` inverse + extension), diagonales → combinaisons (spin + flip) autorisées seulement si vitesse > 9 m/s.
- **El Rollo** : R1 dans la fenêtre « contact lèvre » : le rider est en Air ou en Ride avec `ξ ∈ [ξ_c − 0,1λ, ξ_c + 0,15λ]` et φ ∈ [1,0 ; 1,8] → rotation **assistée** autour de l'axe `F` (roll), suivant la trajectoire de la lèvre (le rider est « porté » : `p` suit la Bézier de lèvre à 60 % + balistique 40 % pendant 0,6 s) puis balistique pure. Stick droit latéral pendant le roll → **ARS** (ajoute un spin 360° autour de `ŷ`).
- **Grab** : Carré maintenu en Air : rotation ×0,8, score ×1,15, pose de grab.
- **Drop-knee** : L1 bascule la stance (0,4 s de transition, impossible en Air) ; en DK : `ω_max` +25 %, `τ_grip` −20 %, pumping −30 %, tricks « DK snap » et « DK 360 » (stick X + R1).
- **Kick-out** : Croix sur épaule (E < 0,3).

### 7.2 Contrôle de rotation en Air
`AirRotationController` : orientation cible = orientation actuelle + rotation demandée ; vitesse angulaire : flick 400°/s (spin), 330°/s (flip), hold 220°/s ; accélération 1 800°/s² ; **auto-complete** : quand aucune entrée, la rotation se termine au multiple de 180° le plus proche si l'écart < 60° et le temps restant (estimé via balistique + surface) < 0,45 s — « aide à l'atterrissage » réglable (Assist : Full/Light/Off).

### 7.3 Nommage
`TrickDef` (SO) : id, nom FR/EN, catégorie, entrée(s), rotation nominale (axes/angles), conditions (vitesse min, phase de lèvre, stance), score base, difficulté, animation/pose. Le nom affiché est composé : `[Grab] + [Rollo|Backflip|Invert|Air] + [Forward|Reverse] + [180…900]` (« ARS » si Rollo + spin ≥ 270°).

### 7.4 Atterrissage (`LandingEvaluator`)
À l'instant de contact : `align = dot(board.up, n)`, `yawErr` = angle entre `F` et `v̂` projetés, `ωres` vitesse angulaire résiduelle.
- **Succès** : `align > 0,82` (≈35°) et `yawErr < 40°` et `ωres < 250°/s` → exécution `= 0,6 + 0,9·smoothstep(0,82, 0,98, align)·(1 − yawErr/40°)`.
- **Sketchy** (succès dégradé) : `align ∈ [0,7 ; 0,82]` → exécution 0,6, animation de rattrapage, vitesse ×0,7.
- **Échec** → Wipeout.
- **Zones** : atterrir dans la mousse ou le flat après un air = autorisé (les tricks d'air se finissent souvent dans l'écume), mais atterrir « sur la lèvre en train de tomber » = Wipeout.
- **Commit** : le score du trick entre dans la chaîne après 1,0 s de contrôle (Ride sans Wipeout).

---

## 8. Notation et session
- `WaveScorer` accumule par vague : tricks validés (avec horodatage, `E` au départ, section), temps de tube pondéré, chaîne de flow, pénalités. Formule §2.7.
- `JudgePanel` : convertit `R` en note 0–10, génère 3 sous-notes cosmétiques, émet `ScoreEvents.WaveScored`.
- `SessionManager` : gère le mode (Free/Heat), le timer de heat (accélération hors ride), les vagues comptées (une « vague » commence au TakeOff et finit au KickOut/Wipeout/fin de vague), les résultats (deux meilleures), le classement local (JSON).
- **HUD** : nom du trick + points en popup (empilés, couleur par catégorie), multiplicateur de chaîne, timer de tube, note de vague à la fin (animation « juges »), meilleures notes en Heat, timer et compteur de vagues.

---

## 9. Caméra (Cinemachine 3)
- `CinemachineBrain` sur la Main Camera (`Default Blend` 0,6 s EaseInOut). Caméras (priorités) :
  - **RideCam** (10) : `CinemachineCamera` + `CinemachineThirdPersonFollow` (shoulder offset (0,6 ; 0,9 ; 0), camera distance 4,5 m, damping (0,25 ; 0,4 ; 0,35)) + `CinemachineRotationComposer` (look-ahead 0,35 s, dead zone 0,08) ciblant `Rider/CameraTarget` (décalé de 0,4·v̂ vers l'avant, lissé) ; **FOV** = 55 + 15·clamp(|v|/14) ; **dutch** = −6°·x (lean) ; hauteur minimale au-dessus de l'eau 0,35 m (clamp après solve, `CinemachineDecollider` avec layer `Water` désactivé car l'eau n'est pas collidable : clamp custom en `CinemachineExtension`).
  - **TubeCam** (20 quand `InTube` ≥ 0,25 s) : derrière et légèrement extérieure (offset (−1,2 ; 0,5 ; −3,0) dans le repère vague), FOV 68, regarde vers la sortie du tube (point `ξ_c + w_v` à +20 m le long de `T`), damping 0,15, noise Perlin faible.
  - **AirCam** (20 en Air) : suit avec damping plus lent (0,6), FOV 62, garde la lèvre et le rider dans le cadre (`CinemachineTargetGroup` rider + point d'impact), léger ralenti de l'orbite.
  - **WipeoutCam** (30) : sous l'eau/à la surface avec `Underwater` post-process, noise fort, puis remonte.
  - **LineupCam** (5) : en Paddle : plus large (distance 6 m, FOV 60), regarde vers le large quand une série arrive (poids sur `CinemachineTargetGroup` rider + point « série »).
  - **ReplayCam** (P8) : `CinemachineSplineDolly` autour du spot pour le replay de la meilleure vague (Recorder non requis : replay par rejeu déterministe des états sauvegardés à 30 Hz).
- **Impulses** : `CinemachineImpulseSource` sur atterrissage (amplitude ∝ v_impact), lèvre qui frappe près du rider, wipeout.
- **Stick droit** en Ride (hors Air) : nudge d'orbite ±25° avec retour automatique en 1,5 s.

---

## 10. Rendu, éclairage, environnement
- **Skybox** : HDRI Poly Haven (`Skybox/Cubemap`), Ambient = Skybox (patch SB pour l'éclairage particules), Reflection = Skybox ; par preset horaire : `secluded_beach` (matin, défaut), `umhlanga_sunrise`, `spiaggia_di_mondello` (midi), `venice_sunset`/`the_sky_is_on_fire` (soir), `fish_hoek_beach` (couvert). Le `Directional Light` est aligné sur le soleil de l'HDRI (angle stocké dans `EnvironmentPreset`), intensité 2,5–4 (HDR), ombres 2 cascades (Low) / 4 (High), distance 60/150 m, soft shadows High+.
- **Volumes** : `GlobalVolume` (Tonemapping ACES, Bloom 0,15 seuil 1,1, Vignette 0,15, Color Adjustments : contraste +10, saturation +8, White Balance chaud +5 le soir), `UnderwaterVolume` (Color Adjustments teinte cyan, Depth of Field léger, Vignette 0,4, `Underwater.shadergraph` fullscreen pass avec caustiques/bulles sur High), `TubeVolume` (léger blue tint, vignette 0,25).
- **Terrain** : Unity Terrain 512×512 m (heightmap 513) par spot, 3–4 `TerrainLayer` (Poly Haven `coast_sand_02` sec, `coast_sand_05` mouillé, `coast_sand_rocks_02`, `rock_face_03`), hauteur-based blend via masque ; Detail : `grass_bermuda_01` (GPU instancing) ; arbres `island_tree_*` ; rochers glTF (colliders mesh convexes simplifiés ou capsules). Le **fond marin** est le même terrain (continuité plage → bathymétrie).
- **Eau** : océan ambiant SB (opaque) + SurfWaves (§5.4). Interaction rider/eau : décalcomanie d'ondulation (URP Decal) sous la planche en Paddle, VFX de sillage en Ride.
- **Mesh LOD / GPU Resident Drawer** : rochers et arbres en `MeshRenderer` statiques (GRD + GPU occlusion sur High) ; Mesh LOD (Unity 6) pour les modèles Poly Haven 2k si > 30k triangles (générer via import settings « Mesh LOD »).
- **Anti-aliasing** : Low = FXAA ; Medium = TAA ; High = STP (render scale 0,75 → 1) ou MSAA 4x (sans STP) ; Ultra = MSAA 4x + TAA off + render scale 1,0. Sur Iris Xe : FXAA + render scale 0,75.
- **APV/lightmaps** : pas de baking en v1 (extérieur, soleil unique) ; `Light Probe Group` léger pour le personnage près des rochers (optionnel).

---

## 11. Input (Input System 1.20)
- Asset `Assets/_Project/Settings/Input/BiscotteActions.inputactions` déclaré **Project-wide** (`Edit > Project Settings > Input System Package > Project-wide Actions`), classe C# générée `BiscotteActions`. Le fichier `Assets/InputSystem_Actions.inputactions` du template est supprimé.
- **Action maps** : `Surf` (Move: Vector2 ; Look: Vector2 ; Pump ; Stall ; Pop ; Rollo ; Grab ; Stance ; DuckDiveBail ; Reset ; Pause ; AirRotate: Vector2 (stick droit / souris delta) ; CameraNudge: Vector2), `UI` (Navigate, Submit, Cancel, Point, Click, ScrollWheel, TabLeft, TabRight), `Debug` (ToggleOverlay, SlowMo, TeleportPeak, NextWave).
- **Control schemes** : `KeyboardMouse` (requiert Keyboard + Mouse) et `Gamepad` (requiert Gamepad). Bindings gamepad génériques (`<Gamepad>/buttonSouth`…) → couvre DualSense (USB/BT) et Xbox. Processors : stick deadzone 0,12/0,92, `AirRotate` avec `ScaleVector2` pour la souris (sensibilité réglable), `Invert Y` option.
- **Détection de périphérique** : `InputUser`/`PlayerInput` non utilisés (solo) ; `InputRouter` écoute `InputSystem.onActionChange`/`onEvent` pour déterminer le **dernier périphérique actif** → `GlyphProvider` choisit le jeu Kenney : `PlayStation` si `device is DualSenseGamepadHID || DualShockGamepad`, `Xbox` si `XInputController`, sinon `Generic` ; clavier/souris → `KeyboardMouse`. Les glyphes sont des sprites Kenney (sheets) référencés par nom de contrôle (`buttonSouth` → `playstation_button_cross`, etc., table dans `GlyphMap.asset`).
- **Haptique DualSense** : `Haptics` service : `Gamepad.current.SetMotorSpeeds(low, high)` avec courbes : atterrissage (0,6/0,3 sur 0,15 s), passage dans la mousse (0,25 continu modulé), lèvre qui frappe (0,9/0,6 × 0,25 s), tube (0,15 basse fréquence continue), pump réussi (0,2/0,5 × 0,08 s). `DualSenseGamepadHID.SetLightBarColor` : bleu (lineup) → cyan (ride) → vert (tube) → orange (air) → rouge (wipeout). Tout est ignoré silencieusement en Bluetooth (limitation Unity documentée) — message d'aide dans Options. **Gâchettes adaptatives : hors périmètre v1** (stretch P8 : rapport HID de sortie à la UniSense, MIT).
- **Rebinding** : écran Options → `InputActionRebindingExtensions.PerformInteractiveRebinding` par binding, sauvegarde `actions.SaveBindingOverridesAsJson()` dans le fichier de sauvegarde, restauration au boot ; base : sample « Rebinding UI » du package (référence, pas copié tel quel).
- **Clavier AZERTY** : bindings sur codes physiques (`<Keyboard>/w` = touche physique ; Unity affiche la lettre locale via `displayName`) → pas de cas spécial, mais les glyphes affichent la lettre réelle (`InputControlPath.ToHumanReadableString`).

---

## 12. Personnage, animation, planche
- **Rider** : `Mannequin_F.fbx` (Quaternius, Universal Rig) importé en **Humanoid** (avatar créé, T-pose validée) ; les clips UAL (`AnimationLibrary_Unity_Standard.fbx`, `UAL2_Standard.fbx`) importés en Humanoid également (retarget) ; clips utiles : `Swim_Fwd_Loop`/`Swim_Idle_Loop` (rame, retravaillés par IK), `Jump_*`, `Roll` (base El Rollo), `Hit_Knockback`/`Death01` (wipeout enter), `LayToIdle`, `Slide_*`, `Idle_*`. Quand Universal Base Characters (itch) sera fourni par l'humain, il remplace le mannequin sans changer les clips (même rig).
- **Poses prone** : construites par **Animation Rigging** plutôt que par clips : `Rig_Prone` : `TwoBoneIKConstraint` mains → poignées `BoardRoot/GripNose`, `GripRail` ; `MultiAimConstraint` tête → direction de regard (look-ahead/lèvre) ; `ChainIKConstraint` colonne → cambrure selon `lean` ; jambes : `TwoBoneIK` genoux/pieds → `FinTargets` (palmes qui traînent/battent). Les paramètres (poids, offsets) sont pilotés par `RiderAnimationDriver` depuis l'état : Paddle (bras alternés : cycle procédural sinus 1,4 Hz sur les cibles d'IK + clip `Swim_Fwd_Loop` en additive 30 %), Ride (poses de carve gauche/droite par blend de cibles, trim avant/arrière = translation du torse), Tube (rider tassé, tête tournée vers la sortie), Air (poses par trick : Rollo = roll de `CharacterRoot`, Backflip/Invert = clips courts keyframés dans Unity + rotation racine), Land (compression), DK (pied avant sur le deck via IK, genou arrière posé).
- **Planche** : mesh procédural `BodyboardMeshBuilder` (ProBuilder ou API Mesh : 105 × 55 × 5,5 cm, nez arrondi, tail crescent/bat, canaux, rails), matériau Lit (deck mat coloré, slick brillant), `BoardSpec` définit forme/couleurs ; 3 variantes (Crescent, Bat, Wide). Leash (LineRenderer + ressort) attaché au poignet.
- **Ragdoll** : généré une fois via `Ragdoll Wizard` (Editor) sur le mannequin → prefab `RiderRagdoll` (joints, colliders) ; en Wipeout, on active les `Rigidbody` d'os, on désactive Animator/Rigs ; forces : poussée de l'eau (`Velocity`), flottabilité par os (`k = 12 N/kg` sous la surface), traînée 0,6. Retour : blend « Get up » via `Animator` + snap sur planche.
- **Palmes** : deux meshes simples (ProBuilder) sur les pieds, animées par IK ; spray VFX sur battement en rame.

---

## 13. VFX (VFX Graph, GPU)
| Effet | Source | Comportement | Budget Low / High |
|---|---|---|---|
| `LipSpray` | `CrestSample.lipTip`, `dir`, `phase ∈ [1,2]` | Gouttes fines projetées depuis la lèvre le long de `D`, emportées par le vent (offshore = long panache arrière), fade 0,8 s | 4k / 20k particules |
| `ImpactFoam` | `CrestSample.impact`, `phase ∈ [1,6 ; 2,6]` | Bursts d'écume blanche épaisse (textures SB `seaFoamBurst`), taille ∝ H, retombée puis conversion en `FaceFoam` | 2k / 10k |
| `FaceFoam` | maillage (uv1.y foam) + impacts | Particules-flotteurs collées à la surface (échantillonnage `SurfWaveMath` en Shader Graph VFX via buffer de paramètres ou via readback zéro : les particules lisent une **texture de hauteur** 256×64 rendue par le job → `Texture2D` upload chaque frame (32 Ko)) | 3k / 12k |
| `Whitewater` | phase 3 | Volume de mousse roulante (strips + soft particles), bruit | 2k / 8k |
| `RiderSpray` | rails de la planche, `|v_R|`, lean | Gerbe latérale en carve, sillage en ligne droite | 1k / 4k |
| `Splash` | Land/Wipeout/DuckDive | Burst + gouttes + anneau (décal) | 1k / 3k |
| `PaddleSplash` | mains en rame | Petites éclaboussures rythmées | 0,3k / 1k |
| Ambiant SB `oceanVFX` | Storm Breakers | Déferlantes au large (masquées dans l'empreinte SurfWave) | capacité ×0,5 / ×1 |
`VfxBudget` ajuste les capacités par preset ; tous les VFX utilisent l'éclairage « fake lit » (ambiant + soleil ×0,5, technique SB) pour le coût, sauf `Splash` (Lit) sur High.

---

## 14. Audio
- `AudioMixer` `BiscotteMixer` : groupes Master / Ambience / Waves / Rider / VFX / UI / Music ; snapshots `Surface`, `Underwater` (low-pass 600 Hz, réverb), `Tube` (réverb courte, low-pass 2 kHz, boost des graves), `Menu`.
- `OceanAudio` : boucle d'ambiance (`ocean_ambience_loop_57s_generated.wav`, à remplacer par un enregistrement Sonniss/Freesound CC0 si fourni) + **synthèse procédurale** : bruit rose filtré (comme `BreakersAudio`) modulé par la somme des `whitewater` proches de la caméra (rayon 60 m) ; déferlement de la SurfWave : one-shots `beach_wave_0x` déclenchés au passage φ 1→2 du segment le plus proche (pitch ±10 %, volume ∝ H), spatialisés.
- `RiderAudio` : sifflement de planche (bruit blanc filtré, cutoff ∝ |v|, volume ∝ |v|²), rail en carve (bruit + grain), pump (whoosh court), atterrissage (impact Kenney `impactSoft_*` + splash), wipeout (splash lourd + bulles), rame (clapotis rythmés), respiration en apnée.
- `UiAudio` : Kenney interface sounds ; trick popups (ding/whoosh courts), score de vague (tambour/jingle court synthétisé ou Kenney).
- Musique : hors périmètre v1 (emplacement prévu ; sources royalty-free listées dans `RESEARCH_SOURCES.md`).

---

## 15. UI / UX (UI Toolkit)
- **Boot** → **MainMenu** (Free Surf, Heat, Options, Quitter ; fond : caméra dolly sur un spot avec vagues) → **SpotSelect** (cartes des 3 spots, difficulté, meilleurs scores) → **ConditionsEditor** (sliders H/T/direction/vent/marée/heure + presets ; aperçu texte : « 1,8 m, 11 s, offshore léger ») → **Session** → **Results** (notes, tricks, défis) → retour.
- **HUD** (`HUD.uxml`) : bas gauche vitesse (arc) + jauge de pump ; bas centre glyphes contextuels (2–3 max : « Pump R2 », « Rollo R1 » quand disponible) ; haut droite timer/vagues/meilleures notes (Heat) ; centre haut popups de tricks + chaîne ; indicateur de tube (barre de profondeur) ; message d'apnée en wipeout ; annonce de série (« Série dans 8 s » + flèche). Tout en `USS` avec variables (couleurs sable/turquoise/blanc), polices Bebas Neue (chiffres), Righteous (tricks), Nunito (texte).
- **Pause** : Reprendre / Options / Changer conditions (Free) / Quitter session.
- **Options** : Graphismes (preset, résolution, plein écran, VSync, render scale, upscaler, FOV), Audio (5 volumes), Contrôles (rebinding, sensibilité souris, inversion Y, vibration ON/OFF, assist d'atterrissage Full/Light/Off, indicateur de take-off), Langue (FR/EN), Accessibilité (ralenti global 0,8–1,0, taille HUD, daltonisme : palettes).
- **Localisation** : tables CSV → `ScriptableObject` `LocTable` ; clé → texte ; changement à chaud.
- **Sauvegarde** : `SaveSystem` JSON (`Application.persistentDataPath/biscotte_save.json`) : options, overrides de bindings, meilleurs scores par spot/mode, défis, déblocages. Écriture atomique (fichier temporaire + rename).

---

## 16. Performance et qualité
### 16.1 Budgets (1080p, preset Low, Iris Xe — mesurés sur la machine de dev)
| Poste | CPU main thread | GPU |
|---|---|---|
| SurfWave jobs (2 vagues actives) | ≤ 1,0 ms (workers) + 0,3 ms upload | maillage ≤ 1,5 ms |
| Océan ambiant SB (vertex shader) | 0,2 ms | ≤ 3,5 ms |
| Rider + tricks + caméra | ≤ 0,5 ms | — |
| VFX Graph | ≤ 0,4 ms | ≤ 3,0 ms |
| Terrain + rochers + végétation | ≤ 0,8 ms (culling) | ≤ 3,0 ms |
| Post-process + UI | 0,3 ms | ≤ 1,5 ms |
| Ombres | 0,5 ms | ≤ 1,5 ms |
| **Total** | **≤ 8 ms** | **≤ 14 ms** (60 fps avec marge) |

### 16.2 Presets
| Preset | Render scale | AA | Ombres | VFX | Eau |
|---|---|---|---|---|---|
| Low (iGPU) | 0,75 | FXAA | 1 cascade 1024, 40 m | ×0,25 | Opaque, pas de réfraction, normales ×1 |
| Medium | 0,85 | TAA | 2 cascades 2048, 60 m | ×0,5 | Opaque + normales ×2 |
| High | 1,0 (ou STP 0,77→1) | STP/TAA | 4 cascades 2048, 120 m, soft | ×1 | Transparent + réfraction Scene Color, probe temps réel 2 Hz |
| Ultra | 1,0 | MSAA 4x | 4 cascades 4096, 200 m, soft | ×1,5 | Idem + textures 4k, Mesh LOD off |
Réglages URP par preset = 4 `UniversalRenderPipelineAsset` (`Assets/_Project/Settings/Quality/`) + `QualitySettings` 4 niveaux ; `Mobile_*` du template supprimés.

### 16.3 Règles
- Profiler avant/après chaque phase (`get_performance_stats`, Unity Profiler, Frame Debugger, Rendering Statistics). Project Auditor (`unity command audit`) à chaque fin de phase.
- Pas de `Camera.main` dans les boucles (cache), pas de `GetComponent` en Update, pas de `Find`.
- Textures : 2k max (Low/Med/High), mipmaps, compression BC7/BC5 ; HDRI 4k → cubemap 1024 (Low) / 2048 (High) via import settings.
- Ordre d'exécution documenté (§5.5) ; `Physics.autoSyncTransforms = false` ; `Time.fixedDeltaTime = 1/60` ; `maximumDeltaTime = 1/20`.
- VSync réglable ; `Application.targetFrameRate` −1 par défaut.

---

## 17. Données, sauvegarde, debug
- ScriptableObjects (`Assets/_Project/Data`) : `SurfSpot`, `SurfBreakProfile`, `ConditionsPreset`, `EnvironmentPreset`, `SurfWaveTuning`, `RiderTuning`, `BoardSpec`, `CameraTuning`, `TrickCatalog`/`TrickDef`, `ScoringTuning`, `HapticsProfile`, `GlyphMap`, `LocTable`, `DebugSettings`, `VfxBudget`.
- Debug (`F1`) : overlay (état rider, |v|, E, φ, ξ, peel distance, tube depth, chaîne de score, fps, ms CPU/GPU), gizmos (BreakLine α, lèvre, impact, échantillons de surface), cheat menu (spawn vague maintenant, set conditions, téléport au peak, slow-mo, invincible, caméra libre).
- Replay (P8) : enregistrement des états rider/vagues (30 Hz) pour rejouer la meilleure vague (déterminisme garanti par `WaveClock` et seeds).

---

## 18. Structure projet, asmdefs, conventions
Voir `Docs/AGENT_PLAYBOOK.md` §3–4 (fait foi). Rappels : `Biscotte.Runtime` référence `Unity.Splines`, `Unity.Cinemachine`, `Unity.InputSystem`, `Unity.Burst`, `Unity.Mathematics`, `Unity.Collections`, `StormBreakers.Runtime`, `Unity.RenderPipelines.Universal.Runtime` (Decal, Volume), `Unity.TextMeshPro` (si uGUI utilisé pour des popups monde), `Unity.VisualEffectGraph.Runtime` ; `Biscotte.Editor` référence en plus `Unity.Splines.Editor`, `Unity.Cinemachine.Editor`. Allow unsafe code : OUI pour `Biscotte.Runtime` (écriture MeshData/GraphicsBuffer).

---

## 19. Plan d'implémentation par phases

Format : `Pn-Tk` — tâche ; **DoD** (definition of done) ; **Vérif** (commandes). Une tâche = un commit. Les phases sont séquentielles ; à l'intérieur, les tâches marquées ∥ peuvent être parallélisées (agents distincts, dossiers distincts).

### P0 — Fondations du projet (≈ 1 jour)
- **P0-T1** Nettoyage template : supprimer `Assets/TutorialInfo`, `Assets/Readme.asset`, `Assets/InputSystem_Actions.inputactions`, `Mobile_RPAsset/Mobile_Renderer`, niveau de qualité « Mobile » ; renommer `SampleScene` → `Playground`. Player Settings : companyName « ForgeOff », productName « Biscotte », resolution fullscreen window, run in background ON, API DX12 + DX11 fallback (liste explicite), `apiCompatibilityLevel` .NET Standard 2.1 (défaut), Incremental GC ON. DoD : projet compile, scène `Playground` ouverte. Vérif : `get_player_settings`, `get_quality_settings`, `list_open_scenes`.
- **P0-T2** Arborescence `Assets/_Project/*` + asmdefs (`Biscotte.Runtime` unsafe, `Biscotte.Editor`, tests) + `Tools/` + `.editorconfig`. DoD : `recompile_status = completed`, 0 warning nouveau. ∥
- **P0-T3** Qualité/URP : 4 URP assets Low/Medium/High/Ultra (clonés de `PC_RPAsset`, réglages §16.2), `QualitySettings` 4 niveaux (High par défaut ; Low forcé automatiquement au premier lancement si `SystemInfo.graphicsMemorySize < 3000` ou GPU intégré détecté), renderer Forward+ vérifié, GRD Instanced, Depth/Opaque texture ON, HDR ON, `Time.fixedDeltaTime = 1/60`. DoD : fichiers dans `Settings/Quality`, `get_quality_settings` conforme. ∥
- **P0-T4** Input : `BiscotteActions.inputactions` (maps/actions/schemes §11), project-wide, classe générée. DoD : `InputSystem.actions` non nul en Play ; test EditMode charge l'asset et vérifie les actions attendues. ∥
- **P0-T5** Core : `GameBootstrap` (scène `Boot` → charge `MainMenu` ou `Playground` en dev), `WaveClock`, `ServiceRegistry`, `DebugSettings`, `DebugOverlay` (F1, fps/ms), `SaveSystem` (JSON, tests EditMode). DoD : tests verts. ∥
- **P0-T6** Git : commit baseline (humain ou agent autorisé) ; `git lfs ls-files` non vide. Vérif : `git status` propre.
- **P0-T7** Tests infra : `Biscotte.Tests.EditMode` avec un test trivial ; `unity command run_tests --mode EditMode` passe. DoD : rapport JUnit dans `Build/`.

### P1 — Océan ambiant et environnement (≈ 2–3 jours)
- **P1-T1** Scène `Playground` : `Ocean.prefab` SB instancié, `OceanAmbient` (wrapper) avec preset « Baie matin » (λ = 35/14/6/2 m, intensités 0,9/0,8/0,7/0,9, directions vers la plage), vent 4 m/s offshore, HDRI `secluded_beach`, soleil aligné, `GlobalVolume`. DoD : capture 1600×900 nette, 0 erreur console, fps ≥ 60 en Low sur Iris Xe (`get_performance_stats`).
- **P1-T2** Patchs SB documentés : (a) éclairage particules via `ambientProbe` ; (b) `ResetStatics` ; (c) « wave mask » globals dans `ocean.shadergraph` (4 boîtes) ; (d) `FindObjectOfType` → `FindFirstObjectByType` ; (e) suppression de la dépendance à `Camera.main` dans `BreakersAudio` (injection). DoD : `PATCHES.txt` à jour, scènes d'exemple SB toujours fonctionnelles.
- **P1-T3** `OceanMath` (port Burst de `Ocean.OceanDeformation/GetHeight/GetNormal/GetVelocity`) + test de parité (1000 points aléatoires, 20 temps, tol 1e-4) + benchmark (1000 échantillons < 0,25 ms Burst). DoD : tests verts. ∥
- **P1-T4** `WaterSurfaceComposite` (ambiant seul pour l'instant) + `SampleBatch` job + gizmo d'échantillonnage (grille 20×20 flottante). DoD : gizmos collés à la surface visuelle (écart < 3 cm mesuré par test PlayMode qui compare `Sample` et le vertex shader via `Ocean.OceanDeformation`). ∥
- **P1-T5a** Optimisation des modèles Poly Haven (obligatoire avant tout placement) : `Tools/OptimizePolyHavenModels.cs` (run_script) → copie des meshes glTF dans `Assets/_Project/Art/Models/PolyHaven/`, génération de Mesh LODs (`MeshLodUtility.GenerateMeshLods`), mesh de base décimé (cibles : rochers/falaises ≤ 60 k tris, arbres ≤ 40 k, petits props ≤ 20 k ; repli UnityMeshSimplifier MIT), matériaux URP/Lit (diff/nor_gl/arm → mask map), colliders convexes simplifiés (≤ 255 tris) ou capsules, prefabs dans `Assets/_Project/Prefabs/Env/`. DoD : tableau tris avant/après dans `Docs/perf/P1.md` ; scène de test avec les 16 prefabs ≤ 2 ms GPU en Low. ∥
- **P1-T5** Terrain « Baie Biscotte » : 512 m, plage en pente 1:30, bathymétrie (barre de sable à 90 m, profondeur 2,5 m ; chenal), layers Poly Haven (import settings §ASSETS_MANIFEST), rochers/arbres (prefabs optimisés de P1-T5a) placés par `Tools/BuildBaieEnvironment.cs` (`run_script`), colliders. DoD : capture, terrain lié à `OceanController.terrain` (effets rivage visibles). ∥
- **P1-T6** Presets d'environnement (`EnvironmentPreset` × 5 : matin/aube/midi/soir/couvert) + `EnvironmentDirector` (HDRI, soleil, volume, vent). DoD : switch à chaud via cheat menu, captures des 5. ∥
- **P1-T7** Audio de base : mixer, snapshots, `OceanAudio` (boucle + bruit procédural). DoD : écoute en Play, pas de clic de boucle (test : analyse des 100 ms de jonction). ∥
- **P1-T8** Perf pass : Project Auditor, Frame Debugger ; budget §16.1 respecté sur `Playground` sans SurfWave. DoD : tableau de mesures dans `Docs/perf/P1.md`.

### P2 — SurfWave (≈ 4–6 jours)
- **P2-T1** `SurfWaveMath` (§5.1–5.2) en `static` Burst-compatible : `Evaluate(params, s, ξ, t, out h, out n, out vel, out φ, out E)`, `EvaluateLip`, `IsInTube`, `CrestSample`. Tests EditMode : continuité C⁰/C¹ en `s` et `ξ`, bornes (`|h| ≤ 1,2 H`), φ monotone en `t`, peel angle = `atan(f')` (spline test linéaire), tube test (points connus), déterminisme (mêmes entrées → mêmes sorties, bit-exact). DoD : tests verts + benchmark (8 448 évals < 0,5 ms sur 4 workers).
- **P2-T2** `SurfSpot`, `SurfBreakProfile`, `ConditionsPreset` (SO) + `BreakLineTool` (Editor : gizmos α/V_s/H/I, slider d'aperçu). DoD : spot « Baie » avec BreakLine A-frame (deux splines : gauche et droite), profil de sections. ∥
- **P2-T3** `SurfWave` + `SurfWaveMeshJob` + `SurfWaveRenderer` (MeshData, lèvre, jupe vers `OceanMath`, bounds, LOD, `CrestSample` buffer). DoD : vague visible en Play, 0 GC alloc/frame (Profiler), job < 0,6 ms, pas de plis (capture rasante).
- **P2-T4** Shader `SurfWaveWater` (§5.4, version Opaque) + `Foam` subgraph + `WaveSSS` subgraph ; matériau par preset. DoD : captures matin/soir ; lèvre rétro-éclairée visible ; écume cohérente avec φ. ∥
- **P2-T5** `WaveSetScheduler` + pool + synchro ambiant (fondu) + masque océan. DoD : séries de 3 vagues toutes 60 s, aucune couture visible (capture au bord de l'empreinte), océan ambiant abaissé sous la vague.
- **P2-T6** `WaterSurfaceComposite` complet (ambiant + SurfWaves, fondu 6 m) + test PlayMode « la hauteur échantillonnée = hauteur du maillage » (écart < 2 cm sur 500 points). DoD : tests verts.
- **P2-T7** Whitewater (φ = 3) : décroissance, matériau écume, propagation vers la plage, extinction. DoD : capture séquence 0/3/6/12 s. ∥
- **P2-T8** Perf : 2 vagues + ambiant ≤ budget ; presets Low/High. DoD : `Docs/perf/P2.md`.
- **P2-T9** Import bathymétrique optionnel (`FromTerrain`, règle 0,78) : BreakLine générée pour « Baie » et comparée à la version manuelle. DoD : outil fonctionnel, doc d'usage dans `Docs/authoring.md`.

### P3 — Rider v1 : « Playable 1 » (≈ 4–5 jours)
- **P3-T1** `RiderController` (états Paddle/DuckDive/TakeOff/Ride/KickOut/Wipeout(simplifié : respawn)/Recover) + `RiderTuning`/`BoardSpec` + capsule + planche procédurale (`BodyboardMeshBuilder`). DoD : on peut ramer, prendre une vague, la surfer jusqu'au bout en Play sur `Playground` avec clavier et manette ; test PlayMode « take-off sur vague canonique ».
- **P3-T2** Équations §6.3 avec gizmos (a_g, a_push, v, F, n) et overlay. DoD : vitesse 6–10 m/s tenue dans la pocket sans pump ; le rider ralentit sur l'épaule ; carve lisible. Tests EditMode sur les fonctions pures (slip, align, pump window).
- **P3-T3** Pump/Stall/Trim + feedback minimal (FOV, son placeholder). DoD : gain de vitesse mesuré +8 %/pompe synchronisée (log de test).
- **P3-T4** Caméra `RideCam` + `LineupCam` + clamp au-dessus de l'eau + impulses basiques. DoD : jamais sous l'eau en Ride (test PlayMode : 60 s de ride, `camera.y − Height ≥ 0,3`). ∥
- **P3-T5** `InputRouter` + `GlyphProvider` + HUD minimal (vitesse, glyphes 2 actions) en UI Toolkit. DoD : glyphes PS5 quand DualSense (test avec `InputTestFixture` : ajout d'un `DualSenseGamepadHID` simulé). ∥
- **P3-T6** Haptique DualSense (rumble atterrissage/mousse, lightbar) — USB. DoD : vérifié à la main par l'humain (checklist), pas d'exception en BT. ∥
- **P3-T7** Wipeout simplifié (capsule dynamique + respawn) + duck dive + mousses. DoD : boucle complète lineup → vague → wipeout → retour, sans blocage (test PlayMode 5 min automatisé avec entrées scriptées).
- **P3-T8** Playtest interne : 10 vagues consécutives sans bug bloquant ; ajustement `RiderTuning`. DoD : rapport `Docs/playtests/P3.md` avec captures.

### P4 — Tube, air, feel (≈ 4 jours)
- **P4-T1** État Tube (`InTube`, profondeur, foam ball, lèvre vs capsule) + `TubeCam` + `TubeVolume` + snapshot audio. DoD : tube de 3 s réalisable à « La Dalle » prototype (BreakLine temporaire) ; test PlayMode de détection.
- **P4-T2** État Air : pop, éjection, balistique, `Land`, `LandingEvaluator`, assist. DoD : air 1–3 m, atterrissages réussis/ratés conformes aux seuils (tests EditMode sur l'évaluateur).
- **P4-T3** `AirRotationController` (flick/hold/auto-complete), grab. DoD : 360 en 0,9 s flick ; auto-complete testé. ∥
- **P4-T4** El Rollo assisté (suivi de lèvre) + ARS. DoD : Rollo réussi 8 fois sur 10 par un testeur après 5 min ; capture. ∥
- **P4-T5** Drop-knee stance (physique + pose IK placeholder). DoD : bascule, DK snap fonctionnel.
- **P4-T6** Feel pass : courbes de FOV, dutch, impulses, ralenti d'entrée de tube ; `RiderFeedback` central (événements → caméra/haptique/audio/VFX). DoD : checklist §2.8 mesurée.

### P5 — Tricks, scoring, HUD : « Vertical slice » (≈ 3–4 jours)
- **P5-T1** `TrickCatalog` (§2.6) + `TrickDetector` + nommage. Tests EditMode : séquences d'entrées → trick attendu. DoD : tests verts.
- **P5-T2** `WaveScorer`/`JudgePanel`/`SessionScore` (§2.7) + tests EditMode (cas : 3 tricks chaînés, répétition, wipeout, tube 4 s). ∥
- **P5-T3** HUD complet (popups, chaîne, tube, note de vague, série). DoD : captures ; aucune allocation par frame dans l'UI (Profiler). ∥
- **P5-T4** Mode Heat (timer, 2 meilleures, résultats) + Free Surf ; `MainMenu` → `SpotSelect` → `Session` → `Results` (UI Toolkit, navigation manette). DoD : boucle complète jouable au pad sans souris.
- **P5-T5** Spot « Pointe Longue » (terrain + BreakLine longue + sections) ; « La Dalle » (slab). DoD : 3 spots jouables, presets de conditions ×3 chacun.
- **P5-T6** Playtest + tuning score/difficulté. DoD : `Docs/playtests/P5.md`.

### P6 — Personnage, animation, VFX, audio (≈ 5–7 jours)
- **P6-T1** Import Humanoid (mannequin + clips UAL/UAL2), avatar, `Animator` (états de base), `Rig_Prone` (Animation Rigging) et `RiderAnimationDriver`. DoD : rame et ride crédibles (captures), pas de pop d'IK.
- **P6-T2** Poses/clips de tricks (Rollo, backflip, invert, spins, grab, DK) keyframés dans Unity ; blend avec rotation racine. DoD : chaque trick a une pose ; capture ×6. ∥
- **P6-T3** Ragdoll wipeout + recover. DoD : aucun NaN, retour propre. ∥
- **P6-T4** Planche finale (3 formes, matériaux, leash, palmes). ∥
- **P6-T5** VFX Graph (§13) + `WaveVfxBinder` + texture de hauteur pour `FaceFoam` + budgets. DoD : captures Low/High ; GPU VFX ≤ 3 ms Low. ∥
- **P6-T6** Audio complet (§14). DoD : mix validé à l'écoute ; pas de clipping (limiteur sur Master). ∥
- **P6-T7** Post-process/underwater/tube volumes finaux ; environnement de « La Dalle » (falaises Poly Haven, rochers avec colliders → wipeout « rocher »). ∥
- **P6-T8** Si fourni par l'humain : Universal Base Characters / Starter Assets → remplacement du mannequin (procédure documentée).

### P7 — Modes, menus, options, contenu : « Beta » (≈ 3–4 jours)
- **P7-T1** Options complètes (graphismes/audio/contrôles/rebinding/accessibilité/langue) + sauvegarde. DoD : chaque option persiste après redémarrage (test PlayMode sur `SaveSystem`).
- **P7-T2** `ConditionsEditor` (Free Surf) + presets par spot + effets §2.4 (vent/marée/direction) implémentés dans `SurfWaveParams`. DoD : 5 combinaisons testées, captures. ∥
- **P7-T3** Défis + progression + déblocages (planches/presets). ∥
- **P7-T4** Localisation FR/EN complète. ∥
- **P7-T5** Tutoriel intégré (Baie, 6 étapes guidées : rame, duck dive, take-off, pump, tube, air). DoD : complétable au pad en < 6 min.
- **P7-T6** Shorebreak (optionnel) ; Trick Challenge (optionnel).
- **P7-T7** Playtest externe (3 personnes) → `Docs/playtests/P7.md`.

### P8 — Optimisation, QA, packaging : « Release candidate » (≈ 3 jours)
- **P8-T1** Profiling complet Low/High ; corrections ; STP/FSR ; Mesh LOD ; GPU occlusion ; textures. DoD : budgets §16 tenus sur les 3 spots (tableau `Docs/perf/P8.md`).
- **P8-T2** Build Profiles `Windows_Dev` (Mono, dev) et `Windows_Release` (IL2CPP si module + VS présents, sinon Mono, strip engine code, Master). DoD : `unity command build` OK, exe lancé 10 min sans erreur (log).
- **P8-T3** QA : matrice manette (DualSense USB/BT, Xbox, clavier AZERTY/QWERTY), résolutions (1280×720 → 3840×2160), alt-tab, changement de manette en cours de jeu, sauvegarde corrompue. DoD : `Docs/qa/checklist.md` cochée.
- **P8-T4** Replay de la meilleure vague + photo mode (optionnels). 
- **P8-T5** Gâchettes adaptatives DualSense (stretch : rapport HID, `InputDevice.ExecuteCommand`, référence UniSense). 
- **P8-T6** Crédits (ATTRIBUTIONS), icône, splash, versionnage, README utilisateur.

### Jalons et critères de sortie
| Jalon | Phase | Critère |
|---|---|---|
| Foundations | P0–P1 | Océan ambiant + plage + presets, 60 fps Low, tests infra verts |
| Playable 1 | P3 | Rame → take-off → ride → fin de vague, clavier et manette, 60 fps Low avec 2 vagues |
| Vertical slice | P5 | Tube + airs + tricks + score + HUD + menus + 3 spots ; « fun » validé en playtest interne |
| Beta | P7 | Personnage animé, VFX/audio complets, options, tutoriel, localisation |
| RC | P8 | Perf/QA/packaging ; build Release jouable 30 min sans bug bloquant |

---

## 20. Tests et QA
- **EditMode** : `SurfWaveMathTests`, `OceanMathParityTests`, `RiderPhysicsTests` (fonctions pures), `LandingEvaluatorTests`, `TrickDetectorTests` (séquences), `ScoringTests`, `SaveSystemTests`, `InputActionsAssetTests`, `LocalizationTests`.
- **PlayMode** : `SurfWaveMeshConsistencyTest` (sample vs maillage), `TakeoffScenarioTest`, `TubeDetectionTest`, `WipeoutRecoveryTest`, `CameraAboveWaterTest`, `SessionLoopSoakTest` (5 min d'entrées scriptées), `PerformanceSmokeTest` (frame time moyen < 20 ms en Editor Low, avertissement seulement).
- **Perf** : `Docs/perf/Pn.md` par phase (méthode : 60 s en Play, `get_performance_stats` toutes les 5 s, moyenne/percentile 95).
- **Playtests** : grille d'observation (compréhension, frustration, moments forts), 3 sessions minimum (P3, P5, P7).
- **QA finale** : matrice §P8-T3.

---

## 21. Risques et mitigations
| Risque | Impact | Mitigation |
|---|---|---|
| Port Storm Breakers (Shader Graph/VFX 12 → 17.6) présente des artefacts (reflets, particules, sorting) | Visuel océan ambiant | Smoke test déjà OK (compilation, shaders, VFX). Repli : océan ambiant = `WaterSimple_FoamMask` (sample Unity) + Gerstner CPU (Catlike) ; l'API `IWaterSurface` isole le choix. |
| Lèvre/tube : maillage auto-intersectant, seams | Visuel, gameplay tube | Lèvre = ruban séparé, φ capé, tests de continuité ; repli MVP : pas de surplomb (φ face ≤ 1,2) + tube « implicite » (VFX + caméra + assombrissement) |
| Perf iGPU insuffisante | Cible 60 fps | Presets, render scale, VFX budget, ocean mesh LOD ; mesure à chaque phase ; possibilité de réduire `Ns` et la portée de l'océan SB |
| Feel du contrôleur difficile à régler | Fun | Tous paramètres en SO, overlay de debug, playtests P3/P5, assist réglable |
| Animation prone convaincante avec des clips génériques | Qualité perçue | Animation Rigging procédurale prioritaire ; poses keyframées courtes ; option Mixamo/UBC via l'humain |
| DualSense en Bluetooth sans haptique | UX | Message Options, fonctionnalité dégradée silencieuse |
| Fast Enter Play Mode et statiques | Bugs éditeur | Règle `ResetStatics` + test PlayMode « deux Play consécutifs » |
| URP Compatibility Mode absent (6.4+) | Passes custom | Toute passe custom (`Underwater`) en Render Graph ou via Full Screen Pass Renderer Feature (aucun code de passe legacy) |
| Modèles glTF (glTFast) matériaux à ré-importer | Workflow | Dupliquer les matériaux générés dans `_Project/Art/Materials` (règle du manifeste) |

---

## 22. Décisions prises, hypothèses, questions ouvertes
Décisions : océan hybride SB + SurfWave ; CPU authoritative ; UI Toolkit ; Cinemachine 3 ; project-wide actions ; Poly Haven/Kenney/Quaternius CC0 ; Mono en dev ; 3 spots ; scoring APB-like ; pas de multi.
Hypothèses : DA réaliste stylisée ; solo ; 60 fps Low iGPU ; nom « Biscotte ».
Questions ouvertes (réponse humaine souhaitée, défauts appliqués sinon) : (1) personnage final (mannequin Quaternius vs Universal Base Characters vs Mixamo) ; (2) Steam ou non (achievements, Steam Input) ; (3) musique ; (4) mode Shorebreak en v1.

---

## 23. Glossaire (FR/EN)
- **Lineup** : zone d'attente au large où l'on choisit les vagues. **Peak / pic** : point où la vague commence à casser. **Peel angle** : angle entre la ligne d'écume et la crête ; petit = vague rapide. **Pocket** : zone d'énergie près de la lèvre. **Épaule / shoulder** : partie molle non déferlée. **Lèvre / lip** : crête projetée. **Tube / barrel** : cavité sous la lèvre. **Foam ball** : boule d'écume à l'impact de la lèvre. **Closeout** : section qui casse d'un coup. **Rampe / ramp** : section propice aux airs. **Section** : portion de vague au comportement homogène. **Série / set** : groupe de vagues. **Duck dive** : passer sous la vague. **Take-off** : bascule au départ. **Prone** : allongé. **Drop-knee (DK)** : un genou sur la planche. **El Rollo** : rotation avec la lèvre. **ARS** : Air Roll Spin. **Invert** : rotation jambes au-dessus de la tête. **Kick-out** : sortie volontaire. **Wipeout** : chute. **Trim** : réglage d'assiette avant/arrière. **Pump** : mouvement rythmique pour accélérer. **Stall** : freiner pour se caler dans le tube.
