"""Losslessly cut the approved panorama into independently placeable map parts."""
from pathlib import Path
from PIL import Image
import json,shutil,sys
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'art/combat8'))
# Reuse metadata helpers without importing the animation packaging program.
exec((ROOT/'art/combat8/package.py').read_text(encoding='utf-8').split('SPECS=')[0].replace('HERE=Path(__file__).resolve().parent','HERE=ROOT/"art/combat8"'))
source=RES/'prologue_route/moonlit_route.png'
master=ROOT/'art/prologue_route/moonlit_route-master.png'
if not master.exists():shutil.copyfile(source,master)
im=Image.open(master);dest=RES/'prologue_route/parts';folder(dest)
parts=[]
for row,(top,bottom) in enumerate([(0,300),(300,548),(548,724)]):
    for column in range(3):
        left=column*724;right=left+724;name=f'{["canopy","landmarks","foundation"][row]}-{column+1}'
        tile=im.crop((left,top,right,bottom));path=dest/(name+'.png');tile.save(path);image_meta(path,tile.size)
        parts.append({'name':name,'source_rect':[left,top,right,bottom],'image':'../../assets/resources/prologue_route/parts/'+name+'.png','offset':[left,top],'layer':row})
write(ROOT/'art/prologue_route/background-parts.json',{'art_source':'host_image','master':'moonlit_route-master.png','source_size':im.size,'ground_y':548,'world_scale':3600/2172,'world_left':-1640,'parts':parts})
bundle=json.loads((ROOT/'art/prologue_route/map-bundle.json').read_text(encoding='utf-8'))
bundle['layers']=[{'name':p['name'],'kind':'image','image':p['image'],'offset':p['offset']} for p in parts]+[{'name':'playable-props','kind':'objects'}]
write(ROOT/'art/prologue_route/map-bundle.json',bundle)
print('Split approved background into',len(parts),'registered parts; source pixels preserved')
