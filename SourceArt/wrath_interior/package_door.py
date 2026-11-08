"""Slice and register the generated door; preserve source RGB, no resizing."""
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import label

root = Path(__file__).resolve().parents[2]
sheet = Image.open(root / "SourceArt/wrath_interior/DoorOpening.png").convert("RGBA")
out = root / "Assets/Art/Wrath/DoorOpening"
out.mkdir(parents=True, exist_ok=True)
for i in range(16):
    x, y = i % 4, i // 4
    cell = sheet.crop((round(x * sheet.width / 4), round(y * sheet.height / 4),
                       round((x + 1) * sheet.width / 4), round((y + 1) * sheet.height / 4)))
    pixels = np.array(cell)
    mask = pixels[:, :, 3] >= 128
    ids, _ = label(mask)
    counts = np.bincount(ids.ravel())
    counts[0] = 0
    mask &= counts[ids] >= 12
    pixels[:, :, 3] = np.where(mask, 255, 0)
    cell = Image.fromarray(pixels)
    bounds = cell.getbbox()
    canvas = Image.new("RGBA", (320, 320))
    canvas.paste(cell, (68 - bounds[0], 315 - bounds[3]))
    canvas.save(out / f"door-{i + 1:02}.png")
