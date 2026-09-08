# USER ACTIONS — Ce que seul l'humain peut faire

Tout ce qui est listé ici demande un compte, un clic dans un navigateur, ou une élévation Windows (UAC). Rien n'est bloquant pour démarrer l'implémentation : chaque item indique la phase à partir de laquelle il devient nécessaire et le repli prévu.

## A. Asset Store Unity (compte Unity connecté : Maxime CAVILLE) — facultatif mais recommandé

Procédure commune : ouvrir le lien, cliquer « Add to My Assets », puis dans l'Editor `Window > Package Manager > My Assets`, `Download` puis `Import`. Un agent peut ensuite finir l'import avec `unity command eval --code "UnityEditor.AssetDatabase.ImportPackage(@\"<chemin .unitypackage>\", false);"` (cache : `%APPDATA%\Unity\Asset Store-5.x\`).

| Priorité | Asset (gratuit) | Lien | Pourquoi | Phase |
|---|---|---|---|---|
| 1 | **Starter Assets – Third Person Character Controller (URP)** – Unity Technologies | https://assetstore.unity.com/packages/essentials/starter-assets-thirdperson-urp-196526 | Personnage humanoïde riggé « PlayerArmature » + clips (idle/walk/run/jump) de qualité Unity, base propre pour le rider si les mannequins Quaternius ne conviennent pas ; référence de contrôleur Cinemachine 3 + Input System | 6 |
| 2 | **Particle Pack** – Unity Technologies | https://assetstore.unity.com/packages/vfx/particles/particle-pack-127325 | Textures/prefabs de particules (fumée, éclaboussures) pour enrichir spray/écume | 6 |
| 2 | **Cartoon FX Remaster Free** – Jean Moreno | https://assetstore.unity.com/packages/vfx/particles/cartoon-fx-remaster-free-109565 | Splashs/impacts stylisés (URP OK) pour les feedbacks de tricks/HUD | 6 |
| 3 | **AllSky Free – 10 Sky/Skybox Set** – rpgwhitelock | https://assetstore.unity.com/packages/2d/textures-materials/sky/allsky-free-10-sky-skybox-set-146014 | Skyboxes supplémentaires (les HDRI Poly Haven sont déjà importées) | 7 |
| 3 | **Skybox Series Free** – Avionx | https://assetstore.unity.com/packages/2d/textures-materials/sky/skybox-series-free-103633 | Idem, 16 ciels HDRI | 7 |
| 3 | **Terrain Sample Asset Pack** – Unity Technologies | https://assetstore.unity.com/packages/3d/environments/landscapes/terrain-sample-asset-pack-145808 | Stamps/brushes/matériaux de terrain PBR (URP) pour sculpter la plage/falaises | 6 |
| 4 | **POLYGON – Starter Pack** – Synty | https://assetstore.unity.com/packages/3d/environments/polygon-starter-pack-art-by-synty-156819 | Props low-poly (si direction artistique stylisée retenue pour le décor) | 6 |

## B. Contenus gratuits hors Asset Store nécessitant un clic (CC0)

| Priorité | Contenu | Lien | Pourquoi | Repli déjà en place |
|---|---|---|---|---|
| 1 | **Quaternius – Universal Base Characters** (CC0) | https://quaternius.itch.io/universal-base-characters | Personnages humains « universal rig » compatibles avec les Universal Animation Library 1 & 2 déjà importées ; permet un rider homme/femme crédible | Mannequin_F (UAL2) déjà importé dans `Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/Mannequin_F` |
| 2 | **KayKit – Character Animations** et **KayKit Adventurers** (CC0) | https://kaylousberg.itch.io/kaykit-character-animations , https://kaylousberg.itch.io/kaykit-adventurers | Alternative stylisée (chunky) si l'on choisit une DA cartoon | — |
| 2 | **Mixamo** (Adobe, gratuit avec compte, usage commercial autorisé dans un jeu, pas de redistribution brute) | https://www.mixamo.com | Clips « Swimming », « Treading Water », « Falling », « Getting Up » et personnages réalistes ; auto-rig d'un mesh custom | Clips UAL (crawl, swim, idle, etc.) |
| 3 | **Sonniss GDC Game Audio Bundles** (royalty-free, très volumineux) | https://gdc.sonniss.com/ | Enregistrements océan/vagues/underwater pro | Boucle d'ambiance générée + 4 samples CC0 + audio procédural (Storm Breakers) |

Déposer les téléchargements dans `C:\Users\Max\Documents\Biscotte\Incoming\` (dossier hors Assets) : un agent les triera et les importera en respectant `Docs/ASSETS_MANIFEST.md`.

## C. Outils de build (à faire avant la Phase 8 « release »)

1. **Module IL2CPP Windows** (1 Go, demande une élévation UAC) :
   ```powershell
   unity install-modules --editor-version 6000.6.0f1 --module windows-il2cpp --yes --accept-eula
   ```
2. **Visual Studio 2022 Build Tools** avec la charge de travail « Développement Desktop en C++ » + **Windows 10/11 SDK** (requis par IL2CPP). Installateur : https://visualstudio.microsoft.com/fr/visual-cpp-build-tools/ . Le module `visualstudio` du Hub (Visual Studio Community 2026, 1.6 Go) est une alternative.
3. Rien à faire pour les builds de développement : le backend **Mono** est déjà présent (`Windows Build Support (Mono)` inclus dans l'Editor).

## D. Git

Le dépôt a des fichiers indexés mais **aucun commit**. Avant que les agents ne commencent :
```powershell
cd C:\Users\Max\Documents\Biscotte
git add -A
git commit -m "chore: baseline Unity 6.6 URP project + third-party CC0 assets + docs"
```
Vérifier que `git lfs ls-files | Measure-Object` liste bien les binaires lourds (règles LFS déjà dans `.gitattributes`). Un remote (GitHub/GitLab/UVCS) est conseillé : `unity projects` ne le fait pas pour un projet existant, utiliser `gh repo create` ou l'interface web.

## E. Manette PS5 (DualSense)

- Brancher la DualSense **en USB** pour les tests : le retour haptique (moteurs) et la barre lumineuse ne fonctionnent pas en Bluetooth avec l'Input System (limitation documentée). Les entrées fonctionnent dans les deux cas.
- Si Steam est lancé avec « Steam Input » activé pour les manettes PlayStation, la manette peut apparaître comme une Xbox (XInput) : désactiver Steam Input pour les tests DualSense, ou tester hors Steam.

## F. Décisions en attente de l'humain (réponses par défaut appliquées si silence)

1. Direction artistique : **réaliste stylisé** (Poly Haven + Storm Breakers) — défaut retenu. Alternative : cartoon (Synty/KayKit).
2. Nom du jeu : « Biscotte » (nom de projet) — provisoire.
3. Langue de l'UI : FR + EN — défaut.
4. Cible de perf : 60 fps 1080p « Low » sur Iris Xe ; 120 fps 1440p « High » sur GPU dédié — défaut.
