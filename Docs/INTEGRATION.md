# Gemeinsames Testprojekt, 09.10.2026

## v0.3: zuerst die dynamische Welt testen

`Assets/TerraLoom/Integration/Generated/TerraLoomDynamic.unity` enthaelt nur
Konfiguration, Referenzen und Kamera/Licht. Play erzeugt die komplette Welt aus
dem Seed. Same seed wiederholt, Next seed baut Terrain, Ziele, Bereiche, Fluss,
Weg/Bruecke und Vegetation neu. Tab startet den Begehversuch am tatsaechlichen
seedabgeleiteten westlichen Ziel. UseSeedConfiguration=false verwendet dieselbe
Runtime-Pipeline mit manuell gepflegtem Core-Terrain, Zielen und Bereichen.

Tools > TerraLoom > World Workbench zeigt Core, regionale Dekoration und die
installierten Module mit gemeinsamen Scene-Ebenen/Legende. Die Consumer-Komposition
beziehungsweise Seed-Rezept-Karte ist der Einstieg fuer den gemeinsamen Neubau.
Module koennen auf dem Core-Objekt oder getrennten Objekten mit Core-Referenz liegen.

Vier Bereiche vergleichen separate Sommer-/Winter-Prefabs im Westen mit denselben
Prefabs und regionalen Shaderwerten im Osten. 14 originale Nature-Prefabs, zwei
Terrain-Layer und vier Wasserpaletten sind verfuegbar. Eis ist ein visueller Shaderstil.
Core Docs/REGIONAL-STYLES.md und EDITOR-EXTENSIONS.md beschreiben Anpassung und APIs.
Die statische Szene unten bleibt als Bake-/Reload-Nachweis erhalten.

Core, Paths und Rivers als Geschwisterordner anordnen. `python Rivers/Tools/create_integration.py`
erzeugt daneben das Unity-Projekt `Integration`. Es benutzt relative lokale UPM-Pakete;
die drei Produktpakete behalten eigenstaendige Abhaengigkeiten. Nur das Verbraucherbeispiel
referenziert Paths und Rivers gemeinsam. Unity 6000.3.9f1 / URP 17.3.0.

Im Integration-Projekt **Tools > TerraLoom > Integration > Build Test Scene** aufrufen.
Danach `Assets/TerraLoom/Integration/Generated/TerraLoomIntegration.unity` oeffnen.
Die gespeicherte Szene enthaelt Terrain, Originalterrain-Rueckstellreferenz, Flussbett,
Ufer, Wasser, Wegnetz, ausgehandelte Bruecke, Rampen und eigene Nature-Beispiele.
Play druecken; Tab startet den Begehversuch am westlichen Wegziel. WASD bewegt,
Maus schaut, Space springt, Esc/Tab kehrt zur Uebersicht zurueck.

Auf **Rivers Sample World** regelt `TerraLoomIntegration` den gemeinsamen Neuaufbau:
ein kopierter Core-Eingabestand, Rivers-Angebote, Core-Querungspruefung, Paths-Planung,
danach Geometrie. Beide Module bleiben unabhaengig. Individuelle Generate-Knoepfe
benutzen ihre eigenen Eingaben; fuer die gemeinsame Welt den Zusammensetzungs-Inspector
verwenden. Nach Neuaufbau beide Mesh-/Terrain-Bakes erneut ausfuehren und Szene speichern.

Der Planer verlangt fuer jede ueberlappende Wasserflaeche einen passenden Kandidaten.
Schutzflaechen werden dadurch niemals freigegeben. Erste automatische Querungen sind
achsenparallele Bruecken mit Terrainabdeckung aus Core-Rasterdaten; Rampen pruefen
Laengs-/Querneigung und passen ihre Landkante ans Terrain an. Boden/Rampen werfen
keinen Zusatzschatten; ausschliesslich das Deck wirft Brueckenschatten. Automatische
Furten, schräge freie Bruecken und globale Hydrologie sind keine Zusagen dieser Version.

Die Komposition plant beide Module vor dem Aufbau. Ein Planungsfehler oder vorab
abgebrochener Lauf behaelt den bisherigen Stand. Die Materialisierung ist pro Modul
atomar; ein spaeter Geometriefehler im zweiten Modul kann einen bereits erneuerten Fluss
zuruecklassen. Nach Behebung die gesamte Komposition erneut erzeugen. Es gibt keinen
allgemeinen gemeinsamen asynchronen Transaktionsmanager.

Native Pruefungen stehen in `IntegrationProject/Assets/TerraLoom/Integration/Tests`.
PlayMode prueft echte Bruecke, vertikale Colliderabdeckung, CharacterController-Traversierung,
Schattenrollen, unveraendertes Originalterrain, Cancel/Regeneration und Clear. EditMode
oeffnet die gespeicherte Szene erneut und prueft Asset-, Material-, UV- und Terrainbindungen.

Die eigenstaendigen Module verwenden veroeffentlichte Git-Pins. Falls Unity keinen
GitHub-Lesezugriff hat, aktiviert `Tools/UseLocalCore.ps1` voruebergehend den lokalen Core;
`-Restore` stellt Manifest/Lockfile wieder her. Das Integration-Projekt braucht diesen
Schritt nicht. Fuer Produkt-Audits die veroeffentlichten Pins wiederherstellen.
