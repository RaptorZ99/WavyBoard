"""Wave profile lab: prototype of the Teahupoo-style cross-section used by WaveProfile.cs, with graphs.

The cross-section of the surf wave is ONE continuous curve, from the flat water in front of the wave (beach side) up
the face, round the back wall of the tube, along the ceiling (lip underside), round the lip tip, back over the top of
the lip and the crest, and down the back of the wave to the flat water behind. It is a centripetal Catmull-Rom spline
through 15 control points whose positions are keyframed over the life of the wave (swell -> steep -> pitch -> throw ->
barrel -> impact -> mound -> bore -> flat). Units are normalised by the wave height H (crest above mean sea level);
x points toward the beach, x = 0 is the back wall of the tube.

  python3 Tools/wave_profile_lab.py [out_dir]     -> stages.png, barrel_scale.png, peel.png, checks in the console

Keep the KEYS table identical to WaveProfile.cs (the C# port is checked against this file's numbers).
"""
import math
import os
import sys

import numpy as np
import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/Screenshots~/lab"
os.makedirs(OUT, exist_ok=True)

ROLES = ["F2", "F1", "FOOT", "FACE_LO", "FACE_MID", "WALL", "CEIL", "LIPIN", "TIP", "LIPOUT", "LIPTOP", "CREST", "BACK", "B1", "B2"]
M = len(ROLES)
SEG = [10, 8, 8, 8, 8, 10, 10, 9, 9, 10, 10, 10, 8, 10]      # samples per spline segment (sum = vertex count - 1)
NU = sum(SEG) + 1


def bump(xs, h, wf, wb, xc=0.0):
    ys = []
    for x in xs:
        u = (x - xc) / (wf if x >= xc else wb)
        ys.append(h * (0.5 + 0.5 * math.cos(math.pi * min(1.0, abs(u)))))
    return ys


def key_from_bump(xs, h, wf, wb, xc=0.0):
    return list(zip(xs, bump(xs, h, wf, wb, xc)))


SWELL_X = [7.0, 3.6, 2.4, 1.75, 1.25, 0.85, 0.55, 0.32, 0.14, -0.02, -0.2, -0.5, -1.4, -3.6, -7.0]

# (name, points) front -> back, normalised by H. Hand-shaped against the reference photos (Teahupoo): a square, roomy
# tube under a thick lip that lands a full wave-height in front of the back wall.
KEYS = {
    "swell": key_from_bump(SWELL_X, 0.42, 3.0, 3.2),
    "steep": [(7, 0), (3.4, 0), (2.1, 0.03), (1.45, 0.19), (0.95, 0.45), (0.58, 0.67), (0.36, 0.78), (0.2, 0.83), (0.07, 0.855),
              (-0.05, 0.86), (-0.2, 0.845), (-0.5, 0.78), (-1.45, 0.44), (-3.6, 0), (-7, 0)],
    "pitch": [(7, 0), (3.2, 0), (1.7, 0.02), (0.95, 0.18), (0.42, 0.47), (0.17, 0.72), (0.16, 0.9), (0.3, 0.99), (0.46, 1.01),
              (0.4, 1.08), (0.18, 1.1), (-0.25, 1.02), (-1.25, 0.55), (-3.6, 0), (-7, 0)],
    "throw": [(7, 0), (3.1, 0), (1.55, 0.015), (0.85, 0.14), (0.32, 0.4), (0.04, 0.66), (0.2, 0.92), (0.62, 0.87), (0.9, 0.62),
              (1.02, 0.83), (0.66, 1.05), (0.12, 1.08), (-1.1, 0.6), (-3.6, 0), (-7, 0)],
    "barrel": [(7, 0), (3.0, 0), (1.55, 0.005), (0.8, 0.08), (0.24, 0.31), (0.0, 0.6), (0.24, 0.9), (0.92, 0.68), (1.16, 0.03),
               (1.4, 0.4), (1.06, 0.93), (0.3, 1.1), (-1.0, 0.64), (-3.6, 0), (-7, 0)],
    "barrel2": [(7, 0), (3.0, 0), (1.6, 0.0), (0.82, 0.075), (0.26, 0.3), (0.02, 0.58), (0.26, 0.88), (0.98, 0.64), (1.24, -0.07),
                (1.47, 0.37), (1.1, 0.9), (0.33, 1.08), (-1.0, 0.63), (-3.6, 0), (-7, 0)],
    "impact": [(7, 0), (3.0, 0), (1.62, 0.0), (0.85, 0.07), (0.32, 0.26), (0.12, 0.44), (0.36, 0.63), (0.92, 0.47), (1.38, -0.12),
               (1.62, 0.3), (1.12, 0.76), (0.42, 0.9), (-0.9, 0.55), (-3.6, 0), (-7, 0)],
    "mound": key_from_bump([7, 3.6, 2.5, 2.05, 1.72, 1.45, 1.22, 1.03, 0.86, 0.68, 0.48, 0.18, -0.75, -3.5, -7], 0.6, 1.9, 2.6, 0.9),
    "bore": key_from_bump([7, 4.2, 3.2, 2.7, 2.35, 2.05, 1.8, 1.58, 1.38, 1.16, 0.9, 0.5, -0.5, -3.4, -7], 0.34, 2.1, 2.8, 1.35),
    "flat": key_from_bump([7, 4.4, 3.4, 2.9, 2.55, 2.25, 2.0, 1.78, 1.58, 1.36, 1.1, 0.7, -0.3, -3.3, -7], 0.03, 2.3, 3.0, 1.55),
}
for k, v in KEYS.items():
    assert len(v) == M, (k, len(v))

# life of one point of the crest: (key, time relative to its break, seconds). The swell -> steep part is driven by the
# shoaling of the whole wave (see shape_at), the rest by the local break time.
TIMELINE = [("steep", -1.6), ("pitch", 0.0), ("throw", 0.75), ("barrel", 1.5), ("barrel2", 3.3), ("impact", 4.1), ("mound", 5.2),
            ("bore", 7.5), ("flat", 16.0)]


def catmull(p0, p1, p2, p3, t):
    """Centripetal Catmull-Rom (alpha = 0.5) between p1 and p2."""
    def tj(ti, a, b):
        d = math.hypot(b[0] - a[0], b[1] - a[1])
        return ti + max(d, 1e-4) ** 0.5
    t0 = 0.0
    t1 = tj(t0, p0, p1)
    t2 = tj(t1, p1, p2)
    t3 = tj(t2, p2, p3)
    tt = t1 + (t2 - t1) * t
    def lerp(a, b, ta, tb):
        w = (tt - ta) / (tb - ta)
        return (a[0] + (b[0] - a[0]) * w, a[1] + (b[1] - a[1]) * w)
    a1 = lerp(p0, p1, t0, t1); a2 = lerp(p1, p2, t1, t2); a3 = lerp(p2, p3, t2, t3)
    b1 = lerp(a1, a2, t0, t2); b2 = lerp(a2, a3, t1, t3)
    return lerp(b1, b2, t1, t2)


def hermite_keys(keys, times, t):
    """Catmull-Rom (uniform in key index, with the time remapped piecewise-linearly) across a list of key shapes."""
    n = len(keys)
    if t <= times[0]:
        return keys[0]
    if t >= times[-1]:
        return keys[-1]
    i = 0
    while times[i + 1] < t:
        i += 1
    u = (t - times[i]) / (times[i + 1] - times[i])
    k0 = keys[max(0, i - 1)]; k1 = keys[i]; k2 = keys[i + 1]; k3 = keys[min(n - 1, i + 2)]
    out = []
    u2, u3 = u * u, u * u * u
    for j in range(M):
        pt = []
        for c in range(2):
            p0, p1, p2, p3 = k0[j][c], k1[j][c], k2[j][c], k3[j][c]
            v = 0.5 * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u2 + (-p0 + 3 * p1 - 3 * p2 + p3) * u3)
            pt.append(v)
        out.append(tuple(pt))
    return out


def shape_at(tau, shoal=1.0, heavy=1.0):
    """Control points at time tau (s) relative to the local break; shoal 0..1 grows the swell into the steep face."""
    pre = [tuple(np.add(np.multiply(a, 1 - shoal), np.multiply(b, shoal))) for a, b in zip(KEYS["swell"], KEYS["steep"])]
    keys = [pre] + [KEYS[n] for n, _ in TIMELINE[1:]]
    times = [TIMELINE[0][1]] + [t for _, t in TIMELINE[1:]]
    pts = hermite_keys(keys, times, tau)
    if heavy < 1.0:
        # mellow sections: the lip throws less far and thinner, the tube is smaller (spilling-ish curl)
        pts = [(x * (0.55 + 0.45 * heavy) if 5 <= j <= 11 and x > 0 else x, y) for j, (x, y) in enumerate(pts)]
    return pts


def curve(pts):
    out = []
    ext = [(2 * pts[0][0] - pts[1][0], 2 * pts[0][1] - pts[1][1])] + list(pts) + [(2 * pts[-1][0] - pts[-2][0], 2 * pts[-1][1] - pts[-2][1])]
    for i in range(M - 1):
        for k in range(SEG[i]):
            out.append(catmull(ext[i], ext[i + 1], ext[i + 2], ext[i + 3], k / SEG[i]))
    out.append(pts[-1])
    return np.array(out)


def seg_intersections(c, skip=3):
    """Pairs of crossing segments of the polyline (ignoring neighbours)."""
    hits = []
    n = len(c) - 1
    for i in range(n):
        a, b = c[i], c[i + 1]
        for j in range(i + skip, n):
            p, q = c[j], c[j + 1]
            d = (b[0] - a[0]) * (q[1] - p[1]) - (b[1] - a[1]) * (q[0] - p[0])
            if abs(d) < 1e-12:
                continue
            t = ((p[0] - a[0]) * (q[1] - p[1]) - (p[1] - a[1]) * (q[0] - p[0])) / d
            u = ((p[0] - a[0]) * (b[1] - a[1]) - (p[1] - a[1]) * (b[0] - a[0])) / d
            if 0 <= t <= 1 and 0 <= u <= 1:
                hits.append((i, j))
    return hits


def main():
    # 1. life of a point of the crest
    fig, ax = plt.subplots(figsize=(15, 6))
    taus = [-1.6, -0.8, 0.0, 0.4, 0.75, 1.1, 1.5, 3.3, 3.7, 4.1, 5.2, 7.5, 12.0]
    cmap = plt.get_cmap("viridis")
    for i, t in enumerate(taus):
        c = curve(shape_at(t))
        ax.plot(c[:, 0], c[:, 1] + 0.0, color=cmap(i / (len(taus) - 1)), lw=1.6, label=f"tau={t:+.1f}s")
    ax.set_aspect("equal"); ax.set_xlim(-4, 4); ax.set_ylim(-0.3, 1.3); ax.grid(alpha=0.3)
    ax.set_title("Coupe de la vague au fil du temps (unites = hauteur H, x vers la plage)")
    ax.legend(ncol=7, fontsize=7, loc="lower center")
    fig.tight_layout(); fig.savefig(os.path.join(OUT, "stages.png"), dpi=110); plt.close(fig)

    # 2. each key alone with its control points
    fig, axs = plt.subplots(4, 3, figsize=(15, 12))
    for ax, (name, pts) in zip(axs.flat, KEYS.items()):
        c = curve(pts)
        ax.fill_between(c[:, 0], -0.4, np.minimum.accumulate(c[:, 1][::-1])[::-1] * 0 - 0.4, color="none")
        ax.plot(c[:, 0], c[:, 1], "b-", lw=1.5)
        p = np.array(pts)
        ax.plot(p[:, 0], p[:, 1], "r.", ms=5)
        for j, r in enumerate(ROLES):
            if 2 <= j <= 12:
                ax.annotate(r, p[j], fontsize=6, color="r")
        ax.set_aspect("equal"); ax.set_xlim(-2.2, 2.6); ax.set_ylim(-0.3, 1.3); ax.grid(alpha=0.3); ax.set_title(name)
    fig.tight_layout(); fig.savefig(os.path.join(OUT, "keys.png"), dpi=100); plt.close(fig)

    # 3. the barrel at game scale with a prone bodyboarder and a drop-knee rider
    H = 4.0
    fig, ax = plt.subplots(figsize=(12, 6))
    c = curve(shape_at(2.4)) * H
    ax.fill(np.concatenate([c[:, 0], [c[-1, 0], c[0, 0]]]), np.concatenate([c[:, 1], [-2, -2]]), color="#1b7fa3", alpha=0.35)
    ax.plot(c[:, 0], c[:, 1], color="#0b4f6c", lw=2)
    ax.add_patch(plt.Rectangle((1.4, 0.3), 1.0, 0.45, color="k"))
    ax.add_patch(plt.Rectangle((2.9, 0.18), 0.45, 1.25, color="#444"))
    ax.set_aspect("equal"); ax.set_xlim(-8, 10); ax.set_ylim(-1, 5.5); ax.grid(alpha=0.3)
    ax.set_title("Tube a l'echelle (H = 4 m) : bodyboarder allonge (noir) et drop-knee (gris)")
    fig.tight_layout(); fig.savefig(os.path.join(OUT, "barrel_scale.png"), dpi=110); plt.close(fig)

    # 4. peel: sections along the crest at one instant (vp = 5 m/s), seen in 3D
    from mpl_toolkits.mplot3d import Axes3D  # noqa: F401
    fig = plt.figure(figsize=(15, 7))
    ax = fig.add_subplot(111, projection="3d")
    vp = 5.0
    for s in np.linspace(-10, 80, 46):
        tau = (60 - s) / vp if s < 60 else -(s - 60) / vp * 0.4
        c = curve(shape_at(tau)) * H
        ax.plot(np.full(len(c), s), c[:, 0], c[:, 1], color=cmap(min(1, max(0, (tau + 2) / 12))), lw=0.8)
    ax.set_xlabel("s (le long de la crete, m)"); ax.set_ylabel("x (vers la plage)"); ax.set_zlabel("y")
    ax.set_box_aspect((90, 20, 6)); ax.view_init(elev=18, azim=-60)
    ax.set_title("Deroule le long de la crete (vp = 5 m/s)")
    fig.tight_layout(); fig.savefig(os.path.join(OUT, "peel.png"), dpi=100); plt.close(fig)

    # checks: self intersections (only the lip tip may plunge through the water in front after the landing)
    for t in np.linspace(-1.6, 16, 90):
        c = curve(shape_at(t))
        hits = seg_intersections(c)
        bad = [h for h in hits if not (t >= 1.3 and h[0] < 40)]
        if bad:
            print(f"tau={t:+.2f}: self-intersection {bad[:4]}")
    print("NU =", NU, "points per section")


if __name__ == "__main__":
    main()
