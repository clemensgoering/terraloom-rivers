# Rivers: Editor und Sample

Ein `TerraLoomWorld` mit zugewiesenem Unity Terrain und zwei unterschiedlichen Zielen genügt. Der Core-Terraingenerator ist optional; fremdes Terrain und Landschaftsbereiche ohne Biome sind zulässig. Terrain benötigt Einheitsmaßstab und unveränderte Weltrotation. Im `TerraLoomRivers`-Inspector World sowie eigene Water-, Bed- und Bank-Materialien zuweisen. Automatische Quell-/Mündungswahl nutzt Core-Ziele; alternativ `Automatic Source And Mouth` ausschalten und Verbindungen über stabile Ziel-IDs eintragen.

`Generate` verwendet den Runtime-Planer. `Carve Terrain Copy` erzeugt eine TerrainData-Kopie und verbindet Terrain und TerrainCollider damit; das Originalasset bleibt unverändert. Wasser liegt bei Gelände + 0,02 m, das Bett bei Wasser − Depth. `Clear` entfernt die erzeugte Geometrie und stellt das Original wieder her. Ohne Copy-Carving bleibt das Gelände unverändert: die Geometrie ist ein Overlay und kann vom Gelände verdeckt werden. Wasser ist eine sichtbare Oberfläche, keine Strömungssimulation.

Width, Depth und Bank Width bestimmen den Querschnitt. Cell Size, Sample Spacing und Arbeitsbudgets begrenzen Suche und Abtastung. Maximum Slope begrenzt das Gefälle; bergauf verlaufende Wasserabschnitte sind unzulässig. Region Costs beeinflussen die Routenwahl, Protected Areas sperren Bereiche. Diagnosemeldungen im Inspector beachten: Ziele und der gesamte Korridor müssen innerhalb der Terrainbounds liegen; fehlende Routen, unzulässige Ziele oder ausgeschöpfte Budgets nicht als erfolgreichen Plan behandeln.

`Save Plan JSON` schreibt UTF-8 ohne BOM. `Validate JSON against Current Input` prüft ohne Geometrieneubau. `Load and Validate JSON` prüft gegen das aktuelle Gelände, Ziele und Profil und erzeugt die Geometrie erneut; JSON ist kein unabhängiger Terrain-/Materialexport. Ein geändertes Eingabeset kann die Wiederverwendung ablehnen.

`Bake Assets` in einen Ordner unter Assets speichert Terrainkopie, nötigenfalls das transiente Original, Meshes und transiente Materialien/Texture2D. MeshCollider und TerrainCollider erhalten die gespeicherten Daten; Materialbindungen bleiben erhalten. Existierende Assets bleiben bestehen, neue interaktive Bake-Dateien bekommen eindeutige Pfade. Transiente Render-/Cubemap-Texturen und Vegetationsprefabs vorab selbst persistieren. Danach die als geändert markierte Szene speichern. Generate/Load erzeugen wieder eine Vorschau; nach Änderungen erneut backen. Clear nach einem Bake löscht die gespeicherten Assets nicht.

`Tools > TerraLoom > Rivers > Build Sample Scene` fragt nach dem Speichern offener Szenen. Das eigenständige Beispiel landet in `Assets/TerraLoom/RiversSamples/Generated/RiversSample.unity`: 129×129 Höhenwerte, 96×16×96 m, eben in X, Höhe `6 − 0,03*z`, Quelle (48,8), Mündung (48,88), explizite Verbindung. Originalterrain, Textur/Layer, eigene Bett-/Ufer-/Wassermaterialien, Bake und Plan werden mit Provenienz gespeichert. Wasser nutzt `TerraLoom/RiverWater`, falls vorhanden, sonst URP/Lit. Wiederholungen erhalten GUIDs eigener gekennzeichneter Sampleassets; unmarkierte bestehende Dateien führen zum Abbruch. Eigene Dateien außerhalb von Generated aufbewahren.

GPU-freier, offlinefähiger Batch-Einstieg (ersetzt die offene Szene):

```text
Unity -batchmode -nographics -quit -projectPath <Rivers-Projekt> -executeMethod TerraLoom.Rivers.Editor.RiversSampleBuilder.BuildBatch -logFile <Logdatei>
```

Der Builder rendert keine Bilder; native Import-/EditTests und visuelle Abnahme erfolgen separat. Diese erste Version plant begrenzte, abgetastete Flusskorridore; sie garantiert keine globale Hydrologie oder beliebig komplexe Flussnetze. Rivers publiziert Korridor-/Reservierungs-/Querungsangebote über Core für Paths; dadurch entsteht noch keine fertige Brücke. Paths und Rivers importieren einander nicht. Vorhandene Wasser-/Querungsangebote sind für Verbraucher schreibgeschützte Planinformationen.
