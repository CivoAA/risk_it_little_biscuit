# Story — Welt, Figuren, Dialoge, Art-Prompts

Stand: 2026-09-21.
Diese Datei ist die Story-Bibel für *Risk it, Little Biscuit*. Sie erfindet nichts
neu, was schon im Spiel steht, sondern verknotet das Vorhandene: den Cookie-Krieg
aus dem Buch im Hub, die Kronen auf den Mini-Bossen, den Keks-König, die vier
Map-Szenen, die fünf Karten in der Levelauswahl, den Ofen und die Statuen.

Deutsch ist die Autorensprache (wie `de.json`). Die **Art-Prompts sind auf
Englisch** — Bildgeneratoren verstehen englische Pixelart-Begriffe deutlich
zuverlässiger, und die Farbcodes sind eh sprachneutral.

---

## 0 Kurzfassung

> Im Cookie-Krieg verteidigten die Donuts ihren **süßen Thron**. Der Thron war
> nie ein Möbelstück — er war die **Glasur** selbst: etwas, das nicht tötet,
> sondern überzieht. Wen sie überzieht, der ist süß, ruhig und gehorsam und
> findet das gut.
>
> Ein einzelner Keks beendete den Krieg, indem er die Krone der Donuts aufsetzte
> — damit sie kein anderer trägt. Er hält sie seit hundert Jahren. Im Hub steht
> seine Statue: **der Held des Zuckers**.
>
> Er hält nicht mehr lange. Die Glasur sickert die Kellertreppe herunter und
> verteilt Kronen: an ein Marshmallow, an eine Maus, an einen Muffin, an etwas
> Kaltes im Nachtwald. Der **Erste Ofen** unten in der Backstube ist fast aus —
> und was nicht mehr gebacken wird, kann nicht mehr kämpfen.
>
> **Mutter Hefe** weckt das letzte ungeglasierte Blech und gibt den Auftrag:
> vier Welten leerräumen, vier Kronen zurückbringen. Kronen brennen gut. Brennt
> der Ofen wieder heiß, backt er die Waffe, die die fünfte Krone knackt.
>
> Die fünfte sitzt auf dem Kopf des Helden.

**Der Titel als Motto:** Mutter Hefe verspricht dir nichts. Du bist ein Keks,
Kekse werden gegessen. *Risk it, little biscuit.*

---

## 1 Woran das andockt

Alles hier hängt an etwas, das es schon gibt. Wer beim Einbauen zweifelt, findet
die Quelle in der rechten Spalte.

| Story-Element | steckt schon im Projekt |
|---|---|
| Cookie-Krieg, Kekse gegen Donuts, "süßer Thron" | Buch-Seiten im Hub, `hub.unity` Zeile ~12477 |
| Held des Zuckers, erster gebackener Keks | `bookhint - Statue_hero`, `bookhint - Statue_keks` |
| Glasur, die wie Schnee über die Felder floss | ebenfalls Buch-Seite 2 |
| Kronen als Zeichen der Mini-Bosse | `ach.Kill_10_Miniboss.desc`: "an der Krone zu erkennen" |
| Keks-König als Endgegner mit Krone | `EnemyKeckKönig.cs`, `fin_der_konig_des_spielfelds.png` |
| Zwei Phasen, Phase 2 ab halbem Leben | `EnemyKeckKönig.cs`, `PhaseTwoAt = 0.5f` |
| "Zuckerguss" als Phasenname der Küche | `WavePlans.World0()` |
| Der Ofen als Aufbruchsbild | Lied-Seite im Hub: "Wenn der Ofen glüht und die Reise beginnt" |
| Fünf Levelkarten, Karte 5 gesperrt | `hub.unity`, `storyUnlockId: Level5_Story` |
| Vier Map-Szenen | `Assets/Scenes/Maps/Map_World0..3.unity` |
| Vier spielbare Kekse | `Characters.cs` |
| Frost-Gegner für eine kalte Welt | `freeze_marshmallow.png`, `freeze_marshmallow_miniboss.png` |
| Zwischenrufe im Lauf | `SpawnDirector.Say(string)` |
| Fackeln und Punktlichter im Hub | `fackel.anim`, `Spot Light 2D (0..3)`, `Global Light 2D` |

---

## 2 Die Weltlogik in drei Begriffen

### Die Glasur *(der Bösewicht)*
Kein Monster, kein Heer. Eine Substanz mit Willen — der Rest des Donut-Throns.
Sie greift nicht an, sie **bietet an**. Wen sie überzieht, den tötet sie nicht:
er wird süß, glänzend, ruhig und gehorsam und hält das für Glück. Deshalb
kämpfen die Gegner auch nicht wütend — sie wollen dich nur **mitnehmen**.

Die Glasur hat keinen Körper und kein Sprite. Sie spricht durch Kronenträger, in
der Textbox tropft sie von oben ins Bild. Sie duzt, sie verspricht Ruhe, sie
benutzt nie ein Ausrufezeichen.

### Die Krone
Ein Ring aus getrockneter Glasur — ein Donut, der hart geworden ist. Wer eine
trägt, gehört ihr und wird stärker. **Das erklärt die Mini-Boss-Krone im Spiel
rückwirkend**: eine Krone = eine Beförderung durch die Glasur. Kronen lassen sich
abnehmen, solange sie neu sind. Nach hundert Jahren sind sie festgebacken.

### Die Glut
Der **Erste Ofen** in der Backstube hat den allerersten Keks gebacken (die Statue
daneben). Seine Glut ist fast aus. Jede zurückgebrachte Krone wird verfeuert und
die Glut springt eine Stufe hoch.

**Das ist die Fortschrittsanzeige des Spiels und kostet fast nichts:** Nach jeder
Welt wird der Hub sichtbar heller und wärmer. `Global Light 2D` hoch, ein
`Spot Light 2D` mehr an, die Fackeln brennen höher, die Statuen kommen aus dem
Dunkeln. Am Anfang sieht man die Statue des Helden kaum. Beim vierten Mal sieht
man ihr ins Gesicht — und erkennt, wen man da gleich totschlägt.

---

## 3 Wann die Story kommt

**Alles hängt an einem einzigen Punkt: dem SPIELEN-Knopf in der Levelauswahl.**
Kein Herumlaufen im Hub, kein Ansprechen, kein Suchen. Du drückst auf Level 1,
Mutter Hefe sagt dir, warum du da hochgehst und was du mitbringen sollst, und
dann lädt die Welt.

```
   Levelauswahl → SPIELEN
        │
        ├── Modus ENDLESS ────────────────────────► Welt lädt sofort. Kein Wort.
        │                                            Nach dem Boss geht es weiter.
        │
        └── Modus STORY
             │
             ├── Briefing schon gelaufen? ────────► Welt lädt sofort.
             │
             └── noch nicht:
                   Auswahl schließt
                   Textbox im Hub: Mutter Hefe, 3-5 Seiten
                   letzter Klick  →  Welt lädt
                   Flag gesetzt, kommt nie wieder
```

Sechs Dialoge im ganzen Spiel, einer pro Levelkarte plus Epilog. Das ist alles.

| Wann | Wer redet | Wie lang |
|---|---|---|
| SPIELEN auf Karte 1 | Mutter Hefe | 6 Seiten (einmal länger, das ist der Einstieg) |
| SPIELEN auf Karte 2 | Mutter Hefe | 4 Seiten |
| SPIELEN auf Karte 3 | Mutter Hefe | 4 Seiten |
| SPIELEN auf Karte 4 | Mutter Hefe | 5 Seiten |
| SPIELEN auf Karte 5 | Mutter Hefe | 9 Seiten (die Beichte) |
| Nach dem letzten Boss | Krümel, der Ofen, Mutter Hefe | 9 Seiten |

**Kein Rückkehr-Dialog.** Was du mitgebracht hast, quittiert sie in der ersten
Zeile des nächsten Briefings. Ein Dialogpunkt pro Welt, mehr nicht — sonst
stehst du nach jedem Lauf wieder in einer Textbox.

**Endless ist der Story-Ausschalter.** Wer die Welt schon kennt und nur spielen
will, hakt Endless an und bekommt kein Wort. Das ist auch die Begründung im
Spiel: die Story ist das eine Mal, wo du mit einem Ziel hochgehst. Endless ist
danach — der Guss hört ja nicht auf, nur weil der Kopf ab ist.

---

## 4 Die vier Welten + Der Thron

Die Reihenfolge der Levelkarten ist die Story-Reihenfolge. Die Map-IDs bleiben
wie sie sind — nur Karte 3 und 4 kommen dazu.

### Karte 1 — Die Küche · `Map_World0` · Plan `World0`
Da oben wurdest du gebacken. Die Glasur ist schon durch die Ritze im Fenster.
Der Phasenname **"Zuckerguss"** im bestehenden Wellenplan ist ab jetzt wörtlich
gemeint: da fängt sie an, das Blech zu überziehen.
**Kronenträger:** *Die Erste Krone* — ein Marshmallow, das seine Krone die ganze
Zeit gerade rückt, weil sie zu groß ist. Tragikomisch, kein Kämpfer. Erster Kill
soll sich leicht falsch anfühlen.
**Levelauswahl-Text:** `Mehl, Zucker und Ärger. Hier fängt alles an.` *(bleibt)*

### Karte 2 — Der Krümelwald · `Map_World3` · Plan `World3`
Hinter der Küche. Kein echter Wald — gewachsen aus dem, was nach dem Krieg
liegen blieb. Die Bäume sind Krümel, die Wurzeln geschlagen haben. Hier hat man
damals zusammengekehrt, und die Mäuse merken sich, wer kehrt.
**Kronenträger:** *Die Messermaus* — kennt Mutter Hefes Namen. Erste Ahnung, dass
die Alte mehr weiß, als sie sagt.
**Levelauswahl-Text:** `Zwischen den Bäumen wird es voll.` *(bleibt)*

### Karte 3 — Zuckerdorf · `Map_World2` · Plan `World2`
Die Phasen heißen schon `Gassen`, `Marktplatz`, `Backstube`. Das Dorf hat nie
gekämpft: sie haben die Tür aufgemacht, weil Guss so schön glänzt. Alle lächeln.
Alle meinen es ernst. Das ist das Unangenehme daran.
**Kronenträger:** *Der Bürgermuffin* — höflich, gastfreundlich, bietet dir bis
zum letzten Leben Kuchen an.
**Levelauswahl-Text:** `Alle lächeln. Keiner blinzelt.`

### Karte 4 — Der Nachtwald · `Map_World1` · Plan `World1`
Derselbe Wald, hundert Jahre später und bei Nacht — hier war der Krieg zu Ende.
Guss, der lange steht, wird hart wie Eis. Kalt, still, Reif auf allem.
Kostet kaum Arbeit: gleiche Tiles, blaues Licht, Frost-Gegner
(`freeze_marshmallow.png` liegt schon da).
**Kronenträger:** *Die Kalte Krone* — ein gefrorenes Marshmallow, das nichts mehr
sagt. Hier liegt auch das zerbrochene Backblech des Helden im Reif: der Fund, der
den Twist auslöst.
**Levelauswahl-Text:** `Hier war der Krieg zu Ende. Es ist nie wieder warm geworden.`

### Karte 5 — Der Süße Thron · neue Szene · `storyUnlockId: Level5_Story`
Oben im Laden steht eine Vitrine: Glas, Samt, kleines Schild davor. Da stellt man
die besten Kekse rein. Für immer. Angeschaut werden ist auch eine Art, gegessen
zu werden. Auf der Tortenplatte unter der Glasglocke sitzt der Keks-König.
Kein Wellenplan-Marathon — kurzer Anmarsch, dann der Kampf.
**Levelauswahl-Text:** `Glas, Samt, ein kleines Schild. Der beste Keks von allen.`

### Endless
Story-Begründung in einem Satz, Mutter Hefe nach dem Abspann:
`»Guss hört nicht auf, nur weil der Kopf ab ist.«`

---

## 5 Die Figuren

### 5.1 MUTTER HEFE — Auftraggeberin
**Was:** Ein zweihundert Jahre alter Sauerteig-Ansatz in einem gesprungenen
Tonkrug, neben dem Ofen. Blasen an der Oberfläche formen Augen und Mund, wenn sie
redet. Ein alter Holzlöffel steckt im Krug wie ein Wanderstab.
**Rolle:** Die einzige Stimme der Story. Sie redet **immer am SPIELEN-Knopf**,
kurz bevor eine Welt lädt: was du da oben holen sollst und warum. Fünfmal im
ganzen Spiel, plus Epilog.
**Stimme:** Kurze Sätze. Keine Erklärung zweimal. Nennt dich *Kleiner* oder
*Krümel*. Backvergleiche benutzt sie nicht als Witz, sondern weil sie nichts
anderes kennt. Unhöflich aus Zuneigung.
**Ihr Geheimnis:** Sie war dabei. Der Held war ihr Freund. Sie hat ihn die Krone
aufsetzen lassen, weil es keine bessere Idee gab, und trägt das seit hundert
Jahren mit sich herum.
**Steht:** Im Hub links vom Ofen, in Reichweite von Regal und Altar. Sie muss
**nicht** ansprechbar sein — die Story läuft ohne eine einzige Interaktion
durch. Sie soll nur zu sehen sein, damit die Textbox ein Gesicht hat.
**Technik:** 64×64-Sprite, 4 Frames Idle (Blubbern), 2 Frames Reden. Ein
`HubInteractable` (Vorlage: `BookHint.cs`) ist optional und kommt später.

### 5.2 DER ERSTE OFEN — der Einsatz
**Was:** Ein gemauerter Backofen, viel zu alt, Ruß, ein Riss quer über die Front.
Hinter der Klappe eine Glut, die man am Anfang suchen muss.
**Rolle:** Kein Gesprächspartner. Er ist das, was stirbt. Fünf Zustände: *fast
aus → 4× heller*. Verfeuert die Kronen, backt die Belohnungen, backt am Ende die
Waffe.
**Stimme:** Keine. Er knackt, er seufzt Luft aus. **Genau ein Mal im ganzen
Spiel sagt er ein Wort** — im Epilog. Das ist der Moment, der sitzt. Nicht
verschenken.

### 5.3 DIE GLASUR — der Bösewicht
**Was:** Zuckerguss mit Absicht. Kein Körper, kein Gesicht, kein Boss-Kampf.
Man sieht sie nur als das, was sie überzogen hat: den Glanz auf den Gegnern, den
Ring auf ihren Köpfen, das Tropfen in der Textbox.
**Rolle:** Verteilt Kronen, kommentiert dich während der Läufe
(`SpawnDirector.Say`), redet im Finale durch den König.
**Stimme:** Sanft, geduldig, in der zweiten Person. Verspricht Ruhe, nicht Macht.
Nie ein Ausrufezeichen, nie eine Drohung. Das Unangenehme ist, dass sie **recht
hat**: aufhören wäre wirklich leichter.

> »Du musst nicht rennen.«
> »Setz dich. Ich mach dich süß, dann tut nichts mehr weh.«
> »Er hat auch gerannt. Frag ihn, wie das ausgegangen ist.«

### 5.4 KRÜMEL DER ERSTE / DER KEKS-KÖNIG — Endgegner
**Was:** Der erste Keks, den der Erste Ofen je gebacken hat. Der Held des
Zuckers von der Statue. Heute: riesig, rissig, ein Auge zugewachsen, die Krone
seit hundert Jahren festgebacken. Das Sprite gibt es
(`fin_der_konig_des_spielfelds.png`).
**Rolle:** Der Kampf, den man nicht gewinnen *will*. Er greift nicht an, er
**verliert die Kontrolle**.
**Sein Kampf erzählt sich aus dem bestehenden Code von selbst**, ohne eine Zeile
Balancing anzufassen:

| im Code | was es jetzt bedeutet |
|---|---|
| Phase 1: lange Vorwarnung, Aim-Lock friert das Ziel ein | Er hält zurück. Er zielt absichtlich daneben. |
| "Nach jeder Attacke steht er kurz still" | Er wartet, dass du triffst. |
| Lauftempo knapp unter Spielertempo | Er holt dich nie ein. Er will dich nicht einholen. |
| Phase 2 ab 50 %: drei Charges, kürzere Vorwarnung | Die Glasur übernimmt. Er ist nicht mehr am Steuer. |

**Stimme:** Zwei Stimmen in einem Mund. Seine eigene: abgebrochen, wenige Worte,
höflich. Die andere: die Glasur, glatt und vollständig.

### 5.5 Die vier Kronenträger
Kurze Rollen, bestehende Sprites, je ein Satz Persönlichkeit.

| Welt | Name | Basis-Art | Persönlichkeit | Banner (ohne Umlaute!) |
|---|---|---|---|---|
| 1 Küche | **Die Erste Krone** | `fin_an_Krone_richten.png` / MiniBossMarshmello | Rückt ständig die zu große Krone gerade. Will gefallen. | `DIE ERSTE KRONE` |
| 2 Krümelwald | **Die Messermaus** | `fin_MesserMaus.png` | Merkt sich alles. Kennt Mutter Hefes Namen. | `MESSERMAUS` *(bleibt)* |
| 3 Zuckerdorf | **Der Bürgermuffin** | `fin_muffin.png` + Krone | Gastfreundlich bis zum Schluss. Bietet Kuchen an. | `DER BUERGERMUFFIN` |
| 4 Nachtwald | **Die Kalte Krone** | `freeze_marshmallow_miniboss.png` | Sagt nichts. Ist zu lange zu kalt gewesen. | `DIE KALTE KRONE` |

### 5.6 Die vier Spielfiguren
Die existierenden Skins aus `Characters.cs` bekommen je einen Satz — mehr braucht
die Auswahl nicht. Alle vier sind **dasselbe letzte Blech**: aus dem Ofen, bevor
der Guss reinkam.

| Skin | Name | Startwaffe | Ein Satz |
|---|---|---|---|
| 0 | **Brauner Keks** | Shurikookie | Ganz normal gebacken. Das ist die ganze Besonderheit. |
| 1 | **Grauer Keks** | Spike Fork | Zehn Minuten zu lang drin. Hart, dunkel, hält mehr aus. |
| 2 | **Roter Keks** | Blade Swarm | Zu viel Farbe im Teig. Zittert. Schlägt zuerst. |
| 3 | **Marmelade** | Marmeladenglas | Kein Keks. Hat sich dazugestellt und niemand hat widersprochen. |

### 5.7 ESPRESSO — Händler *(optional, Art existiert)*
`Pipos_Assets/Espresso mug.png` liegt schon da, und `Kaffeepfütze` ist eine
Waffe. Eine kleine Espressotasse, die viel zu schnell redet, hinter dem Shop.
Nicht story-relevant, aber kostenlos.
**Stimme:** Ein Satz ohne Punkt. Atmet nie.
> »NochwasNochwasNochwas — du hast Münzen, ich hab Zeug, das ist im Grunde schon der ganze Handel, guck einfach.«

---

## 6 Storyboard

Sechs Dialoge, fünf Läufe. Von oben nach unten ist das das ganze Spiel.

```
  ┌─ SPIELEN auf Karte 1 ──────────────────────────────────────┐
  │  DIALOG 1  Wer du bist. Was die Glasur ist. Vier Kronen.   │
  └──────────────────────┬─────────────────────────────────────┘
                         ▼
     LAUF: Die Küche   ·  Glasur-Einwurf in Phase "Zuckerguss"
                       ·  Boss: Die Erste Krone
                         │
  ┌─ SPIELEN auf Karte 2 ▼─────────────────────────────────────┐
  │  DIALOG 2  "Eine. Guck nicht so stolz."                    │
  │            Der Wald ist aus Kriegskrümeln gewachsen.       │
  └──────────────────────┬─────────────────────────────────────┘
                         ▼
     LAUF: Krümelwald  ·  die Messermaus kennt Hefes Namen
                       ·  Boss: Die Messermaus
                         │
  ┌─ SPIELEN auf Karte 3 ▼─────────────────────────────────────┐
  │  DIALOG 3  "Iss nichts."  Das Dorf hat freiwillig          │
  │            aufgemacht. Alle lächeln.                       │
  └──────────────────────┬─────────────────────────────────────┘
                         ▼
     LAUF: Zuckerdorf  ·  die Glasur lädt dich zum Kaffee
                       ·  Boss: Der Bürgermuffin
                         │
  ┌─ SPIELEN auf Karte 4 ▼─────────────────────────────────────┐
  │  DIALOG 4  "Da war der Krieg zu Ende.                      │
  │             Und ja. Ich war dabei."                        │
  └──────────────────────┬─────────────────────────────────────┘
                         ▼
     LAUF: Nachtwald   ·  Boss: Die Kalte Krone
                       ·  danach: Karte 5 öffnet (Level5_Story)
                         │
  ┌─ SPIELEN auf Karte 5 ▼─────────────────────────────────────┐
  │  DIALOG 5  DIE BEICHTE. Wer da oben in der Vitrine sitzt   │
  │            und warum er sich hingesetzt hat.               │
  └──────────────────────┬─────────────────────────────────────┘
                         ▼
     LAUF: Der Süße Thron
           Phase 1 - er hält zurück.  Phase 2 - die Glasur übernimmt.
                         │
  ┌─ nach dem Sieg ──────▼─────────────────────────────────────┐
  │  DIALOG 6  Die Krone bricht. Krümel zerfällt.              │
  │            Der Ofen sagt sein einziges Wort. Epilog.       │
  └────────────────────────────────────────────────────────────┘

  ENDLESS  auf jeder Karte, jederzeit:  kein Dialog, nach dem Boss
           läuft die Endlos-Phase weiter (steht schon in WavePlans).
```

---

## 7 Die Dialoge

Ein Absatz = eine Seite in der Textbox. Klick blättert weiter. Gesprochen wird,
sofern nicht anders markiert, von **Mutter Hefe**.

Die Texte gehören in einen statischen Katalog `StoryDialogs.cs` (wie `Ach` und
`Unlocks`), nicht ins Szenen-YAML. Loc-Keys nach Schema `story.w1.p1` …

---

### DIALOG 1 · SPIELEN auf Karte 1 — Die Küche
`story.w1` · Flag `story_w1_seen`

```
»Aufgewacht. Endlich.«

»Du bist aus dem letzten Blech. Dem letzten, das durchkam, bevor der Zucker in den Ofen kroch.«

»Riech mal. Das ist kein Kuchen, das ist Glasur. Sie tut dir nichts — das ist ja das Miese. Sie zieht dich über, und dann bist du süß und still und findest das gut.«

»Oben in der Küche sitzt was Weiches mit einer Krone auf dem Kopf. Die Krone will ich. Kronen brennen gut, und der Ofen da hinten ist fast aus.«

»Vier Stück brauch ich. Dann brennt er wieder heiß genug für das, was danach kommt.«

»Kann sein, dass du nicht wiederkommst. Du bist ein Keks, Kekse werden gegessen. Riskierst du's, Krümel?«
```

### DIALOG 2 · SPIELEN auf Karte 2 — Der Krümelwald
`story.w2` · Flag `story_w2_seen`

```
»Eine. Guck nicht so stolz, das war ein Marshmallow.«

»Hinter der Küche liegt der Wald. Der ist nicht gewachsen, der ist liegengeblieben — nach dem Krieg hat man zusammengekehrt, und was in der Ecke lag, hat Wurzeln geschlagen.«

»Da wohnen Mäuse. Mäuse mit Messern. Mäuse merken sich, wer kehrt.«

»Die Größte trägt Nummer zwei auf dem Kopf. Hol sie runter — und hör nicht hin, wenn sie redet.«
```

### DIALOG 3 · SPIELEN auf Karte 3 — Zuckerdorf
`story.w3` · Flag `story_w3_seen`

```
»Zwei. Der Ofen zieht schon wieder besser, hörst du das?«

»Jetzt das Dorf. Netteste Gegend, die du je sehen wirst. Die haben nicht gekämpft — die haben die Tür aufgemacht, weil Guss so schön glänzt.«

»Sie werden dir Kuchen anbieten. Iss nichts. Setz dich nicht. Bleib nicht stehen.«

»Der Bürgermuffin trägt die dritte. Er wird dich bis zum Schluss siezen.«
```

### DIALOG 4 · SPIELEN auf Karte 4 — Der Nachtwald
`story.w4` · Flag `story_w4_seen`

```
»Drei. Guck mal hoch — man sieht wieder die Statuen.«

»Die letzte liegt im Nachtwald. Derselbe Wald, andere Seite. Da war der Krieg zu Ende.«

»Kalt ist es da. Guss, der lange steht, wird hart wie Eis. Da bewegt sich nichts mehr, und das ist nicht beruhigend.«

»Und bevor du fragst: ja. Ich war dabei.«

»Geh. Ich red nicht gern darüber, solange der Ofen kalt ist.«
```

### DIALOG 5 · SPIELEN auf Karte 5 — Der Süße Thron
`story.w5` · Flag `story_w5_seen` · **die Beichte**

```
»Vier. Der Ofen ist heiß.«

»Setz dich kurz hin. Das hier dauert, und ich sag es nur einmal.«

»Du warst im Nachtwald, also hast du das Blech gesehen. Zerbrochen, mit einem Namen drin.«

»Er hieß Krümel. Der Erste. Der auf dem Sockel da drüben, an dem du jeden Tag vorbeiläufst.«

»Er hat den Krieg nicht beendet, indem er geredet hat. Das steht so im Buch, weil es sich besser liest.«

»Er hat die Krone aufgesetzt. Damit sie keiner sonst trägt. Dann ist er hochgegangen und hat sich in die Vitrine gesetzt. Hundert Jahre. Allein.«

»Ich hab ihn gelassen. Mir ist nichts Besseres eingefallen — und das ist mir seitdem jeden Tag eingefallen.«

»Der Ofen hat dir was gebacken. Geh hoch und nimm einem müden alten Keks den Hut ab. Mehr ist das nicht.«

»Und dann kommst du wieder runter. Diesmal will ich das hören.«
```

### DIALOG 6 · nach dem Sieg über den Keks-König — Epilog
`story.end` · Flag `story_end_seen` · läuft **im Hub**, direkt nach der Rückkehr

Seite 1–4 erzählen nach, was oben passiert ist — so braucht es keine Zwischen-
sequenz in der Boss-Szene. Wer später doch eine will, schneidet hier.

```
[Die Krone ist gesprungen. Nicht laut. Wie Zuckerguss, der vom Blech bricht.]

KRÜMEL:  »Oh. … es ist leise.«

KRÜMEL:  »Sag der Alten, sie hatte recht. Sag ihr nicht, dass ich das gesagt hab.«

[Hundert Jahre Keks sind sehr viele Krümel. Sie fallen durch die Dielen. Nach unten. In den Ofen.]

DER ERSTE OFEN:  »… danke.«

[Mehr sagt er nie wieder.]

MUTTER HEFE:  »Er ist unten angekommen. Ich hab ihn gehört.«

MUTTER HEFE:  »Setz dich. Es gibt frische.«

MUTTER HEFE:  »Und dann räumst du den Rest weg. Guss hört nicht auf, nur weil der Kopf ab ist. — Aber heute nicht. Heute isst du was.«
```

---

### Im Lauf · `SpawnDirector.Say()`

Drei Sekunden im Wellenfeld, kostet keine Textbox und unterbricht nichts.
**Ohne Umlaute** — das Feld kann keine (darum steht bei euch `KEKS-KOENIG`).

| Welt | Auslöser | Text |
|---|---|---|
| Küche | Phase „Zuckerguss" beginnt | `ES FAENGT AN ZU GLAENZEN` |
| Krümelwald | Boss-Beat | `SIE KENNT DEN NAMEN DER ALTEN` |
| Zuckerdorf | Phase „Marktplatz" | `BLEIB DOCH ZUM KAFFEE` |
| Zuckerdorf | Phase „Backstube" | `DU MUSST NICHT RENNEN` |
| Nachtwald | Phase „Daemmerung" | `HIER HAT ES AUFGEHOERT` |
| Thron | Boss betritt das Feld | `KRUEMEL DER ERSTE` |
| Thron | Phase 2 bei 50 % | `ER HAELT NICHT MEHR` |
| Thron | kurz danach | `SCHLAG DIE KRONE` |
| Thron | unter 15 % | `MACH SCHON` |

### Levelauswahl · Beschreibungstexte

Karte 1 und 2 stehen schon so in `hub.unity` und bleiben.

| Karte | Text |
|---|---|
| 1 Die Küche | `Mehl, Zucker und Ärger. Hier fängt alles an.` |
| 2 Der Krümelwald | `Zwischen den Bäumen wird es voll.` |
| 3 Zuckerdorf | `Alle lächeln. Keiner blinzelt.` |
| 4 Der Nachtwald | `Hier war der Krieg zu Ende. Es ist nie wieder warm geworden.` |
| 5 Der Süße Thron | `Glas, Samt, ein kleines Schild. Der beste Keks von allen.` |

### Die Statuen im Hub

Ersetzt die Seiten an `bookhint - Statue_hero`:
```
Diese Statue zeigt den Held des Zuckers.

Das Gesicht ist abgeschliffen. Jemand hat es angefasst. Oft.
```

`bookhint - Statue_keks` bleibt fast wie gehabt, bekommt eine zweite Seite:
```
Das ist eine Statue, die den ersten gebackenen Keks zeigt.

Es ist dieselbe Statue wie die andere. Das fällt nur niemandem auf.
```

### Optional: Mutter Hefe ansprechen

Sie steht im Hub neben dem Ofen und ist ansprechbar, muss es aber nicht sein —
die Story läuft ohne sie durch. Wer sie anspricht, kriegt einen Satz je Stand:

| Kronen | Zeile |
|---|---|
| 0 | »Steh nicht rum. Die Küche wird nicht leerer.« |
| 1 | »Eine. Und drei, die noch rumlaufen.« |
| 2 | »Halbzeit. Iss was, du klapperst.« |
| 3 | »Eine noch. Und dann red ich mit dir.« |
| 4 | »Der Ofen wartet nicht ewig, und ich auch nicht.« |
| durch | »Setz dich einfach mal hin, Krümel.« |

---

## 8 Art-Prompts

Alle Prompts sind für Pixelart-Bildgeneratoren geschrieben (englisch). Der
**Style-Block** kommt bei *jedem* Prompt davor — er hält die Figuren im selben
Stil wie `fin_main_base_idol.png` und `fin_marshmallow.png`.

### 8.1 Style-Block *(immer voranstellen)*

```
Chunky pixel art game sprite, hand-drawn look, drawn at 64x64 pixels per frame,
subject fills roughly 50x56 px of the cell and sits on the bottom edge.
Thick dark-brown outline (#3B2118), 3 to 4 shading values only, no gradients,
no anti-aliasing, no dithering on the character body, hard pixel edges.
Warm bakery palette: cookie gold #EFB357 / #F2B45B / #DF9A44, deep brown
#3B2118 / #442115 / #301B14, cream #FEE8B3 / #E6C493, burnt orange #EF8735 /
#AF4F18, muted violet shadow #3D324F, dusty rose #79414C.
Front-facing, flat, no perspective, no ground shadow baked in.
Single light source from the upper left. Big simple eyes, one white highlight
pixel each. Cute but worn, Vampire-Survivors top-down readability:
silhouette must read at 32 px.
Fully transparent background. Horizontal sprite strip, frames evenly spaced,
no padding, no labels, no drop shadow, no background scenery.
```

### 8.2 Technische Vorgaben für alle neuen Sheets

| | Wert |
|---|---|
| Frame | 64×64 px (Boss: siehe unten) |
| Sheet | waagerechter Streifen, `frames × 64` breit |
| Import | `spriteMode: 2` (Multiple), `spritePixelsToUnits: 64`, `filterMode: 0` (Point), keine Kompression |
| Boss-Sheet | folgt `fin_der_konig_des_spielfelds.png`: hochskaliert, `spritePixelsToUnits` = Framebreite |
| Ablage | Figuren → `Assets/Art/Chars/`, Gegner → `Assets/Art/Gegner/`, Hub-Objekte → `Assets/Art/new/Hub/` |
| Font-Falle | UI-Text mit Umlauten **nur** Jersey10 (`49e9b75b…`). Das Wellenfeld im Lauf kann keine Umlaute — Banner-Text ohne (`KEKS-KOENIG`) |

> `.meta`-Konvention beim Kopieren beachten: alle `spriteID`s und Namen neu
> setzen, sonst zeigen zwei Sheets auf dieselben Sub-Sprites.

---

### PROMPT 1 — Mutter Hefe · Idle
*4 Frames, 256×64*

```
[STYLE-BLOCK]

A living sourdough starter in a cracked clay crock, seen from the front.
Squat round terracotta pot in dusty rose (#79414C) and muted brown, one visible
crack running down the left side, a faded flour handprint on the belly of the
pot. Filling the top: pale beige bubbling dough (#E6C493 / #FEE8B3) that rises
slightly above the rim and sags over one edge.
Two large dark eyes sit in the dough surface, formed by bubbles; a wide simple
mouth below, slightly downturned, grumpy but kind. No nose, no arms.
An old worn wooden spoon is stuck upright in the dough at a slant, like a
walking stick. A dusting of flour on the shoulder of the pot.
4-frame idle loop: the dough slowly rises and settles, one bubble swells and
pops at a different spot in each frame, the eyes stay steady, the spoon tilts
by one pixel with the dough.

Horizontal strip, 4 frames, 64x64 each, 256x64 total.
```

### PROMPT 2 — Mutter Hefe · Reden
*2 Frames, 128×64*

```
[STYLE-BLOCK]

Same living sourdough starter in a cracked clay crock as before, identical
colours, identical pot, identical slanted wooden spoon.
2-frame talking loop: frame 1 mouth closed and flat, frame 2 mouth open into a
small dark oval, the whole dough body squashed down by two pixels as if
pushing a word out, one bubble bursting near the mouth.
Eyes half-lidded in both frames. Impatient, not cheerful.

Horizontal strip, 2 frames, 64x64 each, 128x64 total.
```

### PROMPT 3 — Der Erste Ofen · 5 Glut-Stufen
*5 Frames, 320×128 · Frame 64×128 (steht höher als eine Figur)*

```
[STYLE-BLOCK]
Override frame size: 64x128 px per frame, object fills the full width and
stands on the bottom edge.

An ancient stone bread oven, front view. Soot-blackened brick in deep brown
(#3B2118) and grey-violet (#3D324F), uneven hand-laid stones, one long diagonal
crack across the whole front. An arched iron door in the middle with a small
grated window. A short chimney stub on top, leaning. A cold iron peel leans
against the side.
5 frames, same oven, only the fire changes, left to right:
 1 - almost out: a single dull red ember (#AF4F18) behind the grate, everything
     else dark, no light spill.
 2 - low fire: small orange flame (#EF8735), faint warm light on the bricks
     right around the door.
 3 - burning: orange and gold flames (#EF8735 / #F2B45B), warm light washing
     the lower front of the oven, first faint glow from the crack.
 4 - hot: tall bright flames, the crack glows gold along its whole length,
     light spilling onto the ground in front.
 5 - roaring: white-gold fire (#FEE8B3) pressing against the grate, the whole
     oven front lit, sparks rising from the chimney.
No character, no hands, no text on the oven.

Horizontal strip, 5 frames, 64x128 each, 320x128 total.
```

### PROMPT 4 — Die Glasur · Textbox-Overlay
*1 Bild, 320×48, kein Charakter*

```
Pixel art UI overlay element, 320x48 px, fully transparent background.
Thick glossy white-pink icing (#FEE8B3 highlight, #E6C493 body, #79414C shadow)
hanging from the TOP edge of the frame and dripping downward in uneven tongues
of different lengths, the longest reaching about 40 px down, three separate
drops already detached and falling below.
Thick dark outline (#3B2118) on the underside of every drip, one bright
highlight line along the top of each tongue.
Wet, heavy, slow-moving. No face, no eyes, no letters.
Hard pixel edges, no anti-aliasing, no gradient, no drop shadow.
Designed to sit on top of a dialogue box and drip into it.
```

### PROMPT 5 — Die Glasur · Kronen-Sigil
*1 Icon, 32×32 — für Levelauswahl, Unlocks, Fortschritt*

```
[STYLE-BLOCK]
Override frame size: 32x32 px, icon fills 26x26 px, centred.

A crown made of hardened donut glaze, seen from the front. A thick uneven ring
of set icing (#FEE8B3 / #E6C493) with four stubby drip-points rising from it
like crown tines, each tine ending in a frozen drop. The glaze is cracked in
two places, showing dull grey-violet (#3D324F) underneath.
It is clearly a donut that has gone hard and been sharpened by time.
No head wearing it, no gems, no gold. Thick dark outline (#3B2118).
Transparent background, single icon, no text.
```

### PROMPT 6 — Krümel der Erste · Statue / Rückblende
*1 Bild, 64×96*

```
[STYLE-BLOCK]
Override frame size: 64x96 px.

A worn stone statue of a small round cookie standing on a low plinth,
front view. The cookie is carved in pale grey stone (#E6C493 desaturated toward
#3D324F), chunky and simple: round body, two stubby legs, two little arms held
at its sides, chocolate chips rendered as carved dimples.
Its FACE IS WORN SMOOTH - no eyes, no mouth, just a polished blank oval, as if
touched by a hand over and over for a hundred years. The polished area is
slightly lighter than the rest of the stone.
It wears NO crown. Chipped left shoulder, a hairline crack across the chest.
A little dust and cobweb where the plinth meets the body.
Lit dimly from the left, cold violet shadow (#3D324F) on the right side.
Sad, small, ordinary. Not heroic posing.
Transparent background, single image, no base scenery, no text on the plinth.
```

### PROMPT 7 — Der Keks-König · Phase 2
*6 Frames, Boss-Maßstab. Vorlage: `fin_der_konig_des_spielfelds.png`*

```
[STYLE-BLOCK]
Override: boss scale, subject fills the full frame, 6-frame horizontal strip.

A huge cracked cookie king, front view, matching an existing sprite: round
golden-brown cookie body (#EFB357 / #DF9A44), a single large dark eye ringed
like a chocolate chip, a wide flat dark mouth, two tiny stubby arms and two
small feet under the enormous body.
PHASE TWO version, the glaze has taken over:
- The golden crown on his head is no longer separate from him: thick icing
  (#FEE8B3) runs down from the crown over his forehead and has set there,
  sealing one side of his face.
- His eye is glazed over, the pupil dulled to pale cream with only a thin dark
  ring left - he is not looking at anything.
- Deep fracture lines (#301B14) spread across his body from under the crown
  outward, glowing faint orange (#EF8735) in the depths, like a cookie about to
  break apart.
- His mouth hangs open, crumbs falling from the corner.
6-frame loop: heavy laboured breathing, body swelling and sinking, crumbs
dropping from the cracks, the icing on his face catching a highlight, one frame
where the dark ring of his pupil briefly sharpens - he is still in there.
Tragic, not evil. He looks tired, not angry.

Horizontal strip, 6 frames, evenly spaced, transparent background.
```

### PROMPT 8 — Die Erste Krone · Kronenträger Welt 1
*4 Frames, 256×64*

```
[STYLE-BLOCK]

A plump marshmallow enemy, front view: soft rounded off-white body
(#FEE8B3 / #E6C493) with a slightly squashed bottom, two stubby arms, two tiny
feet, two large dark eyes and a small nervous smile.
On its head sits a crown of hardened glaze (#E6C493, cracked, four stubby
drip-tines) that is clearly TOO BIG for it - it slides down over one eye.
4-frame loop: the marshmallow bounces gently in place and pushes the crown back
up with one stubby arm, frame by frame - crown slipping, arm lifting, crown
straightened, crown starting to slip again.
Eager to please, slightly embarrassed. Not menacing.

Horizontal strip, 4 frames, 64x64 each, 256x64 total.
```

### PROMPT 9 — Der Bürgermuffin · Kronenträger Welt 3
*4 Frames, 256×64*

```
[STYLE-BLOCK]

A large muffin enemy standing upright, front view. Fluted brown paper case
(#835A50 / #634145) as the lower body with vertical pleats, a domed golden-brown
muffin top (#EFB357 / #DF9A44) bulging over the rim, blueberries as dark dots.
Small polite eyes and a wide closed friendly smile on the dome.
A sash of white icing (#FEE8B3) runs diagonally across the paper case like a
mayor's ceremonial sash, and a glaze crown (#E6C493, cracked, four drip-tines)
sits neatly and perfectly straight on top.
He is holding out a tiny slice of cake on a tiny plate with one stubby arm,
offering it to the viewer.
4-frame loop: a slow welcoming bow and rise, the offered plate held steady the
whole time, the smile never changing, one frame where the icing sash glistens.
Hospitable to the point of being unsettling.

Horizontal strip, 4 frames, 64x64 each, 256x64 total.
```

### PROMPT 10 — Die Kalte Krone · Kronenträger Welt 4
*4 Frames, 256×64*

```
[STYLE-BLOCK]
Palette shift: cold variant. Keep the outline (#3B2118) but shift body colours
toward frost: pale blue-white #DCEAF2, ice shadow #8FA9C4, deep cold violet
#3D324F. Keep only the crown warm-toned.

A frozen marshmallow enemy, front view: the same plump marshmallow shape as the
common enemy, but encased in frost - a rime crust on its shoulders and head,
small ice crystals growing from its back, a frozen drip hanging from its chin.
Its eyes are two flat dark holes with no highlight at all. No mouth.
The glaze crown on its head has frozen solid ONTO it: the icing has run down
over its forehead and set into a clear ice-like mask, fusing crown and body
into one piece. The crown is the only warm colour in the sprite (#E6C493).
4-frame loop: almost no movement - a very slow shallow sway, one frame where a
crack of frost creeps one pixel further, one frame where a single crystal
catches light. It should look like something that stopped moving a long time
ago and has not noticed.

Horizontal strip, 4 frames, 64x64 each, 256x64 total.
```

### PROMPT 11 — Das zerbrochene Backblech · Story-Fund Welt 4
*1 Bild, 64×64*

```
[STYLE-BLOCK]
Palette shift: cold, frosted. Metal greys #8FA9C4 / #555555 / #3D324F with
warm rust (#AF4F18) in the scratches.

An old baking tray lying on frozen ground, broken cleanly into two pieces that
lie slightly apart, seen from a top-down three-quarter angle.
Dented, scorched dark at the centre from decades of use, rust creeping along
the broken edge. A crust of pale frost (#DCEAF2) along the upper rims and in
the dents.
Scratched into the raised rim of the larger piece, in crooked uneven capital
letters carved by a finger, the word: KRUEMEL
The letters are legible but rough, deeper at the start of each stroke.
Nothing else in frame. Transparent background, no ground, no snow drift.
```

### PROMPT 12 — Die Vitrine · Arena Karte 5
*1 Hintergrundbild, 320×180*

```
Pixel art game background, 320x180 px, painted in the same chunky style as a
cosy 2D bakery game: thick dark outlines (#3B2118), flat shading, warm palette
(#E6C493, #BF9E7A, #B67948) with cold accents (#3D324F, #8FA9C4).

Interior of a bakery shop window at night, seen straight on. Dominating the
centre: a tall glass display case on a wooden counter, its panes catching pale
moonlight from the left. Inside the case, a round velvet-topped cake stand in
dusty rose (#79414C) with a brass rim, empty, waiting - large enough for
something big to sit on.
A small handwritten card in a brass holder in front of the case, too small to
read, just three dark pixel dashes suggesting words.
Around it: empty wire shelves, a cold till, a pair of tongs on a hook, dried
icing crusted along the counter edge and running down one leg in hardened
tongues (#FEE8B3, set hard, cracked).
Cold blue moonlight from the window on the left, one dying warm glow from a
stairwell down on the right - the only warm thing in the room.
No characters, no text, no UI. Dark, still, reverent, like a chapel.
```

### PROMPT 13 — Espresso · Händler *(optional)*
*4 Frames, 256×64*

```
[STYLE-BLOCK]

A small espresso cup character, front view: thick white-cream ceramic cup
(#FEE8B3 / #E6C493) with a chunky handle on the right and a dark coffee surface
(#3B2118) filling the top, crema ring in warm tan (#BF9E7A).
Two wide over-caffeinated eyes float in the coffee surface, pupils tiny,
both looking in slightly different directions. A jittery open mouth below.
It stands on a saucer that it never quite stops sliding around on.
4-frame loop: vibrating in place - the cup shakes one pixel left and right, the
coffee surface sloshes and one drop jumps out and back, the saucer rattles, the
eyes never blink.
Fast, friendly, exhausting.

Horizontal strip, 4 frames, 64x64 each, 256x64 total.
```

### PROMPT 14 — Die vier Kekse · Auswahl-Portraits
*4 Bilder, je 48×48 — für `HubCharacterSelectUI`*

```
[STYLE-BLOCK]
Override frame size: 48x48 px portrait bust, head and shoulders only, centred,
facing the viewer.

Four separate portraits of round cookie characters in the same pose and
lighting, to be used side by side in a character select screen:

1 BRAUNER KEKS - a plain golden-brown chocolate chip cookie (#EFB357 / #DF9A44),
  four dark chips, calm steady eyes, small neutral mouth. Utterly ordinary and
  quietly fine with that.

2 GRAUER KEKS - the same cookie baked ten minutes too long: dark charcoal brown
  (#442115 / #301B14) with burnt-orange edges (#AF4F18), a chipped corner, one
  eye narrowed, jaw set. Hard and unbothered.

3 ROTER KEKS - a cookie with too much red in the dough (#79414C into #EF8735),
  edges slightly raw and glossy, both eyes wide and darting, mouth a tight
  crooked line, one crumb flaking off mid-air. Twitchy, first to swing.

4 MARMELADE - not a cookie at all: a small squat jam jar (#79414C glass filled
  with dark red preserve, #E6C493 cloth lid tied with string), eyes on the jar
  front, a shy small smile, a smear of jam on the glass. Standing with the
  others as if nobody has questioned it yet.

Each on a fully transparent background, no frame, no text, no background colour.
```

---

## 9 Einbau

Die Story läuft mit **drei kleinen Eingriffen**. Alles danach ist Kür und kann
einzeln nachkommen, ohne dass etwas davon abhängt.

### Das Kleinste, was funktioniert

**A) Callback an der Textbox** — `Assets/Scripts/Hub/HubUI.cs`

`ShowDialogue(string[])` hat heute keinen Rückweg. Ohne den weiß die
Levelauswahl nicht, wann sie laden darf. Eine Überladung reicht:

```csharp
System.Action onClosed;                 // Feld neben pages

public void ShowDialogue(string[] newPages, System.Action closed)
{
    onClosed = closed;
    ShowDialogue(newPages);
}
```
und in `CloseDialogue()` ganz am Ende:
```csharp
var cb = onClosed; onClosed = null; cb?.Invoke();
```
Erst nullen, dann rufen — sonst hängt der Callback noch, wenn er selbst eine
neue Textbox öffnet.

**B) Der Katalog** — neu: `Assets/Scripts/Hub/Story/StoryDialogs.cs`

Statischer Katalog wie `Ach` und `Unlocks`, keine ScriptableObjects, kein
Inspector. Zwei Methoden reichen:

```csharp
public static string[] Briefing(int mapId);   // null = kein Dialog für die Karte
public static bool Seen(int mapId);           // schon gelaufen?
public static void MarkSeen(int mapId);
```

Speichern über das, was es schon gibt: eine Unlock-Id je Dialog
(`story_w1_seen` … `story_w5_seen`, `story_end_seen`) in `Unlocks.cs`
eintragen — dann liegt der Story-Stand automatisch im Spielstand, überlebt
Neustarts und taucht sogar im Unlock-Panel auf, falls ihr das wollt. Kein
eigenes Speicherformat.

**C) Der Hook** — `HubLevelSelectUI.Play()`, Zeile 994

Ganz an den Anfang von `Play()`, **nach** der `mayPlay`- und `IsLinked`-Prüfung
(sonst läuft der Dialog auch bei einem gesperrten Level) und **vor** allem
anderen. Der eigentliche Startablauf wandert unverändert in ein
`StartRun()`:

```csharp
// Story-Briefing: nur im Story-Modus, nur beim ersten Mal.
// Die Auswahl macht vorher zu - die Textbox liegt auf sortingOrder 100,
// die Levelauswahl auf 135 und wuerde sie sonst verdecken.
if (!endlessChosen && !StoryDialogs.Seen(e.mapId))
{
    string[] pages = StoryDialogs.Briefing(e.mapId);
    if (pages != null && HubUI.Instance != null)
    {
        StoryDialogs.MarkSeen(e.mapId);
        Close();
        HubUI.Instance.ShowDialogue(pages, () => StartRun(e));
        return;
    }
}

StartRun(e);
```

⚠️ **Die Reihenfolge ist wichtig.** `Close()` muss vor `ShowDialogue` laufen:
die Textbox hängt am HubUI-Canvas (`sortingOrder = 100`), die Levelauswahl liegt
auf 135. Umgekehrt redet Mutter Hefe hinter dem Auswahlfenster.

`MarkSeen` steht bewusst **vor** dem Dialog, nicht im Callback: wer mitten im
Briefing Alt+F4 drückt, soll es beim nächsten Mal nicht nochmal sehen.

Damit läuft die ganze Story. Mehr ist für Punkt 1–5 der Dialogliste nicht nötig.

### Epilog nach dem Boss

Der einzige Dialog, der nicht am SPIELEN-Knopf hängt. Der Hub weiß beim Laden,
ob der Lauf gewonnen wurde (`GameSession`) — beim Betreten prüfen:
Karte 5 geschafft und `story_end_seen` noch offen → einmal `ShowDialogue`.
Ein `Start()` im Hub, fünf Zeilen.

### Danach, in dieser Reihenfolge

1. **Texte ohne Code** — Levelauswahl-Beschreibungen für Karte 3–5, die neuen
   Seiten an `bookhint - Statue_hero` und `- Statue_keks`. Nur Felder in
   `hub.unity`. ⚠️ Szene nie zwischen Markern ersetzen, Unity sortiert die Datei
   nach `fileID` um — Felder einzeln anfassen.

2. **`SpawnDirector.Say()`-Zeilen** — die Einwürfe aus Abschnitt 7 an Beats in
   `WavePlans.cs` hängen. Kein neuer Code, nur Aufrufe. **Ohne Umlaute.**

3. **Sprecher über dem Text** — Dialog 6 wechselt zwischen Krümel, dem Ofen und
   Mutter Hefe. Solange es keine Namenszeile gibt, steht der Sprecher im Text
   (`KRÜMEL: »…«`) — das reicht fürs Erste. Schöner: eine zweite TMP-Zeile über
   `bodyText` und `ShowDialogue(speaker, pages, closed)`.

4. **Mutter Hefe als Sprite im Hub** — sie muss nicht ansprechbar sein, die
   Story läuft ohne sie durch. Aber sie sollte zu sehen sein, sonst redet eine
   Textbox mit sich selbst. Wenn ansprechbar: `HubInteractable`, Vorlage
   `BookHint.cs`, zwanzig Zeilen.

5. **Glut-Stufen im Hub** — `Global Light 2D`, die vier `Spot Light 2D` und das
   Ofen-Sprite an die Zahl der zurückgebrachten Kronen hängen. Ein Skript, fünf
   Zustände. Bester sichtbarer Gewinn pro Zeile Code im ganzen Dokument.

6. **Karte 5** — neue Map-Szene `Map_World4` (Die Vitrine) plus Wellenplan
   `World4`: kurzer Anmarsch, dann `BossFinale`. In `hub.unity` an Karte 5
   `mapId: 4` eintragen — die Sperre `Level5_Story` steht dort schon.

7. **Kronenträger statt Keks-König in Welt 1–4** — heute läuft `BossFinale` mit
   `EnemyId.KeksKoenig` auf jeder Karte. Für die Story braucht jede Welt ihren
   eigenen Kronenträger, und der König bleibt Karte 5 vorbehalten. Vier neue
   `EnemyId`s, Prefabs aus den Mini-Boss-Varianten, `BossFinale(plan, bossId)`
   mit Parameter. Das ist der größte Posten der Liste — die Story funktioniert
   aber auch ohne, dann steht der König eben noch überall.

8. **Englisch** — die Dialoge in `en.json` sind mechanische Nacharbeit, sobald
   der deutsche Text steht. Nicht vorher: solange umgeschrieben wird, laufen
   beide Dateien auseinander. ⚠️ `de.json`/`en.json` sind CRLF mit einem Eintrag
   pro Zeile — anhängen, nie per Serializer neu schreiben.

### Was Endless betrifft

Nichts. `endlessChosen` ist im Hook die erste Bedingung, damit fällt der ganze
Story-Zweig weg. Die Endlos-Phase nach dem Boss steht bereits in
`WavePlans.BossFinale()` und bleibt unangetastet.
