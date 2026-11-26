# -*- coding: utf-8 -*-
"""
像素废土 Codex 素材生成器
输出到 Assets/Art/PixelWasteland/，全部整数像素、有限色、硬边。
用法: python Docs/_tools/gen_art.py
"""
import os, random
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'PixelWasteland')

C = dict(
    void='#0B0908', coal='#191412', steel='#2B2320', plate='#3F342C',
    hi='#574A3F', edge='#726151', edge2='#8D7A66',
    rd='#4F2617', rust='#7D3A1F', rustl='#A85226',
    amber='#F0A050', amber_d='#A56328', toxic='#A8EE54', blood='#C04A38',
    bone='#E9DCC0', boned='#AB9B80', bonedd='#776853',
    screen='#0A1310', phos='#6ADFA6', base='#1E1814',
)


def hx(s, a=255):
    s = s.lstrip('#')
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


def save(im, rel):
    p = os.path.join(OUT, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    im.save(p)
    return p


def put(im, x, y, col):
    """带裁剪的写像素"""
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), col)


def rim(d, w, h, light, dark, depth=2):
    """硬边斜面：左上亮、右下暗"""
    d.line([0, 0, w - 1, 0], fill=hx(dark), width=1)
    d.line([0, 0, 0, h - 1], fill=hx(dark), width=1)
    d.line([0, h - 1, w - 1, h - 1], fill=hx(dark), width=1)
    d.line([w - 1, 0, w - 1, h - 1], fill=hx(dark), width=1)
    d.line([1, 1, w - 2, 1], fill=hx(light), width=1)
    d.line([1, 1, 1, h - 2], fill=hx(light), width=1)
    d.line([1, h - 2, w - 2, h - 2], fill=hx(dark), width=1)
    d.line([w - 2, 1, w - 2, h - 2], fill=hx(dark), width=1)


def frame_profile(im, w, h, profile):
    """同心矩形描边，构成钢板框架"""
    d = ImageDraw.Draw(im)
    for off, col in profile:
        d.rectangle([off, off, w - 1 - off, h - 1 - off], outline=hx(col), width=1)
    return d


def rivet(im, x, y, light=C['edge'], body=C['hi'], dark=C['void']):
    d = ImageDraw.Draw(im)
    d.rectangle([x, y, x + 3, y + 3], fill=hx(body))
    d.line([x, y, x + 2, y], fill=hx(light))
    d.line([x, y, x, y + 2], fill=hx(light))
    d.line([x + 3, y + 1, x + 3, y + 3], fill=hx(dark))
    d.line([x + 1, y + 3, x + 3, y + 3], fill=hx(dark))
    im.putpixel((x + 1, y + 1), hx(light))


def speckle(im, x0, y0, x1, y1, colors, density=0.06, seed=7):
    rnd = random.Random(seed)
    for y in range(y0, y1):
        for x in range(x0, x1):
            if rnd.random() < density:
                im.putpixel((x, y), hx(rnd.choice(colors)))


# ---------------------------------------------------------------- 钢板面板
PLATE_PROFILE = [
    (0, C['void']), (1, C['void']),
    (2, C['hi']), (3, C['hi']), (4, C['plate']),
    (5, C['base']), (6, C['void']), (7, C['hi']),
]


def ring_speckle(im, w, h, border, colors, density=0.10, seed=9, margin=6):
    """边框带内的锈蚀磨损：成簇的 1~3 像素锈点，避免大面积橙色噪声。
    border 可为 int 或 (水平边宽, 垂直边宽)；density 仅缩放簇数量。"""
    if isinstance(border, (tuple, list)):
        bx, by = border
    else:
        bx, by = border, border
    rnd = random.Random(seed)
    count = max(5, int((w + h) * 0.06 * (density / 0.10)))
    for _ in range(count):
        side = rnd.randint(0, 3)
        if side == 0:
            x, y = rnd.randint(4, w - 4), rnd.randint(4, max(5, by - 2))
        elif side == 1:
            x, y = rnd.randint(4, w - 4), rnd.randint(min(h - 5, h - by + 2), h - 4)
        elif side == 2:
            x, y = rnd.randint(4, max(5, bx - 2)), rnd.randint(4, h - 4)
        else:
            x, y = rnd.randint(min(w - 5, w - bx + 2), w - 4), rnd.randint(4, h - 4)
        for _ in range(rnd.randint(1, 3)):
            put(im, x + rnd.randint(-1, 2), y + rnd.randint(-1, 2), hx(rnd.choice(colors)))


def make_panel(w, h, base, border, profile, seed, with_rivets=True, rust=True):
    im = Image.new('RGBA', (w, h), hx(base))
    frame_profile(im, w, h, profile)
    # 中央必须是纯色：该区域会被 9-slice 拉伸
    ImageDraw.Draw(im).rectangle([border, border, w - 1 - border, h - 1 - border], fill=hx(base))
    if rust:
        ring_speckle(im, w, h, border, [C['rd'], C['rust'], C['rustl']], 0.10, seed)
    if with_rivets:
        off = min(10, max(4, border // 3))
        for (x, y) in [(off, off), (w - off - 4, off), (off, h - off - 4), (w - off - 4, h - off - 4)]:
            rivet(im, x, y)
    return im


def gen_panels():
    # 主面板 128x128  border 48
    im = make_panel(128, 128, C['base'], 48, PLATE_PROFILE, 101)
    save(im, 'UI/panel_large.png')

    # 凹陷屏幕 128x128  border 40
    im = Image.new('RGBA', (128, 128), hx(C['screen']))
    prof = [(0, C['void']), (1, C['void']), (2, C['void']), (3, C['coal']),
            (4, C['steel']), (5, C['plate']), (6, C['hi'])]
    frame_profile(im, 128, 128, prof)
    ImageDraw.Draw(im).rectangle([40, 40, 87, 87], fill=hx(C['screen']))
    ring_speckle(im, 128, 128, 40, [C['coal'], C['steel']], 0.10, 202)
    dd = ImageDraw.Draw(im)
    dd.line([6, 121, 121, 121], fill=hx('#1B3A2E'))
    dd.line([6, 122, 121, 122], fill=hx('#25604A'))
    save(im, 'UI/panel_inset.png')

    # 图标插槽 128x128 border 48（中央纯色，凹槽全在边框带内）
    im = make_panel(128, 128, '#1C1512', 48, PLATE_PROFILE, 303, rust=False)
    d = ImageDraw.Draw(im)
    d.rectangle([9, 9, 118, 118], outline=hx(C['void']), width=1)
    d.rectangle([10, 10, 117, 117], outline=hx(C['coal']), width=1)
    d.rectangle([11, 11, 116, 116], outline=hx(C['steel']), width=1)
    ring_speckle(im, 128, 128, 48, [C['rd'], C['rust']], 0.09, 311)
    save(im, 'UI/socket_frame.png')

    # 数据块 128x128 border 24
    prof_amber = [(0, C['void']), (1, C['rd']), (2, C['rust']), (3, C['rustl']),
                  (4, C['rd']), (5, C['void']), (6, C['rust'])]
    im = make_panel(128, 128, '#221A14', 24, prof_amber, 404, with_rivets=True)
    save(im, 'UI/extra_bg.png')


def gen_buttons():
    for name, base, accent in [('btn_normal', C['steel'], None),
                               ('btn_hover', C['plate'], C['amber']),
                               ('btn_press', C['coal'], None)]:
        im = Image.new('RGBA', (96, 48), hx(base))
        prof = [(0, C['void']), (1, C['void']),
                (2, C['hi']), (3, C['hi']), (4, C['plate']),
                (5, C['base']), (6, C['void'])]
        if name == 'btn_press':
            prof = [(0, C['void']), (1, C['void']),
                    (2, C['void']), (3, C['coal']), (4, C['steel']),
                    (5, C['hi']), (6, C['void'])]
        frame_profile(im, 96, 48, prof)
        if accent:
            d = ImageDraw.Draw(im)
            d.rectangle([2, 2, 93, 45], outline=hx(accent), width=1)
        ImageDraw.Draw(im).rectangle([32, 12, 63, 35], fill=hx(base))
        ring_speckle(im, 96, 48, (32, 12), [C['steel'], C['plate']], 0.10, 505)
        save(im, 'UI/%s.png' % name)


def gen_tabs():
    # idle
    im = Image.new('RGBA', (64, 64), hx(C['coal']))
    prof = [(0, C['void']), (1, C['void']), (2, C['steel']), (3, C['plate']), (4, C['void'])]
    frame_profile(im, 64, 64, prof)
    ImageDraw.Draw(im).rectangle([24, 12, 39, 51], fill=hx(C['coal']))
    ring_speckle(im, 64, 64, (24, 12), [C['steel'], C['plate']], 0.10, 606)
    save(im, 'UI/tab_idle.png')

    # active
    im = Image.new('RGBA', (64, 64), hx(C['steel']))
    prof = [(0, C['void']), (1, C['void']), (2, C['hi']), (3, C['plate']), (4, C['void'])]
    frame_profile(im, 64, 64, prof)
    d = ImageDraw.Draw(im)
    d.rectangle([4, 2, 59, 5], fill=hx('#6F9C2B'))
    d.rectangle([4, 6, 59, 7], fill=hx(C['toxic']))
    d.rectangle([24, 12, 39, 51], fill=hx(C['steel']))
    ring_speckle(im, 64, 64, (24, 12), [C['steel'], C['plate']], 0.10, 707)
    save(im, 'UI/tab_active.png')


def gen_scroll():
    im = Image.new('RGBA', (16, 16), hx(C['screen']))
    frame_profile(im, 16, 16, [(0, C['void']), (1, C['coal']), (2, C['steel'])])
    for x in range(4, 12):
        im.putpixel((x, 8), hx(C['steel']))
    save(im, 'UI/scroll_track.png')

    im = Image.new('RGBA', (16, 16), hx(C['edge']))
    frame_profile(im, 16, 16, [(0, C['void']), (1, C['void']), (2, C['edge2']), (3, C['hi']), (4, C['plate'])])
    for y in range(6, 10):
        im.putpixel((8, y), hx(C['edge']))
    save(im, 'UI/scroll_thumb.png')


def gen_misc():
    # 警示斜纹 32x8
    im = Image.new('RGBA', (32, 8))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 31, 7], fill=hx('#17120E'))
    for y in range(8):
        for x in range(32):
            if ((x + y) // 4) % 2 == 0:
                im.putpixel((x, y), hx(C['amber']))
    save(im, 'UI/hazard_strip.png')

    # 扫描线 4x4（可平铺）
    im = Image.new('RGBA', (4, 4), (0, 0, 0, 0))
    for y in (0, 1):
        for x in range(4):
            im.putpixel((x, y), (0, 0, 0, 150))
    save(im, 'UI/scanline.png')


# ---------------------------------------------------------------- 图标
ICONS = {
    'card': [
        "................", "....########....", "....#wwwwww#....", "....#w#rr#w#....",
        "....#w#rr#w#....", "....#wwwwww#....", "....#w#rr#w#....", "....#w#rr#w#....",
        "....#wwwwww#....", "....#w#rr#w#....", "....#w#rr#w#....", "....#wwwwww#....",
        "....#wwwwww#....", "....########....", "................", "................",
    ],
    'ladder': [
        "................", "................", "................", "..aaaa..........",
        "..aaaa..........", "..aaaaaa........", "..aaaaaa........", "..aaaaaaaa......",
        "..aaaaaaaa......", "..aaaaaaaaaa....", "..aaaaaaaaaa....", "..aaaaaaaaaaaa..",
        "..aaaaaaaaaaaa..", "................", "................", "................",
    ],
    'skull': [
        "................", "................", "....########....", "..##wwwwwwww##..",
        "..#wwwwwwwwww#..", "..#ww##ww##ww#..", "..#ww##ww##ww#..", "..#wwwwwwwwww#..",
        "..##ww#ww#ww##..", "..#wwwwwwwwww#..", "...#ww#ww#ww#...", "...#wwwwwwww#...",
        "....########....", "................", "................", "................",
    ],
    'tower': [
        "................", "................", "...##########...", "...#wwwwwwww#...",
        "...#w##ww##w#...", "...#wwwwwwww#...", "...##########...", "................",
        "..############..", "..#wwwwwwwwww#..", "..#w##w##w##w#..", "..#wwwwwwwwww#..",
        "..############..", "................", "................", "................",
    ],
    'chip': [
        "................", "...##......##...", "...##......##...", ".##############.",
        ".#wwwwwwwwwwww#.", ".#w##########w#.", ".#w#wwwwwwww#w#.", ".#w#w######w#w#.",
        ".#w#w######w#w#.", ".#w#wwwwwwww#w#.", ".#w##########w#.", ".#wwwwwwwwwwww#.",
        ".##############.", "...##......##...", "...##......##...", "................",
    ],
    'doc': [
        "................", "...########.....", "...#wwwwww##....", "...#wwwwwww##...",
        "...#wwwwwwwww#..", "...#w######ww#..", "...#wwwwwwwww#..", "...#w#####www#..",
        "...#wwwwwwwww#..", "...#w######ww#..", "...#wwwwwwwww#..", "...#w####wwww#..",
        "...#wwwwwwwww#..", "...###########..", "................", "................",
    ],
    'lock': [
        "................", ".....######.....", "....##....##....", "....#......#....",
        "....#......#....", "....#......#....", "..############..", "..#wwwwwwwwww#..",
        "..#wwww##wwww#..", "..#wwww##wwww#..", "..#www####www#..", "..#wwww##wwww#..",
        "..#wwwwwwwwww#..", "..############..", "................", "................",
    ],
    'unlock': [
        "................", ".....######.....", "....##....##....", "....#......#....",
        "....#...........", "....##..........", "..############..", "..#wwwwwwwwww#..",
        "..#wwww##wwww#..", "..#wwww##wwww#..", "..#www####www#..", "..#wwww##wwww#..",
        "..#wwwwwwwwww#..", "..############..", "................", "................",
    ],
    'search': [
        "................", "...####.........", "..#wwww#........", ".#ww##ww#.......",
        ".#w#..ww#.......", ".#w#..ww#.......", ".#ww##ww#.......", "..#wwww#........",
        "...######.......", "......####......", ".......####.....", "........####....",
        ".........####...", "..........##....", "................", "................",
    ],
    'warn': [
        "................", "................", ".......##.......", "......####......",
        "......####......", ".....##ww##.....", ".....##ww##.....", "....##w##w##....",
        "....##w##w##....", "...##ww##ww##...", "...##ww##ww##...", "..##wwwwwwww##..",
        "..############..", "................", "................", "................",
    ],
    'arrow_left': [
        "................", "................", "................", ".....##.........",
        "....###.........", "...####.........", "..#####.........", ".######.........",
        "#######.........", ".######.........", "..#####.........", "...####.........",
        "....###.........", ".....##.........", "................", "................",
    ],
}

ICON_COL = {'#': C['void'], 'w': C['bone'], 'a': C['amber'], 'r': C['blood']}


def gen_icons():
    for name, rows in ICONS.items():
        im = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
        for y, row in enumerate(rows):
            for x, ch in enumerate(row[:16]):
                col = ICON_COL.get(ch)
                if col:
                    im.putpixel((x, y), hx(col))
        save(im, 'Icons/%s.png' % name)


# ---------------------------------------------------------------- 背景
def skyline(im, W, horizon, seed, minh, maxh, minw, maxw, color, winrate=0.0, ymax=None):
    d = ImageDraw.Draw(im)
    rnd = random.Random(seed)
    x = -8
    while x < W + 8:
        w = rnd.randint(minw, maxw)
        h = rnd.randint(minh, maxh)
        top = horizon - h
        segs = rnd.randint(2, 4)
        segw = max(2, w // segs)
        for s in range(segs):
            sh = h if rnd.random() > 0.5 else int(h * (0.62 + rnd.random() * 0.33))
            d.rectangle([x + s * segw, horizon - sh, x + (s + 1) * segw - 1, horizon], fill=hx(color))
        if rnd.random() < 0.3:
            ax = x + w // 2
            d.rectangle([ax, top - 8, ax, top], fill=hx(color))
        if winrate > 0:
            sy = horizon - int(h * 0.55)
            for wy in range(sy, horizon - 4, 4):
                for wx in range(x + 2, x + w - 2, 4):
                    if rnd.random() < winrate:
                        put(im, wx, wy, hx(rnd.choice([C['amber'], C['amber'], C['blood']])))
        x += w + rnd.randint(2, 10)


def make_bg(W=480, H=270, horizon=162):
    im = Image.new('RGBA', (W, H), hx('#0B0807'))
    d = ImageDraw.Draw(im)
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
                    im.putpixel((x, y1 - 1), hx(sky[i + 1]))
                    if x + 1 < W:
                        im.putpixel((x + 1, y1 - 2), hx(sky[i + 1]))

    skyline(im, W, horizon, 1337, 118, 156, 14, 32, '#3A2530')
    skyline(im, W, horizon, 90210, 70, 110, 16, 38, '#241619', 0.09)
    skyline(im, W, horizon, 4242, 18, 50, 9, 24, '#150E0C', 0.04)

    # 地平线雾
    for y in range(horizon - 10, horizon):
        for x in range(0, W, 2):
            if (x // 2 + y) % 3 == 0:
                im.putpixel((x, y), hx('#E09650'))

    # 地面
    d.rectangle([0, horizon, W - 1, H - 1], fill=hx('#2A1F18'))
    d.rectangle([0, horizon, W - 1, horizon + 6], fill=hx('#4D3620'))
    d.rectangle([0, horizon + 7, W - 1, horizon + 15], fill=hx('#3A2B1E'))
    rnd = random.Random(555)
    for _ in range(160):
        x = rnd.randint(0, W - 1)
        y = rnd.randint(horizon + 8, H - 1)
        im.putpixel((x, y), hx('#3A2B1E'))

    # 地平线亮线
    d.line([0, 232, W - 1, 232], fill=hx('#4D3620'))

    # 辐射积水
    rnd2 = random.Random(31)
    for _ in range(14):
        x = rnd2.randint(0, W - 8)
        y = rnd2.randint(236, 246)
        w = rnd2.randint(3, 9)
        d.line([x, y, min(W - 1, x + w), y], fill=hx(C['toxic']))

    # 前景废墟（基座 232，向上长）
    rnd3 = random.Random(8123)
    x = -10
    while x < W + 12:
        big = rnd3.random() < 0.55
        w = rnd3.randint(24, 52) if big else rnd3.randint(9, 20)
        h = rnd3.randint(16, 40) if big else rnd3.randint(6, 14)
        top = 232 - h
        d.rectangle([x, top, x + w - 1, 232 + h], fill=hx('#150E0C'))
        if rnd3.random() < 0.65:
            cw = max(2, int(w * (0.28 + rnd3.random() * 0.42)))
            cx = x + rnd3.randint(0, max(0, w - cw))
            d.rectangle([cx, top, cx + cw - 1, top + rnd3.randint(2, 7)], fill=hx('#2A1F18'))
        if rnd3.random() < 0.8:
            d.line([x, top, x + w - 1, top], fill=hx('#B06A3A'))
        if rnd3.random() < 0.5:
            for wy in range(top + 5, 228, 5):
                for wx in range(x + 2, x + w - 2, 5):
                    if rnd3.random() < 0.1:
                        put(im, wx, wy, hx('#8A5330'))
        x += w + rnd3.randint(3, 18)

    # 电线杆
    for i in range(5):
        px = 20 + i * 98 + rnd3.randint(0, 30)
        ph = rnd3.randint(26, 48)
        d.rectangle([px, 232 - ph, px, 232], fill=hx('#150E0C'))
        d.line([px - 5, 232 - ph, px + 5, 232 - ph], fill=hx('#150E0C'))

    # 飞鸟
    for (bx, by) in [(112, 22), (128, 14), (140, 26)]:
        im.putpixel((bx, by), hx('#241619'))
        im.putpixel((bx + 2, by), hx('#241619'))
        im.putpixel((bx + 1, by + 1), hx('#241619'))
    return im


def main():
    gen_panels()
    gen_buttons()
    gen_tabs()
    gen_scroll()
    gen_misc()
    gen_icons()
    save(make_bg(), 'BG/codex_bg.png')

    total = 0
    for base, dirs, files in os.walk(OUT):
        for f in files:
            total += 1
    print('assets written under', OUT)
    print('count =', total)


if __name__ == '__main__':
    main()
