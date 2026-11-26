# -*- coding: utf-8 -*-
"""复刻 HTML 所需的覆盖层素材：插槽棋盘底、玻璃反光、扫光带。"""
import os
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
ART = os.path.join(ROOT, 'Assets', 'Art', 'PixelWasteland')


def checker(size=16, a='#1C1614', b='#17120F'):
    im = Image.new('RGBA', (size, size))
    ca = _rgb(a)
    cb = _rgb(b)
    for y in range(size):
        for x in range(size):
            im.putpixel((x, y), ca if ((x < size // 2) == (y < size // 2)) else cb)
    return im


def _rgb(hexs, alpha=255):
    hexs = hexs.lstrip('#')
    return (int(hexs[0:2], 16), int(hexs[2:4], 16), int(hexs[4:6], 16), alpha)


def glass(W=320, H=180):
    """左上斜向高光，向右下淡出"""
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = im.load()
    for y in range(H):
        for x in range(W):
            # 对角线距离（0 左上 → 1 右下）
            t = (x / float(W) * 0.62 + y / float(H) * 0.38)
            if t < 0.18:
                a = int(14 * (1.0 - t / 0.18))
            elif t < 0.46:
                a = 14
            else:
                a = max(0, int(14 * (1.0 - (t - 0.46) / 0.36)))
            if a > 0:
                px[x, y] = (255, 255, 255, a)
    return im


def sweep(W=8, H=256):
    """竖直柔光带：中间亮、两端透明（作为全屏扫光的贴图）"""
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = im.load()
    for y in range(H):
        t = y / float(H - 1)
        # 三角形包络
        a = 1.0 - abs(t - 0.5) * 2.0
        a = max(0.0, a) ** 2.2
        v = int(a * 26)
        if v > 0:
            for x in range(W):
                px[x, y] = (190, 255, 220, v)
    return im


if __name__ == '__main__':
    os.makedirs(os.path.join(ART, 'UI'), exist_ok=True)
    checker().save(os.path.join(ART, 'UI', 'socket_checker.png'))
    glass().save(os.path.join(ART, 'UI', 'glass.png'))
    sweep().save(os.path.join(ART, 'UI', 'sweep.png'))
    print('overlays ok')
