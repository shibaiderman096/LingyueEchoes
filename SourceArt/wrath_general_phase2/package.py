"""Package image-model drawings; no procedural character drawing or interpolation.

One scale and hip origin per action, binary alpha, shared gameplay pivot.
Grid boundaries use half-up rounding to match the original-sheet spill audit.
"""
from pathlib import Path
import hashlib
import json
import math
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = Path(__file__).resolve().parent
DEST = HERE.parents[1] / 'Assets/Resources/WrathGeneral/phase2/frames'
CANVAS, ROOT = 384, (230, 346)
ACTIONS = {'idle': (197, 8), 'walk': (195, 16), 'hurt': (195, 32),
           'stun': (195, 24), 'phase': (201, None)}

def edge(value):
    return math.floor(value + .5)

def finish(pixels):
    solid = pixels[:, :, 3] >= 128
    labels, _ = ndimage.label(solid, structure=np.ones((3, 3), dtype=int))
    areas = np.bincount(labels.ravel())
    keep = areas >= 3
    keep[0] = False
    solid = keep[labels]
    pixels[:, :, 3] = np.where(solid, 255, 0)
    pixels[~solid] = 0
    return pixels

def boot_line(pixels, action):
    h, w = pixels.shape[:2]
    x0, x1 = edge(w * .44), edge(w * .86)
    y0 = edge(h * .72)
    rgb = pixels[y0:, x0:x1, :3].astype(float)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    gold = (r > g * 1.05) & (r < g * 2.2) & (g > b * 1.1) & (g > 55)
    gold &= pixels[y0:, x0:x1, 3] > 0
    ys, _ = np.where(gold)
    if not len(ys):
        raise ValueError(f'{action}: no boot calibration')
    return int(ys.max()) + y0

def export():
    report = {'art_source': 'host_image', 'route': 'built-in image_gen',
              'canvas': CANVAS, 'pivot': [230 / 384, 37 / 384], 'pixelsPerUnit': 180,
              'root': [230, 347], 'frameCount': 80, 'alpha': 'binary 0/255; transparent RGB zero',
              'registration': 'fixed hip X and one scale per action; grounded boot baseline',
              'clips': {}}
    for action, (source_x, fps) in ACTIONS.items():
        source = Image.open(HERE / f'{action}.png').convert('RGBA')
        width, height = source.size
        first = finish(np.array(source.crop((0, 0, edge(width / 4), edge(height / 4)))))
        first_foot = boot_line(first, action)
        ys, _ = np.where(first[:, :, 3] > 0)
        target_height = 275 if action == 'phase' else 286
        scale = target_height / (first_foot - int(ys.min()) + 1)
        frames, records = [], []
        for index in range(16):
            row, col = divmod(index, 4)
            box = (edge(col * width / 4), edge(row * height / 4),
                   edge((col + 1) * width / 4), edge((row + 1) * height / 4))
            pixels = finish(np.array(source.crop(box)))
            foot = boot_line(pixels, action)
            image = Image.fromarray(pixels)
            # One action-wide transform. NEAREST preserves authored pixel edges.
            scaled_size = (edge(image.width * scale), edge(image.height * scale))
            image = image.resize(scaled_size, Image.Resampling.NEAREST)
            source_root = source_x * (box[2] - box[0]) / (width / 4)
            offset = (ROOT[0] - edge(source_root * scale), ROOT[1] - edge(foot * scale))
            output = Image.new('RGBA', (CANVAS, CANVAS))
            output.paste(image, offset)
            if np.count_nonzero(np.array(output)[:, :, 3]) != np.count_nonzero(np.array(image)[:, :, 3]):
                raise ValueError(f'{action}-{index+1}: registration clips source pixels')
            bbox = output.getbbox()
            if min(bbox[0], bbox[1], CANVAS-bbox[2], CANVAS-bbox[3]) < 8:
                raise ValueError(f'{action}-{index+1}: insufficient export gutter {bbox}')
            target = DEST / action / f'{action}-{index+1:02}.png'
            target.parent.mkdir(parents=True, exist_ok=True)
            output.save(target, optimize=True)
            frames.append(output)
            records.append({'frame': index+1, 'sourceRect': box, 'sourceBootY': foot,
                            'offset': offset, 'bounds': bbox,
                            'sha256': hashlib.sha256(output.tobytes()).hexdigest()})
        if len({record['sha256'] for record in records}) != 16:
            raise ValueError(f'{action}: duplicate pose')
        atlas = Image.new('RGBA', (CANVAS*4, CANVAS*4))
        for index, frame in enumerate(frames):
            atlas.paste(frame, ((index % 4)*CANVAS, (index // 4)*CANVAS))
        atlas.save(HERE / f'{action}-registered.png', optimize=True)
        duration = [62]*16 if action == 'idle' else [63]*16 if action == 'walk' else [42]*15+[800] if action == 'stun' else [94]*16 if action == 'phase' else [34]*16
        preview_frames = frames + frames[-2::-1] if action == 'stun' else frames
        if action == 'stun':
            duration += [42]*15
        preview_frames[0].save(HERE / f'{action}.webp', save_all=True, append_images=preview_frames[1:], duration=duration, loop=0, lossless=True, method=6)
        report['clips'][action] = {'fps': fps, 'loop': action in ('idle', 'walk'),
                                  'scale': scale, 'sourceRootX': source_x,
                                  'recovery': 'reverse entry, stable frame16 hold' if action == 'stun' else 'included' if action == 'hurt' else None,
                                  'frames': records}
    (HERE / 'manifest.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'frames': 80, 'canvas': CANVAS, 'alpha': [0, 255],
                      'actionScales': {a: r['scale'] for a, r in report['clips'].items()}}))

if __name__ == '__main__':
    export()
