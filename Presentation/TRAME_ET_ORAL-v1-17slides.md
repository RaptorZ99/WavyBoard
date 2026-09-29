# WavyBoard · trame et texte de la soutenance

Soutenance de 10 minutes maximum, à trois voix : Alexandre, Maxime, Louis. Diaporama : `Presentation/WavyBoard-soutenance.html` (s’ouvre dans Chrome, fonctionne hors ligne). Site et rapport en ligne : https://raptorz99.github.io/WavyBoard/ (rapport : https://raptorz99.github.io/WavyBoard/rapport).

**Durée visée : 9:31** (1253 mots à ~140 mots par minute, respirations et changements de slide compris), soit environ 29 secondes de marge sur les 10 minutes (la parole seule fait 8:57). Répartition : Alexandre 191 s, Maxime 172 s, Louis 174 s.

## 1. Le fil directeur

- **La question (problématique)** : que se passe-t-il quand on confie à des agents IA une technologie que l’on ne maîtrise pas soi-même ?
- **La métaphore filée** : la présentation suit la vie d’une vague, comme notre rapport web. La **houle** (préparer : veille, spec, harnais), le **déferlement** (construire, casser, recommencer), le **tube** (le jeu : vague, gameplay, architecture), l’**écume** (ce que nous en retenons). Le diaporama la montre : la vague du jeu, calculée en direct, grandit en bas de l’écran slide après slide, se creuse au troisième acte et finit en écume sur les dernières slides, qui passent en clair.
- **La boucle** : on ouvre sur « aucun de nous trois n’avait jamais utilisé Unity », on ferme sur « les agents ont écrit le code, mais les décisions, les références et le goût sont restés les nôtres ».
- **La phrase à retenir** : une IA ne remplace pas la compréhension, elle la rend plus urgente.
- **Pourquoi cet ordre** : le jury voit d’abord le résultat (la vidéo du jeu), puis la question, puis la méthode (veille, spec, harnais), puis le récit honnête des échecs et de ce qui les a débloqués, puis seulement le jeu et sa technique, enfin les limites et les leçons. La méthode vient avant la technique parce que c’est elle, le sujet du projet ; les échecs viennent avant la vague finale parce qu’ils expliquent pourquoi elle marche.

## 2. Répartition et minutage

| # | Slide | Acte | Qui parle | Durée | Cumul |
|---|---|---|---|---|---|
| 01 | WavyBoard | Ouverture | Alexandre | ~28 s | 0:28 |
| 02 | Le pari | Ouverture | Alexandre | ~35 s | 1:03 |
| 03 | La veille | I · La houle | Alexandre | ~35 s | 1:38 |
| 04 | La spec et le prompt | I · La houle | Alexandre | ~30 s | 2:08 |
| 05 | Le harnais Claude Code | I · La houle | Alexandre | ~32 s | 2:40 |
| 06 | L’IA pilote Unity | II · Le déferlement | Maxime | ~37 s | 3:17 |
| 07 | Le cheminement | II · Le déferlement | Maxime | ~37 s | 3:54 |
| 08 | Six vagues, cinq abandonnées | II · Le déferlement | Maxime | ~32 s | 4:26 |
| 09 | La boucle de retour | II · Le déferlement | Maxime | ~37 s | 5:03 |
| 10 | Le déclic | III · Le tube | Louis | ~29 s | 5:32 |
| 11 | La vague | III · Le tube | Louis | ~39 s | 6:11 |
| 12 | Le gameplay | III · Le tube | Louis | ~42 s | 6:53 |
| 13 | L’architecture | III · Le tube | Louis | ~39 s | 7:32 |
| 14 | La stack | III · Le tube | Louis | ~30 s | 8:02 |
| 15 | Les limites | IV · L’écume | Maxime | ~34 s | 8:36 |
| 16 | Ce que nous referions | IV · L’écume | Alexandre · Maxime · Louis | ~21 s | 8:57 |
| 17 | La morale | IV · L’écume | Alexandre | ~34 s | 9:31 |

**Passages de parole** (à répéter, c’est ce que le jury remarque le plus) :

1. Alexandre → Maxime, fin de la slide 05 : « Sur le papier, tout était prêt. / Mais Unity avait d’autres plans. » Alexandre regarde Maxime, qui enchaîne sans « merci ».
2. Maxime → Louis, fin de la slide 09 : « Et le résultat, c’est Louis qui va vous le montrer. »
3. Louis → Maxime, fin de la slide 14 : « Maxime, place au bilan. »
4. Slide 16 : chacun dit ses deux leçons, dans l’ordre Alexandre, Maxime, Louis, sans transition.
5. Alexandre conclut (slide 17) : la dernière phrase est apprise par cœur.

Celui qui parle avance ses propres slides (flèche ou télécommande). À chaque changement d’acte, un carton de 1,5 seconde affiche l’acte et le nom de l’orateur : c’est le moment du passage de relais.

## 3. Le texte à dire

Repères de jeu : `/` courte respiration · `//` pause marquée · **gras** = mot à appuyer légèrement. Le même texte s’affiche dans la vue présentateur (touche P).

### Ouverture · Accroche, question, plan

#### ⏱ 01 · WavyBoard · Alexandre · ~28 s

*À l’écran : Vidéo du jeu plein écran : le rider dans le tube.*

Bonjour à tous. //

Ce que vous voyez derrière nous n’est pas une vidéo de surf. / C’est **notre jeu**. //

Il s’appelle WavyBoard : / on y rame vers une vague lourde, / on se cale dans le tube, / et on s’envole de la lèvre. //

Et pourtant, quand nous avons commencé, / aucun de nous trois n’avait jamais utilisé **Unity**. //

Je suis Alexandre, / voici Maxime et Louis.

#### ⏱ 02 · Le pari · Alexandre · ~35 s

*À l’écran : La question du projet, l’ouverture d’Unity 6.6 aux agents, le plan en quatre temps.*

C’était voulu : / un site web avec une IA, nous savions que ça marchait. //

Nous voulions tester autre chose : / **que se passe-t-il quand on confie à des agents IA une technologie que l’on ne maîtrise pas soi-même ?** //

Et Unity 6.6 venait justement d’ouvrir son éditeur aux agents. //

Nous allons vous le raconter comme la vie d’une vague : / la houle, c’est la préparation ; / le déferlement, avec Maxime ; / le tube, avec Louis ; / et l’écume, ce que nous en retenons.

### I · La houle · Préparer : veille, spec, harnais

#### ⏱ 03 · La veille · Alexandre · ~35 s

*À l’écran : Les six domaines de la veille, vérifié contre observé, ce que nous en avons tiré.*

Notre première règle : / aucune ligne de code avant la **veille**. //

Nous l’avons confiée au modèle le plus réfléchi, Claude Fable 5.1, en effort maximal. / Car c’est au début qu’une erreur coûte le plus cher. //

Il a étudié Unity, le rendu de l’eau, / et même la physique du surf. / Chaque fait était classé : / vérifié, avec sa source, / ou observé sur notre machine. //

Premier constat : / aucun océan du marché ne sait faire un **tube**. / Notre vague serait donc faite maison.

#### ⏱ 04 · La spec et le prompt · Alexandre · ~30 s

*À l’écran : La spécification (708 lignes, 9 phases) et le prompt initial.*

La veille a produit une **spécification** de sept cents lignes, / avec un plan en neuf phases. //

Notre prompt de départ décrivait l’expérience voulue, / pas l’implémentation. / Et il exigeait un plan **autosuffisant**. //

Pourquoi ? / Parce qu’ensuite, nous avons vidé le contexte, / et confié le code à un nouvel agent, avec la spec pour seule mémoire. //

Avec le recul, l’esprit de la spec a tenu. / La lettre, beaucoup moins.

#### ⏱ 05 · Le harnais Claude Code · Alexandre · ~32 s

*À l’écran : Le harnais : spec, CLAUDE.md, playbook, actions humaines, Git, mémoire. Et notre rôle.*

Autour des agents, nous avons construit un **harnais**. //

Un fichier de règles de quatorze lignes, lu par chaque agent au démarrage : / par exemple, ne jamais modifier une scène à la main. / Un guide pour piloter l’éditeur. / Et Git comme mémoire, / avec le nom du modèle sur chaque commit. //

Notre rôle à nous ? / Directeurs artistiques, testeurs manette en main, / et **arbitres**. //

Sur le papier, tout était prêt. / Mais Unity avait d’autres plans.

*→ Regard vers Maxime, qui s’avance.*

### II · Le déferlement · Construire, casser, recommencer

#### ⏱ 06 · L’IA pilote Unity · Maxime · ~37 s

*À l’écran : Le terminal : les commandes du CLI tapées en direct, la photo de la vague qui apparaît, les pièges.*

Unity est un éditeur lourd, / plein d’état caché. / Et l’agent n’y clique **jamais**. / Il lui envoie des commandes, / et il lit les réponses. //

Trois commandes ont tout changé : / Eval, qui exécute du C# dans l’éditeur, / Run tests, / et surtout Capture, qui photographie ce que voit la caméra. / Ce sont **ses yeux** : / plus de sept cents photos. //

Mais aucun tutoriel ne parle des pièges. / Une seule erreur de compilation, / et l’éditeur passe en mode sans échec : / la ligne de commande ne répond plus.

#### ⏱ 07 · Le cheminement · Maxime · ~37 s

*À l’écran : La frise du 8 au 29 septembre, colorée par modèle.*

Le 8 septembre, tout va très vite. / En un après-midi, l’agent livre un **prototype jouable**. / Pendant ce temps, Alexandre branche nos planches de bodyboard. //

Le soir même, nous laissons l’agent travailler seul. / Treize commits pendant que nous dormons, / et tous les tests au vert à trois heures du matin. //

Le 10, nous reconstruisons tout sur une base propre. / Puis, dix-sept jours de pause. //

Et le 28 septembre, en une seule journée, / tout ce qui rend le jeu bon aujourd’hui est écrit.

#### ⏱ 08 · Six vagues, cinq abandonnées · Maxime · ~32 s

*À l’écran : Les six vagues en images, avec la raison de chaque abandon.*

Au total, nous avons construit **six** vagues, / et jeté les cinq premières. //

La première était invisible : / ses matrices n’existaient qu’en mémoire, jamais sauvegardées. //

Celle de la nuit fonctionnait. / Mais chaque correction ajoutait un réglage : / quatre-vingt-dix paramètres, / trois interfaces superposées. //

La suivante était propre et testée. / Notre verdict : // « un mur avec un parasol au-dessus ». //

Deux fois, la meilleure décision a été de **repartir de zéro**, / en gardant les idées, pas le code.

#### ⏱ 09 · La boucle de retour · Maxime · ~37 s

*À l’écran : L’échelle des outils, du plus lent au plus rapide, et les 22 tests.*

Ce qui a tout débloqué tient en une phrase : / une IA avance aussi vite que **sa boucle de retour**. //

Au début, pour vérifier son travail, l’agent devait lancer le jeu. / Puis il a su photographier la vague sans le lancer. / À la fin, un bot ridait une vague entière / en une **fraction de seconde**. //

Nos vingt-deux tests se lisent comme des promesses faites au joueur : / par exemple, « sort d’un tube profond ». //

Et le résultat, c’est Louis qui va vous le montrer.

*→ Louis prend la parole.*

### III · Le tube · Le jeu : vague, gameplay, architecture

#### ⏱ 10 · Le déclic · Louis · ~29 s

*À l’écran : 28.09 : les trois ingrédients, et l’avant/après de la vague.*

Le 28 septembre, trois choses changent en même temps. / Un meilleur modèle, Opus 5.5. / Une base saine : nous repartons du 8 septembre. / Et ces outils de test, enfin rapides. //

Surtout, nous donnons une **cible** à l’agent : / deux photos de Teahupoo, à Tahiti. / Une seule vague, un seul rouleau, une mer qui bouge un peu. //

Quatre heures plus tard, / la vague est là.

#### ⏱ 11 · La vague · Louis · ~39 s

*À l’écran : La coupe de la vague du jeu, calculée en direct : 15 points, 10 formes clés, et le déroulement.*

La vague est le **personnage principal** du jeu. //

Sa coupe tient en une seule courbe, / qui passe par quinze points de contrôle. / Ces points bougent entre dix formes clés, dessinées d’après les photos : / vous la voyez naître, se creuser, puis casser. //

Le secret du déroulement est simple : / chaque point de la crête vit la même vie, / mais **décalée dans le temps**. / C’est ce décalage qui fait courir le rouleau. //

Et la même fonction sert au rendu et à la physique : / on surfe exactement l’eau que l’on voit.

#### ⏱ 12 · Le gameplay · Louis · ~42 s

*À l’écran : Take-off, tube, envol en vidéo ; les mécaniques ; le geste au stick droit.*

Côté jeu, on rame, / la vague nous soulève : / c’est le take-off. //

Ici, la vitesse vient **de la vague**. / Un jour, nous avons écrit à l’agent : / « je suis aspiré vers le fond du rouleau ». / Il a traduit : le rider plafonnait sous la vitesse du rouleau. //

On pompe en rythme pour accélérer. / On freine pour laisser le tube se refermer sur soi. / Et en montant vite, la lèvre nous catapulte. //

En l’air, tout se joue au stick droit, comme dans le jeu Skate : / on charge, on relance, / et le chemin du pouce choisit la figure.

#### ⏱ 13 · L’architecture · Louis · ~39 s

*À l’écran : Le flux : ce qui entre, ce qui est simulé à 50 Hz, ce qui est montré.*

Côté architecture, tout suit un flux simple. //

À gauche, ce qui entre : la manette, le clavier, / ou notre bot, / qui passe par **la même interface** qu’un joueur. //

Au centre, ce qui est simulé, cinquante fois par seconde : / le rider, qui est une machine à états, / les figures, / et l’eau, qui répond à une seule question : « quelle est l’eau en ce point ? » //

À droite, ce qui est montré : / une caméra entièrement procédurale, qui ne finit jamais dans la vague, / et un seul shader pour toute la mer.

#### ⏱ 14 · La stack · Louis · ~30 s

*À l’écran : Les technologies par couche et l’arborescence du dépôt.*

Pour la stack : Unity 6.6, avec le pipeline de rendu URP. / Près de neuf mille lignes de C#, / accélérées par Burst et le Job System pour recalculer la vague à chaque image. / Un shader en HLSL, / l’Input System pour la manette PS5, / et Python pour nos outils de test. //

Le site, en React et three.js, / est déployé en ligne par GitHub Actions. //

Maxime, place au bilan.

*→ Maxime reprend la parole.*

### IV · L’écume · Ce que nous en retenons

#### ⏱ 15 · Les limites · Maxime · ~34 s

*À l’écran : Quatre limites : la dernière marche, voir et sentir, l’accumulation, Unity face au web.*

Ce projet montre aussi les limites de l’exercice. //

L’IA prototype très vite. / Mais la dernière marche est longue : / elle voit une forme, / elle ne sent pas une manette. //

Elle accumule, aussi. / Au grand ménage final, nous avons supprimé plus d’**un million** de lignes. / Notre propre code, lui, n’a presque pas bougé. //

Enfin, Unity reste un terrain difficile pour une IA. / La vague 3D de notre site a pris une soirée. / Sous Unity, **trois semaines**.

#### ⏱ 16 · Ce que nous referions · Alexandre · Maxime · Louis · ~21 s

*À l’écran : Les six leçons, deux par orateur.*

**Alexandre** : Ce que nous referions ? / Planifier avant de coder, avec le meilleur modèle. / Et écrire les règles une fois pour toutes.

**Maxime** : Donner des yeux à l’IA, puis un simulateur. / Et oser repartir de zéro.

**Louis** : Viser avec des images, pas avec des adjectifs. / Et nettoyer régulièrement.

#### ⏱ 17 · La morale · Alexandre · ~34 s

*À l’écran : La phrase finale, merci, questions.*

Quand nous avons commencé, aucun de nous ne connaissait Unity. / Les agents ont écrit le code. / Mais les décisions, les références et le goût / sont restés **les nôtres**. //

Une IA ne remplace pas la compréhension. // Elle la rend plus **urgente**. //

Et la suite ? / Plus de gameplay, de meilleurs assets, / et une sortie sur **Steam**. //

Merci pour votre attention. / Le rapport est en ligne, le lien est à l’écran. / Nous sommes prêts pour vos questions.

Durée estimée : ~9:31.

## 4. Utiliser le diaporama

- Ouvrir `Presentation/WavyBoard-soutenance.html` dans Chrome (double-clic). Tout est local : polices, vidéos et images sont dans `Presentation/assets/`. Ne pas séparer le fichier HTML de ce dossier.
- `F` plein écran · `→` ou `Espace` slide suivante · `←` précédente · `B` écran noir (pendant les questions) · `H` aide.
- **Vue présentateur** : brancher le projecteur en écran étendu, mettre le diaporama sur le projecteur, puis `P`. Une fenêtre s’ouvre sur l’ordinateur avec le texte de la slide, l’orateur, la slide suivante et le minuteur (temps écoulé, temps prévu à cette slide, temps passé sur la slide). Le minuteur démarre seul au passage à la slide 02 ; `T` le remet à zéro. Autoriser les pop-ups si Chrome les bloque.
- `N` affiche le texte sous la slide, pour répéter sur un seul écran.
- `E` rend les textes modifiables (clic dans le texte) ; `Ctrl S` enregistre une copie du diaporama avec les corrections, à placer à côté du dossier `assets/`.
- L’adresse `…/WavyBoard-soutenance.html#8` ouvre directement la slide 8.
- Le site est affiché deux fois : sur la slide 14 (déploiement GitHub Actions) et sur la dernière slide, avec un QR code vers https://raptorz99.github.io/WavyBoard/ que le jury peut scanner pendant les questions. Garder la slide 17 affichée pendant les questions (ou `B` pour un écran noir).

## 5. Si le jury pose ces questions

- **« Quelle part du code avez-vous écrite vous-mêmes ? »** (Louis) Presque rien directement : 56 des 58 commits sont co-signés par un modèle Claude. Notre travail : cadrer (spec, règles), fournir les références, jouer, juger, et décider quand repartir de zéro. Nous avons appris Unity en lisant les commits et en comprenant chaque échec.
- **« Comment vous êtes-vous réparti le travail ? »** (chacun pour soi) Alexandre : les planches DGZ et l’intégration 3D. Maxime : le prototype, le HUD, le réglage de la vague. Louis : la vague Teahupoo, le gameplay, la caméra. La veille, la spec et les tests manette en main étaient communs.
- **« Pourquoi ne pas avoir pris un océan existant ? »** (Alexandre) La veille l’a montré : URP n’a pas de système d’eau et aucun océan disponible ne sait faire un rouleau qui se referme. Storm Breakers a servi au début, puis a été retiré au grand ménage (il n’en reste que le son du vent).
- **« Comment savez-vous que le jeu marche sans y jouer ? »** (Maxime) 22 tests automatiques, 49 cas, qui rident des vagues entières avec un bot branché sur les mêmes commandes qu’un joueur, sans lancer le jeu (RideSim, à 50 Hz). `playtest.py` vérifie en plus la vraie partie : caméra, HUD, fluidité. Et nous jouons, parce qu’un test ne dit pas si c’est amusant.
- **« Est-ce Opus 5.5 qui a tout débloqué ? »** (Louis) Le 28 septembre combine trois changements : un meilleur modèle, une base saine, des tests rapides. Git ne permet pas de séparer leur part. Sans Opus 5.5, pas de journée du 28 ; sans les deux premières semaines d’erreurs, pas de journée du 28 non plus.
- **« Qu’est-ce qui a coûté le plus cher ? »** (Maxime) La vague : six versions. Et les pièges propres à Unity : état caché dans les scènes et les matériaux, mode sans échec, jeu qui s’arrête sans le focus, Burst qui échoue en silence.
- **« Le jeu est-il distribuable ? »** (Alexandre) Il se joue aujourd’hui dans l’éditeur Unity, à la manette PS5 ou au clavier et à la souris. Le site (https://raptorz99.github.io/WavyBoard/) montre les vidéos et le rapport complet. La suite : plus de gameplay, de meilleurs assets, un menu, l’anglais, d’autres spots, et une sortie sur Steam.
- **« Que feriez-vous différemment ? »** (Louis) Construire la boucle de vérification rapide (photos sans lancer le jeu, simulation) dès la première semaine, et viser des photos de référence dès le premier jour : la v4, réglée par des chiffres, n’a jamais vraiment été jouée.

## 6. Répétition

- Deux répétitions complètes chronométrées avec la vue présentateur. Cible : 9 min 15, jamais plus de 9 min 45.
- Si ça déborde, couper dans cet ordre : la phrase « Pendant ce temps, Alexandre branche nos planches » (slide 07), « Le site, en React et three.js, est déployé en ligne par GitHub Actions » (slide 14), « Chaque fait était classé… » (slide 03).
- Ne jamais lire la slide : elle montre les chiffres, la voix raconte.
- Plan B : si une vidéo ne se lance pas, l’image fixe reste affichée et le discours ne change pas.
