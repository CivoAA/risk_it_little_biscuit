"""Globale Spielmetriken von Steam abrufen (RunMetrics.cs zaehlt sie im Spiel).

Braucht einen Publisher-Web-API-Key (Steamworks > Users & Permissions >
Manage Groups > Web API Key). Der Key gehoert NIE ins Repo - nur als
Umgebungsvariable:

    PowerShell:  $env:STEAM_PUBLISHER_KEY = "..."
    python Tools/steam_metriken.py
    python Tools/steam_metriken.py --csv metriken.csv
    python Tools/steam_metriken.py --tage 7      # nur die letzten 7 Tage
    python Tools/steam_metriken.py --reset       # Nullpunkt auf jetzt setzen

Steam kann die Gesamtsummen nicht zuruecksetzen. --reset merkt sich den
aktuellen Stand in Tools/metriken_nullpunkt.json, danach zaehlt nur, was
seitdem dazukam (gilt nicht fuer --tage).

Die Stat-Namen kommen aus steam_metriken_stats.txt im Projektordner
(Unity: Tools > Erfolge > Metriken-Stats ausgeben).
"""
import argparse
import csv
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

APP_ID = 4036370
URL = "https://partner.steam-api.com/ISteamUserStats/GetGlobalStatsForGame/v1/"
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CHUNK = 40
BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "metriken_nullpunkt.json")

CHARS = ["Keks", "Marmelade", "Onigiri", "Toast", "Zwiebelritter", "Mochi", "Inari", "Schleimkoenig", "Tofu"]
MAPS = {"world1": "Kueche", "world2": "Wald", "world5": "Eis", "world6": "Geisterwald", "world4": "Vulkan"}


def fetch(key, names, start=None, end=None):
    params = {"key": key, "appid": APP_ID, "count": len(names)}
    for i, n in enumerate(names):
        params[f"name[{i}]"] = n
    if start is not None:
        params["startdate"] = start
        params["enddate"] = end
    url = URL + "?" + urllib.parse.urlencode(params)
    try:
        with urllib.request.urlopen(url, timeout=30) as r:
            data = json.load(r)["response"]
    except urllib.error.HTTPError as e:
        sys.exit(f"HTTP {e.code} - Key falsch oder kein Publisher-Key? ({e.reason})")
    if data.get("result") != 1:
        return None
    out = {}
    for name, entry in data.get("globalstats", {}).items():
        if "total" in entry:
            out[name] = int(entry["total"])
        elif "history" in entry:
            out[name] = sum(int(h["total"]) for h in entry["history"])
    return out


def fetch_all(key, names, start=None, end=None):
    values, missing = {}, []
    for i in range(0, len(names), CHUNK):
        chunk = names[i:i + CHUNK]
        got = fetch(key, chunk, start, end)
        if got is None:
            # Ein nicht angelegter / nicht aggregierter Stat laesst die ganze Anfrage
            # scheitern - dann einzeln nachfragen.
            got = {}
            for n in chunk:
                single = fetch(key, [n], start, end)
                if single is None:
                    missing.append(n)
                else:
                    got.update(single)
        values.update(got)
    return values, missing


def pct(part, whole):
    return f"{100 * part / whole:5.1f} %" if whole else "    - "


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv", help="zusaetzlich alle Rohwerte in diese Datei schreiben")
    ap.add_argument("--tage", type=int, help="nur die letzten N Tage statt Gesamtsumme")
    ap.add_argument("--reset", action="store_true", help="aktuellen Stand als Nullpunkt merken")
    args = ap.parse_args()

    key = os.environ.get("STEAM_PUBLISHER_KEY")
    if not key:
        sys.exit("STEAM_PUBLISHER_KEY ist nicht gesetzt.")

    path = os.path.join(ROOT, "steam_metriken_stats.txt")
    names = [l.strip() for l in open(path, encoding="utf-8") if l.strip()]

    start = end = None
    if args.tage:
        end = int(time.time())
        start = end - args.tage * 86400

    v, missing = fetch_all(key, names, start, end)

    if args.reset:
        stamp = time.strftime("%d.%m.%Y %H:%M")
        with open(BASE, "w", encoding="utf-8") as f:
            json.dump({"seit": stamp, "werte": dict(sorted(v.items()))}, f, indent=2)
        print(f"Nullpunkt gesetzt ({stamp}): {len(v)} Stats in {BASE}")
        return

    zeitraum = "letzte %d Tage" % args.tage if args.tage else "gesamt"
    if not args.tage and os.path.exists(BASE):
        base = json.load(open(BASE, encoding="utf-8"))
        v = {n: max(0, x - base["werte"].get(n, 0)) for n, x in v.items()}
        zeitraum = "seit " + base["seit"]
    g = lambda n: v.get(n, 0)

    print(f"\n=== Charaktere ({zeitraum}) ===")
    runs_total = sum(g(f"m_char{c}_runs") for c in range(len(CHARS)))
    print(f"{'Charakter':<15}{'Laeufe':>8}{'Anteil':>10}{'Siege':>8}{'Siegquote':>11}{'Tode':>7}{'Abbruch':>9}{'Stunden':>9}")
    for c, name in sorted(enumerate(CHARS), key=lambda x: -g(f"m_char{x[0]}_runs")):
        r, w, d, m = (g(f"m_char{c}_{s}") for s in ("runs", "wins", "deaths", "min"))
        print(f"{name:<15}{r:>8}{pct(r, runs_total):>10}{w:>8}{pct(w, r):>11}{d:>7}{max(0, r - w - d):>9}{m / 60:>9.1f}")

    print("\n=== Welten ===")
    print(f"{'Welt':<15}{'Story':>8}{'Endless':>9}{'Siege':>8}{'Siegquote':>11}{'Stunden':>9}")
    for m, name in MAPS.items():
        s, e, w, mi = (g(f"m_map_{m}_{x}") for x in ("story", "endless", "wins", "min"))
        print(f"{name:<15}{s:>8}{e:>9}{w:>8}{pct(w, s):>11}{mi / 60:>9.1f}")

    print("\n=== Waffen / Buffs / Evos (am Laufende im Besitz) ===")
    picks = sorted(((n[len("m_pick_"):], g(n)) for n in names if n.startswith("m_pick_")), key=lambda x: -x[1])
    for wid, count in picks:
        print(f"{wid:<24}{count:>8}{pct(count, runs_total):>10} der Laeufe")

    if missing:
        print(f"\n{len(missing)} Stats fehlen in Steamworks oder sind nicht 'Aggregated':")
        print("  " + ", ".join(missing))

    if args.csv:
        with open(args.csv, "w", newline="", encoding="utf-8") as f:
            wr = csv.writer(f)
            wr.writerow(["stat", "wert"])
            for n in names:
                wr.writerow([n, g(n)])
        print(f"\nCSV: {args.csv}")


if __name__ == "__main__":
    main()
