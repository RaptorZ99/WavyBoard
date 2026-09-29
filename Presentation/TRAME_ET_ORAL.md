# WavyBoard · trame et texte de la soutenance (12 slides)

Soutenance de 10 minutes maximum, démo comprise, à trois voix : Alexandre, Maxime, Louis. Diaporama : `Presentation/WavyBoard-soutenance.html` (Chrome, hors ligne). Site et rapport : https://raptorz99.github.io/WavyBoard/ (rapport : https://raptorz99.github.io/WavyBoard/rapport). L’ancienne version en 17 slides est sauvegardée à côté (`*-v1-17slides.*`).

**Durée visée : 9:16**, soit 0:44 de marge sur les 10 minutes. Parole : 963 mots, environ 6:53 à ~140 mots par minute. Démo en direct : 2:00, bascule vers Unity comprise. Temps de parole hors démo : Alexandre ~2:17, Maxime ~2:19, Louis ~2:17, et Alexandre joue pendant la démo.

## 1. Le fil directeur

- **La question** : que se passe-t-il quand on confie à des agents IA une technologie que l’on ne maîtrise pas soi-même ? Elle est posée dès la première slide, sous le titre.
- **Le fil** : quatre parties simples, préparation (veille, spec, harnais), construction (six versions de la vague), le jeu (la vague, l’architecture, la stack), le bilan (limites et leçons), puis la démo. En bas de l’écran, la vague du jeu, calculée en direct, grandit de slide en slide et finit en écume sur la dernière.
- **Le temps fort** : la démo en direct, juste avant la conclusion. Elle remplace la slide de gameplay : on ne décrit pas les mécaniques, on les montre.
- **La boucle** : on ouvre sur « aucun de nous trois n’avait jamais utilisé Unity », on ferme sur « les décisions, les références et le goût sont restés les nôtres ».
- **La phrase à retenir** : une IA ne remplace pas la compréhension, elle la rend plus urgente.

## 2. Les 12 slides et le minutage

| # | Slide | Partie | Qui | Durée | Cumul |
|---|---|---|---|---|---|
| 01 | WavyBoard | Ouverture | Alexandre | ~0:41 | 0:41 |
| 02 | La veille et la spécification | I · Préparation | Alexandre | ~0:51 | 1:32 |
| 03 | Le harnais Claude Code | I · Préparation | Alexandre | ~0:41 | 2:13 |
| 04 | L’IA pilote Unity | II · Construction | Maxime | ~0:40 | 2:53 |
| 05 | Six versions de la vague | II · Construction | Maxime | ~1:03 | 3:56 |
| 06 | La vague | III · Le jeu | Louis | ~0:41 | 4:37 |
| 07 | L’architecture | III · Le jeu | Louis | ~0:33 | 5:10 |
| 08 | Les technologies | III · Le jeu | Louis | ~0:29 | 5:39 |
| 09 | Les limites | IV · Le bilan | Maxime | ~0:35 | 6:14 |
| 10 | Ce que nous referions | IV · Le bilan | Alexandre · Maxime · Louis | ~0:27 | 6:41 |
| 11 | Démonstration | En direct | Alexandre (joue) | ~2:00 | 8:41 |
| 12 | Conclusion | Conclusion | Louis | ~0:35 | 9:16 |

**Passages de parole** (à répéter : c’est ce que le jury remarque le plus) :

1. Alexandre → Maxime, fin de la slide 03 : « Sur le papier, tout était prêt. / Mais Unity nous réservait quelques surprises. » Maxime enchaîne sans « merci ».
2. Maxime → Louis, fin de la slide 05 : « Quatre heures plus tard, la vague est là. » Louis enchaîne sur la vague.
3. Louis → Maxime, fin de la slide 08 : « Maxime va maintenant vous parler des limites. »
4. Slide 10 : deux leçons chacun, Alexandre, Maxime, Louis. Louis termine par « Et maintenant, place au jeu. Alexandre ? »
5. Slide 11 : Alexandre joue et commente. Il revient au diaporama et passe à la slide 12.
6. Slide 12 : Louis conclut. La dernière phrase est apprise par cœur.

À chaque nouvelle partie, un carton affiche le nom de la partie et de l’orateur pendant environ 3 secondes (y compris « Démonstration · Alexandre ») : c’est le moment du passage de relais. Appuyer sur `→` pendant le carton affiche directement la slide, sans la sauter.

## 3. La démo en direct (slide 11, ~2 min, Alexandre)

**Avant la soutenance**

- Unity 6000.6.0f1 ouvert sur `Assets/_Project/Scenes/Playground.unity`, vue Game réglée sur « Play Maximized » pour jouer en plein écran. L’entrée en Play est rapide (Enter Play Mode Options activé).
- Manette PS5 branchée en USB (les vibrations ne passent qu’en USB), testée une fois. Clavier en secours : ZQSD, Espace, Maj, Ctrl, souris.
- Faire une ride complète de répétition juste avant de passer, puis arrêter le Play.
- Diaporama en plein écran (`F`) sur le projecteur ; Unity prêt derrière, accessible en un `Cmd+Tab`.

**Pendant la démo** (le parcours affiché sur la slide 11)

1. `Cmd+Tab` vers Unity, Play.
2. Croix directionnelle haut (ou `N`) : appeler une vague tout de suite, sans attendre la série.
3. Ramer vers la plage (stick gauche, Croix pour sprinter) : take-off.
4. Pump en rythme (R2), puis se caler (L2) : le tube se referme au-dessus ; stick en avant et pump pour sortir.
5. Monter la face vite : envol, puis stick droit vers le bas et relance en diagonale : El Rollo.
6. Sortir par l’épaule (L2 vers l’épaule) : la vague reçoit sa note sur 10.
7. Triangle (ou `R`) pour revenir au line-up si une ride tourne mal ; recommencer à l’étape 2.
8. Quitter le Play, `Cmd+Tab` vers le diaporama, `→` : slide 12.

**Plan B** : si Unity ne répond pas, rester sur la slide 11 et appuyer sur `V` : la bande-annonce du jeu passe en plein écran (avec le son). Alexandre dit le même commentaire dessus. `V` à nouveau pour l’arrêter.

## 4. Le texte à dire

Repères : `/` courte respiration · `//` pause marquée · **gras** = mot à appuyer légèrement · [crochets] = indication de jeu, ne se dit pas. Le même texte s’affiche dans la vue présentateur (touche `P`).

### Ouverture · La question du projet

#### ⏱ 01 · WavyBoard · Alexandre · ~0:41

*À l’écran : La vidéo du jeu en plein écran, le titre et la question du projet.*

Bonjour à tous. //

Ce que vous voyez derrière nous n’est pas une vidéo de surf : / c’est **notre jeu**, WavyBoard. //

Et pourtant, au début du projet, / aucun de nous trois n’avait jamais utilisé Unity. / Je suis Alexandre, / voici Maxime et Louis. //

Notre question : / **que se passe-t-il quand on confie à des agents IA une technologie que l’on ne maîtrise pas soi-même ?** //

Nous allons vous raconter comment nous avons préparé le projet, / construit le jeu, / et ce que nous en retenons. / Et à la fin, je vous montrerai le jeu en direct.

### I · Préparation · Veille, spécification, harnais IA

#### ⏱ 02 · La veille et la spécification · Alexandre · ~0:51

*À l’écran : La veille (six domaines, sources vérifiées) et la spécification (708 lignes, 9 phases, le prompt).*

Notre première règle : / pas une ligne de code avant une vraie **veille**. //

Nous l’avons confiée au modèle le plus réfléchi, Claude Fable 5.1, en effort maximal, / parce que c’est au début qu’une erreur coûte le plus cher. / Chaque information était soit vérifiée à la source, / soit testée sur notre machine. / Et le premier constat a été clair : / aucun océan existant ne sait faire un **tube**. / Il fallait donc créer notre propre vague. //

Cette veille a abouti à une spécification de sept cents lignes, découpée en neuf phases. / Elle devait **se suffire à elle-même** : / nous avons ensuite ouvert une session vierge, / et un nouvel agent a écrit le code avec ce document pour seule mémoire.

#### ⏱ 03 · Le harnais Claude Code · Alexandre · ~0:41

*À l’écran : Le harnais : la spécification, les règles, le guide, le rôle des humains, Git, la mémoire. Notre rôle, et un modèle par étape.*

Autour des agents, nous avons construit un cadre : / le **harnais**. //

Un fichier de règles, quatorze lignes seulement, que chaque agent lit en démarrant. / Un guide pour piloter l’éditeur Unity. / Et Git comme mémoire commune, / avec le nom du modèle sur chaque commit. //

Et nous, dans tout ça ? / Nous étions directeurs artistiques, testeurs manette en main, / et **arbitres**. / Par exemple, nous écrivions : / « je suis aspiré vers le fond du rouleau ». / L’agent traduisait ce ressenti en chiffres, / puis en correction testée. //

Sur le papier, tout était prêt. / Mais Unity nous réservait quelques surprises.

*→ Regard vers Maxime, qui s’avance.*

### II · Construction · Six versions en trois semaines

#### ⏱ 04 · L’IA pilote Unity · Maxime · ~0:40

*À l’écran : Le terminal tapé en direct, la capture prise par l’agent, et les cinq outils de vérification.*

Unity est un logiciel lourd, / qui garde beaucoup d’informations en dehors du code. / L’agent n’y clique **jamais** : / il lui envoie des commandes. / Il peut exécuter du code dans l’éditeur, / lancer les tests, / et surtout prendre des captures d’écran : / ce sont **ses yeux**. / Il en a pris plus de sept cents. //

C’est notre plus grande leçon : / une IA avance aussi vite qu’elle peut **vérifier son travail**. / Au début, il fallait lancer le jeu à chaque fois. / À la fin, un robot jouait une vague entière / en une **fraction de seconde**.

#### ⏱ 05 · Six versions de la vague · Maxime · ~1:03

*À l’écran : Du 8 au 29 septembre : les six versions de la vague et le modèle qui les a écrites. Chaque flèche agrandit la version suivante, la flèche après ⑥ revient à la vue d’ensemble.*

Le 8 septembre, en un après-midi, l’agent livre un premier **prototype jouable**. / Pendant ce temps, Alexandre intègre nos planches de bodyboard. / Mais au total, nous avons construit **six** versions de la vague, / et abandonné les cinq premières. //

[→ ①] La première était invisible. / [→ ②] La deuxième, transparente : on voyait le sable à travers l’eau. / [→ ③] Et la troisième a été cassée par une scène mal sauvegardée. //

[→ ④] La nuit suivante, nous laissons l’agent travailler seul : / treize commits pendant que nous dormons, / et quatre-vingt-dix réglages empilés. / [→ ⑤] La version d’après était propre et testée, / mais elle ressemblait à « un mur avec un parasol au-dessus ». / Deux fois, nous avons préféré repartir de zéro. //

[→ ⑥] Le 28 septembre, tout se débloque : / un modèle plus performant, Opus 5.5, / un redémarrage sur une base saine, / des tests enfin rapides, / et deux photos de Teahupoo comme cible. / Quatre heures plus tard, / la vague est là. [→ vue d’ensemble]

*→ Louis prend la parole.*

### III · Le jeu · La vague, l’architecture, les technologies

#### ⏱ 06 · La vague · Louis · ~0:41

*À l’écran : La coupe de la vague du jeu, calculée en direct : ses quinze points, ses étapes, et le déroulement le long de la crête.*

Dans ce jeu, le personnage principal, / c’est **la vague**. //

Vue en coupe, elle tient en une seule courbe, / qui passe par quinze points de contrôle. / Ces points se déplacent entre dix formes clés, dessinées d’après les photos : / vous la voyez ici naître, se creuser, puis déferler. //

Pour que le rouleau avance le long de la vague, / chaque point de la crête suit la même évolution, / avec un léger **décalage dans le temps**. //

Enfin, le même calcul sert à l’affichage et à la physique : / le joueur glisse exactement sur l’eau qu’il voit.

#### ⏱ 07 · L’architecture · Louis · ~0:33

*À l’écran : Les trois temps du jeu : les entrées, la simulation cinquante fois par seconde, l’affichage.*

Le jeu se lit en trois temps. //

D’abord les entrées : / la manette, le clavier, / ou notre robot de test, / qui passe par **les mêmes commandes** qu’un joueur. //

Ensuite la simulation, cinquante fois par seconde : / le personnage, ses figures, / et l’eau, qui répond à une seule question : / « où est la surface à cet endroit ? » //

Enfin l’affichage : / une caméra automatique, qui ne passe jamais sous l’eau, / et un seul shader pour toute la mer.

#### ⏱ 08 · Les technologies · Louis · ~0:29

*À l’écran : Les technologies par domaine, l’organisation du dépôt et le site en ligne.*

Côté technologies : / Unity 6.6 et son moteur de rendu URP, / près de neuf mille lignes de C#, / accélérées par Burst pour recalculer la vague à chaque image, / un shader en HLSL, / et Python pour nos outils de test. //

Le site du projet est en React et three.js, / et GitHub Actions le met en ligne automatiquement. //

Maxime va maintenant vous parler des limites.

*→ Maxime reprend la parole.*

### IV · Le bilan · Limites et leçons

#### ⏱ 09 · Les limites · Maxime · ~0:35

*À l’écran : Quatre limites : la finition, le ressenti, l’accumulation, et Unity face au web.*

L’IA produit un prototype très vite. / Mais la finition prend beaucoup plus de temps : / elle voit une image, / mais elle ne ressent pas le jeu entre ses mains. //

Elle accumule, aussi : / lors du grand ménage final, nous avons supprimé plus d’**un million** de lignes, / alors que notre propre code n’a presque pas bougé. //

Enfin, Unity reste un terrain difficile pour une IA : / la vague de notre site web a demandé une soirée, / celle du jeu, **trois semaines**.

#### ⏱ 10 · Ce que nous referions · Alexandre · Maxime · Louis · ~0:27

*À l’écran : Les six leçons, deux par personne.*

**Alexandre** : Si c’était à refaire, nous garderions six choses. / Planifier avant de coder, avec le meilleur modèle. / Et écrire les règles une bonne fois pour toutes.

**Maxime** : Donner à l’IA des yeux, puis un simulateur. / Et ne pas hésiter à repartir de zéro.

**Louis** : Montrer des images plutôt que des adjectifs. / Et faire le ménage régulièrement. // Et maintenant, place au jeu. / Alexandre ?

### En direct · La démonstration

#### ⏱ 11 · Démonstration · Alexandre · ~2:00

*À l’écran : « En direct » : le programme de la démonstration, ce qu’Alexandre va montrer, manette en main.*

[Alexandre passe sur Unity, la scène prête, manette en main.]

Je suis au large, j’attends une vague. / Normalement, une série arrive toutes les quarante-deux secondes, / mais je peux en appeler une tout de suite. //

Je rame vers la plage… / la vague me soulève, / et c’est parti. //

Ma vitesse vient de la vague elle-même. / J’accélère en pompant, en rythme. //

Maintenant, je freine : / le rouleau me rattrape et se referme au-dessus de moi. / Je suis dans le **tube**. / J’accélère pour en sortir. //

Je remonte la vague à toute vitesse… / et la crête me projette en l’air. / Stick droit vers le bas, puis vers le haut : / un El Rollo. //

Je sors de la vague par le côté, / et j’obtiens une note sur dix. //

[Retour au diaporama : slide suivante.]

*Plan B : Si Unity ne répond pas : rester sur la slide, appuyer sur V pour lancer la bande-annonce en plein écran, et dire le même commentaire dessus.*

### Conclusion · La morale et la suite

#### ⏱ 12 · Conclusion · Louis · ~0:35

*À l’écran : La phrase finale, merci, la suite, le QR code vers le site.*

Quand nous avons commencé, aucun de nous ne connaissait Unity. / Les agents ont écrit le code. / Mais les décisions, les références et le goût / sont restés **les nôtres**. //

Une IA ne remplace pas la compréhension. // Elle la rend plus **urgente**. //

Et la suite ? / Enrichir le gameplay, améliorer les assets, / et sortir le jeu sur **Steam**. //

Merci pour votre écoute. / Le rapport complet est en ligne, le lien est à l’écran. / Nous sommes prêts pour vos questions.

Durée estimée : ~9:16, démo comprise.

## 5. Utiliser le diaporama

- Ouvrir `Presentation/WavyBoard-soutenance.html` dans Chrome. Tout est local (polices, vidéos, images dans `Presentation/assets/`) : ne pas séparer le fichier de ce dossier.
- `F` plein écran · `→` ou `Espace` slide suivante · `←` précédente · `B` écran noir · `H` aide · `V` vidéo de secours de la démo (slide 11).
- **Vue présentateur** : projecteur en écran étendu, diaporama sur le projecteur, puis `P`. Une fenêtre s’ouvre sur l’ordinateur avec le texte, l’orateur, la slide suivante et le minuteur (écoulé, prévu, temps sur la slide). Il démarre seul au passage à la slide 02 ; `T` le remet à zéro. Autoriser les pop-ups dans Chrome.
- `N` affiche le texte sous la slide, pour répéter sur un seul écran. `E` rend les textes modifiables, `Ctrl S` enregistre une copie à côté du dossier `assets/`.
- `…/WavyBoard-soutenance.html#9` ouvre directement la slide 9.
- Le site apparaît sur la slide 08 (déploiement GitHub Actions) et sur la dernière, avec un QR code que le jury peut scanner pendant les questions.

## 6. Si le jury pose ces questions

- **« Quelle part du code avez-vous écrite vous-mêmes ? »** (Louis) Presque rien directement : 56 des 58 commits sont co-signés par un modèle Claude. Notre travail : cadrer (spec, règles), fournir les références, jouer, juger, décider quand repartir de zéro. Nous avons appris Unity en lisant les commits et en comprenant chaque échec.
- **« Comment vous êtes-vous réparti le travail ? »** (chacun pour soi) Alexandre : les planches DGZ et l’intégration 3D. Maxime : le prototype, le HUD, le réglage de la vague. Louis : la vague Teahupoo, le gameplay, la caméra. La veille, la spec et les tests manette en main étaient communs.
- **« Pourquoi ne pas avoir pris un océan existant ? »** (Alexandre) URP n’a pas de système d’eau et aucun océan disponible ne sait faire un rouleau qui se referme. Storm Breakers a servi au début, puis il est parti au grand ménage.
- **« Comment savez-vous que le jeu marche sans y jouer ? »** (Maxime) 22 tests automatiques, 49 cas, qui rident des vagues entières avec un bot branché sur les mêmes commandes qu’un joueur, sans lancer le jeu (RideSim, 50 Hz). `playtest.py` vérifie la vraie partie : caméra, HUD, fluidité. Et nous jouons, parce qu’un test ne dit pas si c’est amusant.
- **« Qu’est-ce qui a coûté le plus cher ? »** (Maxime) La vague : six versions. Et les pièges propres à Unity : une erreur de compilation met l’éditeur en mode sans échec (le CLI ne répond plus), le jeu s’arrête quand l’éditeur perd le focus, un job Burst échoue en silence et tourne 25 fois plus lentement, des valeurs de jeu finissent dans les matériaux, donc dans git.
- **« Est-ce Opus 5.5 qui a tout débloqué ? »** (Louis) Le 28 septembre combine trois changements : un meilleur modèle, une base saine, des tests rapides. Git ne permet pas de séparer leur part. Sans Opus 5.5, pas de journée du 28 ; sans les deux premières semaines d’erreurs, pas non plus.
- **« Et la suite ? »** (Alexandre) Plus de gameplay, de meilleurs assets, un menu, l’anglais, d’autres spots, et une sortie sur Steam. Aujourd’hui le jeu se lance dans l’éditeur Unity, à la manette PS5 ou au clavier et à la souris.
- **« Que feriez-vous différemment ? »** (Louis) Construire la boucle de vérification rapide dès la première semaine, et viser des photos de référence dès le premier jour : la v4, réglée par des chiffres, n’a jamais vraiment été jouée.

## 7. Répétition

- Deux répétitions complètes chronométrées, démo comprise, avec la vue présentateur. Cible : 8 min 30, jamais plus de 9 min 30.
- La démo est le seul moment élastique : si elle dépasse 2 min 30, Alexandre coupe après l’El Rollo et revient au diaporama.
- Si la parole déborde, couper d’abord : « Pendant ce temps, Alexandre branche nos planches » (slide 05), puis « Le site du projet est en React et three.js… » (slide 08).
- Ne jamais lire la slide : elle montre les chiffres, la voix raconte.
