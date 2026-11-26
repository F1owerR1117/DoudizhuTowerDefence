# -*- coding: utf-8 -*-
"""补充素材：暗角 + 背景提亮。用法: python Docs/_tools/gen_extra.py"""
import os
from PIL import Image, ImageEnhance

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
ART = os.path.join(ROOT, 'Assets', 'Art', 'PixelWasteland')


def make_vignette(W=320, H=180):
    """径向暗角：边缘不透明黑，中心全透明。平滑渐变，不做像素化。"""
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = im.load()
    cx, cy = (W - 1) / 2.0, (H - 1) / 2.0
    maxd = (cx * cx + cy * cy) ** 0.5
    for y in range(H):
        for x in range(W):
            dx = (x - cx) / cx
            dy = (y - cy) / cy
            # 椭圆距离，中心留出干净区域
            d = min(1.0, (dx * dx * 0.85 + dy * dy * 0.85) ** 0.5)
            a = 0.0
            if d > 0.55:
                t = (d - 0.55) / 0.45
                a = (t ** 1.6) * 0.72
            if a > 0.004:
                px[x, y] = (0, 0, 0, int(min(255, a * 255)))
    return im


def brighten(path, factor=1.18, contrast=1.04):
    im = Image.open(path).convert('RGBA')
    r, g, b, a = im.split()
    rgb = Image.merge('RGB', (r, g, b))
    rgb = ImageEnhance.Brightness(rgb).enhance(factor)
    rgb = ImageEnhance.Contrast(rgb).enhance(contrast)
    r2, g2, b2 = rgb.split()
    out = Image.merge('RGBA', (r2, g2, b2, a))
    out.save(path)
    return path


if __name__ == '__main__':
    os.makedirs(os.path.join(ART, 'UI'), exist_ok=True)
    make_vignette().save(os.path.join(ART, 'UI', 'vignette.png'))
    print('vignette ok')

    bg = os.path.join(ART, 'BG', 'codex_bg.png')
    brighten(bg, 1.18, 1.05)
    print('bg brightened')
