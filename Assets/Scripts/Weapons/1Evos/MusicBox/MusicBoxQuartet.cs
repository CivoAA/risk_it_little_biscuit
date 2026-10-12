using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spieluhr-Quartett - Evo aus Mochi-Melodie + Turret.
///
/// Mochi stellt <see cref="Boxes"/> Musikboxen (<see cref="MusicBox"/>) um
/// sich herum ab, jede in eine andere Richtung. Jede spielt die Melodie wie
/// die Mochi-Melodie auf Hoechststufe: Noten auf den Schlaegen, Akkord-Ring
/// auf der Vier, Notenregen im Finale. Mochi selbst singt nicht mehr mit.
///
/// Alles haengt an einem Takt: es gibt nur eine Melodie, die Boxen spielen
/// sie gleichzeitig. Gesetzt werden sie auf der Eins, sie stehen
/// <see cref="BoxBars"/> Takte und klappen auf der Eins wieder zu - im selben
/// Moment stehen die neuen an Mochis neuer Stelle. Ohne Notenblatt fuer die
/// Musik zaehlt der eigene Takt aus dem Cooldown (wie bei der Melodie).
///
/// Skilltree (Mochi) laeuft mit:
///   Fortissimo - ein Crescendo fuer alle Boxen; ganz oben werden alle Ringe
///                auf einmal doppelt so gross.
///   Bass-Sog   - Treffer einer Box locken Gegner zu ihr (<see cref="BassSog"/>).
///   Paukenschlag geht weiter von Mochi aus (<see cref="Paukenschlag"/>).
/// </summary>
public class MusicBoxQuartet : MochiMelody
{
    /// <summary>So viele Boxen stehen auf einmal.</summary>
    public const int Boxes = 4;

    /// <summary>So viele Takte steht ein Satz Boxen, dann kommt der naechste.</summary>
    public const int BoxBars = 4;

    /// <summary>Abstand der Boxen zu Mochi (Tiles).</summary>
    public const float BoxDistance = 2.2f;

    /// <summary>Schaden je Box relativ zur hoechsten Melodie-Stufe.</summary>
    public const float DamageFactor = 1.2f;

    /// <summary>Zielsuche je Box (Melodie Hoechststufe: 8) - jede Box kuemmert sich um ihre Ecke.</summary>
    public const float BoxRange = 7f;

    protected override int TableLevel => int.MaxValue;

    private readonly List<MusicBox> boxes = new List<MusicBox>();
    private int barsLeft;
    private double swapDsp = -1;
    private int layout;

    protected override void Awake()
    {
        base.Awake();
        WeaponStats max = stats[stats.Count - 1].Clone();
        max.damage *= DamageFactor;
        max.range = BoxRange;
        max.description = "Vier Musikboxen spielen die Melodie im Takt, alle vier Takte an neuer Stelle";
        stats = new List<WeaponStats> { max };
        maxweaponLevel = 0;
    }

    protected override void CollectSources(List<Source> into)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            MusicBox box = boxes[i];
            if (box == null || box.Closing) continue;
            into.Add(new Source { center = box.Center, mouth = box.Mouth, box = box });
        }
    }

    protected override void OnStarted()
    {
        // Die ersten Boxen kommen auf der naechsten Eins
        barsLeft = 0;
        swapDsp = -1;
    }

    protected override void OnStopped()
    {
        swapDsp = -1;
        CloseAll();
    }

    void OnDisable()
    {
        CloseAll();
    }

    /// <summary>Auf jeder Eins nachzaehlen; nach <see cref="BoxBars"/> Takten auf genau dieser Eins neu setzen.</summary>
    protected override void OnStep(double beat, double dsp)
    {
        if (Mathf.Abs((float)(beat % 4.0)) > 1e-3f) return;

        if (barsLeft > 0)
        {
            barsLeft--;
            if (barsLeft > 0) return;
        }
        barsLeft = BoxBars;
        swapDsp = dsp;
    }

    /// <summary>Der Wechsel geht genau dann, wenn die Eins erklingt - vor den Noten dieses Schlags.</summary>
    protected override void BeforeFire(double dspNow)
    {
        if (swapDsp < 0 || dspNow < swapDsp) return;
        swapDsp = -1;
        PlaceBoxes();
    }

    private void PlaceBoxes()
    {
        CloseAll();

        PlayerController player = PlayerController.Instance;
        Vector2 at = player != null ? (Vector2)player.transform.position : (Vector2)transform.position;

        // Abwechselnd ueber Kreuz und schraeg, damit es nicht immer dieselben Ecken sind
        float start = layout % 2 == 0 ? 45f : 0f;
        layout++;
        for (int i = 0; i < Boxes; i++)
        {
            float a = (start + 360f / Boxes * i) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            boxes.Add(MusicBox.Spawn(at + dir * BoxDistance));
        }
    }

    private void CloseAll()
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            if (boxes[i] != null) boxes[i].Close();
        }
        boxes.Clear();
    }
}
