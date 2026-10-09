# Rivers: Editor und Sample

Im bisherigen Mesh-Modus sind Water, Bed und Banks gerenderte Meshes, Bed/Banks auch Collider. Sie
verschwinden nach Carving nicht. CarveTerrainCopy senkt eine eigene Terrainkopie
unter die geplanten Faces; Clear stellt die Quelle wieder her. Wasser liegt bei WaterInset=0 auf erfasster Mittenterrainhoehe+2cm; Depth senkt nur
das Bett. WaterInset>0 senkt Wasser zusaetzlich ab und erweitert die Wasserflaeche
bis zu den abgetasteten Boeschungsschnitten. Das ist experimentell: vor Publish
pruefen Vertices und Dreieckszentren das wirklich geschnittene Unity-Terrain.
Die Wasserflaeche wird zuvor gegen das echte Terrain beschnitten, ohne den
Wasserstand zu aendern oder zusaetzliche Erdarbeit auszufuehren. Unaufgeloeste
Konflikte verwerfen den Aufbau und erhalten den vorherigen Fluss. Die gekruemmte
Standalone-Demo funktioniert jetzt bei0.3m und0.6m; gemeinsame Querungen bleiben offen.
Standard und gemeinsame Demos bleiben bei0. Details: Core/Docs/INSET-WATER.md.
Die einfache geneigte Demo zeigt weiterhin keine landschaftliche Produktqualitaet.

Rivers-v5 erzwingt einen abgetasteten Mindestradius auf finalen Terrain-Samples
inkl. innerer Endzonen. MinimumBendRadius0=max(0.25m,Breite/2+BankWidth), positiv
= Meter. Messarm=max(1m,Breite/2+BankWidth), nahe Enden symmetrisch gekuerzt.
Fuenf bestehende Filletvorschlaege muessen Radius, Terrain, Downhill und Ufer-
pruefung bestehen. Kein neuer Suchretry und keine kontinuierliche Garantie.
Profil-v3/neue Algorithmusversion: alte Plaene aus dem Rezept neu generieren.

Lokale Uferplanung (`rivers-local-banks-v3`): sichtbare Bankpunkte folgen dem
erfassten Terrain, waehrend Querungen eine separate konservative Hoehenhuelle
verwenden. Wasser/Bett fallen stromabwaerts; lokale Ufer duerfen steigen.
Mesh und Terraincarver teilen exakt geplante Querschnitte und Dreiecksdiagonalen.
Carving senkt nur abgedeckte Rasterpunkte, maximal 2*Depth+WaterInset+2cm, ohne neue Endkappen
oder Aufschuettung. JSON Format2 speichert die lokalen Ufer fuer die Scene-Vorschau;
alte Rezepte explizit neu generieren. Braune Footprint-Linien zeigen die echten
Bankseams. Details und offene Raster-/Dichtungsgrenzen: Core/Docs/BANK-EARTHWORK.md.

Der Planer rundet die Suchroute vor Bett-/Ufer-/Wasserplanung tangential und
validiert den ganzen finalen Korridor erneut, einschliesslich Abflussgefaelle.
Unmoegliche Kurven werden diagnostiziert statt als rohe Rasterkette publiziert.
Gerade Taeler erhalten keine kuenstlichen Maeander. Inspector/Workbench zeigt
Laenge, Punktzahl, Steigung und Richtungswechsel der finalen Wasserlinie.

Zusaetzlich zeigt der Inspector den kleinsten abgetasteten XZ-Kurvenradius mit
festen 1-m-Messarmen entlang der finalen Wasserlinie. Messpunktzahl und Skala
sind sichtbar; Endbereiche ohne volle Arme bleiben ausgeschlossen. Dies ist
eine reine Diagnose ohne Terrainaufnahme/Neuplanung und keine kontinuierliche
Kruemmungs- oder Uferfreigabe. Die Runtime-Messung liegt gemeinsam in Core.

Ein `TerraLoomWorld` mit zugewiesenem Unity Terrain und zwei unterschiedlichen Zielen genÃ¼gt. Der Core-Terraingenerator ist optional; fremdes Terrain und Landschaftsbereiche ohne Biome sind zulÃ¤ssig. Terrain benÃ¶tigt EinheitsmaÃŸstab und unverÃ¤nderte Weltrotation. Im `TerraLoomRivers`-Inspector World sowie eigene Water-, Bed- und Bank-Materialien zuweisen. Automatische Quell-/MÃ¼ndungswahl nutzt Core-Ziele; alternativ `Automatic Source And Mouth` ausschalten und Verbindungen Ã¼ber stabile Ziel-IDs eintragen.

`Generate` verwendet den Runtime-Planer. `Carve Terrain Copy` erzeugt eine TerrainData-Kopie und verbindet Terrain und TerrainCollider damit; das Originalasset bleibt unverÃ¤ndert. Wasser liegt bei GelÃ¤nde + 0,02 m, das Bett bei Wasser âˆ’ Depth. `Clear` entfernt die erzeugte Geometrie und stellt das Original wieder her. Ohne Copy-Carving bleibt das GelÃ¤nde unverÃ¤ndert: die Geometrie ist ein Overlay und kann vom GelÃ¤nde verdeckt werden. Wasser ist eine sichtbare OberflÃ¤che, keine StrÃ¶mungssimulation.

Width, Depth und Bank Width bestimmen den Querschnitt. Cell Size, Sample Spacing und Arbeitsbudgets begrenzen Suche und Abtastung. Maximum Slope begrenzt das GefÃ¤lle; bergauf verlaufende Wasserabschnitte sind unzulÃ¤ssig. Region Costs beeinflussen die Routenwahl, Protected Areas sperren Bereiche. Diagnosemeldungen im Inspector beachten: Ziele und der gesamte Korridor mÃ¼ssen innerhalb der Terrainbounds liegen; fehlende Routen, unzulÃ¤ssige Ziele oder ausgeschÃ¶pfte Budgets nicht als erfolgreichen Plan behandeln.

`Save Plan JSON` schreibt UTF-8 ohne BOM. `Validate JSON against Current Input` prÃ¼ft ohne Geometrieneubau. `Load and Validate JSON` prÃ¼ft gegen das aktuelle GelÃ¤nde, Ziele und Profil und erzeugt die Geometrie erneut; JSON ist kein unabhÃ¤ngiger Terrain-/Materialexport. Ein geÃ¤ndertes Eingabeset kann die Wiederverwendung ablehnen.

`Bake Assets` in einen Ordner unter Assets speichert Terrainkopie, nÃ¶tigenfalls das transiente Original, Meshes und transiente Materialien/Texture2D. MeshCollider und TerrainCollider erhalten die gespeicherten Daten; Materialbindungen bleiben erhalten. Existierende Assets bleiben bestehen, neue interaktive Bake-Dateien bekommen eindeutige Pfade. Transiente Render-/Cubemap-Texturen und Vegetationsprefabs vorab selbst persistieren. Danach die als geÃ¤ndert markierte Szene speichern. Generate/Load erzeugen wieder eine Vorschau; nach Ã„nderungen erneut backen. Clear nach einem Bake lÃ¶scht die gespeicherten Assets nicht.

`Tools > TerraLoom > Rivers > Build Sample Scene` fragt nach dem Speichern offener Szenen. Das eigenstÃ¤ndige Beispiel landet in `Assets/TerraLoom/RiversSamples/Generated/RiversSample.unity`: 129Ã—129 HÃ¶henwerte, 96Ã—16Ã—96 m, eben in X, HÃ¶he `6 âˆ’ 0,03*z`, Quelle (48,8), MÃ¼ndung (48,88), explizite Verbindung. Originalterrain, Textur/Layer, eigene Bett-/Ufer-/Wassermaterialien, Bake und Plan werden mit Provenienz gespeichert. Wasser nutzt `TerraLoom/RiverWater`, falls vorhanden, sonst URP/Lit. Wiederholungen erhalten GUIDs eigener gekennzeichneter Sampleassets; unmarkierte bestehende Dateien fÃ¼hren zum Abbruch. Eigene Dateien auÃŸerhalb von Generated aufbewahren.

GPU-freier, offlinefÃ¤higer Batch-Einstieg (ersetzt die offene Szene):

```text
Unity -batchmode -nographics -quit -projectPath <Rivers-Projekt> -executeMethod TerraLoom.Rivers.Editor.RiversSampleBuilder.BuildBatch -logFile <Logdatei>
```

Der Builder rendert keine Bilder; native Import-/EditTests und visuelle Abnahme erfolgen separat. Diese erste Version plant begrenzte, abgetastete Flusskorridore; sie garantiert keine globale Hydrologie oder beliebig komplexe Flussnetze. Rivers publiziert Korridor-/Reservierungs-/Querungsangebote Ã¼ber Core fÃ¼r Paths; dadurch entsteht noch keine fertige BrÃ¼cke. Paths und Rivers importieren einander nicht. Vorhandene Wasser-/Querungsangebote sind fÃ¼r Verbraucher schreibgeschÃ¼tzte Planinformationen.


`Tools > TerraLoom > Rivers > Build Terrain Brush Sample Scene (Experimental)` baut die
gekrümmte Standalone-Szene jetzt mit TerrainBrush und WaterInset=0.6m. Es verwendet denselben eigenen
Generated-Ordner wie die Standarddemo, fragt nach ungespeicherten Szenen und ersetzt
nur eigene markierte Sampleassets. Batch: RiversSampleBuilder.BuildInsetBatch.
Es ist keine gemeinsame Brückendemo. Native Tests prüfen zusätzlich Seed42/43/71.

Neue Wasser-Meshes speichern Halbbreite in UV-Kanal2 (Unity UV3), unabhängig von
Across/Along in UV0. Clipping interpoliert beide Daten; dies verhindert falsche
Schaumbänder an neu eingefügten Innenvertices. Der Shader hat einen Fallback für
alte Zweispalten-Bakes. Schaum folgt derzeit dem geplanten Rand, nicht einer exakten
Distanz zur endgültig beschnittenen Uferlinie. Natürliche Ufermaterialien bleiben offen.


TerrainBrush ist ein experimenteller Terrain-Modus: nur Wasser als Mesh, Bett und
weich auslaufende Boeschungen im Terrain mit TerrainCollider. Bed/Bank-Materialien
sind nicht erforderlich. Positive WaterInset, BankWidth und CarveTerrainCopy sind
Pflicht. Das Terrain behaelt seine vorhandenen Layer; Materialmalerei folgt separat.
Profil-v4 enthaelt Modus und Rasterguard; alte Plaene neu generieren. Schutzflaechen
werden konservativ gegen angrenzende Rasterzellen geprueft; Fehler behalten die alte
Welt. Gemeinsame Brueckenfenster noch offen. Details: Core/Docs/TERRAIN-RIVER-BRUSH.md.
