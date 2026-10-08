"""
Rauschen fuer die Vulkanwelt (Tools/vulkan_boden.py, Tools/vulkan_lava.py).

Wertrauschen auf einem Gitter, das sich auf Wunsch wiederholt: mit
period=(px, py) passt die rechte Kante nahtlos an die linke. So bleiben
Fliesen kachelbar und Chunks passen an jeden anderen Chunk.
"""

import numpy as np


def _lattice(seed, w, h):
    return np.random.default_rng(seed).random((h, w))


def value_noise(x, y, seed, period=None):
    """x, y in Gitter-Einheiten (Arrays). period=(px, py) Gitterzellen oder None."""
    x = np.asarray(x, dtype=np.float64)
    y = np.asarray(y, dtype=np.float64)
    if period is None:
        pw, ph = 1024, 1024
        x = x + 512.0
        y = y + 512.0
    else:
        pw, ph = int(period[0]), int(period[1])
    lat = _lattice(seed, pw, ph)
    x0 = np.floor(x).astype(np.int64)
    y0 = np.floor(y).astype(np.int64)
    fx = x - x0
    fy = y - y0
    sx = fx * fx * (3 - 2 * fx)
    sy = fy * fy * (3 - 2 * fy)
    xa, xb = x0 % pw, (x0 + 1) % pw
    ya, yb = y0 % ph, (y0 + 1) % ph
    a = lat[ya, xa]
    b = lat[ya, xb]
    c = lat[yb, xa]
    d = lat[yb, xb]
    top = a + (b - a) * sx
    bot = c + (d - c) * sx
    return top + (bot - top) * sy


def fbm(x, y, seed, octaves=3, period=None, gain=0.5):
    """Summe mehrerer Oktaven, Ergebnis grob 0..1. Mit period bleibt jede Oktave periodisch."""
    total = np.zeros(np.broadcast(np.asarray(x), np.asarray(y)).shape)
    amp, norm, f = 1.0, 0.0, 1
    for o in range(octaves):
        per = None if period is None else (period[0] * f, period[1] * f)
        total += amp * value_noise(np.asarray(x) * f, np.asarray(y) * f, seed + o * 1013, per)
        norm += amp
        amp *= gain
        f *= 2
    return total / norm


def grid(w, h):
    """Pixelmitten als (X, Y)-Arrays, Form (h, w)."""
    ys, xs = np.mgrid[0:h, 0:w]
    return xs + 0.5, ys + 0.5
