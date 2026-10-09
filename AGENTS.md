# TerraLoom Rivers: Arbeitsgrundlage
Lies README.md und Docs/CLOUD-HANDOFF.md zuerst; weitere Quellen nur themenbezogen.
Die verbindliche Entscheidungssammlung liegt im [Core-Konzept](https://github.com/clemensgoering/terraloom-core/blob/c81edfdbadcc67bc9402c5e8fefa2adb4b169d91/Docs/CONCEPT.md); Epos-Herkunft dort in Docs/EPOS-TRANSFER.md. Core ist im Projektmanifest auf `c81edfdbadcc67bc9402c5e8fefa2adb4b169d91` gepinnt.
- Arbeite sparsam: kleine klare Aufgaben, gezielte Suchen, guenstige Subagenten bei Bedarf.
- Zentrale Algorithmen fuer Editorvorschau und Runtime; keine getrennten Kopien.
- Runtime-Planung ohne Unity/Editor-Abhaengigkeit; Adapter/Editor separat.
- Fremdterrain, Bereiche ohne Biome und dynamische Ziele sind Pflichtfaelle.
- Core besitzt gemeinsame Vertraege; Paths/Rivers importieren einander nicht.
- Keine Epos-Kaufassets, Zugangsdaten oder privaten Drittanbieterquellen kopieren.
- Eine Datei pro MonoBehaviour/ScriptableObject; GUIDs erhalten, neue Assets mit .meta.
- JSON UTF-8 ohne BOM; keine absoluten lokalen Pfade als Produktionsabhaengigkeit.
- `python Tools/verify.py`; bei Runtime-Aenderung auch `--compile`.
- Native Unity pruefen, wenn verfuegbar; fehlenden Runner/visuelle Abnahme offen nennen.
- Regressionen pruefen Verhalten; compile/struktureller Audit ist keine Produktreife.
- Kein Epos-Rueckfluss ohne separate lokale Pruefung und Beachtung seiner Arbeitsordnung.
- Epos-Ergebnisse nicht als TerraLoom-Testergebnisse ausgeben.
- Entwicklungslayout und spaetere Asset-Store-Verpackung getrennt beurteilen.
