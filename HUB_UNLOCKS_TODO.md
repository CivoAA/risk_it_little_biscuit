# Unlocks im Hub - offener Rest

Notiz für den nächsten, der die **Achievements in den Hub** baut.
Stand: 18.09.2026, Branch `unlock_canvis`.

## Was schon fertig ist

`Assets/Scripts/Unlocks/UI/UnlockPanel.cs` - die Unlock-Anzeige, neu gebaut im
Stil des neuen Achievement-Systems:

- baut sich komplett per Code auf (kein Szenenobjekt, kein Prefab, kein
  Inspector-Gefummel), genau wie `AchievementPanel`
- Inhalt und Reihenfolge kommen aus dem Katalog `Unlocks.cs`
- Aufruf von überall: `UnlockPanel.Open()` / `Close()` / `Toggle()`,
  `UnlockPanel.IsOpen`
- gleiche Aufteilung wie in der World Map: links das Gitter aus Symbolen, rechts
  Name und Beschreibung des angeklickten Unlocks. Gesperrt = ausgegraut, Name
  "???", keine Beschreibung
- hängt an den Ereignissen `Unlocks.Granted`, `Achievements.Unlocked` und
  `Loc.LanguageChanged`, aktualisiert sich also von selbst
- sperrt den Hub, solange es offen ist (`HubUI.PushModal` + Spieler einfrieren) -
  aber nur, wenn wirklich ein `HubUI` in der Szene ist. Im Hauptmenü und im Spiel
  legt es keines an
- Escape und der BACK-Knopf machen zu

Anbindung ans Achievement-System: `Ach.FindByUnlock(unlockId)` (neu in
`Ach.cs`) liefert das Achievement, das einen Unlock mitvergibt. Die Detailspalte
zeigt daraus die Herkunftszeile - bei einem Achievement mit Zähler auch den
Stand (`37 / 100`). Ein verstecktes Achievement bleibt dabei verdeckt.

Neue Loc-Keys in `en.json` / `de.json`: `ui.unlocks.title`, `.close`,
`.counter`, `.empty`, `.from`, `.hint`.

## Was noch fehlt: der Einstieg im Hub

Das Fenster hängt im Hub **an keinem Objekt** - das war so gewollt, weil es zu
den Achievements soll und die es im Hub noch nicht gibt.

Wenn du die Achievements in den Hub baust:

1. Im Achievement-Fenster des Hubs einen Knopf "UNLOCKS" (oder einen Reiter)
   einbauen, der `UnlockPanel.Toggle()` ruft. Mehr ist nicht nötig - das Panel
   baut sich selbst.
2. An der Szene `hub.unity` muss dafür **nichts** geändert werden. Bitte auch
   nicht: Inhalte gehören laut Remaster-Regel in Code-Kataloge, nicht ins
   Szenen-YAML.
3. `UnlockPanel` liegt auf `sortingOrder = 150`. Die Ebenen im Hub: Textbox 100,
   Konsole 120, Shop 130, Levelauswahl 135, Skilltree 140, `AchievementPanel`
   100. Baust du die Hub-Achievements höher als 150, zieh das Panel mit hoch,
   sonst verschwindet es dahinter.
4. Ob die Unlocks ein eigenes Fenster bleiben oder als zweite Spalte im
   Achievement-Fenster landen, ist noch offen. Als eigenes Fenster ist es
   fertig; für eine Spalte kann man `BuildGrid` und `BuildDetail` aus
   `UnlockPanel` übernehmen, die hängen an nichts.

## Zum Anschauen (bis es den Knopf gibt)

Im Hub das Terminal aufmachen und `unlocks` eingeben - versteckter Befehl in
`HubConsoleCheats.cs`, macht nur das Panel auf. Kann raus, sobald der richtige
Einstieg steht. Zum Testen mit Inhalt: `alleswirdgut` (alles frei) oder
`freischalten unlock_boba_gun` (einzeln).

## Nebenbei

- `UnlockUIManager.cs` + der `UnlocksCanvas` in `World Map.unity` bleiben
  unangetastet, damit die alte Szene weiter läuft. Beides kann weg, wenn die
  World Map weg ist.
- Noch kein einziges Achievement setzt `grantsUnlock:`. Deshalb zeigt fast jeder
  gesperrte Unlock nur den allgemeinen Hinweis (`ui.unlocks.hint`). Sobald ein
  Achievement in `Ach.cs` ein `grantsUnlock: "unlock_..."` bekommt, erscheint in
  der Unlock-Anzeige automatisch die Bedingung, und `Achievements` vergibt den
  Unlock beim Freischalten mit.
- Der Katalog-Check (`Tools -> Unlocks -> Katalog prüfen`) warnt weiterhin über
  Unlocks, die kein Shop-Eintrag verlangt - das ist unverändert und hat mit dem
  Panel nichts zu tun.
