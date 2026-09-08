# AGENT PLAYBOOK — Piloter le projet Biscotte avec le Unity CLI

> Mode d'emploi opérationnel pour tout agent IA qui implémente Biscotte.
> La spec fonctionnelle et technique est dans `Docs/BISCOTTE_SPEC.md` : la lire d'abord, puis revenir ici pour la mécanique.

## 1. Règles absolues

1. **Un Editor Unity (6000.6.0f1) est ouvert sur ce projet.** On le pilote avec `unity command …` (package `com.unity.pipeline`). On n'édite **jamais** à la main un `.unity`, `.prefab`, `.asset`, `.mat`, `.controller` pendant que l'Editor est ouvert : fileIDs/GUIDs faux, et l'Editor ne voit pas le changement. Exceptions : fichiers texte purs (`.cs`, `.hlsl`, `.shader`, `.asmdef`, `.json`, `.uxml`, `.uss`, `.inputactions`) — suivis d'un `unity command recompile` (ou d'un `AssetDatabase.Refresh()` via `eval`).
2. **Vérifier avant d'agir** : `unity status --format json` doit montrer `state: ready`. Si la connexion échoue, `unity pipeline list` détecte le **Safe Mode** (erreurs de compilation). Si Safe Mode : corriger les `.cs` (lire `Logs/Editor.log` filtré sur `error CS`), puis demander la relance de l'Editor à l'humain.
3. **Après chaque modification de scripts** : `unity command recompile` → boucler sur `unity command recompile_status` jusqu'à `completed`/`up_to_date` → `unity command get_console_logs` (zéro erreur) avant de continuer.
4. **Sauvegarder** : `unity command save_scene` / `save_all` après toute édition de scène. Committer en Git par étape logique (§8).
5. **Fast Enter Play Mode est activé** (ni domain reload ni scene reload). Tout état `static` mutable doit être réinitialisé via `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` (ou l'attribut `[AutoStaticsCleanup]`, Unity 6.5+). Les classes statiques de Storm Breakers (`StormBreakers.Ocean`) sont concernées.
6. **Ne jamais** fermer/tuer l'Editor par script, ni supprimer `Library/`.
7. **Une seule scène active à la fois** par agent ; les agents parallèles se répartissent par dossiers/prefabs, pas par scène.

## 2. Commandes de base (vérifiées sur cette machine le 2026-09-08)

```bash
unity status --format json                      # Editor connecté ? (state ready)
unity command                                   # catalogue (~517 commandes) ; filtre : unity command --query scene --detail compact
unity command --query add_component --format json   # schéma exact des paramètres d'une commande (fait foi)
unity command get_scene_hierarchy               # arbre de la scène active
unity command list_open_scenes
unity command open_scene --path Assets/_Project/Scenes/Playground.unity
unity command create_gameobject --name Rider --primitive capsule
unity command add_component ...                 # cible par handle / instanceId / hierarchyPath, type par nom
unity command set_component_properties ...      # propriétés sérialisées (JSON)
unity command set_serialized_field ...          # champ précis, références d'assets via ObjectRef (path/guid)
unity command create_script --path Assets/_Project/Scripts/Rider/RiderController.cs
unity command write_text_file --path <rel> --content "<texte>" --confirm true
unity command recompile ; unity command recompile_status
unity command attach_script --target Rider --type Biscotte.Rider.RiderController
unity command create_prefab --target Rider --path Assets/_Project/Prefabs/Rider.prefab
unity command instantiate_prefab --prefab Assets/_Project/Prefabs/Rider.prefab
unity command save_scene
unity command editor_play | editor_stop | editor_pause
unity command capture_game_view --save_path "Screenshots~/shot.png" --width 1600 --height 900   # save_path est RELATIF À Assets/ ; le dossier Assets/Screenshots~ (suffixe ~) est ignoré par l'importeur Unity et par Git
unity command get_performance_stats             # FPS / mémoire / draw calls (Play Mode)
unity command get_console_logs                  # erreurs et warnings récents
unity command run_tests --mode EditMode ; unity command test_status
unity command eval --code "return UnityEngine.Application.unityVersion;" --timeout 60
unity command eval_file --file C:/path/snippet.cs --timeout 120
unity command run_script --file Tools/BuildSteps.cs --entry Biscotte.Tools.Build.Player
unity command package_add --identifier com.unity.xxx@1.2.3 --confirm true   # puis package_status / recompile_status
unity command package_search --query com.unity.xxx                          # paramètre = --query (pas --name)
unity command import_asset --source C:/abs/file.png --path Assets/_Project/Art/file.png
unity command get_import_settings ... | set_import_settings ...
unity command set_quality_settings | set_player_settings | set_physics_settings | set_time_settings   # --confirm true ; --dry_run true pour prévisualiser
unity command build ... ; unity command build_status
unity command audit ; unity command audit_status   # Project Auditor (inclut le module URP)
```

Pièges connus : `package_add --wait true` peut bloquer quand le serveur Pipeline redémarre après un domain reload ; préférer l'appel asynchrone + polling de `package_status`. Les timeouts CLI (`--timeout`, secondes) et `eval --timeout` (millisecondes) sont différents.

### Cycle type « nouveau script + composant »
1. Écrire le `.cs` (outil d'édition de fichiers ou `write_text_file`).
2. `recompile` → poll `recompile_status` → `get_console_logs` (0 erreur).
3. `attach_script` / `add_component` sur le GameObject cible.
4. `set_serialized_field` pour les références (ScriptableObjects, prefabs, materials).
5. `save_scene` (+ `create_prefab` / `apply_prefab_overrides` si l'objet est un prefab).
6. Test en Play Mode : `editor_play` → `capture_game_view` → `get_console_logs` → `editor_stop`.

### Constructions complexes
- `unity command batch --operations '[...]'` : plusieurs opérations en une transaction Undo (exclut build/package/eval/play).
- `unity command run_script --file Tools/<X>.cs --entry <Type.Method>` : C# compilé en mémoire, exécuté dans l'Editor. C'est la voie **préférée** pour construire des scènes/prefabs de façon reproductible et versionnée (les scripts vivent dans `Tools/`, hors `Assets/`).

## 3. Où sont les choses

| Chemin | Rôle |
|---|---|
| `Docs/BISCOTTE_SPEC.md` | **Source de vérité** : design, architecture, plan par phases, critères d'acceptation |
| `Docs/ASSETS_MANIFEST.md` | Inventaire des assets tiers, licences, chemins, usages prévus |
| `Docs/USER_ACTIONS.md` | Actions manuelles réservées à l'humain (Asset Store, IL2CPP, Mixamo, itch.io) |
| `Docs/RESEARCH_SOURCES.md` | Sources (docs Unity 6.6, communauté, science du surf) et ce qu'elles établissent |
| `Docs/research/` | Extraits texte (manuel Storm Breakers, revue scientifique) |
| `Assets/_Project/` | Tout ce que nous créons : Scripts, Editor, Tests, Shaders, VFX, Prefabs, Scenes, Settings, Art, Audio, UI, Data |
| `Assets/ThirdParty/` | Contenu tiers ; ne pas modifier sauf port/compatibilité, patch consigné dans `PATCHES.txt` du dossier concerné |
| `Assets/Samples/Shader Graph/17.6.0/Production Ready Shaders/` | Shaders d'eau officiels de référence (WaterLake, WaterSimple_FoamMask, WaterStream) |
| `Assets/Settings/` | URP assets/renderers du template (`PC_RPAsset` = pipeline actif, `PC_Renderer` = Forward+) |
| `Tools/` | Scripts C# exécutés via `run_script` (construction de scènes, builds), hors Assets |
| `Build/` | Sorties (builds, rapports de tests, captures) — ignoré par Git |

## 4. Conventions de code

- C# 9, `namespace Biscotte.<Domaine>` : Core, Ocean, Wave, Rider, Board, CameraRig, InputSys, Tricks, Scoring, Session, UI, Audio, VFX, Debugging, EditorTools.
- Assembly definitions : `Biscotte.Runtime` (`Assets/_Project/Scripts`), `Biscotte.Editor` (`Assets/_Project/Editor`), `Biscotte.Tests.EditMode` / `Biscotte.Tests.PlayMode` (`Assets/_Project/Tests`). Références runtime : `Unity.Splines`, `Unity.Cinemachine`, `Unity.InputSystem`, `Unity.Burst`, `Unity.Mathematics`, `Unity.Collections`, `StormBreakers.Runtime`, `Unity.VisualEffectGraph.Runtime` (si nécessaire), `Unity.TextMeshPro` (uGUI) si utilisé.
- Données de réglage = `ScriptableObject` dans `Assets/_Project/Data` (jamais de constantes magiques dans les MonoBehaviours).
- Zéro allocation en régime stable dans `Update`/`FixedUpdate` (pas de LINQ, pas de `new` de tableaux, pas de concaténation de strings).
- Maths lourdes (vague, maillage) : `Unity.Mathematics` + jobs `[BurstCompile]` + `Mesh.MeshData`. L'échantillonnage CPU de la surface et la génération du maillage partagent **la même fonction** (une seule vérité).
- Nommage : `PascalCase` types/méthodes/propriétés ; `camelCase` champs privés (`[SerializeField] float pumpBoost`) ; `k_` constantes privées ; `s_` statiques.
- Chaque système expose des Gizmos de debug et des toggles dans `DebugSettings` (ScriptableObject).
- Tests EditMode pour toute fonction mathématique pure (échantillonnage vague, profil de déferlement, scoring, détection tube). Tests PlayMode pour les états du rider (takeoff / ride / air / wipeout) sur une vague déterministe (seed fixe).
- Commentaires et logs en anglais ; UI joueur en français et anglais (tables de localisation simples, `Assets/_Project/Data/Localization`).

## 5. Vérification visuelle et perf

- `capture_game_view --save_path <png>` puis lecture de l'image : revue visuelle obligatoire en fin de tâche graphique.
- Budgets : voir spec §Performance. Mesurer avec `get_performance_stats` en Play Mode sur `Playground`, 1080p, presets « Low » (iGPU Iris Xe de la machine de dev) et « High ».
- `unity command audit` (Project Auditor) à la fin de chaque phase ; corriger les diagnostics Critical/Major.

## 6. Tests et builds

```bash
unity command run_tests --mode EditMode        # réutilise l'Editor ouvert ; puis test_status
unity command run_tests --mode PlayMode
unity test C:/Users/Max/Documents/Biscotte --mode EditMode --report-format junit --output ./Build/test-edit.xml --timeout 900   # variante batch (Editor fermé)
unity build C:/Users/Max/Documents/Biscotte --profile Assets/_Project/Settings/BuildProfiles/Windows_Dev.asset --output-path ./Build/Windows_Dev/Biscotte.exe
```
Exit code 8 = tests en échec (ne pas relancer aveuglément) ; autre code ≠ 0 = problème d'infrastructure.

## 7. Storm Breakers (tiers, CC0) — règles de port

- Dossier `Assets/ThirdParty/StormBreakers` (asmdef `StormBreakers.Runtime`). On utilise le modèle de vague tel quel pour l'océan « ambiant » ; on **re-implémente à l'identique** `Ocean.OceanDeformation` en version Burst (`Biscotte.Ocean.OceanMath`) pour nos jobs, avec un test EditMode qui compare les deux sur 1000 points (tolérance 1e-4).
- Tout patch nécessaire à Unity 6.6 (API obsolètes, upgrade Shader Graph/VFX) est consigné dans `Assets/ThirdParty/StormBreakers/PATCHES.txt`.

## 8. Git

- Branche `main` ; un commit par tâche terminée et vérifiée (tests verts, console propre). Message : `phaseN/<domaine>: <résumé>`.
- `Library/`, `Temp/`, `Logs/`, `Build/`, `UserSettings/` sont ignorés. Les binaires lourds (`.hdr`, `.fbx`, `.wav`, `.png`) sont suivis par Git LFS (`.gitattributes` du template) : vérifier `git lfs ls-files` après le premier commit.
