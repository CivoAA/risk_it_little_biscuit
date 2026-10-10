"""
Nachtsimulation fuer die Vorschauen des Geisterwalds - so ungefaehr rechnet
das URP-2D-Licht im Spiel (Multiply, HDR-Emulation 1 = Licht kappt bei 1).
Das Projekt laeuft im linearen Farbraum, also wird linear gerechnet:

  Licht   = Umgebung (globales Licht der Nacht) + Summe der Punktlichter
  Ergebnis = sRGB(linear(Grundbild) * min(Licht, 1)), danach die Glow-Bilder unbeleuchtet drueber

Werte wie in Geisterwald.cs (NightColor/NightIntensity) halten, sonst luegt die Vorschau.
"""

import numpy as np
from PIL import Image

AMBIENT = (0.0945, 0.119, 0.35)   # = Geisterwald.nightColor * nightIntensity (linear)


def falloff(d, radius, inner=0.0):
    t = np.clip((radius - d) / max(1e-3, radius - inner), 0, 1)
    return t * t * (3 - 2 * t) * (0.55 + 0.45 * t)


def night(base, glow, lights, ambient=AMBIENT, ppu=32):
    """lights: [(x_px, y_px, {r,g,b,radius,intensity,inner}), ...] in Bildpixeln."""
    a = np.asarray(base.convert("RGBA")).astype(np.float64) / 255.0
    h, w = a.shape[:2]
    lt = np.zeros((h, w, 3))
    lt[:] = ambient
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float64)
    for (lx, ly, d) in lights:
        r = d["radius"] * ppu
        dist = np.sqrt((xs + 0.5 - lx) ** 2 + (ys + 0.5 - ly) ** 2)
        f = falloff(dist, r, d.get("inner", 0.0) * ppu) * d["intensity"]
        lt += f[..., None] * np.array([d["r"], d["g"], d["b"]])
    lt = np.minimum(lt, 1.0)
    out = a.copy()
    lin = np.where(a[..., :3] <= 0.04045, a[..., :3] / 12.92, ((a[..., :3] + 0.055) / 1.055) ** 2.4)
    lin = lin * lt
    out[..., :3] = np.where(lin <= 0.0031308, lin * 12.92, 1.055 * np.power(lin, 1 / 2.4) - 0.055)
    img = Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8), "RGBA")
    if glow is not None:
        img.alpha_composite(glow)
    return img
