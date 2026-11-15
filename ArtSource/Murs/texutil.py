# Utilitaires partagés par les générateurs de textures (numpy seul).
import zlib, struct, math
import numpy as np

# ---------------------------------------------------------------- utilitaires
def save_png(path, arr):
    arr = np.clip(arr, 0, 1)
    if arr.ndim == 2:
        ct, ch = 0, 1
        a = (arr * 255 + 0.5).astype(np.uint8)[:, :, None]
    else:
        ch = arr.shape[2]; ct = {3: 2, 4: 6}[ch]
        a = (arr * 255 + 0.5).astype(np.uint8)
    h, w = a.shape[:2]
    raw = b"".join(b"\x00" + a[y].tobytes() for y in range(h))
    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, ct, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)

def tile_noise(n, cells, seed, octaves=1, persist=0.5):
    """Bruit de valeur périodique (tileable), interpolation cosinus, somme fractale. [0,1]"""
    r = np.random.default_rng(seed)
    out = np.zeros((n, n), np.float32); amp = 1.0; tot = 0.0
    for o in range(octaves):
        c = cells * (2 ** o)
        g = r.random((c, c)).astype(np.float32)
        t = np.arange(n, dtype=np.float32) * c / n
        i0 = np.floor(t).astype(int); f = t - i0
        f = (1 - np.cos(f * math.pi)) * 0.5
        i1 = (i0 + 1) % c; i0 = i0 % c
        a = g[i0][:, i0]; b = g[i0][:, i1]; cc = g[i1][:, i0]; d = g[i1][:, i1]
        fx = f[None, :]; fy = f[:, None]
        v = (a * (1 - fx) + b * fx) * (1 - fy) + (cc * (1 - fx) + d * fx) * fy
        out += v * amp; tot += amp; amp *= persist
    return out / tot

def box_blur(a, r):
    if r < 1: return a
    k = 2 * r + 1
    for ax in (0, 1):
        c = np.cumsum(np.concatenate([np.take(a, range(-r - 1, 0), axis=ax), a, np.take(a, range(0, r), axis=ax)], axis=ax), axis=ax, dtype=np.float64)
        sl_hi = [slice(None)] * 2; sl_lo = [slice(None)] * 2
        sl_hi[ax] = slice(k, None); sl_lo[ax] = slice(0, -k)
        a = ((c[tuple(sl_hi)] - c[tuple(sl_lo)]) / k).astype(np.float32)
    return a

def blur(a, r, it=3):
    for _ in range(it): a = box_blur(a, r)
    return a

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)

