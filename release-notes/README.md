# Notes de version

Chaque tag de release `vX.Y.Z` (ou `vX.Y.Z-beta.N`) doit avoir son fichier `release-notes/vX.Y.Z.md` dans le commit
tagué. Le workflow `release.yml` refuse le tag si le fichier manque ou est vide, et place son contenu en tête des notes
de la release GitHub, avant l'installation et les SHA-256.

Contenu : un résumé lisible des nouveautés pour les joueurs, en Markdown (titres `##`, listes), en français. Pseudos
fictifs uniquement (Templar1, Chanter1…).
