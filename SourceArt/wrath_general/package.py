"""Slice the generated poses without redrawing, resizing or interpolating any pose.

Exports the same source pixels on one registered canvas. Binary alpha and removal
of tiny detached alpha specks are the pixel-art export finish, not painted art.
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = Path(__file__).resolve().parent
DEST = HERE.parent.parent / 'Assets/Resources/WrathGeneral/frames'
CANVAS = 384
ROOT = (round(CANVAS * .60), round(CANVAS * .90))
SOURCE_ROOT_X = {'idle': 201, 'walk': 195, 'stun': 185, 'hurt': 196}


def export(previews=False):
    report = {'canvas': CANVAS, 'root': [ROOT[0], ROOT[1]+1],
              'pivot': [ROOT[0]/CANVAS, (CANVAS-ROOT[1]-1)/CANVAS], 'pixelsPerUnit': 180,
              'alpha': 'binary 0/255; transparent RGB zero', 'resampling': 'none',
              'frameCount': 64, 'clips': {}}
    for action, fps in [('idle', 8), ('walk', 16), ('stun', 24), ('hurt', 32)]:
        source = Image.open(HERE / f'{action}.png').convert('RGBA')
        width, height = source.size
        records, frames = [], []
        for index in range(16):
            row, col = divmod(index, 4)
            box = (round(col * width / 4), round(row * height / 4),
                   round((col + 1) * width / 4), round((row + 1) * height / 4))
            pixels = np.array(source.crop(box))
            solid = pixels[:, :, 3] >= 128
            labels, _ = ndimage.label(solid, structure=np.ones((3, 3), dtype=int))
            areas = np.bincount(labels.ravel())
            keep = areas >= 3
            keep[0] = False
            solid = keep[labels]
            pixels[:, :, 3] = np.where(solid, 255, 0)
            pixels[~solid] = 0
            h, w = solid.shape
            # The blade trails to the left; central lower-body pixels own the feet.
            body = solid[int(h * .72):, int(w * .43):int(w * .86)]
            ys, xs = np.where(body)
            if not len(xs):
                raise ValueError(f'{action} {index}: no boot support')
            foot_y = int(ys.max()) + int(h * .72)
            support = solid[max(0, foot_y - 7):foot_y + 1, int(w * .43):int(w * .86)]
            _, boot_x = np.where(support)
            foot_x = (int(boot_x.min()) + int(boot_x.max())) // 2 + int(w * .43)
            # A planted walk foot moves across the stride. Lock the authored hip
            # origin instead of dragging the whole body after each supporting boot.
            anchor_x = SOURCE_ROOT_X[action]
            dx, dy = ROOT[0] - anchor_x, ROOT[1] - foot_y
            output = Image.new('RGBA', (CANVAS, CANVAS))
            output.paste(Image.fromarray(pixels), (dx, dy))
            result = np.array(output)
            if np.count_nonzero(result[:, :, 3]) != np.count_nonzero(solid):
                raise ValueError(f'{action} {index}: registered pose would clip')
            edges = np.concatenate((result[0, :, 3], result[-1, :, 3],
                                    result[:, 0, 3], result[:, -1, 3]))
            if edges.any():
                raise ValueError(f'{action} {index}: sprite touches canvas edge')
            folder = DEST / action
            folder.mkdir(parents=True, exist_ok=True)
            path = folder / f'{action}-{index+1:02}.png'
            output.save(path, optimize=True)
            records.append({'frame': index + 1, 'sourceRect': list(box),
                            'sourceFoot': [foot_x, foot_y], 'sourceAnchorX': anchor_x,
                            'offset': [dx, dy],
                            'bounds': list(output.getbbox()),
                            'sha256': hashlib.sha256(output.tobytes()).hexdigest(),
                            'visiblePixels': int(solid.sum())})
            if previews:
                frames.append(output)
        if len({record['sha256'] for record in records}) != 16:
            raise ValueError(f'{action}: duplicate pose')
        durations=[round(1000 / 24)]*15+[800] if action=='stun' else round(1000/fps)
        if previews:
            frames[0].save(HERE / f'{action}.webp', save_all=True, append_images=frames[1:],
                           duration=durations, loop=0, lossless=True, method=6)
        report['clips'][action] = {'fps': fps, 'loop': action in ('idle', 'walk'),
                                  'loopStartFrame': 16 if action == 'stun' else 1,
                                  'entryFps': 24 if action == 'stun' else fps,
                                  'holdFps': 8 if action == 'stun' else fps,
                                  'recovery': 'reverse entry before configured break ends' if action == 'stun' else 'included' if action == 'hurt' else 'loop',
                                  'frames': records}
    (HERE / 'manifest.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'frames': 64, 'canvas': CANVAS, 'root': ROOT,
                      'alphaValues': [0, 255], 'uniquePosesPerClip': 16}))


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--previews', action='store_true', help='Also export optional animated WebP previews.')
    export(previews=parser.parse_args().previews)
