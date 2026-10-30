# FoosVision Training – Umsetzungsplan

Stand: 2026-10-30. Status: Arbeitspakete 1–3 implementiert und geprüft.
Arbeitspakete 4–11 sind offen; Training ist noch nicht in die App-Laufzeit integriert.

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
  Ansage und Auslösung sowie Abbruch/erneute Bereitschaft. Snake (`snakeShot`) erkennt
  ausschließlich den Setup-Bereich mit 1000 ms Vorbereitungsdelay, ohne Ruhe-/Pendelerkennung.
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

Für Arbeitspaket 3 am 2026-10-30 mit dem Nutzer konkretisiert. Diese Entscheidungen
ersetzen die früher vorgesehenen Ruhe-/Pendelheuristiken und Replay-Wiederverwendung.

### Bestätigte Erkennungsregeln

- **Beide Teams:** Eigene Dreier-/Fünferreihe und Angriffsrichtung aus Tischgeometrie und
  Team ableiten. Prozentangaben quer zum Tor werden zwischen oberer und unterer Bande
  an der betreffenden Reihe berechnet, einschließlich perspektivischer Feldkanten.
- **Snake (`snakeShot`):** Mittlere 15 % der Spielfeldbreite, also 42,5–57,5 %.
  Richtung Zieltor gilt 20–35 % des Abstandes zwischen den Stangenmitten der eigenen
  Dreierreihe und der benachbarten gegnerischen Zweierreihe (0 % unter der Dreierreihe,
  100 % unter der Zweierreihe). Dreierbesitz erforderlich. 1000 ms durchgehend im Bereich
  abwarten, dann Ready; keine Unterscheidung zwischen Pendeln und Ruhe. Das Delay ist
  eine Code-Konstante. Zulässige Bewegung im Bereich startet den Timer nicht neu.
- **Zieher (`pullShot`):** Dreierbesitz reicht für die Position in Richtung Tor.
  Querposition für A 25–35 % von oben, für B gespiegelt 65–75 %. 1000 ms Ruhe;
  die bisherige Toleranz gilt: jeder Positionswert höchstens `dx² + dy² <= 50` vom
  Mittelwert des Vorbereitungsfensters entfernt (rund 7,1 Pixel). Nach Ready wird
  Bewegung gegen diese Ruheposition bewertet.
- **Tick-Tack (`tickTackPass`):** Fünferbesitz und Querposition 0–25 % oder 75–100 %,
  für beide Teams und beide Banden. Zwei Richtungswechsel bestätigen die Vorbereitung.
  Position in Richtung Tor ist für die Vorbereitung nicht eingeschränkt. Als benannte
  V1-Code-Konstante zählen Richtungswechsel erst ab 5 Pixeln vom letzten Extrempunkt;
  dadurch zählt Trackingrauschen nicht als Tick-Tack.
- **Schussabschluss bei Snake/Pull:** Highspeed ab 4 km/h auf mindestens einer Achse und
  beobachteter Ball auf der Zweierreihe des Zieltores. Beide Fakten gehören zur aktuellen
  Bewegung, ihre Reihenfolge ist beliebig. Danach 250 ms Nachlauf anhand der Frame-Zeit,
  dann einmaliges Abschlussereignis. Keine Bewertung der angesagten Variante oder eines Tores.
- **Passabschluss:** Ball auf eigener Dreierreihe oder in gegnerischer Defense bestätigt
  den Pass. Seitliches Verlassen des Setup-Bereichs bei weiterem eigenen Fünferbesitz
  oder Bewegung entgegen der Angriffsrichtung führt zum Abbruch.
- **Entscheidungsfenster nach Setup-Verlust:** Nicht im ersten Bewegungsframe abbrechen.
  V1 verwendet vorläufig 750 ms als benannte Code-Konstante, um Schuss-/Passabschluss
  abzuwarten. Ohne Bestätigung folgt Aborted. Ein bereits bestätigter Schuss darf seinen
  250-ms-Nachlauf abschließen. Dies ist ein Auswertungsfenster, keine weitere Trackingglättung.
- **Anchor:** Letzten gültigen Setup-Frame samt Ballposition und Besitz vor der Bewegung
  festhalten. Eigene Training-Verträge; die bestehenden Game-Replay-Strategien und deren
  Parameter bleiben unverändert. Keine Extraktion von ReplayDecision aus Game für Training.

### Bestätigter Ablauf und Optionen

- **Bedienung/Ausgabe:** Dritter Start-/Stop-Button folgt später; alle Einstellungen per
  Recorder-JSON. Nur der Recorder spricht. Ansagen sind korrelierte Usecase-Ausgaben ohne
  Audio-Rückmeldung; Sprachausgabe startet oder verlängert keine fachlichen Timer.
- **Wartezeit:** Ready und Start der zufälligen Wartezeit im selben Übergang, nach dem
  Vorbereitungsdelay. Je Strategie einmal pro Versuch ziehen. Startwerte: Tick-Tack
  1–10 Sekunden, Snake 1–12 Sekunden, Pull 1–10 Sekunden; zunächst `uniform`.
  Unterstützte Verteilungen für gleichverteiltes `u` in [0,1): `uniform = u`,
  `preferLong = sqrt(u)`, `preferShort = 1 - sqrt(u)`; Zeit = min + (max - min) * Anteil.
  Min = max ergibt feste Wartezeit. Keine zusätzlichen Bias-Parameter.
- **Varianten:** Snake: Push/Pull/Middle; Pull: Short/Middle/Long; Tick-Tack: Wall/Lane/Split.
  Gleichverteilte zufällige Auswahl, direkte Wiederholungen erlaubt. Stabile IDs und
  sprechbare Texte im Core; keine Erfolgsprüfung der angesagten Variante.
- **Timeout:** Nach Variantenansage 3 Sekunden bis Bewegungsbeginn. Rechtzeitig gestartete
  Bewegungen dürfen ihr Entscheidungsfenster und gegebenenfalls den Nachlauf abschließen.
- **Vorzeitige Ausführung/Setup-Verlust:** Einmal Aborted mit intern getrennten Gründen;
  keine Variantenansage mehr für diesen Versuch. Alte Ansagen invalidieren, dabei die
  eigene Aborted-Ausgabe gültig lassen. Nächstes Ready invalidiert auch alte Aborted-Ausgaben.
- **Erneuter Versuch:** Vollständige neue Vorbereitung, frische Erkennungshistorien aller
  Strategien. Unverändert liegender Snake/Pull nach Timeout erzeugt kein weiteres Ready.
  Ein bereits beobachteter Austritt bzw. eine neue Pull-Ruheposition reicht zum erneuten
  Ansetzen; kein zusätzlicher künstlicher Außen-Frame nach einem abgeschlossenen Schuss.
- **Kombinationen:** Alle aktivierten Strategien suchen parallel anhand desselben Snapshots;
  erste erkannte Strategie übernimmt. Keine besondere Mehrdeutigkeitsbehandlung und keine
  zusätzliche Zufallsziehung zur Priorisierung. Während des Versuchs keine Konkurrenz.
- **Tracking/Zeit:** Vorhandene Glättung und Verlustphasen von BallTracking/LiveTracking
  verwenden. Keine zusätzliche Verdeckungstoleranz oder pauschale Zeitlückenregel.
  Keine Ansagen ohne neue Frames, keine Variantenansage ohne beobachteten Ball;
  endgültiger Trackingverlust führt zum Abbruch. Doppelte/rückläufige Frame-Zeit ignorieren.
- **Geometrieänderung:** Kleine laufende Korrekturen normal übernehmen; weder Versuch noch
  Erkennungshistorie deswegen zurücksetzen. Keine zusätzliche Änderungserkennung.
- **Protokoll:** Protokoll- und Major-Version bleiben für diese Prototyp-Erweiterung
  unverändert. Spätere Protokolländerungen trotzdem dokumentieren; beide Rollen mit
  demselben aktualisierten Build prüfen. Keine Ansage-Protokollnachrichten.
- **Replay:** Technische Ausführung erst in Arbeitspaket 9. Wiederholung bis zur nächsten
  bestätigten Vorbereitung/Ready; Ballbewegung nach einem Pass beendet die Schleife nicht.

### Erst für spätere Arbeitspakete offen

- [ ] Replay-Clip-Vor-/Nachlauf, Geschwindigkeit und Replay bei abgebrochenen Versuchen
  für Arbeitspaket 9 konkretisieren. Der fachliche Schussnachlauf von 250 ms und der
  Setup-Anchor aus Arbeitspaket 3 ersetzen noch keine Clip-/Playback-Konfiguration.

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

- [x] Aktualisierungsregeln aus GameSession als kleinen gemeinsamen Domain-Baustein
  extrahieren: Intervalle, Konfidenz/Bewegung, gegenseitige Sperre laufender Updates
  und Abschluss. Instanz gehört jeweils zur aktiven Session.
- [x] Bestehende `UseCases/Calibration` weiterverwenden. Session-spezifische
  CompleteTableUpdate-/CompleteTableSceneUpdate-Usecases bleiben dünne Einstiegspunkte.
- [x] `UseCases/Game/Ports/IFrameVisionOps` in einen passend benannten gemeinsamen
  Usecase-Bereich verschieben, sobald beide Modi ihn nutzen. Keine Puffer in den Core ziehen.
- [x] Recorder `Game/Live/FrameVisionOps`, `Game/Orchestration/CalibrationCoordinator`,
  Vision-Ops und `Game/VisionContextUpdatePublisher` auf tatsächliche Game-Kopplung prüfen.
  Gemeinsame Ausführung in Recorder-Live-/Calibration-Bereiche verschieben; kleine
  session-spezifische Rückmeldungen über schmale Schnittstellen anschließen.
- [x] `FrameProcessingLoop`, BallFinder, Masken, Tracking-Nachrichten und Frame-Metriken
  wiederverwenden. Training bekommt eigene Frame-Input-/Output-Anbindung und Presenter;
  gemeinsame Mapping- und Publikationsarbeit wird extrahiert, nicht die gesamte Game-Klasse kopiert.
- [x] Fehler und Abbruch einer Kalibrierung lösen In-Progress-Zustände zuverlässig auf.
  Späte Ergebnisse einer alten Session dürfen keine neue Session aktualisieren.

Tests: Kalibrierungsanforderungen, Abschluss/Fehler/Stop, Vision-Kontext und gemeinsame
Live-Publikation in bestehenden Domain-, Usecase- und Recorder-Adapter-Testprojekten.

Abnahme: Game läuft weiterhin unverändert; gemeinsame Bausteine hängen nicht von GameSession ab.

Umgesetzt am 2026-10-30:

- `CalibrationCore/Services/TableUpdateSchedule` besitzt pro aktiver Session
  die bisherigen Aktualisierungsintervalle, Konfidenz-/Bewegungsregeln, gegenseitige
  Sperre und Abschlussoperationen. Game delegiert diese Regeln; Priorität und Grenzwerte
  bleiben erhalten. Der Besitzer serialisiert die Zugriffe wie bisher in `GameSession`.
- Recorder `Calibration` führt Tisch-/Szenenupdates mit den bestehenden Calibration-
  Usecases und Vision-Ports aus. `CalibrationSession` liefert schmale, pro Anforderung
  erfasste Rückmeldungen und eine Gültigkeitsprüfung statt einer GameSession-Abhängigkeit.
  Fehlende Frames, Fehler und verworfene Ergebnisse lösen den Abschluss aus; gepoolte
  Frames werden freigegeben. Ergebnisse nach Stop/Session-Wechsel werden verworfen,
  einschließlich einer während der Erkennung veralteten Tischkonfiguration vor dem Speichern.
  Die dünnen Game-Abschluss-Usecases prüfen zusätzlich die anfordernde Session-ID.
- Recorder `Live` enthält `FrameVisionOps`, `VisionContextUpdatePublisher` und
  `LiveFramePublisher` mit neutralen Frame-Fakten, Tracking-Mapping und Maskenpublikation.
  Game behält seinen Presenter und liefert Zeitfoul explizit; Replay bleibt unverändert
  Game-spezifisch. FrameProcessingLoop, BallFinder und vorhandene Frame-Metriken werden
  weiterverwendet; keine Kopie der Game-Frame-Verarbeitung wurde eingeführt.
- Die bedingte Verschiebung von `IFrameVisionOps` ist noch nicht fällig: Es gibt weiterhin
  nur Game als konsumierenden Modus. Der Port bleibt bis zur tatsächlichen zweiten Nutzung
  unter `UseCases/Game/Ports`. Eigene Training-Input-/Output-Ports und Presenter folgen
  mit den Trainings-Usecases bzw. ihrer Recorder-Anbindung in Arbeitspaket 3 und 6.
- Keine neuen Projektabhängigkeiten, Protokoll- oder Konfigurationsänderungen.
  Die Korrekturen bei fehlenden Frames und veralteten Kalibrierungsergebnissen stehen
  in den Release-Notes; weitere Trainingsfunktionen wurden nicht implementiert.

## 3. Training-Domain und Usecases für den ersten Versuch implementieren

Abhängigkeit: Schritte 0–2.

- [x] Eigenständige TrainingSession mit Session-ID, fortlaufender Versuch-ID,
  autoritativem Zustand und unveränderlichem Session-Snapshot der Optionen implementieren.
- [x] Alten SequenceRecognizer, ISequenceRecognizer, Zustandstypen und Prototyp-Tests
  nach Referenzprüfung entfernen; durch neue Strategietests ersetzen.
- [x] Schmalen ITrainingStrategy-Vertrag mit Tracking-Fakten, Vorbereitungs-/Ausführungs-
  ergebnissen, Team/Reihe, Frame-Zeit und Training-Anchor definieren. Keine JSON-,
  Protokoll-, Frame-Puffer- oder Audio-Typen in der Training-Domain.
- [x] Zustandsbesitz trennen: TrainingSession verwaltet IDs, Wait-/Ansage-/Timeout-Lifecycle;
  Strategien besitzen Erkennungshistorie, Vorbereitung und Ausführungsregeln. Kein Switch
  auf Strategie-IDs pro Frame; Erzeugung über eine Factory beim Session-Start.
- [x] Mehrere aktivierte Strategien koordinieren: denselben Snapshot auswerten, erste
  erkannte Vorbereitung wählen, Optionen/Team/Reihe pro Versuch halten, Konkurrenz
  während des Versuchs ausschließen und Historien danach zurücksetzen.
- [x] SnakeShotTrainingStrategy, PullShotTrainingStrategy und
  TickTackPassTrainingStrategy gemäß den vereinfachten Regeln aus Abschnitt 0 implementieren.
- [x] Ablauf Vorbereitung → Ready/Wartezeit → Variantenansage → Ausführung/Auswertung
  → Abschluss oder Aborted → vollständige neue Vorbereitung implementieren.
- [x] Ansagen mit Session/Versuch/Ansage-ID korrelieren. Invalidation bei Abbruch und
  beim nächsten Ready ausgeben; eigene Aborted-Ausgabe nicht pauschal verwerfen.
- [x] Setup-Verlust und vorzeitige Ausführung mit getrennten Abbruchgründen behandeln;
  Timeout und endgültigen Trackingverlust berücksichtigen. Keine Audiofehler als Zustand.
- [x] Kurzes Auswertungsfenster statt sofortigem Abort, 4-km/h-Schussregel mit Ziel-Zweier,
  Pass-Besitzregeln und 250-ms-Schussnachlauf ergänzen. Anchor vor Bewegung festhalten;
  keine Game-Replay-Strategien ändern und keinen Replay-Transport implementieren.
- [x] Wartezeitverteilungen und gleichverteilte strategieeigene Varianten über injizierbare
  Zufallsquelle deterministisch auswählen; eine Wait-Ziehung pro Ready.
- [x] Stop setzt Strategien zurück und invalidiert Ansagen. Neue Session erhält frische
  Tracking-/Strategieinstanzen. Kleine Geometrieupdates ändern den Versuch nicht.
- [x] Explizite monotone Frame-Zeit für alle Fristen verwenden; keine Tasks/Wall-Clock-Timer
  in der Domain, keine Audio-Rückmeldung für Zustandsfortschritt.
- [x] StartTraining, StopTraining, ProcessFrame und Kalibrierungsabschlüsse mit eigenen
  Ports und TrainingSessionStore-Vertrag ergänzen. Start verlangt gültiges Setup;
  Fehler/Cancellation räumen teilweise gestartete Ressourcen auf.
- [x] IFrameVisionOps bei tatsächlicher zweiter Nutzung nach UseCases/Live/Ports verschieben;
  Game und vorhandene Recorder-Implementierung lediglich auf den gemeinsamen Import umstellen.

Umgesetzt am 2026-10-30:

- TrainingFrameResult liefert Tracking-Fakten, aktuellen Zustand/Versuch/Variante,
  Kalibrierungsanforderungen und einmalige fachliche Ereignisse. Recorder-Presenter und
  Laufzeitverdrahtung folgen erst in Arbeitspaket 6.
- ProcessFrame übersetzt das Domain-Ergebnis in einen eigenen Usecase-Response mit
  gruppierten Tracking-/Kalibrierungsdaten, Trainingsstatus und optionalen Ausgaben für
  Vorbereitung, Ansage, Invalidation, Abschluss und Abbruch. Wenige konkrete Records
  halten die jeweils zugehörigen Daten zusammen; fehlende Ausgaben sind null.
  ProcessFrame und Stop befüllen ihre Ausgaben direkt im Interactor, ohne Ereignishierarchie
  oder Mapper. Invalidation wird vor Statusmeldungen und der neuen Ansage angewendet;
  der zuvor invalidierte Versuch bleibt separat korreliert. Trainingsverhalten bleibt erhalten.
- TrainingOptions validiert Aktivierung und Wartegrenzen und hält eine unveränderliche
  Kopie der aktivierten Menge. ITrainingOptionsProvider ist der schmale spätere Mapping-Port;
  JSON-Konfiguration/Settings-Mapping aus Arbeitspaket 4 sind noch nicht implementiert.
- ProcessFrame und Kalibrierungsabschlüsse prüfen Session-IDs; während Vision gewechselte
  Sessions werden verworfen. Stop beendet die Domain-Session vor dem Frame-Source-Shutdown.
- Keine neuen Projekte/Projektabhängigkeiten, Protokoll-, JSON-, App- oder Audioänderungen.
  Game einschließlich ReplayDecision bleibt fachlich unverändert. Kein Release-Note-Eintrag,
  da dieser Core-Schnitt noch keine sichtbare Trainingsfunktion ausliefert.

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
   Arbeitspakete 1 und 2 abgeschlossen; die bedingte spätere Portverschiebung
   ist bei Arbeitspaket 2 dokumentiert.
3. Strategie-Vertrag, Snake, Zieher, Tick-Tack Pass und Koordination
   implementiert (3). JSON-Konfiguration und Mapping folgen als Arbeitspaket 4.
4. Protokoll und Recorder-Laufzeit anschließen (5–6).
5. Viewer-Bedienung und lokale Recorder-Sprachausgabe zum ersten vollständigen Feature
   ergänzen (7–8); Sprachausgabe kann nach Schritt 6 unabhängig von Schritt 7 entstehen.
6. Trainings-Replay als eigenen anschließenden Schnitt umsetzen (9).
7. Dokumentation und Verifikation pro Schnitt nachführen (10).

Erledigte Checkboxen erst nach Implementierung und der jeweiligen Abnahme markieren.
Offene Produktentscheidungen und technische Abweichungen direkt in diesem Plan nachführen.
