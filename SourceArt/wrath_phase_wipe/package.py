"""Slice the image-model full-screen FX; no procedural painting or interpolation."""
from pathlib import Path
import hashlib
import json
import math
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
DEST = HERE.parents[1] / 'Assets/Resources/WrathGeneral/phase2/wipe'

def edge(x):
    return math.floor(x+.5)

def export():
    source = Image.open(HERE/'source.png').convert('RGBA')
    width, height = source.size
    frames, records = [], []
    DEST.mkdir(parents=True, exist_ok=True)
    for index in range(16):
        row, col = divmod(index, 4)
        # Full-bleed effects intentionally reach cell edges. Inset removes painted sheet separators.
        box = (edge(col*width/4)+2, edge(row*height/4)+2,
               edge((col+1)*width/4)-2, edge((row+1)*height/4)-2)
        a = np.array(source.crop(box).resize((512,288), Image.Resampling.NEAREST))
        solid = a[:,:,3] >= 128
        a[:,:,3] = np.where(solid,255,0)
        a[~solid] = 0
        frame = Image.fromarray(a)
        frame.save(DEST/f'wipe-{index+1:02}.png', optimize=True)
        frames.append(frame)
        records.append({'frame': index+1, 'sourceRect': box,
                        'opaqueCoverage': float(solid.mean()),
                        'sha256': hashlib.sha256(a.tobytes()).hexdigest()})
    assert len({r['sha256'] for r in records}) == 16
    for record in records[8:11]:
        assert record['opaqueCoverage'] >= .97, f'Peak texture coverage too low: {record}'
    assert records[0]['opaqueCoverage'] < .1
    assert records[-1]['opaqueCoverage'] < .02
    frames[0].save(HERE/'wipe.webp', save_all=True, append_images=frames[1:],
                   duration=[100]*16, loop=0, lossless=True, method=6)
    atlas = Image.new('RGBA', (2048,1152))
    for i, frame in enumerate(frames):
        atlas.paste(frame, ((i%4)*512,(i//4)*288))
    atlas.save(HERE/'registered.png', optimize=True)
    report = {'art_source':'host_image', 'route':'built-in image_gen',
              'sourceSize':source.size, 'canvas':[512,288], 'count':16,
              'durationSeconds':1.6, 'alpha':'binary; transparent RGB zero',
              'fullBleed':True, 'peakComposite':'runtime opaque charcoal underlay fills tiny alpha holes in frames9-11', 'frames':records}
    (HERE/'manifest.json').write_text(json.dumps(report,indent=2), encoding='utf-8')
    print(json.dumps({'frames':16,'coverage':[round(r['opaqueCoverage'],4) for r in records]}))

if __name__ == '__main__':
    export()
