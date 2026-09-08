# Biscotte — jeu de bodyboard (Unity 6000.6.0f1, URP)

Ce dépôt est implémenté par des agents IA à partir d'une spec écrite à l'avance. **Avant toute action** :

1. Lire `Docs/BISCOTTE_SPEC.md` (source de vérité : design, architecture, plan par phases, critères d'acceptation).
2. Lire `Docs/AGENT_PLAYBOOK.md` (comment piloter l'Editor Unity ouvert via `unity command …`, conventions, tests, Git).
3. Consulter `Docs/ASSETS_MANIFEST.md` avant d'ajouter/utiliser un asset, `Docs/USER_ACTIONS.md` pour ce qui est réservé à l'humain, `Docs/RESEARCH_SOURCES.md` pour les faits établis et leurs sources.

Règles courtes :
- Un Editor Unity est ouvert sur ce projet : ne jamais éditer à la main `.unity/.prefab/.asset/.mat` ; utiliser `unity command` (scène, composants, prefabs, packages, tests, captures).
- Après chaque changement de script : `unity command recompile` → `recompile_status` → `get_console_logs` sans erreur.
- Fast Enter Play Mode est actif : pas d'état statique non réinitialisé.
- Tout le code du jeu vit dans `Assets/_Project/` (asmdef `Biscotte.Runtime`), le contenu tiers dans `Assets/ThirdParty/` (ne pas modifier sans consigner un patch), les scripts d'outillage dans `Tools/`.
- Français pour la doc et l'UI (FR/EN), anglais pour le code, les commentaires et les logs.
