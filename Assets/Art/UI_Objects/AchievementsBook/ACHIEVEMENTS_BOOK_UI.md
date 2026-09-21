# Erfolge- / Unlocks-Buch — Pixel-UI

Aufgeschlagenes Kochbuch, das in der gelben Kapselflüssigkeit schwebt. Geht auf,
wenn der Spieler an der gelben Kapsel mit dem Buch **E** drückt.

Native Auflösung **320x180**, im Spiel 4x hochskaliert. Alle Maße in dieser Datei
sind echte Pixel auf 320x180-Basis, Ursprung **oben links**.

**In den Sprites steht kein einziger Buchstabe.** Alle Beschriftungen, Titel und
Fortschrittszahlen kommen in Unity als TextMeshPro darüber. Die dafür frei
gelassenen Flächen stehen unten in der Textzonen-Tabelle.

![Erfolge](preview_filled.png)

![Unlocks](preview_filled_unlocks.png)

---

## 1. Dateien

```
Assets/Art/UI_Objects/AchievementsBook/
  achievements_book.aseprite      Quelldatei: 13 Layer, 2 Frames,
                                  34 Slices (25 Elemente + 9 Textzonen)
  achievements_book.gpl           Palette (14 Kern- + 5 Akzentfarben)
  preview_page_1.png              Mockup Zustand A (Reiter 1 aktiv)
  preview_page_2.png              Mockup Zustand B (Reiter 2 aktiv, Kachelgitter)
  preview_filled.png              Mockup Erfolge mit echten Icons (Referenz)
  preview_filled_unlocks.png      Mockup Unlocks mit echten Icons (Referenz)
  ACHIEVEMENTS_BOOK_UI.md         diese Datei
  atlas/achievements_book_atlas.png   512x368 Sprite-Sheet
  atlas/achievements_book_atlas.json  JSON Hash inkl. 9-Slice-Centern

Assets/Resources/AchievementsBook/
  ui/                             25 Einzel-PNGs, transparent, 1:1, je mit .meta
  <iconKey>_12/_21/_32.png        84 umgefärbte Icons (siehe Abschnitt 8)

Assets/Art/World-Objects/
  kapsel_erfolgsbuch.png          32x48, PPU 32 - das Weltobjekt (Abschnitt 10)

Assets/Scripts/Achievements/
  UI/AchievementsBookPanel.cs     das Fenster, baut sich per Code auf
  AchievementsBookTrigger.cs      [E] an der Kapsel
  Editor/AchievementsBookSceneTools.cs   Menü zum Setzen des Objekts
```

> Die Runtime-Sprites liegen unter **Resources**, weil sich das Fenster wie
> `AchievementPanel` und `UnlockPanel` komplett per Code aufbaut und deshalb
> nichts über den Inspector verdrahtet bekommt. Die .aseprite-Quelle, die
> Vorschauen und der Atlas bleiben im Art-Ordner.

### Sprite-Liste

| Sprite | Größe | Quelle im .aseprite (Layer @ Frame) |
|---|---|---|
| `bg_capsule` | 320x180 | `bg_capsule` @1 |
| `bubble_01` … `bubble_08` | 6, 8, 8, 10, 12, 12, 14, 16 (quadratisch) | `bg_bubbles` @1 |
| `book_panel` | 290x144 | `book_shadow` + `book_panel` @1 |
| `book_spine` | 8x138 | `book_spine` @1 |
| `tab_active` | 60x14 | `tabs` @1 |
| `tab_inactive` | 60x14 | `tabs` @1 |
| `row_normal` | 132x21 | `rows` @1 (vollständig transparent) |
| `row_selected` | 132x21 | `rows` @1 (nur Rahmen, siehe 5.3) |
| `row_done` | 132x21 | `rows` @1 (goldene Fläche) |
| `row_hover` | 132x21 | `rows` @1 (dünner Rahmen, Maus liegt drauf) |
| `progress_cell_full` | 9x4 | `progress_bar` @1 |
| `progress_cell_empty` | 9x4 | `progress_bar` @1 |
| `slot_small_unlocked` | **21x21** | `slots_small` @1 — der Slot in der Listenzeile |
| `slot_small_locked` | **21x21** | `slots_small` @1 |
| `slot_large_unlocked` | **32x32** | `detail` @1 — der Slot auf der Detailseite |
| `slot_large_locked` | **32x32** | `detail` **@2** |
| `reward_box` | 128x20 | `reward_box` @1 |
| `icon_locked` | 12x12 | `icon_locked` @1 |

> „small“ und „large“ sind relativ gemeint: der Listen-Slot ist 21x21, der
> Detail-Slot 32x32. Die Namen stammen aus der ersten Fassung und bleiben,
> damit bestehende Referenzen nicht brechen.

---

## 2. Palette

Kernpalette, exakt wie vorgegeben:

| Hex | Verwendung |
|---|---|
| `#f2dcbc` | Pergament, Buchseite, aktiver Reiter |
| `#e0c49f` | Belohnungs-Box, leere Fortschrittszelle |
| `#d9b189` | Seitenschatten unten, Buchrücken-Streifen, Trennlinie, gesperrter Slot |
| `#b99772` | Buchrücken-Kanten, Rahmen der Belohnungs-Box, gedämpfte Icons |
| `#8c5a3c` | Holz — Icon-Slot-Grundton, innerer Auswahlrahmen |
| `#6f4630` | Holz dunkel — inaktiver Reiter, Fließtext |
| `#5a3421` | Icon-Slot-Schatten, Haken auf der erledigten Zeile |
| `#4d2e1e` | Reiter-Schatten unten, Titel auf der erledigten Zeile |
| `#3b2b33` | Kontur, durchgehend 1px; äußerer Auswahlrahmen |
| `#b83a52` | gefüllte Fortschrittszelle |
| `#e8b93c` | Kapselgelb — Hintergrund, Belohnungs-Slot |
| `#c9922a` | Kapselrahmen, Schlagschatten des Buchs, Rahmen der erledigten Zeile |
| `#f7d76a` | Kapselgelb hell — Bänderung, Blasenfüllung, **Fläche „erledigt“** |
| `#fff4e0` | 1px Highlight oben (Seite, Reiter, Blase, erledigte Zeile) |

**Zusätzlich 5 Akzente**, weil die Element-Spezifikation sie namentlich verlangt,
sie aber nicht in der 14er-Liste stehen: `#c8a078` (Buchrücken), `#a87a52`
(Slot-Highlight), `#8a5a3d` (Reiter-Highlight), `#9c8268` (Slot gesperrt,
Kontur), `#d4566c` (Fortschrittszelle, Highlight).

Beide Gruppen liegen in `achievements_book.gpl` und in der Sprite-Palette der
.aseprite-Datei (Index 1–14 Kern, 15–19 Akzent, Index 0 transparent).

---

## 3. Seitenaufbau (320x180)

| Element | x | y | b | h |
|---|---|---|---|---|
| Kapselhintergrund | 0 | 0 | 320 | 180 |
| Kapsel-Innenfläche (hinter dem Rahmen) | 5 | 5 | 310 | 170 |
| Reiter 1 | 16 | 9 | 60 | 14 |
| Reiter 2 | 78 | 9 | 60 | 14 |
| Zurück-Knopf | 251 | 9 | 52 | 14 |
| Buchseite (Korpus) | 16 | 23 | 288 | 142 |
| Buchseite inkl. Schlagschatten (= `book_panel`) | 16 | 23 | 290 | 144 |
| Buchrücken | 156 | 25 | 8 | 138 |
| linke Seite, Innenfläche | 17 | 25 | 139 | 137 |
| rechte Seite, Innenfläche | 164 | 25 | 139 | 137 |
| Fortschrittsleiste (10 Zellen) | 20 | 29 | 99 | 4 |
| Zähler neben der Leiste | 121 | 26 | 34 | 10 |
| Listen-Viewport (Erfolge) | 20 | 36 | 132 | 126 |
| Kachelgitter (Unlocks), gleiches Sichtfenster | 20 | 36 | 132 | 126 |
| großer Icon-Rahmen | 217 | 32 | 32 | 32 |
| Trennlinie `#d9b189` | 168 | 132 | 131 | 1 |
| Belohnungs-Box | 169 | 138 | 128 | 20 |

**Sichere Innenfläche des Buchs:** y = 25..161. Zeile 24 ist das 1px-Highlight,
Zeile 162/163 der 2px-Schatten, Zeile 164 die Kontur.

Der aktive Reiter hängt an der Seite: unter ihm sind die Konturzeile (y=23) und
die Highlightzeile (y=24) der Buchseite zugemalt. Das passiert im `tabs`-Layer,
damit `book_panel` als 9-Slice eine geschlossene Oberkante behält.

---

## 4. Textzonen

Alle Angaben in 320x180-Pixeln. „max. Zeichen“ gilt für einen **5x7-Font mit 1px
Laufweite** (6px Vorschub je Glyphe, also `floor((b+1)/6)`).

| Name | x | y | b | h | Ausrichtung | Zeilen | max. Zeichen |
|---|---|---|---|---|---|---|---|
| `text_tab_1` („ERFOLGE“) | 18 | 11 | 56 | 10 | zentriert | 1 | 9 |
| `text_tab_2` („UNLOCKS“) | 80 | 11 | 56 | 10 | zentriert | 1 | 9 |
| `text_back` („ZURUECK“) | 253 | 11 | 48 | 10 | zentriert | 1 | 8 |
| `text_counter` („7 / 41“) | 121 | 26 | 34 | 10 | rechts | 1 | 5 |
| `text_row_title` | 45 | 37 | 94 | 11 | links | 1 | 15 |
| `text_row_progress` | 45 | 47 | 94 | 9 | links | 1 | 15 |
| `text_detail_title` | 168 | 67 | 131 | 14 | zentriert | 1 | 22 |
| `text_detail_desc` | 168 | 84 | 131 | 34 | links | 3 | 22 je Zeile (66) |
| `text_detail_progress` | 168 | 120 | 131 | 9 | zentriert | 1 | 22 |
| `text_reward` | 189 | 141 | 104 | 14 | links | 2 | 17 je Zeile |

`text_row_title` / `text_row_progress` stehen hier für **Zeile 0**. Zeilen-lokal
sind sie `(25, 1, 94, 11)` und `(25, 11, 94, 9)`; die globale y ergibt sich aus
`36 + Zeilenindex * 21`.

> **Wichtig und schon einmal reingefallen:** die Höhe einer Textzone muss
> **größer sein als die Schriftgröße**. Ist das Rechteck zu flach, wirft
> TextMeshPro die Zeile still weg und es steht gar nichts da — genau deshalb
> fehlten in der ersten Fassung die Titel in der Liste (7px Rechteck bei
> Schriftgröße 8). Alle Zonen oben haben jetzt Luft.

**Schriftgrößen** in der Projektkonvention: Titel `12`, Fließtext `8`,
Kleintext `6`.

> **Umlaute:** `PixelUI.FindPixelFont()` liefert ThaleahFat, und ThaleahFat kann
> keine Umlaute. Das Panel sucht darum zuerst **Jersey10** und fällt nur zurück,
> wenn die nicht geladen ist.

---

## 5. Elemente im Detail

### 5.1 Hintergrund und Blasen

- `bg_capsule`: waagrechte Bänder à 8px, abwechselnd `#e8b93c` / `#f7d76a`, an
  den Bandkanten ein sparsames Schachbrett-Dither. Rahmen außen 4px `#c9922a`,
  direkt innen 1px Kontur `#3b2b33`. Als **9-Slice** (Rand 5) eingebunden, damit
  der Rahmen bei jedem Seitenverhältnis am Bildschirmrand klebt.
- `bubble_01..08`: **runde** Blasen, 6 bis 16px Durchmesser. 1px Kontur
  `#3b2b33`, Füllung `#f7d76a`, Glanzpunkt `#fff4e0` oben links, abgedunkelte
  Innenkante `#c9922a` unten. Harte Kanten, kein Anti-Aliasing — die Rundung
  entsteht nur über den Radius.
- **Bewegung:** Die Blasen steigen ausschließlich in den Streifen **links und
  rechts neben dem Buch** auf, nicht über die ganze Breite. In der Mitte wären
  sie hinter dem Buch und würden bloß am Buchrand auftauchen und wieder
  verschwinden — das sah unlogisch aus. Große Blasen sind langsamer (5 px/s)
  als kleine (11 px/s), dazu eine leichte Sinus-Drift von ±1–2px.
- **Zur Laufzeit laufen nur `bubble_01..04`** (6 bis 10px). Bei 320px Breite ist
  so ein Seitenstreifen nur 11px schmal — eine 16er Blase würde über den
  Kapselrahmen laufen. Die Startposition rechnet mit dem Radius, damit keine
  Blase den Rahmen oder die Buchkante überläuft. `bubble_05..08` bleiben
  exportiert und sind für breitere Seitenverhältnisse da; im Mockup liegen sie
  nur im freien Stück der Reiterzeile, damit man sie ausschneiden kann.

### 5.2 Reiter

| Zustand | Aufbau |
|---|---|
| `tab_active` | Füllung `#f2dcbc`, Kontur `#3b2b33` oben/links/rechts, 1px Highlight `#fff4e0`, **unten offen** |
| `tab_inactive` | Füllung `#6f4630`, Kontur rundum, 1px Highlight `#8a5a3d` oben, 1px Schatten `#4d2e1e` unten |

Der **Zurück-Knopf** rechts in derselben Zeile benutzt dasselbe
`tab_inactive`-Sprite, nur als 9-Slice auf 52x14 gezogen: rechte Kante bündig
mit dem Buch, gleiche Höhe wie die Reiter. Beim Überfahren legt sich derselbe
`row_hover`-Rahmen darüber wie bei Zeilen und Kacheln. Beschriftung über den
vorhandenen Schlüssel `ui.achievements.close` — der steht schon in beiden
Sprachdateien, deutsch bewusst **„ZURUECK“ ohne Umlaut**, weil ThaleahFat keine
Umlaute kann.

### 5.3 Listenzeile (132x21)

Zeilen-lokale Maße:

| Teil | x | y | b | h |
|---|---|---|---|---|
| Icon-Slot | 1 | 0 | 21 | 21 |
| Titelzone | 25 | 1 | 94 | 11 |
| Fortschrittszone | 25 | 11 | 94 | 9 |
| Haken (nur `row_done`) | 121 | 7 | 7 | 7 |

Die drei Zustände sind so gebaut, dass sie sich **stapeln lassen**:

| Sprite | Rolle | Aufbau |
|---|---|---|
| `row_normal` | Hintergrund | vollständig transparent |
| `row_done` | Hintergrund | Fläche `#f7d76a`, 1px Rahmen `#c9922a`, 1px Highlight `#fff4e0` oben, Haken `#5a3421` rechts |
| `row_hover` | **Overlay** | nur 1px Rahmen `#8c5a3c`, innen transparent |
| `row_selected` | **Overlay** | nur Rahmen: 1px `#3b2b33` außen, 1px `#8c5a3c` innen, innen transparent |

Hintergrund ist also entweder `row_normal` **oder** `row_done`, und
`row_selected` liegt als reiner Rahmen darüber. So ist eine Zeile gleichzeitig
als erledigt **und** ausgewählt erkennbar. Textfarben: auf Gold `#4d2e1e`, sonst
`#6f4630`.

Die sechs möglichen Zustände, an den Pixeln nachgemessen:

| Zustand | Rahmen | Textfläche |
|---|---|---|
| normal | keiner | frei (transparent) |
| Maus liegt drauf | `#8c5a3c`, 1px | frei (transparent) |
| ausgewählt | `#3b2b33`, 2px | frei (transparent) |
| erledigt | `#c9922a` | `#f7d76a` |
| erledigt + Maus drauf | `#8c5a3c`, 1px | `#f7d76a` |
| erledigt + ausgewählt | `#3b2b33`, 2px | `#f7d76a` |

Der Hover-Rahmen ist bewusst dünner und heller als die Auswahl — er soll zeigen,
worauf der Zeiger liegt, ohne mit „das hier ist gewählt“ zu konkurrieren. Er
liegt als unterstes Kind der Zeile, kann also weder Icon noch Text verdecken,
und wird über `IPointerEnter`/`IPointerExit` geschaltet.

> **Falle beim Export — hier schon einmal reingefallen.** Die drei Zeilen-Sprites
> werden aus dem Mockup ausgeschnitten, und zwar aus *verschiedenen* Zeilen:
> Zeile 0 → `row_done`, Zeile 1 → `row_normal`, Zeile 2 → `row_selected`,
> Zeile 3 → `row_hover`.
> **Die Zeile, aus der `row_selected` kommt, darf nicht gleichzeitig erledigt
> sein.** Sonst nimmt der Auswahl-Rahmen die goldene Fläche mit, ist komplett
> deckend und überdeckt im Spiel den Zeilentext — und „ausgewählt“ sieht dann
> aus wie „erledigt“. Prüfen lässt sich das in einer Zeile: `row_selected.png`
> darf nur **596** sichtbare Pixel haben (die zwei Ringe), nicht 2772.

### 5.4 Kachelgitter (nur Unlocks)

Der Unlocks-Reiter zeigt links **kein** Liste, sondern ein Gitter aus Symbolen —
so wie das alte `UnlockPanel`. Ein Unlock hat keinen Fortschritt und keine
Belohnung, in der Zeile stand also ohnehin nur der Name. Die Kachel gibt dem
Symbol stattdessen die dreifache Fläche; Name und Beschreibung stehen rechts.

| Was | Wert |
|---|---|
| Sichtfenster | dasselbe wie die Liste: `(20, 36, 132, 126)` |
| Kachel | 32x32 (`slot_large_unlocked` / `slot_large_locked`) |
| Spalten | 4 — `4 × 32 + 3 × 1 = 131`, passt in die 132 |
| Schrittweite | x = 33, y = 36 |
| Kachel *i* | `x = 20 + (i mod 4) * 33`, `y = 36 + (i div 4) * 36` |
| sichtbar | 3 Zeilen = 12 Kacheln — alle 10 Unlocks ohne Scrollen |

Das Icon ist die **`_32`-Variante**, also dasselbe Bild wie auf der Detailseite.
Freigeschaltet heißt Holzrahmen und volle Farbe, gesperrt heißt helle Platte und
Dämpfung auf `#b99772` — das Motiv bleibt erkennbar.

Hover und Auswahl brauchen **keine eigenen Sprites**: `row_hover` und
`row_selected` sind 9-Slices mit 2px-Rand, lassen sich also auf 32x32 ziehen,
ohne dass die Kante dicker wird. Ein Rahmen bleibt ein Rahmen.

### 5.5 Fortschrittsleiste

**10** Zellen à 9x4, 1px Lücke, Gesamtbreite 99, linksbündig ab x=20.
Zelle *i* beginnt bei `x = 20 + i*10`. Rechts daneben steht der Zähler
(`text_counter`) — dafür sind es 10 statt 12 Zellen.

- `progress_cell_full`: `#b83a52`, oberste Zeile `#d4566c`.
- `progress_cell_empty`: Füllung `#e0c49f`, 1px Kontur `#b99772`.

Füllregel: sobald **irgendetwas** offen ist, leuchtet mindestens eine Zelle, und
alle zehn leuchten erst bei wirklich allem — sonst sieht „1 von 41“ aus wie
„nichts geschafft“ und „40 von 41“ wie „fertig“.

### 5.6 Icon-Slots

Rahmenstärke wächst mit der Größe, die Innenfläche bleibt transparent:

| Sprite | Größe | freie Innenfläche |
|---|---|---|
| `slot_small_unlocked` / `_locked` | 21x21 | 15x15 ab (3,3) |
| `slot_large_unlocked` / `_locked` | 32x32 | 24x24 ab (4,4) |

Freigeschaltet: 1px Kontur `#3b2b33`, darin Holzringe mit Highlight `#a87a52`
oben/links und Schatten `#5a3421` unten/rechts, Grundton `#8c5a3c`.
Gesperrt: Fläche `#d9b189`, 1px Kontur `#9c8268`, dazwischen ein hellerer Ring
`#e0c49f`.

`icon_locked` (12x12) ist eine generische Schloss-Silhouette und dient nur noch
als Notnagel, wenn zu einem Eintrag gar kein Bild auflösbar ist.

### 5.7 Belohnungs-Box (128x20)

Füllung `#e0c49f`, 1px Rahmen `#b99772`, Icon-Slot 12x12 ab (4,4) in Kapselgelb
`#e8b93c` mit 1px Kontur `#c9922a`, Textzone (20,3,104,14).

---

## 6. 9-Slice

Unity-Border in Pixeln, Reihenfolge **L / B / R / T**:

| Sprite | L | B | R | T | Anmerkung |
|---|---|---|---|---|---|
| `bg_capsule` | 5 | 5 | 5 | 5 | hält den Kapselrahmen am Bildschirmrand |
| `book_panel` | 3 | 5 | 3 | 3 | B=5 wegen 2px Seitenschatten + Kontur + 2px Schlagschatten |
| `book_spine` | 0 | 2 | 0 | 2 | nur vertikal strecken/kacheln |
| `tab_active` | 2 | 2 | 2 | 2 | |
| `tab_inactive` | 2 | 2 | 2 | 2 | |
| `row_normal` | 2 | 2 | 2 | 2 | |
| `row_selected` | 2 | 2 | 2 | 2 | |
| `row_hover` | 2 | 2 | 2 | 2 | wird auch auf 32x32 gezogen, siehe 5.4 |
| `row_done` | 24 | 2 | 14 | 2 | L=24 schützt den Slot-Bereich, R=14 den Haken |
| `reward_box` | 18 | 2 | 4 | 2 | L=18 schützt den gelben Icon-Slot |

Die Werte stehen in den `.meta`-Dateien **und** als `center`-Rechtecke im
Atlas-JSON.

> **Buchrücken separat:** Ein 9-Slice streckt seine Mitte — ein fest in der Mitte
> sitzender Buchrücken würde dabei verzerren. Deshalb ist `book_panel` ohne
> Rücken exportiert und `book_spine` ein eigenes Sprite bei x=156.

---

## 7. Layout-Regeln für Unity

- **Canvas:** CanvasScaler *Scale With Screen Size*, Referenz **320x180**,
  Match *Expand*. Das Buch liegt in einem Container, der immer exakt 320x180 und
  mittig ist; nur der Kapselhintergrund füllt den ganzen Bildschirm.
- **Y-Richtung:** Die Tabellen zählen y von **oben**. Ein RectTransform mit
  Anchor oben-links braucht `anchoredPosition.y = -y`.
- **Liste:** ScrollRect mit Viewport `(20, 36, 132, 126)`, VerticalLayoutGroup,
  `spacing = 0`, **Zeilenhöhe 21**. **6 Zeilen sind sichtbar** (6 × 21 = 126 =
  Viewporthöhe), es gibt also keine halb angeschnittene Zeile.
- **Gitter:** derselbe ScrollRect, nur ein zweiter Content mit GridLayoutGroup,
  `cellSize = 32x32`, `spacing = (1, 4)`, FixedColumnCount **4**. Beim
  Reiterwechsel wird umgeschaltet, welcher Content aktiv ist und an
  `scroll.content` hängt.
- **Scrollen:** Movement Type *Clamped*, Inertia aus, `scrollSensitivity` =
  Zeilenhöhe (21 in der Liste, 36 im Gitter), also genau eine Reihe pro
  Mausrad-Rastung. Pfeiltasten schieben die Auswahl und ziehen den Inhalt nur
  nach, wenn sie aus dem Viewport läuft — dann immer um ganze Reihen.
- **Tastatur im Gitter:** ↑/↓ gehen um **einen** Eintrag weiter, nicht um eine
  ganze Reihe. Damit ist jede Kachel erreichbar; ←/→ bleiben für den
  Reiterwechsel reserviert.
- **Import:** alle PNGs kommen mit `.meta` — Sprite (Single), **Point-Filter**,
  keine Kompression, **Mesh Type FullRect** (Pflicht für 9-Slice), **PPU 100**.

### Ankerpunkte auf einen Blick

| Was | Ankerpunkt (oben links) | Schrittweite |
|---|---|---|
| Reiter *n* | `x = 16 + n*62`, `y = 9` | 62 (60 + 2 Abstand) |
| Fortschrittszelle *i* | `x = 20 + i*10`, `y = 29` | 10 (9 + 1 Lücke) |
| Listenzeile *i* | `x = 20`, `y = 36 + i*21` | 21 |
| Icon-Slot in Zeile *i* | `x = 21`, `y = 36 + i*21` | 21 |
| Textzone in Zeile *i* | `x = 45`, `y = 37 + i*21` | 21 |

---

## 8. Datenanbindung

Es wird **kein zweites Datenmodell** gebaut. Beide Reiter hängen an den
vorhandenen Katalogen.

### Reiter 1 — ERFOLGE

| Was | Wo |
|---|---|
| Katalog / Liste | `Assets/Scripts/Achievements/Ach.cs` → `Ach.All` |
| Eintrag | `AchievementDef`: `Name`, `Description`, `Goal`, `Souls`, `GrantsUnlock`, `Category`, `Hidden` |
| Fortschritt | `Achievements.IsUnlocked(def)`, `GetProgress01(def)`, `GetValue(def)` |
| Zähler an der Leiste | `Achievements.UnlockedCount` / `TotalCount` |
| Icons | `AchievementIcons.Get(def.IconKey)` → `Resources/Achievements/<IconKey>.png` |
| Bestehende Anzeigen | `Achievements/UI/AchievementPanel.cs`, `Worlds/Achievement_UI_Manager.cs` |

Feldzuordnung: `text_row_title` → `def.Name`; `text_row_progress` →
`"{Value} / {Goal}"` (leer, wenn erledigt oder `Goal == 1`);
`text_detail_title` / `text_detail_desc` → `def.Name` / `def.Description`;
`text_reward` → `def.Souls`; Hintergrund `row_done`, wenn
`Achievements.IsUnlocked(def)`.

### Reiter 2 — UNLOCKS

| Was | Wo |
|---|---|
| Katalog / Liste | `Assets/Scripts/Unlocks/Unlocks.cs` → `Unlocks.All` |
| Eintrag | `UnlockDef`: `Name`, `Description`, `IconKey` |
| Zustand | `Unlocks.IsUnlocked(def)`, `UnlockedCount` / `TotalCount` |
| Herkunft | `Ach.FindByUnlock(def.Id)` → liefert die Bedingungszeile |
| Icons | `UnlockIcons.Get(def.IconKey)` → `Resources/Unlocks/` , sonst `Resources/Shop/` |
| **Bestehendes Panel, das abgelöst wird** | `Unlocks/UI/UnlockPanel.cs` (siehe `HUB_UNLOCKS_TODO.md`) |
| World-Map-Variante | `Unlocks/UnlockUIManager.cs` + `UnlocksCanvas` in `World Map.unity` |

Der Unlocks-Reiter zeigt links das **Kachelgitter** (Abschnitt 5.4), rechts
Symbol, Name und Beschreibung. Die **Belohnungs-Box blendet sich dort aus** —
ein Unlock vergibt nichts, die Box stünde nur leer herum. Aus demselben Grund
bleibt die Fortschrittszeile leer.

> Aktuell setzt **kein** Achievement `grantsUnlock:`. Bis das passiert, zeigt die
> Herkunftszeile bei gesperrten Unlocks nur den allgemeinen Hinweis
> (`ui.unlocks.hint`).

### Umgefärbte Icons

`Assets/Resources/AchievementsBook/` enthält zu jedem Motiv **drei** Größen,
Dateiname `<IconKey>_12`, `_21`, `_32`:

- **28 Motive**: 18 aus `Resources/Achievements/` (inkl. `_locked`, `_hidden`,
  `_badge_unlocked`) und 10 Unlock-Icons aus `Resources/Shop/`, aufgelöst über
  die `icon:`-Keys in `Unlocks.cs`.
- **Verfahren**: transparenten Rand abschneiden → Box-Filter auf die
  Slot-Innenfläche skalieren (Seitenverhältnis bleibt) → auf die 19 Farben
  quantisieren, Alpha hart bei 110 geschnitten, also keine AA-Pixel.
- **Leinwand = Slot-Außenmaß, Motiv = Slot-Öffnung.** Damit lässt sich das Icon
  **ohne Versatz** auf dasselbe Rechteck legen wie der Rahmen:

| Datei | Leinwand | Motiv | passt auf |
|---|---|---|---|
| `_12` | 12x12 | 8x8 | Icon-Slot der Belohnungs-Box |
| `_21` | 21x21 | 15x15 | `slot_small_*` in der Listenzeile |
| `_32` | 32x32 | 24x24 | `slot_large_*` auf der Detailseite |

Laden mit einer Zeile, ohne den bestehenden Loader anzufassen:

```csharp
Sprite BookIcon(string iconKey, int size) =>
    Resources.Load<Sprite>($"AchievementsBook/{iconKey}_{size}");
```

Fällt das `null` zurück, bleibt der alte 64x64-Pfad über `AchievementIcons.Get`
bzw. `UnlockIcons.Get` als Rückfall.

**Gesperrte Einträge** werden nicht schwarz ausgemalt, sondern nur mit `#b99772`
gedämpft — das Motiv bleibt erkennbar, der Eintrag trotzdem klar inaktiv.
Versteckte Achievements liefern über `def.Icon` ohnehin schon `_hidden`, da
verrät die Dämpfung nichts.

> Nebenbei: die Quell-PNGs in `Resources/Achievements/` sind auf
> `filterMode: 1` (bilinear) importiert und werden dadurch im Spiel weich
> gezeichnet. Für die neuen Varianten ist das auf Point gestellt.

---

## 9. Der Einstieg im Spiel

**In `hub.unity` steht die Kapsel schon** — Wurzelobjekt `Kapsel_Erfolgsbuch`
bei `(55.5, 13.4)`, in der Reihe mit Skilltree, Levelauswahl und Charakterwahl,
in der Lücke zwischen Bücherhaufen und Shop-Teleport. Play drücken, hinlaufen,
**[E]**.

Für andere Szenen (World Map, Testszene):

> **Tools ▸ Achievements ▸ Buch-Objekt in Szene setzen**

- **Tools ▸ Achievements ▸ Buch-Prefab erzeugen** legt dasselbe als Prefab unter
  `Assets/Prefabs/MapObjects/KapselErfolgsbuch.prefab` ab.
- **Tools ▸ Achievements ▸ Buch oeffnen (nur im Play Mode)** macht das Fenster
  ohne Objekt auf.
- Aus Code: `AchievementsBookPanel.Toggle()` bzw. `Open(0)` / `Open(1)`.

Bedienung: der **ZURUECK-Knopf** oben rechts schließt, ebenso **[E]** und
**Esc**. **←/→** (oder A/D) wechselt den Reiter, **↑/↓** (oder W/S) wählt einen
Eintrag, Mausrad scrollt reihenweise.

Das Fenster liegt auf `sortingOrder = 150` und sperrt den Hub, solange es offen
ist (`HubUI.PushModal` + Spieler einfrieren) — aber nur, wenn wirklich ein
`HubUI` in der Szene ist.

Die Zone steht im Inspector: Rechteck 2 x 2.5 Units, **1.2 nach unten versetzt**,
Hinweis `[E] Erfolge`, Umrandung nur in Reichweite. Der Versatz ist wichtig und
vom Skilltree-Trigger übernommen: die Kapsel steht an der Wand auf y≈13.4, der
Spieler läuft aber auf y≈12 davor. Ohne den Versatz erreicht er die Zone nie.

---

## 10. Cheat-Codes

In der Hub-Konsole (Terminal im Hub), eingetragen in
`Assets/Scripts/Hub/Console/HubConsoleCheats.cs`. Beide sind `hidden: true`,
tauchen also nicht in `hilfe` auf.

| Code | Wirkung |
|---|---|
| `giberfolge` | schaltet den **nächsten** noch offenen Erfolg frei und sagt, wie viele noch fehlen |
| `giberfolge alle` | schaltet alle offenen Erfolge frei |
| `giberfolge <id>` | schaltet genau einen frei, z. B. `giberfolge First_Win` |
| `erfolgeweg` | setzt alle Erfolge zurück |

`giberfolge` geht über `Achievements.Unlock`, also mit allem, was dranhängt:
Cookie Souls, mitvergebene Unlocks (`grantsUnlock`) und die Steam-Meldung. Ein
offenes Buch aktualisiert sich von selbst, weil `Achievements.Unlocked` feuert.

`erfolgeweg` ruft `Achievements.ResetAll()`. Das feuert **kein** Ereignis,
deshalb ruft der Cheat zusätzlich `AchievementsBookPanel.RefreshIfOpen()` — sonst
stünde im offenen Buch noch der alte Stand. Bei Steam bleiben die Erfolge
stehen, das geht nur dort.

Die IDs stehen im Katalog `Assets/Scripts/Achievements/Ach.cs` (z. B.
`First_Game`, `Kill_100`, `Max_Level_Boomerang`). Ein Tippfehler wird abgefangen
und mit der Benutzung beantwortet.

---

## 11. Bewusste Abweichungen

1. **19 statt 14 Farben.** Die Element-Spezifikation nennt `#c8a078`, `#a87a52`,
   `#8a5a3d`, `#9c8268` und `#d4566c` namentlich, die 14er-Palette enthält sie
   nicht. Die Elemente sind exakt wie beschrieben gezeichnet, die fünf Farben
   liegen als getrennte Akzentgruppe in der Palette.
2. **`book_spine` ist ein eigenes Sprite** — ein 9-Slice-`book_panel` mit fest
   eingebautem Rücken würde beim Strecken verzerren.
3. **`row_normal` ist eine leere Datei** — 132x21, 0 sichtbare Pixel. So
   verlangt („normal (transparent)“); der Sinn ist, dass der Image-Slot in jedem
   Zustand dieselbe Größe hat.
4. **Geänderte Maße gegenüber der ersten Spezifikation**, nach Rückmeldung:
   Zeilenhöhe 18 → **21**, Listen-Slot 12x12 → **21x21**, Detail-Slot 21x21 →
   **32x32**, Fortschrittsleiste 12 → **10** Zellen (für den Zähler daneben).
   Die Icons waren bei 8x8 Motivfläche schlicht nicht lesbar.
5. **„Erledigt“ ist eine Fläche, keine Ecke.** Die erste Fassung markierte nur
   mit einem 4x4-Eckchen; das war zu leise. Jetzt goldene Fläche + Haken.
6. **Unlocks bekommen ein Kachelgitter statt einer Liste** (Abschnitt 5.4), wie
   schon im alten `UnlockPanel`. Das Symbol wächst dabei von 21 auf 32 Pixel.
   Die Beschreibung musste dafür **nicht** kleiner werden: das Gitter sitzt auf
   der linken Seite, die Beschreibung auf der rechten — sie behält ihre volle
   Größe und gewinnt sogar Platz, weil die Belohnungs-Box dort entfällt.
