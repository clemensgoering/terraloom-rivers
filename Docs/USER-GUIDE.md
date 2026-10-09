# Rivers: Editor und Sample

Lokale Uferplanung (`rivers-local-banks-v3`): sichtbare Bankpunkte folgen dem
erfassten Terrain, waehrend Querungen eine separate konservative Hoehenhuelle
verwenden. Wasser/Bett fallen stromabwaerts; lokale Ufer duerfen steigen.
Mesh und Terraincarver teilen exakt geplante Querschnitte und Dreiecksdiagonalen.
Carving senkt nur abgedeckte Rasterpunkte, maximal 2*Depth+2cm, ohne neue Endkappen
oder Aufschuettung. JSON Format2 speichert die lokalen Ufer fuer die Scene-Vorschau;
alte Rezepte explizit neu generieren. Braune Footprint-Linien zeigen die echten
Bankseams. Details und offene Raster-/Dichtungsgrenzen: Core/Docs/BANK-EARTHWORK.md.

Der Planer rundet die Suchroute vor Bett-/Ufer-/Wasserplanung tangential und
validiert den ganzen finalen Korridor erneut, einschliesslich Abflussgefaelle.
Unmoegliche Kurven werden diagnostiziert statt als rohe Rasterkette publiziert.
Gerade Taeler erhalten keine kuenstlichen Maeander. Inspector/Workbench zeigt
Laenge, Punktzahl, Steigung und Richtungswechsel der finalen Wasserlinie.

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
