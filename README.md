# StarTrad fork launcher

Version legere de StarTrad pensee pour Circus Launcher.

Objectif :

- fonctionner comme outil autonome minimal ;
- exposer les memes actions que le launcher : detecter, installer, mettre a jour
  et desinstaller la traduction francaise de Star Citizen ;
- rester tres leger : un executable Windows C# WinForms, sans runtime Python ou
  Node.

La logique principale est volontairement proche de celle integree dans Circus
Launcher.

Fichiers produits :

- `dist/StarTrad.exe` : outil autonome.
- `dist/StarTrad_Setup_v1.0.0.exe` : installateur Inno Setup.
- `dist/startrad_launcher_version.json` et
  `dist/circus_launcher_startrad_version.json` : manifeste de version pour le
  launcher et la transition avec l'ancien packaging.
