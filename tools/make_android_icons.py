"""生成安卓图标：各密度 ic_launcher.png（圆角渐变+火）与 ic_launcher_fg.png（自适应前景）。"""
from PIL import Image, ImageDraw, ImageFont
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, "android", "app", "res")
DENSITIES = [("mdpi", 48), ("hdpi", 72), ("xhdpi", 96), ("xxhdpi", 144), ("xxxhdpi", 192)]
C1, C2 = (47, 111, 237), (139, 92, 246)


def load_font(size):
    for path, idx in [("C:/Windows/Fonts/msyhbd.ttc", 0), ("C:/Windows/Fonts/msyh.ttc", 0),
                      ("C:/Windows/Fonts/simhei.ttf", 0)]:
        if os.path.exists(path):
            try:
                return ImageFont.truetype(path, size, index=idx)
            except Exception:
                continue
    return ImageFont.load_default()


def gradient(size):
    img = Image.new("RGBA", (size, size))
    d = ImageDraw.Draw(img)
    for y in range(size):
        t = y / max(1, size - 1)
        d.line([(0, y), (size, y)], fill=(int(C1[0] + (C2[0] - C1[0]) * t),
                                          int(C1[1] + (C2[1] - C1[1]) * t),
                                          int(C1[2] + (C2[2] - C1[2]) * t), 255))
    return img


def draw_glyph(img, size, ratio, dy=0.0):
    """在 img 中央画白色“火”，字号为 size*ratio。"""
    font = load_font(max(8, int(size * ratio)))
    d = ImageDraw.Draw(img)
    text = "火"
    box = d.textbbox((0, 0), text, font=font)
    w, h = box[2] - box[0], box[3] - box[1]
    d.text(((size - w) / 2 - box[0], (size - h) / 2 - box[1] + size * dy), text, font=font,
           fill=(255, 255, 255, 255))


made = []
for name, px in DENSITIES:
    out_dir = os.path.join(RES, "mipmap-" + name)
    os.makedirs(out_dir, exist_ok=True)

    # 传统方形图标（API < 26 使用）
    icon = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    grad = gradient(px)
    mask = Image.new("L", (px, px), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, px - 1, px - 1], radius=int(px * 0.21), fill=255)
    icon.paste(grad, (0, 0), mask)
    draw_glyph(icon, px, 0.62, dy=-0.03)
    p = os.path.join(out_dir, "ic_launcher.png")
    icon.save(p)
    made.append(p)

    # 自适应图标前景（108dp 画布，内容留出安全区）
    fg = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    draw_glyph(fg, px, 0.42, dy=-0.02)
    p = os.path.join(out_dir, "ic_launcher_fg.png")
    fg.save(p)
    made.append(p)

print("已生成安卓图标 %d 个：" % len(made))
for p in made[:4]:
    print("  ", os.path.relpath(p, ROOT))
