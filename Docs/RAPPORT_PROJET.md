# WavyBoard : rapport de projet

**Faire un jeu vidéo Unity avec des agents IA : veille, spécification, implémentation, itérations et limites.**

Alexandre, Maxime, Louis. M2 EFREI, Culture & Concepts Informatiques. Septembre 2026.

![Le jeu final : dans le tube, avec le HUD](../Website/public/media/evolution/11-jeu-final.webp)

## En bref

WavyBoard est un jeu de bodyboard réalisé sous Unity 6.6. On y rame vers une vague lourde inspirée de Teahupoo, on se cale dans le tube, on court contre le rouleau et on s’envole de la lèvre pour enchaîner des figures au stick droit. Il se joue à la manette PS5 ou au clavier et à la souris.

Nous n’avions jamais utilisé Unity. Nous avons fait écrire la quasi-totalité du jeu par des agents IA (Claude Fable 5.1, Opus 4.8, Opus 5 puis Opus 5.5), qui pilotaient l’éditeur Unity ouvert en ligne de commande. Notre rôle : cadrer, fournir les références, juger, jouer, et décider quand repartir de zéro.

Ce rapport raconte ce test grandeur nature : ce que nous avons demandé, comment l’IA s’y est prise, les cinq vagues jetées avant la bonne, et ce que nous en retenons.

| Chiffre | Ce qu’il mesure |
|---|---|
| 21 jours | du premier commit (8 septembre) à la version finale (29 septembre) |
| 58 commits | sur 8 branches, dont 56 co-signés par un modèle Claude |
| 4 modèles | Fable 5.1, Opus 4.8, Opus 5 et Opus 5.5 |
| 6 vagues | cinq approches abandonnées avant la vague finale |
| 721 captures | prises par les agents pour « voir » le jeu |
| 8 845 lignes | de C# dans le jeu final (48 fichiers), plus un shader de 449 lignes |
| 22 tests | automatiques qui rident des vagues entières sans lancer le jeu |
| −1 013 998 lignes | supprimées lors du grand ménage final |

## Le projet et le choix d’Unity

### Ce que nous voulions

Le cahier des charges tenait en quelques phrases : un personnage en position bodyboard dans un océan, des vagues de plusieurs tailles, la possibilité de ramer, de surfer allongé, d’aller dans les rouleaux et de faire des figures. Jouable au clavier et à la souris comme à la manette PS5. Fluide, beau, et surtout amusant.

### Pourquoi Unity

Nous avons choisi Unity précisément parce qu’aucun de nous trois ne le connaissait. Faire un site web ou un script Python avec une IA, nous savions que ça marchait. La question intéressante était : **que se passe-t-il quand on confie à des agents une technologie que l’on ne maîtrise pas soi-même, et sur laquelle les IA sont elles-mêmes moins à l’aise ?**

Le moment s’y prêtait. Unity 6.6 venait d’ouvrir son éditeur aux agents : une ligne de commande (`unity`, version 1.0 bêta) et un paquet « Pipeline » (version 0.6, expérimental) qui exposent environ 517 commandes pour piloter un éditeur ouvert. Nous voulions savoir jusqu’où cela tenait.

### Unity en deux minutes (pour comprendre la suite)

- **L’éditeur** est une application lourde qui garde un état : la scène ouverte, les objets sélectionnés, les ressources importées. Le jeu se lance dans l’éditeur (« Play mode »).
- **Une scène** contient des **GameObjects**, auxquels on attache des **composants** : un script C#, un maillage, une caméra, une lumière.
- **Les ressources** (scènes, prefabs, matériaux) sont des fichiers YAML pleins d’identifiants internes. On ne les édite pas à la main : on passe par l’éditeur.
- **Le rendu** passe par un pipeline (ici URP) et des **shaders** écrits en HLSL, qui s’exécutent sur la carte graphique.
- **Burst et le Job System** compilent du C# spécialisé en code natif parallèle : indispensable pour recalculer une vague à chaque image.
- **Chaque modification de script** déclenche une recompilation et un rechargement : on compte en dizaines de secondes, pas en millisecondes comme en web.

C’est tout ce qui rend Unity difficile pour une IA : beaucoup d’état caché hors du code, des fichiers qu’on ne peut pas écrire directement, et un résultat qui ne se juge qu’en le regardant et en le jouant.

### Le jeu, en une phrase par mécanique

- **Les séries** : une série toutes les 42 secondes, trois vagues à 11 secondes d’écart, en quatre tailles (2,3 m, 3,2 m, 4,1 m et 4,9 m).
- **Le take-off** : on rame vers la plage quand la face se lève derrière soi ; la vague soulève et aligne le rider.
- **La vitesse** vient de la vague : le rouleau déroule entre 4,5 et 8 m/s selon la section, et le stick gauche règle le placement par rapport à lui (+32 % en avant, −22 % en arrière).
- **Le pump** (R2) donne de la vitesse s’il est fait en rythme ; le **stall** (L2) freine pour laisser le tube se refermer au-dessus de soi ; stall et direction ensemble font pivoter la planche pour un demi-tour serré.
- **L’envol** : en montant la face assez vite, la lèvre catapulte le rider en l’air.
- **Les figures** se font au stick droit, façon Skate : on charge vers le bas, on relance vers le haut, et le chemin du pouce choisit la figure (Air, El Rollo, Air reverse, ARS, Backflip, Invert, Grab…). Le même geste change de sens selon la zone : à plat, sur la face, sur la lèvre, dans le tube ou en l’air.
- **Le score** : chaque vague reçoit une note sur 10, et seules les deux meilleures comptent, comme en compétition.

## Notre cadre d’utilisation de l’IA

Nous n’avons pas « demandé un jeu » à une IA. Nous avons posé des règles, et ce sont elles qui ont rendu le projet possible.

1. **Une spécification écrite avant la première ligne de code.** Le document `Docs/WAVYBOARD_SPEC.md` (708 lignes, 23 sections) était la source de vérité : design, architecture, plan en neuf phases (P0 à P8), critères d’acceptation, risques.
2. **Un fichier de règles lu par chaque agent au démarrage** (`CLAUDE.md`, 14 lignes) : lire la spec et le guide avant toute action, ne jamais éditer une scène ou un prefab à la main, recompiler puis lire la console après chaque changement de script, ranger le code du jeu dans `Assets/_Project/`.
3. **Un guide de pilotage de l’éditeur** (`Docs/AGENT_PLAYBOOK.md`) : les commandes vérifiées, les pièges connus, et une règle forte, « revue visuelle obligatoire en fin de tâche graphique ».
4. **Ce qui revient à l’humain** était listé à part (`Docs/USER_ACTIONS.md`) : téléchargements sur l’Asset Store, modules à installer, branchement de la manette, décisions produit. Chaque point indiquait la phase où il devenait nécessaire et le repli prévu, pour que l’humain ne bloque jamais les agents.
5. **Git comme mémoire partagée.** Une branche par tentative, un commit par étape vérifiée, le modèle utilisé en signature de chaque commit (`Co-Authored-By`). Avant chaque redémarrage, un commit de sauvegarde : rien n’a jamais été perdu.
6. **Une mémoire entre les sessions.** L’agent tenait des notes persistantes (pièges du CLI, direction artistique de la vague, choix de gameplay, « étiquette » des tests) relues à chaque nouvelle session.

Notre rôle à nous : **directeurs artistiques, testeurs manette en main et arbitres.** Nous fournissions les références (deux photos de Teahupoo ont tout changé), nous jouions, et nous décrivions ce qui n’allait pas avec nos mots : « je peux pas remonter la vague », « la caméra est dans la texture du tube », « un mur avec un parasol au-dessus ».

## IAgraphie : quels modèles, pour quoi faire

Chaque commit porte le nom du modèle qui l’a écrit. Voici ce que racontent ces signatures.

| Modèle | Effort | Période | Rôle |
|---|---|---|---|
| Claude Fable 5.1 | Max | 8 et 9 septembre | Veille sur le web, spécification et plan, premier prototype jouable, début du run de nuit, tricks Skate v1 |
| Claude Opus 5 (1M de contexte) | Élevé | 8 au 11 septembre | Import des planches DGZ, reconstruction v4, système de figures flick-it |
| Claude Opus 4.8 | Élevé | nuit et matinée du 9 septembre | Relais pendant le run de nuit : tests de la vague, HUD, caméra |
| Claude Opus 5.5 | Élevé | 28 et 29 septembre | Redémarrage, vague Teahupoo, gameplay et caméra, grand ménage, simulation de ride, site et rapport |
| Claude Sonnet 5.5 | Standard | 29 septembre | Agents d’exploration (historique git, code, captures) pour préparer ce rapport |

Deux remarques. D’abord, la phase de veille et de planification a été confiée au modèle le plus lent et le plus réfléchi, en effort maximal : c’est là qu’une erreur coûte le plus cher. Ensuite, une fois le plan écrit, nous avons **compacté le contexte** et lancé un nouvel agent sur l’implémentation, avec la spec comme seule mémoire. C’est tout l’intérêt d’une spec autosuffisante.

Le tournant est net : tout ce qui rend le jeu bon aujourd’hui (la vague, la sensation de vitesse, la caméra, le nettoyage) a été écrit en une journée avec Opus 5.5, entre le matin du 28 septembre et le 29 à 0 h 38. Nous y revenons dans les limites : le modèle n’explique pas tout, mais sans lui nous n’y serions pas arrivés.

## La veille : ce qu’on a cherché, ce qu’on en a tiré

### La méthode

Le premier agent (Fable 5.1, effort maximal) a mené la recherche seul sur Internet, à partir de notre prompt. Il a classé chaque fait en deux catégories : **vérifié** (lu sur une page officielle, avec l’URL) ou **observé** (constaté sur notre machine). Tout est consigné dans `Docs/RESEARCH_SOURCES.md`, avec des extraits texte des documents de référence pour que les agents suivants puissent les relire sans refaire la recherche.

Le 9 septembre, une seconde recherche, menée cette fois par 28 agents en parallèle, a porté sur une seule question : comment faire un rouleau qui se referme, puisqu’aucun système d’océan du marché n’en est capable.

### Ce qui a été couvert

- **Unity 6.4 à 6.6** : nouveautés, pièges de migration, pipeline URP, Render Graph, GPU Resident Drawer, Burst et `Mesh.MeshData`.
- **Les manettes** : Input System 1.20, DualSense.
- **Le rendu de l’eau** : Storm Breakers (océan open source CC0), Crest, Boat Attack, océans FFT, tutoriels Catlike Coding (vagues de Gerstner) et Cyanilux, fils du forum Unity sur les vagues déferlantes, et la technique du film *Surf’s Up* (Sony Imageworks, SIGGRAPH 2007).
- **La science du surf** : angle de déroulement (peel angle), vitesse nécessaire au surfeur *Vs = c / sin α*, intensité de déferlement (« vortex ratio », Mead et Black 2001), revue de Scarfe et al. (2003).
- **Le bodyboard** : styles (allongé, drop-knee), vocabulaire des figures (El Rollo, ARS, invert, backflip), critères de jugement APB/IBC, jeux de référence (Barton Lynch Pro Surfing, YouRiding).
- **Les ressources gratuites** avec licence vérifiée : Storm Breakers, Poly Haven, Quaternius, Kenney et OpenGameArt (toutes CC0), polices sous licence OFL.

### Ce que nous en avons tiré

| Constat | Conséquence dans le jeu |
|---|---|
| URP n’a pas de système d’eau, et aucun océan disponible ne sait faire un tube | La vague est entièrement faite maison ; l’océan autour aussi, à la fin |
| Une seule fonction mathématique doit servir au rendu et à la physique | Le rider surfe exactement la surface qu’on voit, sans relire la carte graphique |
| Une vague est surfable si son angle de déroulement dépasse environ 30° | Le rouleau déroule dans un seul sens, à une vitesse réglée section par section |
| Unity 6.6 désactive le rechargement du domaine en Play mode | Toute variable statique doit être réinitialisée à la main (règle du `CLAUDE.md`) |
| Les vibrations de la DualSense ne passent que par USB | Documenté pour le joueur |
| Les scans Poly Haven font 0,5 à 2 millions de triangles | Interdits tels quels dans une scène jouable |

Avec le recul, **l’esprit de la spec a tenu, la lettre beaucoup moins**. Une vague dessinée plutôt que simulée, une seule fonction pour la physique et le rendu, des maths testables sans lancer le jeu : tout cela est dans le jeu final. En revanche, Storm Breakers, Cinemachine et l’interface en UI Toolkit ont disparu, et le plan de 30 jours de travail s’est fait en deux grandes poussées. Une spec n’est pas une prophétie : c’est un point de départ commun.

## Le prompt initial et le prompt engineering

Voici le prompt qui a lancé le projet, tel que nous l’avons envoyé à Fable 5.1 :

> Tu es un expert senior en création de jeux vidéo Unity. Nous utilisons Unity 6.6, et tu as accès au CLI Unity.
>
> Nous avons pour projet de faire un magnifique jeu de surf type bodyboard. Ce serait un jeu dans lequel nous aurions un personnage qui évoluerait dans un océan, avec des vagues de plusieurs intensités. Le personnage serait par défaut en position bodyboard, pourrait se déplacer, et surfer en position allongé, aller dans les rouleaux, faire des tricks, etc. Nous avons pour but de pouvoir jouer soit en clavier souris, soit en manette de PS5. Il faut être créatif et avoir un vrai gameplay attrayant et divertissant. Le jeu doit être fluide, beau, avoir de belles textures, etc.
>
> J’aimerais que tu trouves par toi-même de belles assets Unity de bonne qualité, gratuites, qu’on pourrait utiliser pour tous les assets qui seront nécessaires à ce jeu.
>
> Tu feras un gros travail de recherche en amont : documentation Unity 6.6 ; communauté autour d’Unity pour avoir des conseils sur la façon de coder notre système cible, si des gens se sont déjà cassé la tête à optimiser des choses, on pourra s’en inspirer ; assets magnifiques et gratuits pour notre jeu. Nous souhaitons vraiment avoir quelque chose de qualitatif et de fluide.
>
> *(suivait la liste complète des commandes du CLI Unity)*
>
> Tu as aussi accès au skill dédié dans `~/.claude/skills/unity-cli` ! Tu devras bien le consulter pour bien comprendre le fonctionnement, ne passe pas à côté d’informations essentielles !
>
> À l’issue de toutes tes recherches, tu vas préparer un plan d’implémentation complet, autosuffisant, qui sera la source de vérité pour notre projet. L’implémentation sera entièrement déléguée aux agents IA, alors il faut que tu prépares la spec qui nous permettra de faire quelque chose de vraiment qualitatif.
>
> Tu vas installer tout le nécessaire, récupérer les choses nécessaires (ou si tu ne peux pas du tout le faire, tu me guides sur quoi faire). Tu prépares tout en amont avant de commencer. Et on implémentera tout ultérieurement.

### Ce que ce prompt fait bien

- **Il donne un rôle** (« expert senior en création de jeux vidéo Unity ») et un contexte technique précis (version, accès au CLI).
- **Il décrit l’expérience voulue**, pas l’implémentation : allongé, rouleaux, tricks, manette PS5.
- **Il fixe une barre de qualité** (fluide, beau, gratuit mais qualitatif) et laisse l’IA choisir les moyens.
- **Il impose la recherche avant l’action**, avec des pistes concrètes : documentation officielle, communauté, ressources.
- **Il fournit les outils** : la liste des commandes et un skill officiel à lire en entier.
- **Il définit le livrable** : un plan autosuffisant, source de vérité, pensé pour être exécuté par d’autres agents.
- **Il sépare les phases** : « on implémentera tout ultérieurement ». Pas de code tant que le plan n’est pas prêt.

### Comment nous avons itéré ensuite

Les prompts suivants étaient courts et concrets. Ils décrivaient un **ressenti de joueur**, pas une solution technique, et s’appuyaient sur des images :

- deux photos de Teahupoo envoyées comme cible, avec la consigne « une seule vague, un seul rouleau, une mer qui bouge un peu » ;
- « je peux pas remonter la vague ni sauter en haut », « la caméra est bloquée à gauche », « la caméra est dans la texture du tube » ;
- « deux vagues peuvent se chevaucher, je passe à travers la première » ;
- « je suis aspiré vers le fond du rouleau : même le joystick vers la sortie, on recule » ;
- « j’ai du mal à faire demi-tour dans les rouleaux, à rentrer et sortir des tubes » ;
- et une contrainte ferme : **toutes les figures restent au stick droit**, comme dans Skate. Les figures sur boutons ont été refusées.

L’agent traduisait ensuite ces phrases en quelque chose de mesurable. Par exemple, « aspiré vers le fond du rouleau » est devenu : « l’ancienne physique plafonnait entre 4,5 et 7 m/s alors que le rouleau déroule entre 5 et 8 m/s ; un rider dans le tube était donc toujours rattrapé ». La correction et le test automatique qui la vérifie ont suivi dans le même commit.

## Comment l’IA pilote Unity

L’agent ne clique jamais dans l’éditeur. Il envoie des commandes à l’éditeur ouvert, via le CLI officiel, et lit les réponses.

```bash
unity status                                    # l'éditeur est-il prêt ?
unity command recompile                         # après chaque changement de script
unity command recompile_status                  # attendre la fin de la compilation
unity command get_console_logs                  # zéro erreur avant de continuer

# exécuter du C# dans l'éditeur : ici, photographier la vague sans lancer le jeu
unity command eval --code 'return WavyBoard.EditorTools.WaveLab.Shot("tube", 40f, 1.2f);'
unity command capture_game_view --save_path Assets/Screenshots~/lab/v6_tube.png

unity command run_tests --mode EditMode         # 22 tests, quelques secondes
```

Trois commandes ont tout changé :

- **`eval`** exécute du C# arbitraire dans l’éditeur. C’est la colonne vertébrale de tous nos outils : placer une caméra, construire une vague à un instant précis, lancer une simulation.
- **`capture_game_view`** enregistre ce que voit la caméra. L’agent relit ensuite l’image : ce sont ses yeux. Il en a pris **721** en trois semaines.
- **`run_tests`** lance les tests automatiques et renvoie le résultat.

L’agent a appris le CLI en lisant le skill officiel publié par Unity (414 lignes et 8 fichiers de référence), puis a consigné ses propres découvertes dans ses notes :

- le code passé à `eval` est un corps de méthode : pas de `using`, types entièrement qualifiés ;
- une opération de plus de 5 secondes sur le fil principal expire côté CLI mais continue dans l’éditeur : il faut interroger l’état et relancer sans casse ;
- une erreur de compilation fait démarrer l’éditeur en « Safe Mode », où le CLI ne répond plus ;
- quand l’éditeur n’a pas le focus, Unity arrête de faire tourner le jeu : il a fallu écrire un « ticker » qui fait avancer les images à la main ;
- un job Burst qui lit un tableau statique géré échoue silencieusement et tourne 25 fois plus lentement ;
- certaines valeurs écrites par le jeu en cours d’exécution atterrissent dans les fichiers de matériaux, donc dans git (six « stashes » de bruit en témoignent).

Aucun de ces pièges n’apparaît dans un tutoriel. Chacun a coûté des heures.

Les images et les vidéos de ce site ont été tournées de la même façon, sans que personne ne touche à l’éditeur : les plans fixes avec `WaveLab`, les vidéos en laissant le bot rider pendant qu’un petit composant (`FrameRecorder`) enregistre chaque image à pas de temps fixe, assemblées ensuite avec ffmpeg.

## Notre boucle de travail : tester sans jouer

Le plus gros enseignement du projet : **une IA avance aussi vite que sa boucle de retour.** Au début, l’agent ne pouvait vérifier son travail qu’en lançant le jeu et en regardant une capture. À la fin, il faisait rider des vagues entières en une fraction de seconde, sans rien lancer.

1. **Veille et spec** : un modèle en effort maximal produit la source de vérité.
2. **Implémentation** : un agent écrit le C# et les shaders, recompile, lit la console.
3. **Vérification automatique** : tests, simulation, graphiques, photos de la vague.
4. **Retour humain** : nous jouons, nous comparons aux photos de référence, nous décrivons ce qui cloche.
5. **Itération** : l’agent corrige, ou nous décidons de repartir d’une base saine.
6. **Nettoyage** : on retire tout ce que le jeu n’utilise plus.

Les outils sont apparus dans cet ordre, chacun raccourcissant la boucle :

| Outil | Ce qu’il fait | Pourquoi c’est important |
|---|---|---|
| Captures en Play mode | Lancer le jeu, capturer l’écran, relire l’image | Les yeux de l’agent, mais lents et dépendants de l’éditeur |
| `HeadlessPlayTicker` | Fait avancer le jeu quand l’éditeur n’a pas le focus | Les tests tournent même quand nous utilisons l’ordinateur |
| `WaveLab` | Construit la vague à un instant précis de sa vie et place la caméra sur un plan nommé (chenal, tube, embouchure, dos, vue aérienne, rider, mousse, line-up) | Photographier la vague sans lancer le jeu, toujours sous le même angle, pour comparer avant et après |
| `wave_profile_lab.py` | Reproduit en Python le profil de la vague et trace ses graphiques | Concevoir la forme sur un graphique avant de la voir en 3D, et vérifier qu’elle ne se croise jamais elle-même |
| `RiderBot` | Un bot qui joue avec exactement les mêmes commandes qu’un joueur, selon un plan : Pocket (tube), InAndOut, Exit, Cruise, Stall, Airs, Carve | Tester le gameplay comme un joueur, pas en trichant |
| `RideSim` | Ride une vague entière dans l’éditeur, sans Play mode, au pas de la physique (50 Hz) : une ride de 40 s prend une fraction de seconde | Des tests de gameplay déterministes et quasi instantanés |
| `playtest.py` + `PlaytestRecorder` | Lance le vrai jeu avec le bot aux commandes, capture l’écran et mesure : tubes, envols, réceptions, chutes, caméra dans l’eau | Vérifier ce que seule la vraie partie montre : la caméra, le HUD, la fluidité |

Les 22 tests finaux se lisent comme une liste de promesses faites au joueur : « attrape la vague et la surfe », « sort d’un tube profond, avec ou sans pump », « pumper bat le simple trim », « caler fait entrer dans le tube », « s’envole de la lèvre et se pose sur la face », « sort de la vague par l’épaule », « pivote serré et repart », « rentre et sort du tube encore et encore », « la caméra suit chaque virage sans clignoter ». La plupart sont vérifiés sur trois tailles de vague, soit 49 cas, tous au vert.

Une leçon au passage : **il faut tester le testeur.** Trois fois, l’agent a cru le jeu cassé alors que c’était son pilote automatique : il tenait le stick « comme une direction alors que c’est un cap », ou rapportait zéro figure parce que ses gestes étaient mal lus. Et un bot qui se crashe en boucle devant nous donne l’impression que le jeu est cassé, même quand les chiffres sont bons. Le bot final surfe proprement : zéro chute sur tous les plans et toutes les tailles.

## La vague : conception et validation

La spec le disait dès le premier jour : « la vague est le personnage principal ». C’est aussi ce qui nous a coûté le plus cher.

### Six approches pour une vague

| Version | Date | Technique | Pourquoi on l’a quittée |
|---|---|---|---|
| Storm Breakers + shader graph | 8 septembre | Océan open source, vague rendue avec une copie générée de son shader graph | Vague invisible (matrices jamais sauvegardées), faces à l’envers |
| Trou dans l’océan | 8 septembre | Découpe en transparence de l’océan sous la vague | Bandes de sable, plaques grises, dos de la vague transparent |
| « Phase 2 » adoucie | 8 septembre | Découpe progressive sur 3 m, tube rond à lèvre fermée | Tuiles d’océan oubliées, scène sauvegardée dans un état cassé |
| Profile-loft (v3) | 9 septembre | Bibliothèque de profils à la *Surf’s Up*, 11 formes × 3 intensités, jobs Burst | Complexité : un rider à 90 réglages, trois HUD superposés, un panneau de 45 curseurs |
| Vague « designée » (v4) | 10 et 11 septembre | Profil à 4 formes sur un axe d’ouverture | Jamais vraiment jouée ; verdict : « un mur avec un parasol au-dessus » |
| **Teahupoo (v5)** | **28 septembre** | **Une courbe de 15 points de contrôle, 10 formes clés dans le temps, un seul shader pour toute l’eau** | **C’est la vague du jeu** |

### Comment fonctionne la vague finale

La coupe de la vague est **une seule courbe continue** : l’eau plate devant, la face, le fond du tube, le plafond, la pointe de la lèvre, le dessus de la lèvre, la crête et le dos. C’est une spline Catmull-Rom centripète qui passe par 15 points de contrôle (F2, F1, FOOT, FACE_LO, FACE_MID, WALL, CEIL, LIPIN, TIP, LIPOUT, LIPTOP, CREST, BACK, B1, B2).

Ces 15 points bougent au fil de la vie de la vague, entre 10 formes clés dessinées à la main d’après les photos : houle, face raide, lèvre qui pitche, lèvre lancée, tube, fin du tube, impact, dôme de mousse, barre, retour au plat. Toutes les unités sont en « hauteurs de vague » : la même table sert pour une vague de 2,3 m comme de 4,9 m.

Le secret du déroulement est simple : **chaque point de la crête vit la même vie, mais décalée dans le temps.** Le point qui casse maintenant est suivi, un peu plus loin, par un point qui cassera dans une seconde. Ce décalage fait courir le rouleau le long de la vague.

![Les coupes de la vague au fil du temps, tracées par wave_profile_lab.py](../Website/public/media/lab/stages.webp)

### Des graphiques avant la 3D

Avant de toucher au jeu, l’agent a écrit un double de la vague en Python (`Tools/wave_profile_lab.py`) qui trace :

- les coupes successives d’un même point de crête, colorées par le temps ;
- les 10 formes clés avec leurs 15 points annotés ;
- le tube à l’échelle, avec un bodyboarder allongé et un rider en drop-knee pour vérifier qu’on y tient ;
- les quatre tailles de vague, avant et après l’agrandissement du tube ;
- le déroulement en 3D le long de la crête.

Le script vérifie aussi, sur 90 instants, que la courbe ne se croise jamais elle-même. Le code C# du jeu est contrôlé contre les chiffres du script : les deux doivent rester identiques.

![Les dix formes clés et leurs points de contrôle](../Website/public/media/lab/keys.webp)

![Le tube de chaque taille de vague, avant et après agrandissement](../Website/public/media/lab/tube_sizes.webp)

### Des photos, pas des chiffres

La différence entre la v4 et la v5 tient moins au code qu’à la méthode. La v4 était réglée par des chiffres dans les messages de commit et n’a jamais été vraiment jouée. La v5 a été dessinée **contre deux photos de Teahupoo**, avec un graphique vérifiable et des photos `WaveLab` prises sous les mêmes angles à chaque itération. Une cible visuelle précise transforme un avis (« c’est moche ») en écart mesurable.

## Le cheminement, commit par commit

| Date | Branche | Modèle | Étape | Ce qui se passe |
|---|---|---|---|---|
| 8 sept., matin | — | Fable 5.1 | Veille et spec | Recherche sur le web, cinq documents dont une spec de 708 lignes et un plan en neuf phases |
| 8 sept., 14 h 29 à 16 h 52 | main | Fable 5.1 | Prototype jouable | 9 commits en 2 h 23 : vague, rider, caméra, score, effets, son. Le premier commit ajoute 6 360 fichiers |
| 8 sept., 15 h 21 | feat/planches-dgz | Opus 5 | Planches DGZ | Cinq coloris importés ; orientation et flottaison « mesurées sur la géométrie plutôt que réglées à l’œil » |
| 8 sept., 20 h 49 | feature/test-louis | Fable 5.1 | Vague « phase 2 » | Découpe adoucie de l’océan, tube rond à lèvre fermée, six premiers tests |
| 8 sept., 23 h 47 à 3 h 16 | feat/wave-v3 | Fable 5.1, puis Opus 4.8 | Run autonome de nuit | 13 commits pendant que nous dormions : refonte « profile-loft » inspirée de *Surf’s Up*, 11 tests sur 11 au vert |
| 9 sept., 8 h 15 à 13 h 41 | feat/wave-v3 | Opus 4.8, puis Fable 5.1 | UX, HUD, figures | HUD « Ligne d’eau », figures Skate v1, panneau de réglage de la vague (45 curseurs, puis 8) |
| 10 et 11 sept. | feat/clean-v4 | Opus 5 | Reconstruction v4 | Repartir de main, vague « designée », figures flick-it, 42 tests. Verdict : « un mur avec un parasol » |
| 12 au 27 sept. | — | — | Pause | 17 jours sans commit |
| 28 sept., 14 h 58 | feat/clean-v4 | Opus 5.5 | Sauvegarde | La v4 est mise de côté : « gameplay restarts from main » |
| 28 sept., 15 h 39 | feat/main-tricks-centered | Opus 5.5 | Redémarrage | Le gameplay de main, les figures de la v4 et un monde centré sur le rider |
| 28 sept., 19 h 48 | feat/main-tricks-centered | Opus 5.5 | Vague Teahupoo | Un seul shader d’eau, le profil à 15 points, 51 533 lignes supprimées |
| 28 sept., 20 h 04 | raptor/main | Opus 5.5 | Renommage | Biscotte devient WavyBoard |
| 28 sept., 21 h 31 | feat/gameplay-camera-v6 | Opus 5.5 | Gameplay et caméra | Toute la face devient surfable, envol depuis la lèvre, caméra procédurale |
| 28 sept., 22 h 42 | feat/gameplay-camera-v6 | Opus 5.5 | Grand ménage | 1 013 998 lignes supprimées, dix paquets retirés |
| 29 sept., 0 h 38 | main | Opus 5.5 | Vitesse « wave power » | Course contre le rouleau, demi-tours pivotés, simulation de ride, 22 tests |
| 29 sept. | Website | Opus 5.5 et Sonnet 5.5 | Site et rapport | Ce document, et les images et vidéos du site tournées dans l’éditeur par le CLI |

### Les grandes étapes

**Le 8 septembre, tout va très vite.** En un après-midi, l’agent livre un prototype jouable : un océan, une vague qui déferle, un rider qui rame, part et surfe, une caméra, un score. Puis les problèmes arrivent, tous typiques d’Unity : la vague est invisible parce que des matrices n’existent qu’en mémoire et ne sont jamais sauvegardées ; un script de diagnostic laisse la scène enregistrée sans océan ; 20 tuiles d’océan sur 21 gardent un vieux matériau et dessinent une bande beige à travers l’eau.

**La nuit du 8 au 9, l’agent travaille seul.** Treize commits entre 23 h 47 et 3 h 16. Il lance une recherche avec 28 agents, reconstruit la vague sur la technique de *Surf’s Up*, écrit les tests, et termine en traquant un bug subtil : en mode « fast math » de Burst, une normalisation de vecteur presque nul produisait des NaN et projetait un sommet au loin, dessinant « un fin triangle turquoise ». Onze tests sur onze au vert à 3 h 16.

**Le 9 au matin, nos retours de joueurs entrent dans les commits** : « je me fais éjecter de la vague et je n’arrive pas à prendre de la vitesse dans le tube ». Le HUD, les premières figures au stick droit et un panneau de réglage de la vague arrivent. Mais la branche grossit : 90 réglages sur le rider, trois HUD qui se superposent.

**Les 10 et 11 septembre, première reconstruction.** Opus 5 repart de la branche principale, plus petite, et construit une vague « designée » et le système de figures flick-it. Le code est propre et testé (42 tests), mais la vague ne convainc pas.

**Le 28 septembre, le déclic.** Après 17 jours de pause, Opus 5.5 reprend la v4 dès le matin et réécrit encore la vague. En début d’après-midi, nous sauvegardons cette branche et repartons de la base du 8 septembre. Nous ne reprenons que deux idées : les figures au stick droit et un monde centré sur le rider. Quatre heures plus tard, la vague Teahupoo est là. Dans la soirée suivent le renommage, la nouvelle caméra, le grand ménage et le modèle de vitesse qui donne enfin la sensation de courir contre le rouleau.

### L’évolution en images

- ![Premier lancement : l’océan Storm Breakers, et pas encore de vague](../Website/public/media/evolution/01-premier-lancement.webp) **8 septembre.** Premier lancement : l’océan est là, la vague non.
- ![La vague apparaît, transparente, avec des bandes de sable](../Website/public/media/evolution/02-vague-transparente.webp) **8 septembre.** La vague apparaît, transparente, traversée de bandes de sable.
- ![Premier tube lisible](../Website/public/media/evolution/03-premier-tube.webp) **8 septembre.** Premier tube lisible, vu de l’intérieur.
- ![Premier rider sur la vague](../Website/public/media/evolution/04-premier-rider.webp) **8 septembre.** Le premier rider sur la face.
- ![Une vague en forme de rectangle posée sur la plage](../Website/public/media/evolution/05-patch-rectangulaire.webp) **9 septembre.** Wave-v3 : un rectangle d’eau posé sur le sable.
- ![La v4 avec son HUD](../Website/public/media/evolution/06-v4-hud.webp) **11 septembre.** V4 : gameplay, HUD et figures fonctionnent, la vague reste plate.
- ![La lèvre ronde de la v5](../Website/public/media/evolution/07-v5-levre.webp) **28 septembre, matin.** Opus 5.5 reprend la vague : la lèvre ronde apparaît.
- ![Le rider sur une face turquoise](../Website/public/media/evolution/08-face-turquoise.webp) **28 septembre, fin de matinée.** Le rider sur une face qui s’illumine.
- ![Le tube parfait photographié par WaveLab](../Website/public/media/evolution/09-tube-parfait.webp) **28 septembre, après-midi.** WaveLab : le tube de la vague Teahupoo, vu de l’intérieur.
- ![La vague Teahupoo vue du chenal](../Website/public/media/evolution/10-teahupoo.webp) **28 septembre, après-midi.** Le chenal : la silhouette de Teahupoo.
- ![Le jeu final, dans le tube](../Website/public/media/evolution/11-jeu-final.webp) **29 septembre.** Le jeu final : dans le tube, jauge de course contre le rouleau à l’écran.

## Le grand ménage

À force d’itérer, le projet avait accumulé des couches : des packs de ressources entiers pour une seule image, des paquets Unity que plus rien n’utilisait, des scripts d’outillage de versions abandonnées. Quand la logique du jeu nous a semblé stable, nous avons demandé à l’agent un audit simple : **ne livrer que ce que le jeu utilise**. Il a parcouru le graphe de dépendances de la scène, puis tout supprimé.

| Ce qui a été supprimé | Fichiers | Lignes |
|---|---|---|
| Packs Kenney (seule la particule d’écume reste) | 5 252 | 271 000 |
| Exemples de Shader Graph | 363 | 413 000 |
| Storm Breakers (seul le son du vent reste) | 294 | 307 000 |
| Poly Haven (seul le ciel HDRI reste) | 208 | 18 000 |
| Quaternius (seul le mannequin reste) | 12 | 1 800 |
| Polices, scripts morts, outils obsolètes | 61 | 1 900 |

Dix paquets Unity sont partis avec : Cinemachine, Splines, Timeline, uGUI et TextMeshPro, VFX Graph, Visual Scripting, AI Navigation, Animation Rigging, glTFast et AI Inference, en plus de ProBuilder la veille.

Le chiffre le plus parlant : **notre propre code C# est passé de 7 726 à 7 680 lignes.** Le gonflement ne venait pas de nous mais des ressources et des paquets que les agents ajoutaient « au cas où » et ne retiraient jamais. Un agent ajoute volontiers ; il ne nettoie que si on le lui demande.

## Comment le jeu fonctionne (vue technique)

- **Entrées** : `InputRouter` lit la manette ou le clavier et la souris (la souris devient un stick virtuel) et expose une interface `IRiderInput`. Le bot de test utilise la même interface.
- **Eau** : `WaterSurfaceComposite` répond à une seule question, « quelle est l’eau en ce point ? », en interrogeant la vague qui possède ce point ou, à défaut, la houle ambiante.
- **Vague** : `SurfWave` reconstruit à chaque image un maillage de 320 × 129 sommets avec des jobs Burst, à partir de `WaveProfile`. Le même profil analytique sert à la physique : on surfe exactement l’eau qu’on voit.
- **Océan** : `OceanSurface` est un maillage polaire centré sur la caméra, fin sous le joueur et grossier jusqu’à 6 km ; `OceanSwell` somme six vagues de Gerstner, avec les mêmes formules en C# et dans le shader.
- **Rider** : `RiderController` est une machine à états (rame, canard, take-off, ride, air, chute, sortie) intégrée à 50 Hz, dont la pose est extrapolée à chaque image pour rester fluide à 60 ou 144 images par seconde. Environ 100 réglages vivent dans un ScriptableObject.
- **Figures** : `FlickIt` reconnaît les gestes du stick (logique pure, testable), `TrickCatalog` les traduit selon la zone, `TrickRunner` superpose jusqu’à quatre rotations.
- **Caméra** : `CameraDirector` est entièrement procédural, sans Cinemachine. Plans de ride, de tube, d’air et de chute ; un bras à ressort teste le segment rider-caméra contre le volume analytique de l’eau pour ne jamais finir dans la vague.
- **Rendu** : un seul shader `WavyBoard/Water` pour la mer et la vague : lumière turquoise à travers la lèvre, reflets, scintillement du soleil, écume en dentelle, ombre dans le tube.
- **Interface et score** : `GameHud` (jauge de course contre le rouleau, indications contextuelles, légende des figures) et `RideScorer` (note sur 10, deux meilleures vagues).
- **Avatar** : un mannequin humanoïde CC0 posé à chaque image par un solveur procédural, sans aucune animation enregistrée.

| Mesure | Valeur |
|---|---|
| Fichiers C# | 48 (40 pour le jeu, 4 pour l’éditeur, 4 pour les tests) |
| Lignes de C# | 8 845 (7 758 jeu, 597 éditeur, 490 tests) |
| Shader | 1 shader, 4 passes, 449 lignes |
| Tests automatiques | 22 (49 cas, tous au vert) |
| Commandes | 12 actions de jeu, 2 schémas (manette, clavier et souris) |
| Figures | plus de 30, réparties sur 5 zones |
| Ressources tierces | toutes CC0 : Quaternius, Poly Haven, Kenney, OpenGameArt, Storm Breakers |

## Les limites de l’exercice

### L’IA prototype vite, la dernière marche est longue

Un prototype jouable en un après-midi, c’est réel. Une vague qui nous plaise et une prise en main agréable, il a fallu trois semaines, six vagues et quatre modèles. Plus on s’approche du résultat voulu, plus chaque progrès coûte cher, parce que ce qui reste à corriger est affaire de goût et de sensation.

### Elle ne voit pas et elle ne sent pas

Une capture d’écran permet à l’agent de voir une forme, pas de juger si une vague est belle ou si une manette répond bien. Tout ce qui relève du ressenti est passé par nous : « un mur avec un parasol », « la caméra est dans le tube », « je recule même stick en avant ». Sans humain qui joue, l’agent optimise des chiffres qui ne disent pas si c’est amusant.

### Itérer trop longtemps fait tourner en rond

Sur la branche wave-v3, chaque correction ajoutait un réglage ou une exception : 90 paramètres sur le rider, trois HUD superposés, un panneau de 45 curseurs. Le code n’était ni simple ni générique, et chaque nouvelle demande devenait plus risquée. Deux fois, la meilleure décision a été de **repartir d’une base saine**, en gardant les idées et pas le code. Deux agents travaillant dans le même dossier ont même mélangé leurs changements dans un commit (« j’ai lancé `git add -A` sans regarder les fichiers de la vague ») : l’erreur est documentée dans le message lui-même.

### Unity est un terrain difficile pour une IA

| Critère | Site web en TypeScript | 3D en WebGL | Algorithme en Python | Jeu sous Unity |
|---|---|---|---|---|
| Présence dans les données d’entraînement | Très forte | Forte | Très forte | Moyenne, et Unity 6.6 est tout récent |
| Boucle de retour | Quelques secondes | Quelques secondes | Quelques secondes | Des dizaines de secondes à quelques minutes |
| Vérifiable par l’IA seule | Oui | En grande partie | Oui | Difficilement |
| État caché hors du code | Faible | Faible | Quasi nul | Fort : scènes, matériaux, état de l’éditeur |
| Maturité de l’outillage IA | Mûr | Mûr | Mûr | Récent : CLI en bêta, Pipeline expérimental |

Ce site en est la démonstration : la vague 3D de sa page d’accueil est un portage en WebGL du profil du jeu, écrit et réglé en une soirée. Le même travail sous Unity nous a pris trois semaines. Ce n’est pas que la vague web soit plus simple ; c’est que tout, autour, aide l’IA : un rechargement instantané, du code qui est toute la vérité, et des technologies qu’elle a vues des millions de fois.

### Le modèle compte, mais pas seul

Nous n’étions pas arrivés à un résultat satisfaisant avant Opus 5.5. Mais la journée du 28 septembre combine trois changements à la fois : un meilleur modèle, un redémarrage depuis une base saine, et des outils de test enfin rapides (photos sans Play mode, graphiques, simulation). Git ne permet pas de séparer leur part. Notre conviction : sans Opus 5.5, pas de journée du 28 ; sans les deux premières semaines d’erreurs, pas de journée du 28 non plus.

## La morale

**Une IA ne remplace pas la compréhension, elle la rend plus urgente.** Nous avons fait un jeu dans une technologie que nous ne connaissions pas, et nous l’avons appris en chemin : en lisant les commits, en comprenant pourquoi une vague était invisible ou pourquoi un rider reculait. Les agents ont écrit le code ; les décisions, les références et le goût sont restés les nôtres.

Ce que nous referions à l’identique :

1. **Planifier avec le meilleur modèle, en effort maximal, avant d’écrire du code.** Une spec autosuffisante permet de repartir d’un contexte vierge.
2. **Écrire les règles une fois pour toutes** (un `CLAUDE.md` court) plutôt que de les répéter.
3. **Donner des yeux à l’IA, puis un simulateur.** Captures, graphiques, photos sans lancer le jeu, tests qui jouent à notre place : chaque outil a divisé le temps de réaction.
4. **Viser avec des images, pas avec des adjectifs.** Deux photos de Teahupoo ont fait plus que cent messages.
5. **Oser repartir de zéro**, avec un commit de sauvegarde, dès que chaque correction en appelle deux autres.
6. **Nettoyer régulièrement** : l’IA accumule, elle ne range pas d’elle-même.

Ce que ce projet nous a appris sur l’IA en général : elle excelle là où le monde est fait de texte, de tests rapides et de technologies très répandues. Dès qu’on sort de ce terrain (un éditeur plein d’état, une sensation à juger, un domaine récent), elle reste d’une aide énorme, à condition qu’un humain tienne la barre.

Pour la suite, il reste beaucoup à faire : un menu, l’anglais, le son réactivé, d’autres spots. Nous savons maintenant comment nous y prendre.

## Annexes

### Sources principales de la veille

- Documentation Unity 6.4 à 6.6 (nouveautés, guide de migration, URP, Render Graph, GPU Resident Drawer, Job System, `Mesh.MeshData`) : docs.unity3d.com
- Input System 1.20 et DualSense : docs.unity3d.com/Packages/com.unity.inputsystem@1.20
- Storm Breakers, océan open source CC0 : github.com/Stormrider31/Storm-Breakers
- Vagues de Gerstner : catlikecoding.com/unity/tutorials/flow/waves
- Anatomie d’un shader d’eau : cyanilux.com/tutorials/water-shader-breakdown
- Simuler des spots de surf avec la bathymétrie : jettelly.com
- Angle de déroulement d’une vague : scienceofsurfing.com
- Scarfe, Elwany, Mead et Black (2003), *The Science of Surfing Waves and Surfing Breaks* : escholarship.org/uc/item/6h72j1fz
- Figures de bodyboard et règles de jugement : surfertoday.com, Wikipédia (El Rollo, ARS)
- Ressources : polyhaven.com, kenney.nl, quaternius.com, opengameart.org

### Documents du projet

- `Docs/WAVYBOARD_SPEC.md` : la spécification initiale (708 lignes)
- `Docs/AGENT_PLAYBOOK.md` : le guide de pilotage de l’éditeur par les agents
- `Docs/RESEARCH_SOURCES.md` : la veille, source par source
- `Docs/ASSETS_MANIFEST.md` : les ressources et leurs licences
- `Docs/USER_ACTIONS.md` : ce qui était réservé aux humains
- `CLAUDE.md` : les règles lues par chaque agent

### Crédits

Mannequin : Quaternius (CC0). Ciel : Poly Haven, *spiaggia di mondello* (CC0). Particule d’écume : Kenney (CC0). Sons de vagues : OpenGameArt (CC0). Son du vent : Storm Breakers (CC0). Tout le reste (vague, océan, shader, rider, caméra, interface, planche) a été écrit pour le jeu.
