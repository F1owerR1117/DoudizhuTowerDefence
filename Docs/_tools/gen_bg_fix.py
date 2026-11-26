# -*- coding: utf-8 -*-
"""背景单独重制（复用 gen_art 的调色板与 skyline）。
用法: python Docs/_tools/gen_bg_fix.py
"""
import os, random
from PIL import Image, ImageDraw
import gen_art as G

C = G.C
hx = G.hx
put = G.put


def city(im, W, horizon, seed, minh, maxh, minw, maxw, color, winrate=0.0):
    """连续型城市剪影：楼体紧邻，偶尔 1~2px 缝隙，避免漏出亮天空。"""
    d = ImageDraw.Draw(im)
    rnd = random.Random(seed)
    x = -6
    while x < W + 8:
        w = rnd.randint(minw, maxw)
        h = rnd.randint(minh, maxh)
        top = horizon - h
        d.rectangle([x, top, x + w - 1, horizon], fill=hx(color))
        if rnd.random() < 0.55:
            sw = rnd.randint(3, max(4, w // 2))
            sx = x + rnd.randint(0, max(0, w - sw))
            sh = rnd.randint(3, max(4, h // 4))
            d.rectangle([sx, top - sh, sx + sw - 1, top], fill=hx(color))
        if rnd.random() < 0.25:
            ax = x + rnd.randint(2, max(3, w - 3))
            ah = rnd.randint(3, 8)
            d.rectangle([ax, top - ah, ax, top], fill=hx(color))
        if winrate:
            for wy in range(top + 4, horizon - 3, 4):
                for wx in range(x + 2, x + w - 2, 4):
                    if rnd.random() < winrate:
                        put(im, wx, wy, hx(rnd.choice(['#F0A050', '#F0A050', '#C04A38'])))
        x += w + (0 if rnd.random() < 0.7 else rnd.randint(1, 2))


def make_bg(W=480, H=270, horizon=150):
    im = Image.new('RGBA', (W, H), hx('#0B0807'))
    d = ImageDraw.Draw(im)

    # --- 天空 12 带 + 带间棋盘抖动 ---
    sky = ['#422B3C', '#4F3137', '#5C3632', '#6A3D2E', '#78452A', '#874D28',
           '#965528', '#A65E2A', '#B6672E', '#C57333', '#D38038', '#E08D3F']
    n = len(sky)
    bh = horizon / float(n)
    for i, c in enumerate(sky):
        y0 = int(i * bh)
        y1 = int((i + 1) * bh)
        d.rectangle([0, y0, W - 1, y1 - 1], fill=hx(c))
        if i < n - 1:
            for x in range(0, W, 2):
                if (x // 2 + i) % 2 == 0:
                    put(im, x, y1 - 1, hx(sky[i + 1]))
                    put(im, x + 1, y1 - 2, hx(sky[i + 1]))

    # --- 三层剪影：连续楼体，避免缝隙漏光 ---
    d.rectangle([0, horizon - 30, W - 1, horizon], fill=hx('#3A2530'))
    city(im, W, horizon, 1337, 78, 108, 16, 34, '#3A2530', 0.0)
    d.rectangle([0, horizon - 16, W - 1, horizon], fill=hx('#241619'))
    city(im, W, horizon, 90210, 52, 84, 14, 30, '#241619', 0.10)
    d.rectangle([0, horizon - 7, W - 1, horizon], fill=hx('#150E0C'))
    city(im, W, horizon, 4242, 18, 44, 10, 24, '#150E0C', 0.05)

    # --- 地平线薄雾（实色 3 行 + 上沿稀疏抖动，不要棋盘）---
    d.rectangle([0, horizon - 3, W - 1, horizon - 1], fill=hx('#7A4A2E'))
    for x in range(0, W, 4):
        put(im, x, horizon - 4, hx('#6A4029'))

    # --- 地面分层 ---
    d.rectangle([0, horizon, W - 1, H - 1], fill=hx('#2A1F18'))
    d.rectangle([0, horizon, W - 1, horizon + 7], fill=hx('#4D3620'))
    d.rectangle([0, horizon + 8, W - 1, horizon + 16], fill=hx('#3B2C1F'))
    d.rectangle([0, horizon + 17, W - 1, horizon + 40], fill=hx('#33261C'))

    # 颗粒与裂缝
    rnd = random.Random(555)
    for _ in range(900):
        x = rnd.randint(0, W - 1)
        y = rnd.randint(horizon + 18, H - 1)
        put(im, x, y, hx(rnd.choice(['#3B2C1F', '#241A14', '#2A1F18'])))
    for _ in range(30):
        x = rnd.randint(0, W - 14)
        y = rnd.randint(202, H - 1)
        d.line([x, y, x + rnd.randint(4, 14), y], fill=hx('#241A14'))

    # 辐射积水
    rnd2 = random.Random(31)
    for _ in range(10):
        x = rnd2.randint(4, W - 16)
        y = rnd2.randint(240, 262)
        d.line([x, y, x + rnd2.randint(4, 12), y], fill=hx('#A8EE54'))

    # --- 前景废墟：基座 262 ---
    rnd3 = random.Random(8123)
    x = -12
    while x < W + 14:
        big = rnd3.random() < 0.5
        w = rnd3.randint(20, 46) if big else rnd3.randint(8, 18)
        h = rnd3.randint(16, 38) if big else rnd3.randint(6, 14)
        top = 262 - h
        d.rectangle([x, top, x + w - 1, 269], fill=hx('#150E0C'))
        if rnd3.random() < 0.65:
            cw = max(2, int(w * (0.28 + rnd3.random() * 0.42)))
            cx = x + rnd3.randint(0, max(0, w - cw))
            d.rectangle([cx, top, cx + cw - 1, top + rnd3.randint(2, 6)], fill=hx('#33261C'))
        if rnd3.random() < 0.75:
            d.line([x, top, x + w - 1, top], fill=hx('#B06A3A'))
        if rnd3.random() < 0.45:
            for wy in range(top + 4, 258, 4):
                for wx in range(x + 2, x + w - 2, 4):
                    if rnd3.random() < 0.10:
                        put(im, wx, wy, hx('#8A5330'))
        x += w + rnd3.randint(4, 18)

    # 电线杆
    for i in range(6):
        px = 10 + i * 84 + rnd3.randint(0, 26)
        ph = rnd3.randint(30, 58)
        d.rectangle([px, 262 - ph, px, 262], fill=hx('#150E0C'))
        d.line([px - 6, 262 - ph, px + 6, 262 - ph], fill=hx('#150E0C'))
        put(im, px, 262 - ph, hx('#8A5330'))

    # 飞鸟
    for (bx, by) in [(112, 14), (128, 8), (140, 18), (300, 20)]:
        put(im, bx, by, hx('#241619'))
        put(im, bx + 2, by, hx('#241619'))
        put(im, bx + 1, by + 1, hx('#241619'))
    return im


if __name__ == '__main__':
    G.save(make_bg(), 'BG/codex_bg.png')
    print('bg regenerated')
