# TerraLoom Rivers
Unity 6000.3.9f1 / URP 17.3.0. Initiales Entwicklungsgeruest, Stand 08.10.2026.
**Noch keine Generierungsalgorithmen und keine eigenen Nature-Kunstassets enthalten.**

## Einstieg
1. Dieses Repository als Unity-Projekt oeffnen: Assets, Packages und ProjectSettings liegen im Root.
2. [Cloud-Uebergabe](Docs/CLOUD-HANDOFF.md) und [Konzept](https://github.com/clemensgoering/terraloom-core/blob/987abb096961c2d2b39fdae951e02898c439a697/Docs/CONCEPT.md) lesen.
3. `python Tools/verify.py` prueft Struktur; `python Tools/verify.py --compile` auch reines C# (.NET SDK).
4. Unity: Tools > TerraLoom > Validate Project Settings oder Batch-ExecuteMethod aus der Uebergabe.

## Struktur
- `Packages/com.terraloom.rivers`: portable Runtime-/Editor-Assemblies.
- `Assets/TerraLoom`: eigene kuenftige Beispiele/Inhalte; `Assets/Scenes/Bootstrap.unity`: URP-Startszene.
- `Assets/Settings`: Desktop-URP (HDR, 4x MSAA, Tiefen-/Opaque-Textur) und mobiles URP-Profil.
- `Docs`: Konzept/Arbeitsuebergabe; `Tools`: cloudfaehiger Audit und Runtime-Compile.

Core ist das kostenlose Hauptprodukt inklusive Foundation und optionalem Terraingenerator.
Paths/Rivers kooperieren ueber Core, ohne dessen Terraingenerator vorauszusetzen.
Das Entwicklungs-UPM-Paketmodell ist noch keine Asset-Store-Verpackungsentscheidung.
`.bootstrap-original` ist eine lokale, unversionierte Sicherung der urspruenglichen Vorlage.
