# Story – „Die Uhr, die nicht klingelte"

Stand: 2026-10-12. Löst die alte Fassung (Mutter Hefe / Glasur) ab, die noch für
die damaligen vier Welten geschrieben war. Alles hier ist eingebaut, der Code
steckt in `Assets/Scripts/Story/`, die Texte in `de.json` / `en.json` (`story.*`).

---

## Kurzfassung

> Seit die Großen fort sind, gehört die Küche uns: Arbeitsplatte, Kräuterwald,
> Gefrierfach und die dunkle Speisekammer. Tief darunter, im Vulkan, steht der
> **Große Ofen**.
>
> Vor hundert Jahren rutschte dort ein Keks hinters Blech. Die Küchenuhr, die
> nach zwölf Minuten hätte klingeln sollen, war eingeschlafen. Die anderen Kekse
> holte jemand trotzdem heraus – ihn nicht. Er glühte hundert Jahre lang und
> wurde zum **Verkohlten**.
>
> Jetzt heizt er den Ofen an, bis die ganze Küche so schwarz ist wie er. Sein
> glühendes Herz hat er in vier **Glutsplitter** zerbrochen und vier Herrschern
> gegeben – die drehen seitdem durch.
>
> **Pomo**, eine alte Tomaten-Küchenuhr, weckt den mutigsten Krümel, den sie
> finden kann: Hol die vier Splitter. Mit allen vier geht die Ofentür auf – sie
> erkennt nur sein Herz.
>
> Der Twist: Pomo *war* die Uhr. Im Geisterwald kommt es heraus. Am Ende, als
> das Herz zerbricht, klingelt sie – zum ersten Mal seit hundert Jahren. Der
> Verkohlte ist **fertig gebacken**.

**Der Titel als Motto:** Pomos letzter Satz im Intro und im Epilog –
*Risk it, little biscuit.* / *Danke, dass du's riskiert hast.*

**Warum der Timer im HUD?** Die Uhr oben im Bild ist Pomo. Sie zählt mit, nach
zwanzig Minuten kommt der Boss.

---

## Die Welten (jede ist ein Teil der Küche)

| Karte | Welt in der Story | Herrscher mit Splitter | Was der Splitter mit ihm macht |
|---|---|---|---|
| 1 Wald (World2) | der Kräuterwald hinterm Brotkasten | **Glutwurz** | steckt in den Wurzeln, sie spuckt Feuer – flüstert „Hüte dich vor der Uhr, die nicht klingelt" |
| 2 Küche (World1) | die Arbeitsplatte | **Keks-König** | früher eitel, aber harmlos – jetzt ein Auge, das überall Feinde sieht |
| 3 Eisgletscher (World5) | das Gefrierfach | **Eiskaiser** | schmilzt ihn von innen, darum friert er alles ein (Schmelz-Tod passt) |
| 4 Geisterwald (World6) | hinten in der Speisekammer, wo das Vergessene landet | **Das Gespenst / Squiddy** | macht das Traubengelee gruselig; Squiddy verrät, was der Verkohlte erzählt hat |
| 5 Vulkan (World4) | der Große Ofen | **Der Verkohlte** (Endboss, bei 20:00) | Herzkammer = das, was von seinem Herzen übrig ist |

**Feste Reihenfolge** (seit 12.10.2026, `StoryProgress.Route`): Wald → Küche →
Eis → Geisterwald → Vulkan. Jeder Splitter öffnet die nächste Welt – Story
*und* Endless; gesperrte Welten zeigen Schloss auf Station und Panorama. Die
Levelauswahl öffnet auf der neuesten offenen Welt.

---

## Ablauf

```
Hub, erster Besuch ── Intro-Cutscene (6 Bilder) ── Gespräch "intro"
        │
Levelauswahl, SPIELEN (Story) ── einmal je Welt: "brief.<Plan>"
        │
Lauf ── 2,5 s: Zwischenruf "start.<Plan>" (Pomo)
     ── Boss erscheint: Zwischenruf "boss.<Plan>"
     ── Boss fällt: "defeat.<Plan>" ── Splitter fliegt ein ("2 von 4") ── "count.N" ── Sieg-Bildschirm
        │
Hub ── "debrief.<Plan>" ── bei vier Splittern "allshards" + Banner DER VULKAN IST OFFEN
        │
Vulkan ── Herz zerfällt ── Finale-Cutscene (abkühlen, Gespräch, DRRRRRING!, zu Sternen, ENDE)
        │
Hub ── "epilogue"
```

**Endless ist der Story-Ausschalter:** kein einziges Wort, wie bisher.
Gewinnt man eine Welt erneut, deren Splitter man schon hat, gibt es den normalen Sieg.

---

## Figuren

**Pomo** – Tomaten-Küchenuhr, zweihundert Jahre alt, weiße Brauen, Zifferband
über der Stirn, Stiel als Aufziehknopf. Kurze Sätze, warm, ein bisschen
grantig, nennt dich „Krümel". Tickt nervös (`<shake>Tick-tick-tick.</shake>`),
wenn es um Uhren geht. Steht im Hub neben der Reisekarte ([E] = ein Satz
passend zum Stand). Ihre Stimme im Dialog: abwechselnd „tick" und „tack".

**Der Verkohlte** – kein Monster aus dem Nichts, sondern der vergessene Keks.
Wütend auf die Uhr, am Ende nur noch müde: „Hundert Jahre hab ich gezählt."

**Die vier Herrscher** sind Opfer, keine Schurken – jeder wird beim Sieg wieder
er selbst und sagt das in seinen letzten Zeilen.

---

## Technik

| Datei | Aufgabe |
|---|---|
| `StoryTexts.cs` | alle Gespräche (Sprecher, Stimmung, Key, deutscher Rückfall), Zwischenrufe, Bildunterschriften |
| `StoryProgress.cs` | Stand in `story.json` (Intro, Briefings, Splitter, Debriefs, Finale, Epilog); in `SaveReset` |
| `StoryDirector.cs` | Regie: Hub-Ereignisse, Lauf-Hooks (`Enemy.BossDefeated`, `SpawnDirector.BossSpawned`), Testtasten |
| `StoryDialog.cs` | Dialogbox UI 2.0: 2x-Portrait, Namensschild, Schreibmaschine, ESC überspringt |
| `StoryBark.cs` | Zwischenruf im Lauf, hält das Spiel nicht an |
| `StoryCutscene.cs` | Intro und Finale als Bilderbuch im Holzrahmen |
| `StoryReveal.cs` | Splitter-Einblendung, Banner |
| `StoryPomoNpc.cs` | Pomo im Hub |
| `StoryArt.cs` / `StorySounds.cs` | Bilder/Namen/Farben je Sprecher, Klänge im Code erzeugt |

Bilder: `Assets/Resources/Story/` aus `pomo.py` und `story_art.py` im
Artwork-Ordner. Intro-Musik: `Resources/Music/story_intro.wav` aus
`intro_musik.py` (Spieluhr, d-Moll → D-Dur, Ticken), `StoryMusic.cs` blendet
Hub-Musik aus und am Ende wieder von vorn ein. Die Spielfigur erscheint in
Dialog und Cutscene über einen unsichtbaren Animator (`StoryArt.PlayerFrame`),
nicht über die Keksdosen-Portraits mit Hintergrund. Text-Markup: `<wave> <shake> <hot> <ice> <jam> <gold>`
(`DialogueText`).

Die Story übernimmt das Ende eines Laufs (`StoryDirector.ClaimsWin`): dann kein
Finisher, kein Tod nach dem Boss, und die Herzkammer des Verkohlten wartet aufs Finale.

### Testen

| Taste / Befehl | Wirkung (nur Editor + Development-Build) |
|---|---|
| **F12** im Lauf | Level sofort gewonnen – Boss gilt als besiegt, das ganze Story-Ende läuft ab |
| **F12** im Hub | nächsten Splitter schenken, das Gespräch danach kommt sofort |
| **Shift+F12** | Story komplett zurücksetzen (Intro kommt wieder) |
| **F11** im Hub | Intro-Cutscene noch einmal |
| Konsole `story` | Stand anzeigen; `story reset`; `story kapitel 0-4` |
