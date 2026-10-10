# Gemeinsames Testprojekt, 09.10.2026

Fruehes visuelles Entwicklungsgate und erster begrenzter Material-/Playervergleich:
[V0-SURFACES.md](V0-SURFACES.md). Kein V0-/M1-Abschluss: Tragwerk, natuerliche
Ufer-/Wegkanten und gekruemmte gemeinsame Querungsfreigabe bleiben offen.

Gemeinsamer Ablauf und Invalidierung: Core `Docs/GENERATION-ORDER.md`.
Core-Schedule prueft Requires/Provides vor Mutation. Die Komposition zeigt die
abgeschlossenen Schritte und bietet eine explizite Freshness-Pruefung. Nach
Erdarbeits-Publikationsfehler bleiben keine veralteten Paths-Collider aktiv.
Vollstaendige Seed-Regeneration ueber die Rezeptkarte; einzelne Modulbuttons
innerhalb einer Komposition sind kein Ersatz fuer den gemeinsamen Aufbau.

Zusaetzlich `Generated/TerraLoomLandscape.unity` testen: gerichtetes Talrelief v2,
vier verbundene Wegabschnitte mit Abzweig und separate Habitatprofile. Kein
gebackenes Terrain; Play baut alles. Finale Kurvenplanung samt geprueften
Bruecken-Bodenanschluessen und versetzter Flussquelle ist aktiv.
Native Teststand: Core94+2, Paths47+2, Rivers20+1, Integration2+3 =171.
Rivers20+1 frisch in isolierter Projektkopie, Integration neu aufgebaut2+3;
Core/Paths aus dem vorigen Block.45portable Faelle frisch gruen.
Windows RiverRadiusPlayerBuild.log/RiverRadiusPlayerSmoke.log: Exit0 und
TERRALOOM_RUNTIME_SMOKE_SUCCESS, Seeds2042/2043/71 je1Fluss4Wege1Deck,
625/642/554regionale Instanzen. Keine neuen Bilder oder Bildfreigabe.
Core/Paths/Integration frisch fuer das Paths-v6-Radiusgate, Rivers aus dem
vorherigen Block (offener Editor). Windows-Belege: Core/Docs/LOCAL-VALIDATION.md.
Gemeinsame Radiusdiagnose zeigt jetzt auch innere Punkte in Endzonen mit
symmetrisch verkuerzten Messarmen; Messabdeckung/Skala stehen im Inspector.
Paths-v6 und Rivers-v4 erzwingen jetzt abgetastete Radiusgates.
Kein Zertifikat fuer externe Endpunktanschluesse oder kontinuierliche Kruemmung.
Begrenzte Paths-Kurvenreparatur und rote Suchspuren: Core/Docs/CURVE-REPAIR.md.
Lokale Ufer und gemeinsame Mesh-/Carverquerschnitte: Core/Docs/BANK-EARTHWORK.md.
Paths prueft vor Publish finale Deck-/Landungsmeshes unterschiedlicher Assemblies
auf widerspruchsfreie Ueberlappungen (1 cm); Details in Core/Docs/CROSSING-COMPATIBILITY.md.
Windows-Kurvenplayer prueft drei Seeds
mit je Fern-/Nah-/Spielerbild. Technischer Nachweis, Bildgestaltung weiterhin offen.

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


## Experimental water inset (2026-10-09)

Shared dynamic and landscape recipes explicitly use WaterInset=0. The expanded wet
bank protection of positive inset can invalidate existing crossing windows with
OutsideCandidate; do not reduce the protected wet footprint to force acceptance.
Standalone inset publication additionally checks actual staged terrain vertices and
triangle centres and retains the previous river on penetration. This is not yet a
supported all-seed combined inset workflow. See Core/Docs/INSET-WATER.md.

RuntimePlayerValidation.BuildRiverEvidenceBatch builds the standalone renderer.
-terraloomSmoke -terraloomWaterInset <metres> selects the transient comparison;
-terraloomScreenshot <absolute PNG path> writes overview, section and player images.
TERRALOOM_RIVER_EVIDENCE_SUCCESS means the runner completed, not visual acceptance.

## Terrain brush crossing fixture (2026-10-10)

`Tools > TerraLoom > Integration > Build Terrain Brush Crossing Scene (Experimental)`
creates `Generated/TerraLoomBrushCrossing.unity`: relief, habitat decoration and one
straight inset terrain channel with a negotiated road bridge. Its source terrain is
saved separately so rebuilding the legacy gallery cannot overwrite that source.
Bed/Bank materials and meshes are unnecessary; terrain supplies the physical channel.
The Rivers bake accepts absent optional materials in brush mode as well.

Composition captures both Paths reservations and Rivers authored protections into
one re-fingerprinted input. Hard Paths reservations receive the river raster support
guard in brush mode; Rivers protections already receive that guard in their adapter.
Reservation IDs must be unique across both inspectors. A protected source rejects
planning before terrain or geometry publication. Sediment layer/exposure edits now
invalidate composition freshness even when geometric plan JSON is unchanged.

The PlayMode crossing regression runs both legacy and brush cases, including collider
coverage, actual CharacterController traversal, protected-source rejection preserving
prior outputs, cancellation, regeneration and reverse Clear. A saved-scene regression
checks water-only meshes, persistent terrain ownership and original collider restoration.
Build `RuntimePlayerValidation.BuildBrushCrossingBatch`, then launch the Windows player
with `-terraloomSmoke -terraloomScreenshot <absolute PNG path>` for five rendered views.

**Still open:** the curved protected standalone river fails combined route planning
with `OutsideCandidate`: adjacent wet rectangles overlap a bridge footprint without
each local candidate containing the full deck. The straight fixture deliberately
isolates the supported case; it does not fix curved crossings or prove arbitrary seeds.
Do not shrink water/protection envelopes to force acceptance. Decoration in this fixture
uses conservative river exclusions and final path-segment envelopes; it is an example,
not a general runtime exclusion updater or shoreline vegetation model.

Validation for this block: 3/3 Integration EditMode and 4/4 PlayMode passed;
29 portable Core/Paths contracts plus 18 river planner checks passed, with all pure
assemblies compiled. Windows build and smoke succeeded. Five final
`brush-crossing-verified*.png` views were inspected: inset terrain channel and walkable
crossing are visible; vegetation is excluded along actual detours. Long height envelopes
currently favour a crossing near the source. Coarse repeating materials, primitive
assets and a thin horizon-line artifact in the river player view remain visual issues;
this is no finished-art approval. Discrete water probes: 6318 vertices minimum +5.0039mm,
2106 triangle centres minimum +80.2946mm. No continuous-clearance guarantee.

`IntegrationSampleBuilder.BuildBatch` also builds the separate brush fixture before
returning to the landscape scene, so a fresh generated test project has all three
saved-scene regression inputs. The added shared protection checks apply equally to
manual and seeded input capture; no all-seed curved-brush acceptance is claimed.

The protected curved-brush rejection is now a reproducible runtime regression with
exact window/request/input diagnostics. See [frozen baseline](CURVED-CROSSING-BASELINE.md)
and its JSON measurements. Latest targeted block: 5/5 Integration PlayMode and
29 portable contracts passed; preserving outputs before publication is not a
multi-module rollback proof. The collective crossing contract remains unimplemented.

10 October update: additive collective contract now exists in Core but is NOT
wired into Paths; curved M1 remains open. New narrow short-timber fixture and final
4Edit/6Play/Windows evidence: [V0 timber](V0-TIMBER.md). Physical ramps/supports do
not establish general area-rights or atomic composition rollback. Waterfall reach
requirements and planned first gate: [design draft](WATERFALL-DESIGN.md).

New early landscape request: [runtime waterfall prototype](WATERFALL-PROTOTYPE.md)
provides an isolated reproducible scene and three1.7m player views plus overview.
It is an explicit recipe, not general Rivers waterfall/module acceptance.

10 October later update: Paths collective current-rights planning/publication is
wired, and a separate6..12m side-truss construction now has real Player evidence
at the frozen local9.511811m crossing. The original full road stays rejected at
both hillside targets; the local construction does not replace that connection.
See [frozen truss evidence and exact limits](FROZEN-TRUSS-CROSSING.md).
