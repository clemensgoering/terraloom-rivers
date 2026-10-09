# TerraLoom Rivers
Unity 6000.3.9f1 / URP 17.3.0. Version 0.3.0, Stand 09.10.2026.
Deterministische Flussplanung, reversible Terrainkopie, Bett/Ufer/Wassergeometrie,
Editor/Runtime und eigene Beispielszene. [Anleitung](Docs/USER-GUIDE.md),
[gemeinsames Testprojekt](Docs/INTEGRATION.md).

## Einstieg
1. Dieses Repository als Unity-Projekt oeffnen: Assets, Packages und ProjectSettings liegen im Root.
2. [Cloud-Uebergabe](Docs/CLOUD-HANDOFF.md) und [Konzept](https://github.com/clemensgoering/terraloom-core/blob/686c6342dc05ed8477c6b98c03795e31d8d0e70f/Docs/CONCEPT.md) lesen.
3. `python Tools/verify.py` prueft Struktur; `python Tools/verify.py --compile` auch reines C# (.NET SDK).
4. Unity: Tools > TerraLoom > Validate Project Settings oder Batch-ExecuteMethod aus der Uebergabe.

## Struktur
- `Packages/com.terraloom.rivers`: portable Runtime-/Editor-Assemblies.
- `Assets/TerraLoom`: eigene gespeicherte Beispiele/Inhalte; `Assets/Scenes/Bootstrap.unity`: URP-Startszene.
- `Assets/Settings`: Desktop-URP (HDR, 4x MSAA, Tiefen-/Opaque-Textur) und mobiles URP-Profil.
- `Docs`: Konzept/Arbeitsuebergabe; `Tools`: cloudfaehiger Audit und Runtime-Compile.

Core ist das kostenlose Hauptprodukt inklusive Foundation und optionalem Terraingenerator.
Paths/Rivers kooperieren ueber Core, ohne dessen Terraingenerator vorauszusetzen.
Das Entwicklungs-UPM-Paketmodell ist noch keine Asset-Store-Verpackungsentscheidung.
`.bootstrap-original` ist eine lokale, unversionierte Sicherung der urspruenglichen Vorlage.

## Ausbau v0.3

World Workbench, gemeinsame Scene-Legende und optionale Modulobjekte: Core `Docs/EDITOR-EXTENSIONS.md`.
Regionale Originalassets, Sommer/Winter durch Prefabs oder Shader und vollstaendige
Runtime-Seedwelt: Core `Docs/REGIONAL-STYLES.md`; Einstieg im gemeinsamen Projekt
`Integration/Assets/TerraLoom/Integration/Generated/TerraLoomDynamic.unity`.
Die Szene baut bei Play Terrain, Ziele, Regionen, Fluss, Wege, Bruecke und Vegetation.
