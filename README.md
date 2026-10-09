# Aion2Assistant

Overlay de jeu pour **AION 2** : timers de boss, meters de combat et historique, affichés par-dessus le jeu sans rien modifier dans celui-ci.

> **Statut : bêta publique `0.3.0-beta.1`.** L'app est fonctionnelle mais encore jeune. Des bugs sont possibles, les retours sont les bienvenus.

Site du projet : <https://nathantek.github.io/aion2assistant/>
Téléchargements : [page des Releases](https://github.com/nathantek/aion2assistant/releases)

## Fonctionnalités

- **Timers** : suivi des temps d'apparition, avec alertes.
- **Boss de terrain** : suivi dédié des boss de terrain.
- **Meters** : DPS, soins et dégâts reçus, par joueur.
- **Boss seul / Tout** : bascule entre les dégâts faits au boss uniquement et l'ensemble du combat.
- **Courbe** : évolution du DPS au fil du combat.
- **Copie texte et image** : copie d'un résumé de combat en texte ou en image.
- **Historique** : consultation des combats passés.
- **Records** : meilleurs résultats conservés.
- **Entraînement** : mode pour s'exercer sur un mannequin.
- **Thèmes** : plusieurs thèmes visuels au choix.

## Raccourcis clavier

Globaux (actifs même quand le jeu a le focus), modifiables dans Réglages › Raccourcis. Verr Num doit être activé.

| Raccourci | Action |
|---|---|
| Ctrl+Alt+Num1 | Onglet DPS |
| Ctrl+Alt+Num2 | Onglet Dégâts reçus |
| Ctrl+Alt+Num3 | Onglet Soins |
| Ctrl+Alt+Num4 | Onglet Historique |
| Ctrl+Alt+Num5 | Onglet Timers |
| Ctrl+Alt+Num/ | Onglet précédent |
| Ctrl+Alt+Num* | Onglet suivant |
| Ctrl+Alt+Num0 | Masquer / afficher |
| Ctrl+Alt+Num7 | Ancrer / libérer |
| Ctrl+Alt+Num8 | Réduire / agrandir |
| Ctrl+Alt+Num9 | Réinitialiser le combat |
| Ctrl+Alt+Num+ | Agrandir l'interface |
| Ctrl+Alt+Num- | Rétrécir l'interface |

Depuis la 0.3.1, les onglets suivent l'ordre DPS, Reçus, Soins, Historique, Timers. Un raccourci d'onglet resté à son ancienne valeur par défaut est mis à jour automatiquement ; un raccourci personnalisé est conservé.

## Installation (courte)

1. Installe [Npcap](https://npcap.com) (nécessaire pour lire le trafic réseau du jeu) et **laisse décochée** l'option « Restrict Npcap driver's access to Administrators only ».
2. Télécharge le dernier zip `Aion2Assistant-<version>-win-x64.zip` depuis la [page des Releases](https://github.com/nathantek/aion2assistant/releases).
3. Décompresse-le dans un dossier de ton choix.
4. Lance `Aion2Assistant.exe`.

Le détail (prérequis, dépannage) est sur le [site du projet](https://nathantek.github.io/aion2assistant/).

## Compiler depuis les sources

Prérequis : Windows, SDK .NET 8.

```
dotnet build Aion2Assistant.sln -c Release
```

L'application WPF se trouve dans `src/Aion2Assistant.App`.

## Non-affiliation

Aion2Assistant est un projet de fan indépendant. Il n'est **pas affilié** à NCSOFT, ni approuvé ou soutenu par elle. AION et AION 2 sont des marques de NCSOFT. L'app est fournie sans garantie ; son usage est à tes risques.

## Licence

Le code est publié sous licence **GPL-3.0** (voir le fichier `LICENSE`).

Les données de jeu (textes, noms de compétences et de monstres) ne sont **pas** couvertes par la GPL : voir [data/NOTICE.md](data/NOTICE.md).


Support me: https://ko-fi.com/nathantek
