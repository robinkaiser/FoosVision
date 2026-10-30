# FoosVision Training – Umsetzungsplan

Stand: 2026-10-30. Status: Arbeitspaket 1 implementiert und geprüft.
Weitere Arbeitspakete sind nicht umgesetzt.

## Ziel und Umfang

Training wird ein eigenständiger fachlicher Bereich neben Setup und Game, mit eigener
Session, eigenen Usecases und eigener Laufzeitsteuerung. Tracking, laufende
Tischaktualisierung und Replay-Technik werden gemeinsam genutzt. Training wird weder
ein Flag innerhalb von Game noch eine abgeleitete GameSession.

Erster nutzbarer Meilenstein: Nach erfolgreichem Tisch-Setup Training starten, eine
strategiespezifische Vorbereitung auf der Dreier- oder Fünferreihe erkennen, „Ready“
ansagen und nach einer zufälligen Wartezeit innerhalb konfigurierter Grenzen eine
passende Schuss- oder Passvariante ansagen. Auslösung und erneute
Bereitschaft werden ebenfalls strategiespezifisch erkannt. Konfiguration
erfolgt ausschließlich über die bestehende Recorder-Config.json. Eine minimale
Start-/Stop-Bedienung erfolgt über einen dritten Button im Viewer. Ansagen erfolgen
ausschließlich über den Lautsprecher des Recorders. Der Viewer ist an der Sprachausgabe
nicht beteiligt; er bedient Training und zeigt Status, Live-Bild und Replay.

Zweiter Meilenstein: Trainingsspezifische Zeitlupen mit eigenen Auslösern,
Zeitfenstern auf Basis der bestehenden Replay-Pipeline. Das Replay wiederholt sich,
bis die nächste Vorbereitung erkannt wird und „Ready“ ertönt; ein noch im Spiel
befindlicher Ball beendet die Trainings-Zeitlupe nicht.

Keine allgemeine Trainingsprogramm-Engine, keine neuen Projekte auf Vorrat und keine
vollständige Duplizierung der Game-Pipeline. Gemeinsamer Code bedeutet nicht gemeinsam
weiterverwendeten Session-Zustand: Eine neue Session erhält frische Instanzen.

## Ausgangslage im Repository vor Arbeitspaket 1

- `Domain/Training/Entities/TrainingSession` ist leer. `Training/Services/SequenceRecognizer`
  und passende Domain-Tests stammen aus einem früheren Prototypen zur Erkennung
  stationärer Auflagen auf Fünfer- und Dreierreihe. Modul und zugehörige alte Verträge
  und Tests werden entfernt; seine Heuristik ist keine Vorgabe für die neue Implementierung.
- `Domain/TrackingCore/Services/GameTracking/GameTracker` verbindet Tracking und
  Besitzdauer bereits mit Zeitfoul- und Replay-Entscheidungen. Der Ordnername allein
  garantiert deshalb keine modusneutrale Schnittstelle.
- `Domain/Game/Entities/GameSession` steuert zusätzlich die laufenden Tisch- und
  Szenenaktualisierungen. Die eigentlichen Kalibrierungs-Usecases liegen bereits
  gemeinsam unter `UseCases/Calibration`.
- Recorder-Adapter für Frame-Verarbeitung, Kalibrierung, Replay und Vision-Kontext
  liegen teilweise unter `Adapters.Recorder/Game`. Der ReplayCoordinator bestimmt
  die Clipdauer aktuell anhand des Game-Trigger-Typs über Konstanten.
- Der Viewer hat bereits gemeinsame Session-, Overlay-, Playback- und Replay-Bausteine.
  `ActiveSession`, `SessionUiStateCalculator` und UI-Code enthalten jedoch konkrete
  Setup-/Game-Annahmen und teilweise einen impliziten Game-Fallback.
- Recorder-Einstellungen werden beim Start geladen; Viewer-Einstellungen werden beim
  Handshake übertragen. Es gibt keine eigene Viewer-Config.json.
- `ProtocolVersions.Current` wird aus der Assembly-Major-Version abgeleitet.
  Für diese Training-Erweiterung bleiben Protokoll- und Major-Version gemäß
  Nutzerentscheidung unverändert, da FoosVision noch im Prototyp-Status ist.

Pfadangaben ohne Projektpräfix beziehen sich in diesem Plan auf die entsprechenden
bestehenden FoosVision-Projekte. Neue Typnamen sind Arbeitsnamen.

## Fachliche und technische Leitentscheidungen

- Der Recorder besitzt Trainingszustand, Versuch-ID, Variantenauswahl und Zeitentscheidung.
  Der Viewer präsentiert Ergebnisse und führt Videowiedergabe aus.
  Ansagen sind lokale Ausgaben am Recorder: Sein Adapter übergibt den Text an den
  lokalen Speech-Port. Keine Ansage-Protokollnachrichten und keine Rückkopplung der
  Sprachausgabe in die Trainingszustände. Ansagelatenz wird bewusst ignoriert.
- Von Beginn an ein Strategy-Pattern in der Training-Domain verwenden. Mehrere Strategien
  können per JSON gleichzeitig aktiviert und beliebig kombiniert werden, beispielsweise
  Tick-Tack Pass + Snake oder Snake + Pull. Jede Trainingsstrategie kapselt ihre Regeln für Vorbereitung,
  Ansage und Auslösung sowie Abbruch/erneute Bereitschaft. „Auflage“ setzt keine Ballruhe
  voraus: Bei Snake ist die vorbereitende Bewegung ausdrücklich Teil der Erkennung.
- TrainingSession besitzt die Instanzen aller aktivierten Strategien und den gemeinsamen
  Session-/Versuchs-Lifecycle. In der Vorbereitungsphase werden alle aktivierten Strategien
  ausgewertet. Die erkannte Strategie übernimmt den jeweiligen Versuch mit ihren Optionen.
  Jede Strategie besitzt ihre eigene Erkennungshistorie und fachlichen Übergangsregeln. Timer-
  Auswertung und Variantenauswahl dürfen kleine gemeinsame Hilfen nutzen; Adapter,
  Viewer und Sprachausgabe enthalten keine strategiespezifischen Fallunterscheidungen.
- Gemeinsames Tracking liefert Fakten: Ballposition, Bewegung, Konfidenz, Ballverlust,
  Ballbesitz und Besitzdauer. Game bewertet Zeitfouls und seine Replay-Auslöser;
  Training bewertet Auflage, Wartezeit, Versuch und Trainings-Replay.
- Fachliche Regeln liegen in Domain/UseCases. Adapter übersetzen und koordinieren
  technische Ausgaben. Composition verdrahtet; Apps bedienen und verwalten Plattform-Lifecycle.
- Frame-Puffer bleiben außerhalb des Core. Gemeinsame Ports verwenden weiterhin
  schmale Vision-Operationen und Frame-Handles bzw. Zeitstempel.
- Vorhandene Projekte bevorzugen. Neue Projektabhängigkeiten und neue Testprojekte
  müssen vor ihrer Einführung separat abgestimmt werden, wie in AGENTS.md gefordert.
- Schritte einzeln mit passenden Tests abschließen. Reine Extraktionen erhalten das
  bisherige Game-Verhalten einschließlich Replay-Timing und Kalibrierung.

## 0. Produktentscheidungen vor der jeweiligen Implementierung festlegen

Bestätigte Entscheidungen stehen im folgenden Abschnitt. Offen bleiben nur die
ausdrücklich als Vorschlag oder spätere Konkretisierung markierten Details.

### Festgelegte Strategien

| Strategie      | Vorbereitung erkennen                               | Spätere Ansage           | Auslösung erkennen                                       |
| -------------- | --------------------------------------------------- | ------------------------ | -------------------------------------------------------- |
| Snake          | Mittiges, stetes Pendeln auf der Dreierreihe        | Push, Pull, Middle       | Schussbewegung links/rechts oder weg von der Stange      |
| Zieher         | Ball ruht auf Höhe des spielerfernen Torpfostens    | Short, Middle, Long      | Schussbewegung mit Mindestweg und Mindestgeschwindigkeit |
| Tick-Tack Pass | Tick-Tack auf der Fünferreihe nahe einer Tischseite | Wall, Lane, Sling/Split  | Passauslösung; genaue Erkennungsregel noch festzulegen   |

Die Auslösung ist zunächst ein fachliches Ereignis mit Frame/Zeitstempel und Position,
kein Nachweis eines erfolgreichen Schusses oder einer korrekt ausgeführten Variante.
Es kann später den Trainings-Replay auslösen bzw. dessen Zeitfenster verankern.

### Bestätigte Entscheidungen

- **Bedienung und Ausgabe:** Dritter Start-/Stop-Button im Viewer; alle Trainingsoptionen
  per Recorder-JSON. Nur der Recorder spricht über seinen Lautsprecher.
- **Protokollversion:** Für diese Erweiterung weder Protokoll- noch Major-Version
  erhöhen. FoosVision ist weiterhin ein Prototyp; die Versionsentscheidung ist geklärt
  und muss in späteren Arbeitspaketen nicht erneut abgestimmt werden. Protokolländerungen
  trotzdem dokumentieren; eine unveränderte Versionsnummer garantiert keine Kompatibilität
  mit älteren Builds.
- **Beide Teams:** Training funktioniert gleichermaßen auf A3/B3 und für Passübungen
  auf A5/B5. Keine fest verdrahtete Trainingsseite. Pro Versuch die erkannte Reihe und
  Teamzuordnung festhalten; Besitzwechsel nicht als Fortsetzung desselben Versuchs behandeln.
- **Strategiekombinationen:** Eine Session unterstützt mehrere gleichzeitig aktivierte
  Strategien. Zwischen Versuchen kann die nächste Vorbereitung zu jeder aktivierten
  Strategie gehören, ohne JSON-Änderung oder Session-Neustart. Wartezeit und Varianten
  gehören weiterhin zur jeweils erkannten Strategie. Pro Versuch gibt es eine zuständige
  Strategie; kein gemeinsamer Timer und keine Vermischung ihrer Ansagen.
- **Vorbereitung:** Snake beginnt mit stetem Pendeln. Zieher verwendet einen ruhenden
  Ball auf Höhe des spielerfernen Torpfostens: Der Ball liegt vom Spieler weg; ein langer
  Zieher zieht ihn zur spielernahen Torseite. Die umgekehrte Richtung wäre ein Schieber.
  „Links“ ist damit keine feste Bildschirmrichtung. Dies für A/B aus Tisch-/Teamgeometrie
  ableiten; keinen mechanischen Anschlag oder zusätzliche Figurenerkennung voraussetzen.
  Tick-Tack Pass startet nahe einer der beiden Tischseiten; beispielsweise bestätigt
  der zweite Richtungswechsel die Vorbereitung. Exakte Bereiche und Schwellen werden
  erst im jeweiligen Arbeitspaket relativ zum Spielfeld festgelegt.
- **Wartezeit:** Jede Strategie besitzt eigene Min-/Max-Dauer und Verteilungswahl,
  passend zur Vorbereitung und den unterschiedlichen Limits auf Fünfer- und Dreierreihe.
  Sobald der Recorder die Vorbereitung bestätigt, fordert er etwa „Snake ready“ oder
  „Pull ready“ an und startet im selben Zustandsübergang die Wartezeit. Bei 2–6 Sekunden
  fordert er die Variantenansage frühestens 2 und spätestens 6 Sekunden danach an.
  Die tatsächliche lokale Ausgabelatenz wird ignoriert; keinen Audio-Abschluss abwarten.
  Einmal pro Versuch ziehen, nicht in jedem Frame neu. Ready ist
  die Bestätigung der Vorbereitung, die spätere Variantenansage nennt das Schuss-/Passziel.
- **Vorzeitige Auslösung:** Versuch abbrechen und Abbruch ansagen, etwa „Aborted“.
  Noch ausstehende Variantenansage verwerfen. Normales Snake-Pendeln/Tick-Tack ist keine
  Auslösung und startet die Wartezeit nicht erneut.
- **Verlust der Vorbereitung nach Ready:** Auch ohne Schuss/Pass abbrechen und erneut
  vollständige Vorbereitung verlangen. Tick-Tack: Ball verlässt den Setup-Bereich.
  Pull: Ball bewegt sich leicht, ohne die Mindestbewegung und Geschwindigkeit eines
  Schusses zu erreichen. Snake: Ball rollt langsam aus der Klemmposition. Die Strategie
  unterscheidet zulässige Vorbereitung, verlorene Vorbereitung und echte Auslösung.
  Konkrete Schwellen und zeitliche Toleranzen werden im jeweiligen Arbeitspaket festgelegt.
  Diese Prüfung gilt nach Ready bis zur echten Auslösung, auch nach der Variantenansage;
  sie darf eine reguläre Auslösung nicht nachträglich als Setup-Verlust werten.
- **Abbruchbezeichnung:** Empfehlung: „Aborted“ für vorzeitige Auslösung oder Verlust
  der Vorbereitung. Intern unterschiedliche Gründe erfassen, dieselbe kurze Ansage nutzen.
  „Failed“ wäre eher für eine spätere Bewertung eines tatsächlich ausgeführten Versuchs
  geeignet; eine solche Erfolgsbewertung gehört noch nicht zum aktuellen Umfang.
- **Neuer Versuch:** Nach regulärer Auslösung oder Abbruch muss der Ball erneut
  in die strategiespezifische Vorbereitung gebracht werden. Neu bestätigen und einmal
  „Ready“ ansagen. Ein unverändert fortgesetztes Vorbereitungsmuster erzeugt keine
  weiteren Versuche oder Ansagen.
- **Varianten:** Stabile IDs, sprechbare Texte und strategieeigene Auswahlregel vorsehen.
  Konkrete Auswahlverteilung, Wiederholungsregel und optionale Teilmengen noch festlegen.
- **Veraltete Ansagen:** Verwerfen. Dafür keinen zusätzlichen Fehlerdialog, keinen
  technischen Versuchsabbruch und keinen Wiederholungs-/Recovery-Ablauf vorsehen.
  „Aborted“ bleibt die fachliche Antwort auf vorzeitige Auslösung oder Verlust der
  Vorbereitung, kein Audiofehler-Handling.
- **Replay:** Wiederholen bis zur nächsten bestätigten Vorbereitung/„Ready“, dann zu
  Live wechseln. Tracking und Vorbereitungserkennung laufen während Replay weiter.
  Ballbesitz oder weitere Ballbewegung nach einem Pass beenden Replay nicht wie bei Game.

### Vorschlag zur Wartezeitverteilung

Jede Strategie erhält ihren eigenen Wait-Block. Folgender JSON-Ausschnitt zeigt die
vorgeschlagene Struktur; Zahlen sind Beispiele, keine Defaults oder festgelegten Zeitlimits:

```json
{
  "training": {
    "enabledStrategies": ["tickTackPass", "snakeShot"],
    "strategies": {
      "snakeShot": {
        "wait": { "minSeconds": 2, "maxSeconds": 6, "distribution": "preferLong" }
      },
      "pullShot": {
        "wait": { "minSeconds": 1, "maxSeconds": 5, "distribution": "uniform" }
      },
      "tickTackPass": {
        "wait": { "minSeconds": 1, "maxSeconds": 3, "distribution": "uniform" }
      }
    }
  }
}
```

Vorgeschlagene Werte: `uniform`, `preferLong`, `preferShort`. Eine einfache, kontinuierliche
Verteilung ohne weitere Nutzerparameter: Für gleichverteiltes u zwischen 0 und 1 wird
der Anteil am Intervall u, sqrt(u) bzw. 1 - sqrt(u). Wartezeit = min + (max - min) * Anteil.
Damit liegt bei `preferLong` die Wartezeit mit 75 % Wahrscheinlichkeit in der längeren
Intervallhälfte; `preferShort` ist spiegelbildlich. min = max ergibt eine feste Wartezeit.
Die Stärke ist zunächst bewusst fest; ein zusätzlicher Bias-Parameter nur bei echtem Bedarf.
Dieser Verteilungsvorschlag ist noch abzustimmen.

### Noch zu konkretisieren – teilweise erst im jeweiligen Arbeitspaket

- [ ] **Erkennung:** Konkrete Bereiche, Ruhe-/Pendelzeiten, Richtungswechsel und Schwellen
  gegen Trackingrauschen später pro Strategie festlegen. Beim Tick-Tack ausdrücklich
  Vorbereitung durch Richtungswechsel von der eigentlichen Passauslösung unterscheiden.
- [ ] **Mehrdeutige Vorbereitung:** Vor Implementierung der Strategiekoordination eine
  deterministische Regel festlegen, falls mehrere Strategien gleichzeitig Ready melden.
  Vorschlag: Bei Mehrdeutigkeit noch keinen Versuch starten, bis die Vorbereitung eindeutig
  ist; nicht zufällig oder abhängig von der Iterationsreihenfolge auswählen.
- [ ] **Verdeckung/Nicht-Schießen:** Toleranz für kurze Tracking-Aussetzer und Musterverlust sowie
  gegebenenfalls einen Timeout nach Ansage festlegen. Kein neuer Versuch ohne erneute Vorbereitung.
- [ ] **Passname:** „Sling“ oder „Split“ für den Pass zwischen Wall und Lane entscheiden;
  Arbeitsbezeichnung im Plan: Split, ohne damit einen etablierten Fachbegriff zu behaupten.
- [ ] **Replay-Details:** Auslöser, Vor-/Nachlauf und Geschwindigkeit später festlegen;
  ebenso, ob vorzeitig abgebrochene Versuche ein Replay erhalten. Das Ende durch die
  nächste Vorbereitung ist bereits entschieden, keine feste Schleifenzahl als Trainingsende.

Abnahme: Bestätigte Entscheidungen umsetzen; verbleibende Details vor dem jeweils
abhängigen Arbeitspaket klären. Noch keine Implementierungs-Checkboxen als erledigt markieren.

## 1. Gemeinsamen Tracking-Kern fachlich abgrenzen

Abhängigkeit: keine Produktentscheidung außer der grundsätzlichen Trennung.

- [x] `GameTracker`, Snapshot, SnapshotFactory und Tests auf gemeinsame Fakten versus
  Game-Entscheidungen aufteilen. Einen modusneutralen Live-Tracking-Snapshot bereitstellen.
- [x] BallTracker, PossessionCalculator, Besitzzeit und Behandlung kurzfristigen
  Ballverlusts wiederverwenden. Zeitfouls und Game-Replay-Strategien ausdrücklich durch
  Game zusammensetzen; vorhandene Replay-Strategien nach fachlicher Zuständigkeit einordnen.
- [x] GameSession und Game-Usecases auf die extrahierten Komponenten umstellen.
  Replay-Anker und bisherige Game-Ausgaben erhalten.
- [x] TableConfig-Updates an alle betroffenen Tracking-Komponenten weiterreichen;
  bei Session-Wechsel alle Tracking- und Entscheidungszustände zurücksetzen.

Tests: Bestehende Domain-Tests für BallTracking, GameTracking, ReplayDecision und
GameSession erhalten; neue gemeinsame Snapshot-Grenze sowie Reset und Ballverlust testen.

Abnahme: Game verhält sich unverändert. Training kann Tracking nutzen, ohne Game-Regeln auszuführen.

Umgesetzt am 2026-10-30:

- `TrackingCore/Services/LiveTracking` enthält `LiveTracker`, `LiveTrackerParams`,
  `LiveTrackingSnapshot` und dessen Factory. Der Snapshot liefert Beobachtung/Kandidaten
  mit Konfidenz, Position, Geschwindigkeit, Verluststatus, nächste Reihe sowie Besitz und
  Besitzdauer; er enthält keine Zeitfoul- oder Replay-Entscheidungen.
- `Game/Services/GameTracking` setzt die gemeinsamen Fakten mit Zeitfoul- und
  Replay-Entscheidungen zusammen und erhält die bisherigen Game-Ausgaben.
  Die bisherigen Replay-Strategien einschließlich Factory liegen unverändert in
  `Game/Services/ReplayDecision`; nur Namespaces und Imports wurden angepasst.
- Der Game-Start erzeugt pro Session frische Ball-, Besitz-, Live-Tracking- und
  Replay-Komponenten. TableConfig-Updates erreichen BallTracker, PossessionCalculator
  und Replay-Strategien. Das bisherige Vorrangverhalten bleibt erhalten: Ein Replay
  bei Ballverlust verhindert in diesem Frame das Zurücksetzen der Besitzzeit.
  Dafür konsumiert Game die neutralen Fakten vor Abschluss des Frames; die öffentliche
  LiveTracker-Operation schließt den Frame selbst ab und benötigt keine Game-Policy.
- Keine neuen Projektabhängigkeiten, Protokoll-, Konfigurations- oder sichtbaren
  Produktänderungen; daher kein Eintrag in den Produkt-Release-Notes. Kalibrierung,
  Recorder-Live-Extraktion und Training-Verhalten bleiben späteren Arbeitspaketen vorbehalten.

## 2. Laufende Tischaktualisierung und Recorder-Live-Bausteine teilen

Abhängigkeit: Schritt 1.

- [ ] Aktualisierungsregeln aus GameSession als kleinen gemeinsamen Domain-Baustein
  extrahieren: Intervalle, Konfidenz/Bewegung, gegenseitige Sperre laufender Updates
  und Abschluss. Instanz gehört jeweils zur aktiven Session.
- [ ] Bestehende `UseCases/Calibration` weiterverwenden. Session-spezifische
  CompleteTableUpdate-/CompleteTableSceneUpdate-Usecases bleiben dünne Einstiegspunkte.
- [ ] `UseCases/Game/Ports/IFrameVisionOps` in einen passend benannten gemeinsamen
  Usecase-Bereich verschieben, sobald beide Modi ihn nutzen. Keine Puffer in den Core ziehen.
- [ ] Recorder `Game/Live/FrameVisionOps`, `Game/Orchestration/CalibrationCoordinator`,
  Vision-Ops und `Game/VisionContextUpdatePublisher` auf tatsächliche Game-Kopplung prüfen.
  Gemeinsame Ausführung in Recorder-Live-/Calibration-Bereiche verschieben; kleine
  session-spezifische Rückmeldungen über schmale Schnittstellen anschließen.
- [ ] `FrameProcessingLoop`, BallFinder, Masken, Tracking-Nachrichten und Frame-Metriken
  wiederverwenden. Training bekommt eigene Frame-Input-/Output-Anbindung und Presenter;
  gemeinsame Mapping- und Publikationsarbeit wird extrahiert, nicht die gesamte Game-Klasse kopiert.
- [ ] Fehler und Abbruch einer Kalibrierung lösen In-Progress-Zustände zuverlässig auf.
  Späte Ergebnisse einer alten Session dürfen keine neue Session aktualisieren.

Tests: Kalibrierungsanforderungen, Abschluss/Fehler/Stop, Vision-Kontext und gemeinsame
Live-Publikation in bestehenden Domain-, Usecase- und Recorder-Adapter-Testprojekten.

Abnahme: Game läuft weiterhin unverändert; gemeinsame Bausteine hängen nicht von GameSession ab.

## 3. Training-Domain und Usecases für den ersten Versuch implementieren

Abhängigkeit: Schritte 0–2.

- [ ] TrainingSession mit Session-ID, Versuch-ID, Zustand und unveränderlichen
  Trainingsoptionen pro Session implementieren.
- [ ] Alten SequenceRecognizer, ISequenceRecognizer und nur dort verwendete Zustandstypen
  sowie Prototyp-Tests entfernen; zuvor Referenzen prüfen. Neue Strategietests ersetzen
  die alten Tests fachlich, ohne deren Heuristiken konservieren zu müssen.
- [ ] Schmalen Domain-Vertrag, beispielsweise ITrainingStrategy, definieren. Eingang:
  Tracking-Fakten, Frame/Zeit, Tisch-/Teamkontext und nötige Versuchsereignisse.
  Ausgang: typisierte Entscheidungen wie Vorbereitung bestätigt, Ansage angefordert,
  Versuch ausgelöst oder abgebrochen. Keine JSON-, Protokoll- oder Audio-Typen.
- [ ] Zustandsbesitz eindeutig halten: TrainingSession verwaltet IDs und gemeinsamen
  Lifecycle, Strategie verwaltet Erkennungshistorie und ihre Ablaufregeln. Ergebnisse
  werden einmalig pro Übergang ausgegeben; keine zweite konkurrierende Zustandsmaschine
  im Presenter oder ein zentraler Switch auf Strategie-IDs pro Frame.
- [ ] Koordination mehrerer aktivierter Strategien ergänzen. In der Suche denselben
  Tracking-Snapshot an alle Vorbereitungserkennungen geben; höchstens einen Versuch
  starten. Strategie-ID, Team/Reihe und zugehörige Optionen für diesen Versuch festhalten.
  Während eines aktiven Versuchs startet keine zweite Strategie einen konkurrierenden
  Timer oder eine Ansage. Nach Auslösung/Abbruch stehen wieder alle aktivierten Strategien
  zur Verfügung; alte Erkennungshistorien so zurücksetzen, dass eine neue Vorbereitung
  erforderlich ist. Dies gilt auch während Replay des vorigen Versuchs.
- [ ] SnakeShotTrainingStrategy, PullShotTrainingStrategy und TickTackPassTrainingStrategy
  (Arbeitsnamen) implementieren,
  einschließlich jeweiliger Vorbereitung, Ansagevarianten, Wartephase und Auslösung.
  Nach Ready läuft die strategiespezifische Wartephase unter der passenden Haltebedingung;
  zulässiges Snake-Pendeln/Tick-Tack bleibt erlaubt, Zieher erfordert weiterhin Ballruhe.
- [ ] Gemeinsamen Ablauf abbilden: Vorbereitung suchen → Ready anfordern und Wartezeit
  starten → bei Ablauf Variantenansage anfordern und Auslösung freigeben →
  Auslösung abwarten → Versuch ausgelöst → erneut bereit. Abbrüche explizit verarbeiten.
  Alle Übergänge entscheidet der Recorder ohne Audio-Rückmeldung. Auslösung liefert einen zeitlichen Anker,
  ohne schon an einen konkreten Replay-Transport gekoppelt zu sein.
- [ ] Vorzeitige Auslösung erzeugt Abbruch mit Aborted-Ansage; alte Variantenansage
  invalidieren. Ready, Variante und Aborted als getrennte, korrelierte Ausgabeereignisse
  behandeln. Versuchsabschluss darf seine eigene Aborted-Ausgabe nicht pauschal verwerfen;
  beim nächsten Ready dürfen dagegen keine alten Ansagen mehr nachlaufen.
- [ ] Verlust der Vorbereitung nach Ready als separaten Strategieausgang mit Abbruchgrund
  modellieren. Wartezeit und ausstehende Variantenansage verwerfen, einmal Aborted ausgeben
  und zur vollständigen Vorbereitungserkennung zurückkehren. Kein automatisches Fortsetzen
  des alten Timers, sobald der Ball wieder im Bereich liegt.
- [ ] Echte Auslösung anhand strategiespezifischer Bewegungs-/Geschwindigkeitskriterien
  von bloßem Setup-Verlust unterscheiden. Entscheidungsreihenfolge und gegebenenfalls
  kurze Beobachtungsfenster definieren, damit die Anlaufbewegung eines Schusses nicht
  schon als Setup-Verlust abgebrochen wird. Kein Replay allein durch Setup-Verlust auslösen.
- [ ] Wartezeit über injizierbare Zufallsquelle einmal pro Vorbereitung bestimmen.
  Verteilung bleibt Domain-Logik; Grenzen/Verteilungswahl kommen aus dem eigenen
  Config-Block der gewählten Strategie. Timer zusammen mit der Ready-Anforderung starten.
- [ ] Strategie bei Stop/Neustart zurücksetzen. Umgang mit aktualisierter Tischgeometrie
  festlegen: Historie neu bewerten oder verwerfen, damit Koordinatenänderungen keine
  scheinbare Ballbewegung/Auslösung erzeugen.
- [ ] Für Zeitentscheidungen monotone, explizit übergebene Zeitwerte verwenden;
  Frame-Zeit und gegebenenfalls Live-Uhr nicht vermischen. Kein Task.Delay in der Domain.
  Vorschlag für V1: bei ausbleibenden Frames keine Ansage auslösen; nach großen
  Zeitlücken Auflage neu bestätigen. Regel deterministisch testen.
- [ ] Variantenauswahl injizierbar und in Tests deterministisch gestalten. Fachliches
  Ergebnis enthält Variante, Versuch und Ansage-ID statt Android-/TTS-Aufrufen.
- [ ] StartTraining, StopTraining, ProcessFrame sowie Kalibrierungsabschluss mit
  eigenen Ports und TrainingSessionStore-Vertrag ergänzen. Start setzt gültiges Setup voraus.
- [ ] Ansagen ausschließlich als Usecase-Ausgaben behandeln; keine Ready-Zeitrückmeldung
  oder zusätzliche Fehlerbehandlung für verworfene Ansagen als Trainingszustand modellieren.

Tests: Snake-Pendeln bestätigt Vorbereitung und hält Wartephase; einzelne Bewegung oder
Rauschen genügt nicht. Überschreiten des Pendelbereichs nach links/rechts und Bewegung
weg von der Stange lösen bei erfüllten Schusskriterien aus. Zieher erkennt Ruhe auf Höhe des spielerfernen Torpfostens und
löst erst bei ausreichendem Bewegungsweg und ausreichender Geschwindigkeit aus. Tick-Tack zählt gültige Richtungswechsel,
erkennt die Vorbereitung an beiden Tischseiten und unterscheidet sie von der Passauslösung.
Alle Strategien für beide Teams testen. Zusätzlich falsche Reihe, gespiegelter Tisch,
Zeitgrenzen, Verdeckung, Zeitlücke, vorzeitige Auslösung, einmalige Ansage/Auslösung,
erneuter Versuch, Stop/Neustart, Geometrieänderung und strategieeigene Variantenauswahl.
Zusätzlich: Tick-Tack verlässt den Bereich, Pull driftet ohne Schuss, Snake rollt langsam
aus der Klemmposition; jeweils Aborted nur einmal, Timer/alte Ansage verworfen und neue
Vorbereitung erforderlich. Dieselben Fälle nach Variantenansage prüfen. Echte Auslösung
und deren Anlaufphase dürfen nicht fälschlich als Setup-Verlust klassifiziert werden.
Zufallsverteilung mit vorgegebenen Zufallswerten prüfen: Grenzen, Monotonie, Spiegelung,
min = max und nur eine Ziehung pro Versuch; keine flakigen statistischen Zufallstests.
Unterschiedliche Wait-Optionen je Strategie und Timerbeginn zusammen mit der Ready-Anforderung
prüfen. Zustandsfortschritt benötigt keine Rückmeldung oder tatsächliche Audioausgabe.
Kombinationen Tick-Tack + Snake und Snake + Pull testen: Wechsel zwischen Versuchen,
inaktive Strategien ignorieren, korrekte Wartezeit/Varianten je erkannter Strategie,
keine konkurrierenden Ansagen, mehrdeutige Erkennung und frische Vorbereitung nach Abbruch.

Abnahme: Vollständiger erster Trainingsablauf ist in-memory ohne Kamera, Netzwerk oder Audio testbar.

## 4. JSON-Konfiguration und Settings-Mapping ergänzen

Abhängigkeit: festgelegte Optionen aus Schritt 0 und Core-Verträge aus Schritt 3.

- [ ] Eigenen `training`-Abschnitt in RecorderSettings/RecorderSettingsJson vorsehen:
  Liste aktivierter Strategien (`enabledStrategies`; IDs `snakeShot`, `pullShot`, `tickTackPass`
  als Arbeitsnamen),
  je Strategie einen eigenen Block für minimale/maximale Wartezeit, Verteilungswahl,
  Variantenauswahlregel und typisierte Erkennungsparameter. Keinen globalen Wait-Wert
  verwenden, der ungewollt Fünfer- und Dreiertraining gleich behandelt.
  Konkrete Einheiten in Feldnamen und Dokumentation angeben; Defaults erst nach
  Produktentscheidung festlegen. Keine frei kombinierbaren Auflage-/Ansage-/Auslöser-IDs,
  die fachlich unpassende Kombinationen erlauben.
- [ ] Ansage-Einstellungen wie Sprache und Sprechgeschwindigkeit ausschließlich in der
  Recorder-Konfiguration vorsehen. Keine Audiooptionen an den Viewer übertragen.
- [ ] Settings-DTOs über Composition in Core-Optionen übersetzen. Das bislang eigenständige
  Settings-Projekt benötigt dafür keine Domain-Abhängigkeit. Schmalen Settings-Port ergänzen,
  ohne Recorder-JSON-Typen in Usecases zu übernehmen.
- [ ] Validierung für Bereiche, leere Variantenlisten/Texte, doppelte IDs und
  widersprüchliche Optionen hinzufügen. Unbekannte Strategie-IDs, zur Strategie unpassende
  Varianten und Parameter zurückweisen. Bestehende Dateien ohne training verwenden Defaults.
  Aktivierungsliste auf unbekannte/doppelte IDs prüfen und mindestens eine aktivierte
  Strategie für den Trainingsstart verlangen. Konfigurierte, aber nicht aktivierte
  Strategien nehmen nicht an der Erkennung teil.
  Wartegrenzen müssen endlich, nicht negativ und min <= max sein; Verteilungswert validieren.
  Schema-Versionierung und unbekannte Eigenschaften bewusst behandeln.
- [ ] Config.example.json-Generierung und bestehenden Texteditor/Validator erweitern.
  Recorder-Neustart und Viewer-Reconnect bleiben der Übernahmeweg.

Tests: RecorderSettingsJsonTests und Initializer-Tests mit alten, gültigen neuen und
ungültigen Konfigurationen für alle drei Strategien; Auswahl/Mapping, unzulässige Kombinationen
und Session-Snapshot der Einstellungen prüfen.

Abnahme: Alte Konfigurationen funktionieren; neue Optionen gelangen validiert an ihren Besitzer.

## 5. Protokoll und Transport vollständig erweitern

Abhängigkeit: Schritte 3–4; Protokoll-Guide vor Änderungen erneut berücksichtigen.

- [ ] StartTraining/StopTraining, CommandMessageType und RecorderRuntimeMode.TrainingRunning
  ergänzen. Bestehende numerische Werte nicht verändern.
- [ ] Trainingsstatus definieren: Session-ID, Versuch-ID, Strategie-ID, Zustand, Variante
  und ausreichend Kontext zur Erkennung veralteter Nachrichten. Strategieergebnisse
  für Anzeige und Playback übersetzen; keine Strategieausführung oder Ansage im Viewer.
- [ ] Vorbereitung-bestätigt/Replays-beenden und Versuchswechsel eindeutig korrelieren
  und ordnen. Neues Ready am Recorder invalidiert alte Replay-Anforderungen. Viewer
  beendet Replay bei der zugehörigen Statusnachricht; ein verspäteter alter Clip darf
  die neue Live-Ansicht nicht ersetzen. Lokale Ready-Ausgabe wartet nicht auf den Viewer.
- [ ] Konkreten bestehenden Kanal samt Liefergarantien prüfen. Duplikate und veraltete
  Trainingsstatus-Nachrichten verwerfen. Keine Ansage-Nachrichten oder Audio-Rückkanäle einführen.
- [ ] Ohne synchronisierte Geräteuhren keine Recorder-Zeitstempel direkt mit Viewer-Uhr
  vergleichen. Frische über Session/Versuch, Nachrichtenreihenfolge und definierte
  Fristlogik absichern; verbleibende Netzwerklatenz als Produktgrenze dokumentieren.
- [ ] Versuchsabbruch/-wechsel und nötige Korrelationsinformationen übertragen.
  HandshakeViewerSettings und Viewer-Mapping benötigen keine Erweiterung für Audiooptionen.
- [ ] NetMQ-Typregistrierung, Serialisierung, Sender/Empfänger und Dispatch für alle
  neuen Verträge ergänzen; vorhandene Kanäle und Ports möglichst unverändert nutzen.
- [ ] Protokoll- und Major-Version unverändert lassen. Recorder und Viewer für die
  Verifikation mit demselben aktualisierten Build betreiben. Auswirkungen auf ältere
  Builds dokumentieren; keinen Versionssprung oder zusätzlichen Kompatibilitätsmechanismus
  allein für diese Prototyp-Erweiterung einführen.

Tests: Nachrichten-Mapping, Dispatch, IDs, Duplikate und veraltete Antworten in bestehenden
passenden Testprojekten. Falls kein geeigneter Platz für Serializer-/Transporttests
existiert, vor einem neuen Testprojekt fragen. Keine echten Sockets in Unit-Tests.

Abnahme: Protokollauswirkungen sind explizit dokumentiert; beide Rollen unterstützen denselben Vertrag.

## 6. Recorder-Adapter, Composition und Lifecycle anschließen

Abhängigkeit: Schritte 2–5.

- [ ] TrainingCommandHandler, TrainingEventPresenter und Training-FramePresenter ergänzen.
  Sie übersetzen Usecase-Ausgaben in Runtime-State, Live-Daten und Trainingsnachrichten.
  Ansage-Ausgaben werden lokal an die Recorder-Sprachkoordination übergeben, nicht
  als Nachrichten an den Viewer gesendet.
- [ ] RecorderCommandRouter um Training erweitern. Gegenseitigen Ausschluss von Setup,
  Game und Training recorderseitig absichern, einschließlich konkurrierender Starts;
  UI-Sperren allein reichen nicht. Regel in Core/Usecases, technische Koordination im Adapter.
- [ ] TrainingModule und InMemory-TrainingSessionStore in Recorder.Composition ergänzen;
  über RecorderCompositionRoot und RecorderHost verbinden.
- [ ] Strategieaktivierung zentral beim Session-Start verdrahten: Validierte Konfiguration
  über eine Factory in frische Instanzen aller aktivierten Strategien übersetzen. ID-Mapping
  gehört an diesen Erzeugungspunkt, Erkennungs- und Ablaufregeln bleiben in der Domain.
  Änderungen der aktivierten Menge/Optionen folgen dem bestehenden Neustart-Workflow.
  Der Wechsel zwischen bereits aktivierten Strategien erfolgt durch die erkannte
  Vorbereitung innerhalb derselben Session, ohne Neustart.
- [ ] StopActiveSessions, EndOfInput, SourceStopped, Fehler, Disconnect und Rollenwechsel
  auf Training erweitern. FrameLoop, Kalibrierung, Replay und ausstehende Ansagen beenden.
- [ ] RecorderRuntimeStateController um Training erweitern. Startfehler räumen teilweise
  erzeugte Ressourcen auf; alte Callback-Ergebnisse werden an der Session-ID verworfen.
- [ ] Diagnostische Videodumps, Session-Bezeichnungen und Metriken für Training mitführen.
  Produktregeln dabei nicht in Logging- oder Dump-Komponenten verlagern.

Tests: Router/Presenter und Runtime-State in Recorder-Adapter-Tests; Usecase-Tests für
Startbedingungen und Ausschluss. Bestehende Setup-/Game-Lifecycle-Fälle erhalten.

Abnahme: Training ist als eigener Recorder-Modus start-/stoppbar und vollständig aufräumbar.

## 7. Viewer-Adapter, Composition und minimale Bedienung anschließen

Abhängigkeit: Schritte 5–6.

- [ ] TrainingCommandSender und Viewer-TrainingModule ergänzen. ConnectedViewerSession
  und IConnectedViewerSession um Trainingskommandos erweitern.
- [ ] Recordergebundene Module und Subscriptions erst nach erfolgreicher Verbindung
  erzeugen. ViewerHost/ViewerCompositionRoot und SessionManager führen Abhängigkeiten
  zur ActiveSession; kein globaler Trainingszustand beim App-Start.
- [ ] ActiveSession um Trainingsstatus erweitern. Bestehende
  Tracking-, Tisch-, Masken-, Playback- und Replay-Presenter wiederverwenden.
- [ ] SessionMode, PendingIntent und SessionUiStateCalculator explizit um Training
  erweitern. Setup/Game-Fallbacks entfernen; Verfügbarkeit aus Setup und Runtime-State ableiten.
- [ ] Live-Playback, FPS, Overlay-Reset und Stop-Regeln für Training ergänzen. Auch
  Android OverlayView auf bisherige Game-only-Bedingungen prüfen.
- [ ] Dritten Start-/Stop-Button und kompakten Trainingsstatus in
  ViewerSessionController, ViewerControlsViewModel und allen vier Control-Layouts ergänzen.
  Variante sichtbar machen; keine Konfigurationsmaske oder zusätzlichen Ansagefehlerdialog bauen.
- [ ] Disconnect, Session-Wechsel und Dispose entfernen Trainings-Subscriptions und
  alte Anzeige-/Playback-Zustände. Audio-Lifecycle verbleibt vollständig beim Recorder.

Tests: CommandTests, RuntimeStateTests, SessionUiStateCalculatorTests, SessionManagerTests,
LiveTrackingTests und neue Trainings-Presenter-Tests in Adapters.Viewer.UnitTests.

Abnahme: Viewer kann Training bedienen und visualisieren; Setup und Game bleiben bedienbar.

## 8. Sprachausgabe implementieren und in den Versuch integrieren

Abhängigkeit: Schritte 0, 3, 4 und 6. Ausgabe ausschließlich am Recorder-Lautsprecher;
Viewer-Implementierung und Audio-Protokoll sind dafür nicht erforderlich.

- [ ] Schmalen Audio-Port, beispielsweise ISpeechOutput, in Ports.Media ergänzen:
  vorbereiten, asynchron sprechen, abbrechen und aussagekräftiges Ergebnis liefern.
  Keine Plattformtypen in Port, Domain oder Protokoll verwenden.
- [ ] Android-TTS-Implementierung in bestehender Media.Android-Infrastruktur vorsehen.
  Vor Umsetzung aktuelle Android-Dokumentation prüfen: Initialisierung, Sprachverfügbarkeit,
  Offline-Stimmen, Abschluss/Fehler-Callbacks, Audio-Fokus und Lifecycle. Keine neue
  Projektabhängigkeit stillschweigend einführen.
- [ ] Dienst über Recorder-Rollenmodul und RecorderCompositionRoot ins TrainingModule
  einspeisen. Android-Engine kann vorbereitet werden; Ansage-Queue und Cancellation
  gehören zur aktiven TrainingSession und werden beim Stop/Neustart zurückgesetzt.
- [ ] Recorder-Adapter koordiniert deduplizierte Ansagen anhand von Session/Versuch/Ansage-ID.
  Audio läuft außerhalb der Frame-Verarbeitung. Warteschlange begrenzen; veraltete
  Ansagen verwerfen und bei Abbruch auch bereits laufende Ausgabe stoppen.
- [ ] Sprachausgabe vorbereiten. Technische
  Fehler auf Infrastruktur-Ebene regulär protokollieren; keinen zusätzlichen
  UI-/Trainings-Fehlerablauf, automatischen Retry oder Versuchsabbruch dafür einbauen.
  Veraltete Ansagen lediglich verwerfen.
- [ ] Zusammenspiel mit Replay, App-Hintergrund, Audio-Fokusverlust, Disconnect und
  Rollenwechsel definieren. Bei Fortsetzung keine alte Ansage nachholen.
- [ ] Recorder führt die angeforderte Ansage lokal aus. Ausgabelatenz beeinflusst weder
  Timer noch Trainingszustand; der Viewer erhält keine Audioanforderung.

Tests: In Recorder-Adapter-Tests Fake-Speech-Port mit kontrollierten Abschlüssen für Duplikate, Abbruch,
Verspätung, Fehler, fehlende Sprache und Reconnect. Hardware/TTS nicht in Unit-Tests.

Abnahme Meilenstein 1: Für alle drei per JSON kombinierbaren Strategien funktionieren Vorbereitung
→ Ready → zufällige Wartezeit → passende Ansage → strategiespezifische Auslösung → neuer Versuch;
vorzeitige Auslösung oder Verlust der Vorbereitung führt zu Aborted und erfordert erneute
Vorbereitung. Dies funktioniert
durch beide Rollen. Gerätetest erfolgt manuell durch den Nutzer.

## 9. Gemeinsame Replay-Ausführung und Training-Replay ergänzen

Abhängigkeit: stabiler Meilenstein 1 und Replay-Entscheidungen aus Schritt 0.

- [ ] Replay-Entscheidung als modusspezifische Domain-Logik behandeln. Gemeinsame
  Anforderung enthält Anker, explizites Clip-Zeitfenster und Wiedergabevorgaben.
  Fachliche Wiederholungs-/Abbruchregeln bleiben im Core, nicht im Medienadapter.
- [ ] Auslösungsereignis der gewählten Trainingsstrategie als möglichen Replay-Anker
  verwenden. Strategieerkennung nicht im ReplayCoordinator erneut implementieren;
  konkrete Replay-Regeln verwenden Strategieergebnisse und Versuchskontext.
- [ ] Recorder Game/Orchestration/ReplayCoordinator als gemeinsame Replay-Ausführung
  extrahieren. Game liefert seine bisherigen Dauern explizit; Training liefert eigene.
- [ ] Replay-Puffergrenzen, noch nicht erfassten Nachlauf, Keyframe-Anforderungen,
  Überlappungen und nicht verfügbare Segmente behandeln. Nicht unbegrenzt puffern/warten.
- [ ] ReplayStartedMessage/ReplayMessage und Mapper um notwendige Informationen ergänzen;
  Session-/Versuchszuordnung, verspätete Clips und Kompatibilität wie in Schritt 5 absichern.
- [ ] Bestehende ReplaySession, Replay-Usecases, ViewerReplayCoordinator und
  ReplayPlaybackRequestMapper für Dauer, Geschwindigkeit, Wiederholungszahl und
  Rückkehrregel prüfen und gezielt erweitern. Analyse und Decoder weiterverwenden.
- [ ] Trainings-Replay wiederholen, bis der Recorder eine neue Vorbereitung bestätigt.
  Laufendes Live-Tracking erkennt diese auch während Wiedergabe/Analyse. Recorder spricht
  Ready lokal und meldet die neue Vorbereitung. Viewer beendet daraufhin Replay und
  wechselt zu Live. Recorder wartet weder auf das Wiedergabeende noch eine Viewer-Bestätigung;
  das neue Setup beendet die Schleife.
- [ ] Game-Regel zur Beendigung bei wieder aufgenommenem Spiel für Training nicht anwenden:
  Nach einem Pass darf Ball auf der Dreierreihe/im Spiel bleiben und Replay weiterlaufen.
  Abbruchpolicy in Replay-Domain/Usecases ausdrücken, nicht nur im UI unterdrücken.
- [ ] Ready invalidiert auch vorbereitete/übertragene alte Replays. Stop/Disconnect beendet
  weiterhin jede Schleife. Nur der Clip wird wiederholt, nicht erneut erfasst/übertragen.

Tests: Bisherige Game-Replay-Szenarien unverändert; Training-Trigger und Zeitfenster,
Puffergrenzen, Wiederholungen bis Ready, weiterlaufender Ball nach Pass, neue Vorbereitung
während Replay, Ready vor Clip-Eingang, vorzeitiger Abbruch und Session-Wechsel.

Abnahme Meilenstein 2: Training besitzt eigene Replay-Regeln und nutzt dieselbe technische Pipeline.

## 10. Dokumentation, Werkzeuge und Abschlussprüfung

Abhängigkeit: jeweils mit dem betroffenen Implementierungsschritt erledigen.

- [ ] Documentation/Configuration.md um Schema, Defaults, Einheiten, Validierung,
  Strategieaktivierung mit Kombinationsbeispielen, Wartezeitverteilung, Sprachvoraussetzungen und
  Recorder→Viewer-Propagation ergänzen.
- [ ] Documentation/Diagnostics.md um Trainingsstatus und Videodumps ergänzen.
  Zustandswechsel strukturiert loggen; häufige Messwerte aggregieren statt Frame-Logspam.
- [ ] README und Documentation/FoosVision Changes.md im aktuellen Release um tatsächlich
  ausgeliefertes Training ergänzen. Diesen Plan allein nicht als Produktänderung eintragen.
- [ ] Protokolländerungen und Kompatibilitätsentscheidung ausdrücklich dokumentieren.
- [ ] VideoPlayer, RecorderCli und ViewerCli auf betroffene Verträge, Settings-Provider,
  Session-Modi und Testdoubles prüfen und nötige Kompilierungsanpassungen vornehmen.
  Trainingssteuerung dort nur ergänzen, wenn sie zum unterstützten Workflow gehören soll.
- [ ] Nach jedem Schritt passende bestehende Testprojekte ausführen. Abschließend
  `dotnet test` und `dotnet build FoosVision.slnx` sequenziell ausführen; keine parallelen
  dotnet-Kommandos. Nicht verfügbare Prüfungen mit Grund dokumentieren.
- [ ] Manuelle Checkliste für den Nutzer: Setup→Training, beide Tischseiten, wiederholte
  Versuche, Ballverdeckung, verworfene alte Ansagen, Replay, Stop während Ansage/Replay,
  Verbindungsabbruch, Reconnect, Rollenwechsel sowie Rückkehr zu Game.
- [ ] Auf realen Geräten 30-fps-Tracking und 120-fps-Stream unter Audio-/Replay-Last prüfen;
  Ansagelatenz und Zuverlässigkeit beobachten. Apps, Tools und Geräteworkflows startet der Nutzer.

## 11. Plan nach Abschluss löschen

Abhängigkeit: Alle vorherigen Arbeitspakete sind abgeschlossen und verifiziert.

- [ ] Sicherstellen, dass dauerhaft relevante Informationen in der Produkt- und
  Entwicklerdokumentation stehen und keine offenen Aufgaben nur in diesem Plan verbleiben.
- [ ] `Training-Plan.md` aus der Repo-Root löschen.

Abnahme: Die Umsetzung ist abgeschlossen; der temporäre Plan ist entfernt.

## Reihenfolge und Fortschritt

1. Entscheidungen für Meilenstein 1 festhalten.
2. Gemeinsames Tracking und Kalibrierung mit Game-Regressionsschutz extrahieren (1–2):
   Arbeitspaket 1 abgeschlossen; Arbeitspaket 2 offen.
3. Strategie-Vertrag, Snake, Zieher, Tick-Tack Pass, Koordination und JSON-Kombinationen
   implementieren (3–4).
4. Protokoll und Recorder-Laufzeit anschließen (5–6).
5. Viewer-Bedienung und lokale Recorder-Sprachausgabe zum ersten vollständigen Feature
   ergänzen (7–8); Sprachausgabe kann nach Schritt 6 unabhängig von Schritt 7 entstehen.
6. Trainings-Replay als eigenen anschließenden Schnitt umsetzen (9).
7. Dokumentation und Verifikation pro Schnitt nachführen (10).

Erledigte Checkboxen erst nach Implementierung und der jeweiligen Abnahme markieren.
Offene Produktentscheidungen und technische Abweichungen direkt in diesem Plan nachführen.
