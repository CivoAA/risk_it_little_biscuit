# Skilltree — Stand und offene Punkte

Stand: 2026-09-18, Branch `new_skill_tree`.
Diese Datei ist die Übergabe für eine neue Claude-Session. Kurz gesagt: das **System**
steht, der **Inhalt** fehlt noch.

---

## Das Ziel

Jeder Charakter hat seinen eigenen Skilltree mit vier Unterkategorien in dieser
Reihenfolge: **Kampf, Geist, Wissen, Glück**. Jede Kategorie hat drei Bahnen
(oben / Mitte / unten), die von links nach rechts laufen — wie im Konzeptbild
`Assets/Konzept_art/Skill_tree.png`.

Jede Kategorie fängt mit **genau einem Startknoten** an (Bahn Mitte, Spalte 0,
Id `kampf_start` usw.). Er kostet nichts, gibt nichts und ist immer offen — von
ihm gehen die drei Bahnen ab, wie der START-Orb im Konzeptbild. Alles andere
hängt daran. Dafür sorgt `SkillTreeAsset.EnsureStartNodes()`; der Editor ruft das
beim Öffnen eines Baums, der Textimport nach dem Einlesen.

Eric baut die Bäume selbst im Editor. Claude baut das System und kann Bäume auf
Zuruf per Textformat erzeugen ("mach Charakter 2 wie Charakter 0, nur mit mehr
Leben").

---

## Was schon fertig ist

### Daten
Ein **ScriptableObject-Asset je Charakter** unter `Assets/Resources/SkillTrees/`.
Bewusste Ausnahme von der sonstigen Regel "Daten in Code" — die zielt auf
Szenen-YAML, nicht auf Assets. Ein Graph-Editor, der C# neu schreibt, wäre fragil.

| Datei | Wofür |
|---|---|
| `Scripts/Skills/Data/SkillTreeAsset.cs` | Das Asset: Kategorien + Knotenliste |
| `Scripts/Skills/SkillCategory.cs` | Kategorie / Bahn / Form, Farben und Vorgabetexte |
| `Scripts/Skills/SkillReward.cs` | Belohnung + Standardwerte je Effekt |
| `Scripts/Skills/SkillGrants.cs` | **Erweiterungspunkt** für Nicht-Wert-Belohnungen |
| `Scripts/Skills/SkillTrees.cs` | Lädt die Assets, `ForCharacter(skinIndex)` |
| `Scripts/Skills/Skills.cs` | `Bonus(...)`, `HasGrant(...)`, Kaufen, Punkte |
| `Scripts/Skills/SkillTreeLayout.cs` | Bahn + Spalte → Position |

### Oberfläche im Hub
| Datei | Wofür |
|---|---|
| `Scripts/Hub/Skilltree/HubSkilltreeUI.cs` | Das Fenster (Titel, Kategorieknöpfe, Karte rechts, Punkte) |
| `Scripts/Hub/Skilltree/HubSkilltreeGraph.cs` | Der Baum im großen Feld, scrollbar |
| `Scripts/Skills/UI/SkillShapeSprites.cs` | Zeichnet die Knotenformen als Pixeltextur |

Scrollen: Mausrad, A/D, Pfeiltasten, Pfeile am Feldrand.
Kategorie wechseln: Klick, W/S, Hoch/Runter.
Die Karte rechts zeigt den Knoten unter der Maus — und, solange die Maus über
einem Kategorieknopf links steht, dessen Kategorie. So sieht man vor dem Klick,
worum es in dem Pfad geht.
Formen: Kreis, Rechteck, Raute, Stern, Sechseck, Dreieck, Kreuz — je Knoten wählbar.
Ein eigenes Sprite am Knoten gewinnt über die gezeichnete Form.

### Werkzeug
`Tools → Skilltree → Editor` (`Scripts/Skills/Editor/SkillTreeEditorWindow.cs`)
- Oben steht die **Charakterliste**: jeder Charakter aus `Scripts/Shop/Characters.cs`,
  auch die ohne Baum ("noch kein Baum — anlegen"). Klick legt ihn an, wahlweise als
  Kopie eines anderen. Bäume ohne Charakter stehen darunter.
- Kategorie oben umschalten
- Knoten anklicken → rechts Name, Form, Preis, Belohnungen, Vorbedingungen
- Am gewählten Knoten drei Plus-Knöpfe: geradeaus, schräg hoch, schräg runter.
  Der neue Knoten hängt automatisch am Ursprung, die Linie zeichnet sich selbst.
- Zwei Vorgänger (z. B. "oben-2 braucht oben-1 UND mitte-1"): Knopf "Verbinden"
  oder die Hakenliste rechts
- Knoten lassen sich ziehen, sie rasten auf Bahn und Spalte ein
- Löschen: das kleine rote **x** an der Ecke des gewählten Knotens, **Entf**, oder
  der Knopf unten im Inspektor. Was daran hing, rutscht auf den Vorgänger nach.
- Der Startknoten ganz links steht fest: nicht verschiebbar, nicht löschbar,
  kein Preis, keine Belohnung. Name und Beschreibung darf er haben.

Weitere Menüpunkte unter `Tools → Skilltree`: Bäume prüfen, Übersicht ausgeben,
Baum aus Textdatei bauen, Baum als Text speichern, Spielstand zurücksetzen,
Beispielbaum neu anlegen.

### So baust du eine Kategorie
1. `Tools → Skilltree → Editor`, oben den **Charakter** wählen, darunter die
   Kategorie. Hat der Charakter noch keinen Baum, fragt der Klick, ob er angelegt
   werden soll — leer oder als Kopie eines anderen.
2. Links steht der **Startknoten**. Ihn anklicken — es erscheinen drei
   Plus-Knöpfe: `+` geradeaus (Bahn Mitte), `↗` nach oben, `↘` nach unten.
   Das sind die drei Wege.
3. Jeden neuen Knoten anklicken und rechts ausfüllen: Name, Form, Preis,
   und unter "Was der Knoten gibt" die Belohnung (Wert oder Schalter).
4. Weiterbauen: den eben angelegten Knoten anklicken, wieder einen Plus-Knopf.
   Der neue hängt automatisch am vorigen, die Linie zeichnet sich selbst.
5. Soll ein Knoten **zwei** Vorgänger haben, ihn anklicken, rechts auf
   "Verbinden" und dann den zweiten Vorgänger anklicken.
6. Falsch gesetzt? Knoten anklicken und **Entf** drücken oder auf das rote **x**
   an seiner Ecke. Was daran hing, rutscht auf den Vorgänger nach.
7. Zwischendurch `Tools → Skilltree → Bäume prüfen` — das meldet Knoten, die an
   nichts hängen, doppelte Plätze und leere Belohnungen.

Spalte 0 gehört dem Startknoten; alles andere fängt bei Spalte 1 an. Ziehen
rastet darum frühestens auf Spalte 1 ein.

### Belohnungen — so wird erweitert
- **Werte**: Eintrag in `SkillType` (`Scripts/Skills/SkillType.cs`), Standardwert in
  `SkillDefaults.ValueFor(...)`, Text in `SkillText.Fallback(...)`. Taucht sofort im
  Editor-Dropdown auf.
- **Schalter** (Waffen-Upgrades usw.): zwei Zeilen in `SkillGrants.cs` — eine
  Konstante und ein Eintrag in `All`. Im Spiel abfragen mit
  `Skills.HasGrant(SkillGrants.Irgendwas)`.
- Ein Knoten darf mehrere Belohnungen gleichzeitig haben.

Verdrahtetes Beispiel: `kampf_oben_3` (Stern, braucht oben-2 **und** mitte-3) gibt
`shurikookie_vier_richtungen`; `Scripts/Weapons/Shurikookie/ShurikenWeapon.cs`
fragt das in `ThrowDirections()` ab und wirft dann in alle vier Richtungen.

### Bäume per Text erzeugen
Format und Beispiele: `Scripts/Skills/Editor/SkillTreeTextIO.cs` (oben im Kommentar).
Vorlage: `Assets/Scripts/Skills/Vorlagen/char_0_standard.txt`.
Eine Zeile je Knoten, z. B.:

```
node id=kampf_oben_3 cat=Kampf lane=Oben step=4 shape=Stern price=60
     name="Vier Richtungen"
     grant=shurikookie_vier_richtungen stat=IncreaseDamage:0.1
     req=kampf_oben_2,kampf_mitte_3
```

Einlesen: `Tools → Skilltree → Baum aus Textdatei bauen`. Überschreibt den Baum mit
derselben `treeId`.

Der Startknoten muss nicht im Text stehen: fehlt er, wird er beim Einlesen
ergänzt, und jede Zeile ohne `req=` hängt sich an ihn.

---

## Was noch offen ist

1. **Inhalt.** Im Hub gibt es zur Zeit nur Charakter 0 (den Animations-Dummy); er
   hängt an `char_0.asset` (`characterIndex: 0`) und bekommt damit den Demo-Baum —
   ohne Balance. Mehr Charaktere gibt es nicht — die drei aus der alten World Map
   sind aus `Scripts/Shop/Characters.cs` raus, ihre Startwaffen stehen dort im
   Kommentar. Kommt einer dazu: in den zwei Listen dort einen Eintrag ergänzen,
   dann steht er im Editor oben in der Auswahl und der Klick legt seinen Baum an.
   Ein Charakter ohne Baum bekommt den Notbaum (Startknoten plus drei leere Bahnen).
2. **Assets.** Alle Formen und Symbole sind zur Laufzeit gezeichnete Platzhalter.
   Eric will da später Pixelart einsetzen — je Knoten über das Feld "Eigenes Bild",
   je Kategorie über "Symbol", Hintergrund über `backgroundSprite` in der
   `HubSkilltreeUI`.
3. **Eigene Schalter.** In `SkillGrants` stehen vier Einträge, zwei davon
   (`zweites_leben`, `erste_truhe_gratis`) sind reine Platzhalter und im Spiel
   nirgends abgefragt.
4. **Alter Fortschritt.** Die alten Äste (Wind/Sword/Heart/Clock) sind weg,
   bestehende Einträge in `skills.json` zählen nicht mehr. War so abgesprochen.
5. **World Map.** `Scripts/Skills/UI/SkillTreeView.cs` und `SkillTreeUIManager.cs`
   gehören zur alten World-Map-Szene und wurden nicht angefasst. Die Szene wird
   nicht mehr angesteuert.

---

## Erster Start in Unity

Beim ersten Öffnen legt `Scripts/Skills/Editor/SkillTreeBootstrap.cs` den
Beispielbaum für Charakter 0 einmalig aus der Textvorlage an und meldet das in der
Konsole. Sobald der Ordner `Assets/Resources/SkillTrees` existiert, tut das Skript
nichts mehr. Die Datei darf weg, sobald es eigene Bäume gibt.

Danach: `Tools → Skilltree → Editor`.
