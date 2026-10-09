# Uebergabe an das Cloud-Projekt TerraLoom

Aktueller Stand 09.10.2026: lokale Testversion v0.2. Siehe USER-GUIDE.md und INTEGRATION.md. Die folgenden Cloud-Angaben sind historische Arbeitsgrundlagen.


## Startauftrag
Zuerst Flusskorridor, Wasserhoehen, Bett/Ufer, Schutzflaechen und Querungskandidaten mit Paths abstimmen. Dann einen minimalen Flussplaner bauen. Keine eigene Kopie des Core-Hoehen-/Regionsvertrags.
Zunaechst Grundlagen/Vertraege und einen gemeinsamen kleinen Nachweis erarbeiten,
keine vollstaendige Epos-Extraktion und keine Marketing-Reife behaupten.
Die verbindliche Entscheidungssammlung liegt im [Core-Konzept](https://github.com/clemensgoering/terraloom-core/blob/1d1f3036d2eea673490762171438c527265e219c/Docs/CONCEPT.md); Epos-Herkunft dort in Docs/EPOS-TRANSFER.md. Core ist im Projektmanifest auf `1d1f3036d2eea673490762171438c527265e219c` gepinnt.

## Verbindliche Entscheidungen aus dem lokalen Chat
- Kostenloses Core = Hauptprojekt + Foundation; kein separates Pflicht-Foundation-Produkt.
- Optionales eigenes Terrain; alle Module auch auf fremdem Terrain nutzbar.
- Unterschiedliche Landschaftsbereiche auch ohne Biome auswerten.
- Manuelle UND dynamische/seedgenerierte Ziele gleichwertig unterstuetzen.
- Paths und Rivers parallel; gemeinsames Muster fuer spaetere Module entwickeln.
- Vorschau, Runtime, Neubau und Wiederverwendung teilen Regeln, Planer und Erzeuger.
- Eigene Texturen/Gras/Steine/Baeume fuer vollstaendige Beispiele und Marketing aufbauen.
- Learnings beidseitig mit Epos austauschen; dortige laufende Welt-/Bausatzarbeit respektieren.

## Kontext und Zugriff
Diese Datei ist der eigenstaendige Kontext. Zugriff auf den lokalen Chat oder andere
Chats wird nicht vorausgesetzt. Die Produktliste dieser Sitzung zeigte ein lokales
TerraLoom-Projekt; das benannte Cloud-Projekt und seine Repository-Rechte wurden
nicht bestaetigt. Die drei Git-Repositories dem gewuenschten Cloud-Projekt zuordnen.
Ein Chatverweis kann ergaenzen, ersetzt diese Uebergabe aber nicht.
Lokale Epos-Pfade sind aus der Cloud nicht erreichbar; relevante Quellen gezielt
bereitstellen, ohne Kaufassets, Tokens oder ganze Dokumentlandschaften zu kopieren.

## Pruefungen
`python Tools/verify.py` braucht nur Python 3 und prueft Metadaten/Struktur.
`python Tools/verify.py --compile` braucht ein .NET SDK (netstandard2.1-Referenzen).
Bei Paths/Rivers wird die gepinnte Core-Version in `.artifacts` ausgecheckt;
dafuer sind GitHub-Leserechte noetig. Alternativ `--core-root /pfad/zum/gepinnten/core`.
Checkout/Compile im Cloud-Setup vorbereiten, wenn die Agentenphase kein Netzwerk hat.
CI der Module prueft zunaechst nur Struktur: ein Repository-Token kann bei privaten
Schwester-Repositories nicht automatisch deren Inhalte lesen.

Native Unity-Pruefung (lokal mit aktiviertem Unity 6000.3.9f1):
```text
Unity -batchmode -nographics -quit -projectPath <repo-root> -executeMethod TerraLoom.Core.Editor.ProjectValidation.Run -logFile <logfile>
```
Cloud-C#-Compile ist kein Unity-/PlayMode-/Bildnachweis. Noch kein Unity-Runner,
Lizenzsecret oder nicht gepruefter Unity-CI-Workflow konfiguriert. Spaetere Algorithmen
brauchen Regel-/Regressionstests sowie native und visuelle Pruefungen.

## Fertig fuer den naechsten Grundlagenblock
Kleiner gemeinsamer Planvertrag, eine manuelle und eine dynamische Zielquelle,
zwei unabhaengige Gelaendequellen, nachvollziehbarer Konflikt und kein stilles Weglassen.
API-Aenderungen in Core zentral vornehmen und Modul-Pins kontrolliert aktualisieren.
