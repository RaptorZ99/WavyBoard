"""Generates a tileable grayscale foam detail texture (irregular foam patches, soft bubbles, fine streaks) for the surf wave shader.
Output: Assets/_Project/Art/Textures/FoamDetail.png (1024x1024, R = foam density 0..1). Requires numpy + Pillow.
Usage: python Tools/make_foam_texture.py"""
import os
import numpy as np
from PIL import Image

N = 1024
rng = np.random.default_rng(11)
OUT = "Assets/_Project/Art/Textures/FoamDetail.png"


def tileable_value_noise(n, period, rng):
    """Smooth-interpolated value noise on a periodic lattice of `period` cells -> seamless tiling."""
    grid = rng.random((period, period)).astype(np.float32)
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32)
    fx = xs * period / n
    fy = ys * period / n
    x0 = np.floor(fx).astype(int) % period
    y0 = np.floor(fy).astype(int) % period
    x1 = (x0 + 1) % period
    y1 = (y0 + 1) % period
    tx = fx - np.floor(fx)
    ty = fy - np.floor(fy)
    tx = tx * tx * tx * (tx * (tx * 6 - 15) + 10)
    ty = ty * ty * ty * (ty * (ty * 6 - 15) + 10)
    a = grid[y0, x0]
    b = grid[y0, x1]
    c = grid[y1, x0]
    d = grid[y1, x1]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def fbm(n, rng, octaves, weights):
    acc = np.zeros((n, n), np.float32)
    for p, w in zip(octaves, weights):
        acc += w * tileable_value_noise(n, p, rng)
    acc -= acc.min()
    acc /= max(1e-6, acc.max())
    return acc


def soft_discs(n, rng, count, rmin, rmax, strength):
    """Wrapped soft discs (bubble clusters), additive with soft falloff: no rings."""
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32) / n
    acc = np.zeros((n, n), np.float32)
    cx = rng.random(count)
    cy = rng.random(count)
    rr = rng.uniform(rmin, rmax, count)
    for i in range(count):
        dx = np.abs(xs - cx[i])
        dx = np.minimum(dx, 1 - dx)
        dy = np.abs(ys - cy[i])
        dy = np.minimum(dy, 1 - dy)
        d = (dx * dx + dy * dy) / (rr[i] * rr[i])
        acc += strength * np.exp(-d * 2.2)
    return acc


# large irregular patches (the main foam/no-foam structure), medium blobs, fine grain
patches = fbm(N, rng, (3, 6, 12), (0.55, 0.3, 0.15))
patches = np.clip((patches - 0.35) * 2.2, 0, 1) ** 1.3
medium = fbm(N, rng, (16, 32), (0.6, 0.4))
fine = fbm(N, rng, (64, 128), (0.6, 0.4))
bubbles = soft_discs(N, rng, 1400, 0.004, 0.012, 0.55)
bubbles = np.clip(bubbles, 0, 1)

foam = 0.55 * patches + 0.2 * medium + 0.12 * fine + 0.25 * bubbles * (0.4 + 0.6 * patches)
foam = (foam - foam.min()) / (foam.max() - foam.min())
foam = np.clip(foam, 0, 1)
img = (foam * 255).astype(np.uint8)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
Image.fromarray(img, mode="L").save(OUT)
print("wrote", OUT, "mean", float(foam.mean()))
