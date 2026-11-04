"""Package host-drawn grids into feet-registered native Creator clips. Never invent poses."""
from pathlib import Path
from PIL import Image, ImageOps
import copy, hashlib, json, shutil, uuid

ROOT=Path(__file__).resolve().parents[2]
HERE=Path(__file__).resolve().parent
RES=ROOT/'assets/resources'
NS=uuid.UUID('238be4e4-a6e3-450e-ad0a-a147f8d51839')
def uid(path): return str(uuid.uuid5(NS,str(path).replace('\\','/')))
def write(path,data):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def folder(path):
    path.mkdir(parents=True,exist_ok=True)
    meta=Path(str(path)+'.meta')
    if not meta.exists():write(meta,{'ver':'1.2.0','importer':'directory','imported':True,'uuid':uid(path.relative_to(ROOT)),'files':[],'subMetas':{},'userData':{}})
image_template=json.loads((RES/'pixel8/frames/idle/idle-01.png.meta').read_text(encoding='utf-8'))
clip_template=json.loads((RES/'pixel8/animations/execute.anim').read_text(encoding='utf-8'))
def image_meta(path,size):
    meta=Path(str(path)+'.meta');u=json.loads(meta.read_text(encoding='utf-8'))['uuid'] if meta.exists() else uid(path.relative_to(ROOT))
    m=copy.deepcopy(image_template);m['uuid']=u;m['userData']['redirect']=u+'@6c48a';w,h=size
    for key,sub in m['subMetas'].items():
        sub['uuid']=u+'@'+key;sub['displayName']=path.stem
        data=sub['userData'];data['imageUuidOrDatabaseUri']=u if key=='6c48a' else u+'@6c48a'
        if key=='f9941':
            for key2,val in {'width':w,'height':h,'rawWidth':w,'rawHeight':h,'trimType':'none','packable':False,'offsetX':0,'offsetY':0,'trimX':0,'trimY':0}.items():data[key2]=val
            data['vertices']={'rawPosition':[-w/2,-h/2,0,w/2,-h/2,0,-w/2,h/2,0,w/2,h/2,0],'indexes':[0,1,2,2,1,3],'uv':[0,h,w,h,0,0,w,0],'nuv':[0,0,1,0,0,1,1,1],'minPos':[-w/2,-h/2,0],'maxPos':[w/2,h/2,0]}
    write(meta,m);return u+'@f9941'
def clip(path,name,frames):
    data=copy.deepcopy(clip_template);data[0]['_name']=name;data[0]['wrapMode']=2 if name in ('idle','run') else 1
    data[0]['sample']=8;data[0]['_duration']=1
    data[5]['_times']=[i/8 for i in range(8)];data[5]['_values']=[{'__uuid__':u,'__expectedType__':'cc.SpriteFrame'} for u in frames]
    write(path,data);meta=Path(str(path)+'.meta');u=json.loads(meta.read_text(encoding='utf-8'))['uuid'] if meta.exists() else uid(path.relative_to(ROOT))
    write(meta,{'ver':'2.0.4','importer':'animation-clip','imported':True,'uuid':u,'files':['.bin'],'subMetas':{},'userData':{'name':name}})
    return u

SPECS={
 'guardian_locomotion': [('idle',0,set(range(8))),('run',8,set())],
 'guardian_blades':[('slash',0,{0,1,7}),('double',8,{0,7})],
 'guardian_air_react':[('jump',0,{7}),('stagger',8,{6,7})],
 'guardian_end':[('broken',0,set()),('dead',8,{0})],
 'guardian_thrust_slam':[('thrust',0,{0,1,7}),('slam',8,{0,1,7})],
 'guardian_leap_spin':[('leap',0,{0,7}),('spin',8,{0,7})],
 'guardian_phase':[('phase',0,{0,1,2,5,6,7}),('guard',8,set(range(8)))],
 'hero_finisher':[('finisher',0,set())], 'hero_execute':[('execute',0,set())]
}
manifest={'schema':'lingyue.drawn-combat.v1','art_source':'host_image','route':'image_gen','logic_hz':60,'pose_fps':8,'clips':{},'sources':[]}
sources=json.loads((HERE/'sources.json').read_text(encoding='utf-8'))
for record in sources['records']:
    key=record['key'];src=Path(record['generated']);master=HERE/(key+'.png')
    if not master.exists():shutil.copyfile(src,master)
    record['project_master']=str(master.relative_to(ROOT)).replace('\\','/');record['sha256']=hashlib.sha256(master.read_bytes()).hexdigest()
    manifest['sources'].append(record)
    sheet=Image.open(master).convert('RGBA');hero=key.startswith('hero_');rows=2 if hero else 4
    cw,ch=sheet.width/4,sheet.height/rows
    target=(192,128) if hero else (256,224);baseline=112 if hero else 200
    # A single body scale per source sheet. Never resize individual poses to their alpha bounds.
    scale=(78/(ch*.884)) if hero else (146/(ch*.90))
    namespace='pixel8' if hero else 'guardian8'
    folder(RES/namespace);folder(RES/namespace/'frames');folder(RES/namespace/'animations')
    for name,start,flips in SPECS[key]:
        dest=RES/namespace/'frames'/name;folder(dest);frame_ids=[];registrations=[]
        for n in range(8):
            cell=start+n;x=round(cell%4*cw);y=round(cell//4*ch)
            img=sheet.crop((x,y,round((cell%4+1)*cw),round((cell//4+1)*ch)))
            if n in flips:img=ImageOps.mirror(img)
            alpha=img.getchannel('A');mask=alpha.point(lambda a:255 if a>=100 else 0);box=mask.getbbox()
            if not box:raise ValueError((key,n,'empty'))
            # Remove nearly transparent export noise, retain all authored colour/edge pixels.
            img.putalpha(alpha.point(lambda a:0 if a<8 else a))
            foot_y=box[3]
            # Register using the midpoint of the planted-foot band, not the sword/alpha width.
            foot_band=mask.crop((0,max(0,foot_y-12),img.width,foot_y)).getbbox()
            root_x=(foot_band[0]+foot_band[2])/2 if foot_band else img.width/2
            resized=img.resize((round(img.width*scale),round(img.height*scale)),Image.Resampling.NEAREST)
            canvas=Image.new('RGBA',target);offset=(round(target[0]/2-root_x*scale),round(baseline-foot_y*scale))
            canvas.alpha_composite(resized,offset)
            file=dest/f'{name}-{n+1:02}.png';canvas.save(file);frame_ids.append(image_meta(file,target))
            registrations.append({'cell':cell,'mirror_to_right':n in flips,'root_source':[root_x,foot_y],'scale':scale,'offset':offset})
        path=RES/namespace/'animations'/(name+'.anim');clip_uuid=clip(path,name,frame_ids)
        manifest['clips'][namespace+'/'+name]={'path':str(path.relative_to(ROOT)).replace('\\','/'),'uuid':clip_uuid,'canvas':target,'anchor':[target[0]/2,baseline],'frames':8,'registration':registrations}

# Preserve the existing rig identity while replacing the retired counter slot.
rig=RES/'pixel8/prefabs/PixelHeroRig.prefab'
text=rig.read_text(encoding='utf-8');old=json.loads((RES/'pixel8/animations/counter.anim.meta').read_text(encoding='utf-8'))['uuid']
text=text.replace(old,manifest['clips']['pixel8/finisher']['uuid']);rig.write_text(text,encoding='utf-8')
write(HERE/'manifest.json',manifest)
print('Packaged',len(manifest['clips']),'clips /',len(manifest['clips'])*8,'drawn frames')
