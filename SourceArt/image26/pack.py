"""Slice reviewed Images 2.6 sheets into registered Unity sprites and native clips."""
import hashlib
import json
import math
from pathlib import Path
import re
import statistics

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = Path(__file__).resolve().parent
RES = HERE.parents[1] / "Assets/Resources"
MANIFEST = RES / "Data/animations.json"


def registered_frames(clip, spec):
    path = HERE / spec["source"]
    source = Image.open(path)
    if source.mode != "RGBA" or source.width != source.height:
        raise ValueError(f"{path.name}: expected square native-alpha sheet")
    if hashlib.sha256(path.read_bytes()).hexdigest() != spec["sha256"]:
        raise ValueError(f"{path.name}: source changed after review")
    edges = [math.floor(i * source.width / 4 + .5) for i in range(5)]
    frames, boxes, roots = [], [], []
    for n in spec["order"]:
        row, col = divmod(n, 4)
        # A reviewed override can follow the actual gutter where it shifts off grid.
        bounds = spec.get("cellOverrides", {}).get(str(n), (edges[col], edges[row], edges[col + 1], edges[row + 1]))
        frame = source.crop(bounds)
        pixels = np.array(frame)
        # The generated alpha has a faint halo. Use a clean binary pixel-art matte
        # and remove isolated specks, retaining all substantial connected parts.
        labels, _ = ndimage.label(pixels[:, :, 3] >= 128, np.ones((3, 3)))
        areas = np.bincount(labels.ravel())
        keep = (labels != 0) & (areas[labels] >= 24)
        pixels[~keep] = 0
        pixels[keep, 3] = 255
        frame = Image.fromarray(pixels)
        box = frame.getbbox()
        if not box or min(box[0], box[1], frame.width-box[2], frame.height-box[3]) < 2:
            raise ValueError(f"{path.name}: unsafe sprite boundary in cell {n + 1}")
        frames.append(frame)
        boxes.append(box)
        height = box[3] - box[1]
        start = int(box[3] - height * (.42 if clip["loop"] else .06))
        end = int(box[3] - height * .23) if clip["loop"] else box[3]
        columns = np.nonzero(keep[start:end])[1]
        roots.append(float(np.median(columns)) if len(columns) else (box[0]+box[2])/2)

    folder, name = clip["folder"], clip["name"]
    original = [Image.open(RES / f"{folder}/frames/{name}/{name}-{i+1:02}.png").convert("RGBA") for i in range(8)]
    target = original[0].size
    baseline = 112 if folder == "pixel8" else 200
    original_heights = [im.getchannel("A").point(lambda a: 255 if a >= 128 else 0).getbbox() for im in original]
    scale = statistics.median(b[3]-b[1] for b in original_heights) / statistics.median(b[3]-b[1] for b in boxes)
    center = statistics.median(roots)
    if clip["loop"]:
        roots = [.65 * center + .35 * root for root in roots]
    # Preserve a shared scale across the clip, including the longest blade reach.
    for box, root in zip(boxes, roots):
        scale = min(scale, (target[0]/2 - 5) / max(1, root-box[0], box[2]-root),
                    (baseline-5) / (box[3]-box[1]))
    for i, (frame, root) in enumerate(zip(frames, roots)):
        fit = frame.resize((round(frame.width*scale), round(frame.height*scale)), Image.Resampling.NEAREST)
        box = fit.getbbox()
        canvas = Image.new("RGBA", target)
        dx, dy = round(target[0]/2-root*scale), baseline-box[3]
        if min(dx + box[0], dy + box[1],
               target[0] - dx - box[2], target[1] - dy - box[3]) < 3:
            raise ValueError(f"{path.name}: registered pose {i + 1} would be clipped")
        canvas.alpha_composite(fit, (dx, dy))
        yield canvas


def pack(clip, spec):
    folder, name = clip["folder"], clip["name"]
    poses = list(registered_frames(clip, spec))
    if len(poses) != 16 or len({hashlib.sha256(p.tobytes()).digest() for p in poses}) != 16:
        raise ValueError(f"{name}: expected sixteen unique poses")
    template = (RES / f"{folder}/frames/{name}/{name}-01.png.meta").read_text(encoding="utf-8")
    pivot = (poses[0].height - (112 if folder == "pixel8" else 200)) / poses[0].height
    template, count = re.subn(r"(?m)^  spritePivot: \{[^\n]+\}$",
                              f"  spritePivot: {{x: 0.5, y: {pivot:.9g}}}", template)
    if count != 1:
        raise ValueError(f"{name}: missing custom import pivot")
    paths, sprites = [], []
    for i, frame in enumerate(poses, 1):
        resource = f"{folder}/frames16/{name}/{name}-{i:02}"
        dest = RES / (resource + ".png")
        dest.parent.mkdir(parents=True, exist_ok=True)
        frame.save(dest)
        guid = hashlib.md5(resource.encode("utf-8")).hexdigest()
        dest.with_suffix(".png.meta").write_text(re.sub(r"(?m)^guid: \w+$", f"guid: {guid}", template, count=1), encoding="utf-8")
        paths.append(resource)
        sprites.append(f"{{fileID: 21300000, guid: {guid}, type: 3}}")

    duration = clip["duration"]
    times = [i*duration/16 for i in range(16)]
    native = RES / (clip["path"] + ".anim")
    yaml = native.read_text(encoding="utf-8")
    values = sprites + [sprites[-1]]
    keys = "\n".join(f"    - time: {t:.8g}\n      value: {value}" for t, value in zip(times+[duration-.00001], values))
    yaml, curves = re.subn(r"(?s)(m_PPtrCurves:\n  - serializedVersion: 2\n    curve:\n).*?(\n    attribute: m_Sprite)", lambda m: m[1]+keys+m[2], yaml)
    yaml, mappings = re.subn(r"(?s)(    pptrCurveMapping:\n).*?(\n  m_AnimationClipSettings:)", lambda m: m[1]+"\n".join("    - "+v for v in values)+m[2], yaml)
    if curves != 1 or mappings != 1:
        raise ValueError(f"{name}: native clip structure changed")
    yaml = re.sub(r"(?m)^  m_SampleRate: .+$", "  m_SampleRate: 60", yaml)
    yaml = re.sub(r"(?m)^    m_StopTime: .+$", f"    m_StopTime: {duration-.00001+1/60:.8g}", yaml)
    native.write_text(yaml, encoding="utf-8")
    clip.update(times=times, frames=paths, sample=60)
    if "contact" in spec:
        clip["contactPose"] = spec["contact"]
    if "secondContact" in spec:
        clip["secondContactPose"] = spec["secondContact"]


if __name__ == "__main__":
    data = json.loads(MANIFEST.read_text(encoding="utf-8"))
    specs = json.loads((HERE / "sources.json").read_text(encoding="utf-8"))["clips"]
    for clip in data["clips"]:
        key = f'{clip["folder"]}/{clip["name"]}'
        if key in specs:
            pack(clip, specs[key])
    MANIFEST.write_text(json.dumps(data, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    print(f"Packed {len(specs)} native clips with 16 unique poses each.")
