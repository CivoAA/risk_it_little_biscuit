using System;
using UnityEngine;

/// <summary>
/// Emotes im Koop: Tasten 1-4 setzen eine Sprechblase ueber die eigene
/// Figur, die alle sehen (Herz, Hilfe, Haha, GG). Gezeichnet wird sie vom
/// <see cref="NetHud"/>; hier nur Eingabe, Netz und wer gerade was zeigt.
///
/// Die Ziffern waehlen sonst Level-Up-Karten und Mixer - solange das eigene
/// Auswahlfenster offen ist (oder das Pausenmenue), bleiben sie dort.
/// </summary>
public class NetEmotes : MonoBehaviour
{
    public enum Kind : byte { Heart = 0, Help = 1, Haha = 2, Gg = 3 }

    public const int Count = 4;

    /// <summary>So lange steht eine Blase.</summary>
    public const float Duration = 2.4f;

    private const float Cooldown = 0.7f;

    public static NetEmotes Active { get; private set; }

    /// <summary>Platz, Emote - fuer Klang und Anzeige.</summary>
    public static event Action<int, Kind> Shown;

    private NetSession session;
    private NetRun run;
    private float nextAllowed;

    private readonly Kind[] kinds = new Kind[NetSession.MaxPlayers];
    private readonly float[] shownAt = new float[NetSession.MaxPlayers];

    private void Awake()
    {
        Active = this;
        session = NetSession.Instance;
        run = GetComponent<NetRun>();
        for (int i = 0; i < shownAt.Length; i++) shownAt[i] = -999f;
        session.On(NetMsg.Emote, OnEmote);
    }

    private void OnDestroy()
    {
        session?.Off(NetMsg.Emote);
        if (Active == this) Active = null;
    }

    /// <summary>Zeigt dieser Platz gerade eine Blase? Alter in Sekunden.</summary>
    public bool TryGet(int slot, out Kind kind, out float age)
    {
        kind = Kind.Heart;
        age = 0f;
        if (slot < 0 || slot >= shownAt.Length) return false;
        age = Time.unscaledTime - shownAt[slot];
        if (age < 0f || age > Duration) return false;
        kind = kinds[slot];
        return true;
    }

    private void Update()
    {
        if (run == null || run.CurrentPhase != NetRun.Phase.Running || run.LocalOut) return;
        if (PauseMenuPanel.IsOpen || LocalChoosing()) return;
        if (Time.unscaledTime < nextAllowed) return;

        for (int i = 0; i < Count; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i) && !Input.GetKeyDown(KeyCode.Keypad1 + i)) continue;
            nextAllowed = Time.unscaledTime + Cooldown;
            Send((Kind)i);
            break;
        }
    }

    /// <summary>Eigenes Emote zeigen und an alle schicken.</summary>
    public void Send(Kind kind)
    {
        session.Begin(NetMsg.Emote).Byte((byte)kind);
        session.SendAll();
        Show(session.LocalSlot, kind);
    }

    private static bool LocalChoosing()
    {
        if (TutorialPanel.Blocking) return true;
        UIController ui = UIController.Instance;
        if (ui == null) return false;
        return (ui.LevelUpPanel != null && ui.LevelUpPanel.activeSelf)
            || (ui.PowerUpPanel != null && ui.PowerUpPanel.activeSelf)
            || (ui.EvoPanel != null && ui.EvoPanel.activeSelf);
    }

    private void OnEmote(int from, NetReader r)
    {
        int kind = r.Byte();
        if (from < 0 || from >= shownAt.Length || kind >= Count) return;
        Show(from, (Kind)kind);
    }

    private void Show(int slot, Kind kind)
    {
        if (slot < 0 || slot >= shownAt.Length) return;
        kinds[slot] = kind;
        shownAt[slot] = Time.unscaledTime;
        Shown?.Invoke(slot, kind);
    }
}
