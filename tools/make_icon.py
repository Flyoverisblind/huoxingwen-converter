"""生成应用图标 app.ico（渐变圆角方块 + 白色“火”字）。"""
from PIL import Image, ImageDraw, ImageFont
import os

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src", "app.ico")
S = 256
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

# 渐变
grad = Image.new("RGBA", (S, S))
d = ImageDraw.Draw(grad)
c1 = (47, 111, 237)
c2 = (139, 92, 246)
for y in range(S):
    t = y / (S - 1)
    d.line([(0, y), (S, y)], fill=(int(c1[0] + (c2[0] - c1[0]) * t),
                                   int(c1[1] + (c2[1] - c1[1]) * t),
                                   int(c1[2] + (c2[2] - c1[2]) * t), 255))

# 圆角遮罩
mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=54, fill=255)
img.paste(grad, (0, 0), mask)

# 白色“火”字
font = None
for path, idx in [("C:/Windows/Fonts/msyhbd.ttc", 0), ("C:/Windows/Fonts/msyh.ttc", 0),
                  ("C:/Windows/Fonts/simhei.ttf", 0), ("C:/Windows/Fonts/simsun.ttc", 0)]:
    if os.path.exists(path):
        try:
            font = ImageFont.truetype(path, 168, index=idx)
            break
        except Exception:
            continue
if font is None:
    font = ImageFont.load_default()

draw = ImageDraw.Draw(img)
text = "火"
bbox = draw.textbbox((0, 0), text, font=font)
w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
draw.text(((S - w) / 2 - bbox[0], (S - h) / 2 - bbox[1] - 6), text, font=font, fill=(255, 255, 255, 255))

img.save(OUT, format="ICO", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print("图标已生成:", OUT, os.path.getsize(OUT), "字节")
