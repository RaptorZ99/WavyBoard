"""Generates the tileable water textures of WavyBoard (numpy + scipy + Pillow):

  Assets/_Project/Art/Textures/Water_Ripples_N.png  tangent-space normal map of wind ripples and small chop, from an
                                                    ocean spectrum (Phillips, directional) by inverse FFT: tileable by
                                                    construction, one tile = RIPPLE_TILE metres of water.
  Assets/_Project/Art/Textures/Water_Foam.png       linear RGB foam masks, one tile = FOAM_TILE metres:
                                                    R = lace: the network of foam filaments whitewater leaves behind
                                                    G = dense aerated foam with bubbles (impact, bore front)
                                                    B = large breakup noise (varies the coverage)
                                                    The shader thresholds them with the foam amount, so a little foam is
                                                    thin filaments and a lot is solid white, never a blurry smear.

Usage: python3 Tools/make_water_textures.py [--preview out_dir]
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy.spatial import cKDTree

N = 1024
RIPPLE_TILE = 8.0
FOAM_TILE = 6.0
OUT_DIR = "Assets/_Project/Art/Textures"
rng = np.random.default_rng(1234)


def ripples():
    """Directional Phillips spectrum -> height by IFFT, gradient by spectral derivative (exact and tileable)."""
    L = RIPPLE_TILE
    k1 = 2 * np.pi * np.fft.fftfreq(N, d=L / N)
    kx, kz = np.meshgrid(k1, k1)
    k = np.sqrt(kx * kx + kz * kz)
    k[0, 0] = 1.0
    wind = np.array([0.94, 0.34])
    V = 3.0                                    # light breeze: ripples + small chop, not a storm
    Lw = V * V / 9.81
    kdotw = (kx * wind[0] + kz * wind[1]) / k
    kdotw2 = (kx * 0.26 + kz * -0.97) / k     # a second, weaker wind sea crossing the first: ripples, not streaks
    ph = np.exp(-1.0 / (k * Lw) ** 2) / k ** 4 * (np.abs(kdotw) ** 1.2 + 0.45 * np.abs(kdotw2) ** 1.2)
    ph *= np.exp(-(k * 0.012) ** 2)            # damp sub-centimetre waves
    ph *= 1.0 / (1.0 + (1.2 / k) ** 4)         # the long swell is geometry, not texture
    ph[0, 0] = 0.0
    amp = np.sqrt(ph / 2.0)
    h0 = (rng.normal(size=(N, N)) + 1j * rng.normal(size=(N, N))) * amp
    hx = np.real(np.fft.ifft2(1j * kx * h0))
    hz = np.real(np.fft.ifft2(1j * kz * h0))
    s = np.percentile(np.sqrt(hx * hx + hz * hz), 99.5)
    hx /= s
    hz /= s
    slope = 0.55                               # max slope of the texture (the shader scales it further)
    nx, nz, ny = -hx * slope, -hz * slope, np.ones_like(hx)
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz)
    nx, ny, nz = nx * inv, ny * inv, nz * inv
    # tangent space: X = +u (image right), Y = +v (image up in Unity = row index down in the PNG), Z = up
    rgb = np.stack([nx * 0.5 + 0.5, -nz * 0.5 + 0.5, ny * 0.5 + 0.5], axis=-1)
    return (np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8)


def tileable_noise(period, octaves=4, persistence=0.5):
    """Periodic fBm from a random spectrum (tileable)."""
    out = np.zeros((N, N))
    k1 = np.fft.fftfreq(N, d=1.0 / N)
    kx, kz = np.meshgrid(k1, k1)
    k = np.sqrt(kx * kx + kz * kz)
    k[0, 0] = 1.0
    spec = (rng.normal(size=(N, N)) + 1j * rng.normal(size=(N, N))) / (k ** 1.6)
    spec *= np.exp(-(k / (period * 2.0 ** octaves)) ** 2)
    spec *= 1.0 - np.exp(-(k / period) ** 2 * 4.0)
    spec[0, 0] = 0
    out = np.real(np.fft.ifft2(spec))
    out -= out.min()
    out /= out.max()
    return out


def voronoi_edges(cells, warp=0.0):
    """F2 - F1 distance (in pixels) of a tileable Voronoi diagram with `cells` random sites. `warp` (pixels) bends the
    cell walls with a periodic noise so the filaments curve like real foam instead of cracking like glass."""
    pts = rng.random((cells, 2)) * N
    tiled = np.concatenate([pts + np.array([dx, dy]) * N for dx in (-1, 0, 1) for dy in (-1, 0, 1)])
    tree = cKDTree(tiled)
    ys, xs = np.mgrid[0:N, 0:N].astype(np.float64)
    if warp > 0.0:
        xs = xs + (tileable_noise(5) - 0.5) * 2.0 * warp
        ys = ys + (tileable_noise(5) - 0.5) * 2.0 * warp
    q = np.stack([xs.ravel() + 0.5, ys.ravel() + 0.5], axis=-1)
    d, _ = tree.query(q, k=2)
    return (d[:, 1] - d[:, 0]).reshape(N, N)


def foam():
    warp = tileable_noise(6)
    # lace: two scales of cells, filaments whose width breathes with a noise field
    e1 = voronoi_edges(90, warp=38.0)
    e2 = voronoi_edges(420, warp=16.0)
    w1 = 3.0 + 9.0 * tileable_noise(4)
    w2 = 1.5 + 4.0 * tileable_noise(8)
    lace = np.maximum(np.exp(-e1 / w1), 0.8 * np.exp(-e2 / w2))
    lace = lace * (0.55 + 0.45 * warp)
    # small holes punched in the filaments (bubbles bursting)
    holes = voronoi_edges(2600, warp=5.0)
    lace *= 0.65 + 0.35 * np.clip(holes / 6.0, 0, 1)
    lace = np.clip(lace / np.percentile(lace, 99.7), 0, 1)

    # dense foam: turbulent body + fine bubbles
    body = tileable_noise(10, octaves=5) * 0.7 + tileable_noise(24, octaves=4) * 0.3
    bub = voronoi_edges(5000, warp=4.0)
    bubbles = 1.0 - np.exp(-bub / 2.0)
    dense = np.clip(0.2 + 0.8 * body, 0, 1) * (0.85 + 0.15 * bubbles)
    dense = (dense - dense.min()) / (dense.max() - dense.min())

    breakup = tileable_noise(2, octaves=3)
    rgb = np.stack([lace, dense, breakup], axis=-1)
    return (np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    r = ripples()
    Image.fromarray(r, "RGB").save(os.path.join(OUT_DIR, "Water_Ripples_N.png"))
    f = foam()
    Image.fromarray(f, "RGB").save(os.path.join(OUT_DIR, "Water_Foam.png"))
    if "--preview" in sys.argv:
        d = sys.argv[sys.argv.index("--preview") + 1]
        os.makedirs(d, exist_ok=True)
        Image.fromarray(r).resize((512, 512)).save(os.path.join(d, "ripples.png"))
        for i, c in enumerate("RGB"):
            Image.fromarray(f[:, :, i]).resize((512, 512)).save(os.path.join(d, f"foam_{c}.png"))
    print("textures written")


if __name__ == "__main__":
    main()
