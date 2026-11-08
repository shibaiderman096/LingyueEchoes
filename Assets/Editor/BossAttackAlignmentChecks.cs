using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LingyueEchoes;
using UnityEditor;
using UnityEngine;

public static class BossAttackAlignmentChecks
{
    static int contacts,samples;
    static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
    static void Need(bool valid,string message){if(!valid)throw new Exception("Boss attack alignment: "+message);}
    [MenuItem("Lingyue/Check Boss attack alignment")]
    public static void Run(){
        contacts=samples=0;
        try{
            Check(null);Check(WrathRoute.General);
            Need(contacts>=60&&samples>10000,"checks exercise both facings, active frames and repeated contacts");
            Debug.Log("BOSS_ATTACK_ALIGNMENT_PASSED: "+contacts+" contacts; "+samples+" independent source-alpha collision samples; both bosses/facings, double pulses, tint and expiry");
        }finally{foreach(var texture in textures.Values)UnityEngine.Object.DestroyImmediate(texture);textures.Clear();}
    }
    static void Check(OpponentPattern pattern){
        int moves=pattern==null?Rules.Boss.Length:pattern.opening.Length;
        for(int index=0;index<moves;index++)for(int face=-1;face<=1;face+=2){
            var c=new Combat{ai=false,god=true,boundsMin=-3000,boundsMax=3000};c.ResetBoss(0,1000,pattern);c.phase=pattern==null?2:3;c.player.x=face*2000;c.DebugBoss(index);
            var root=new GameObject("Boss attack alignment");var effects=new PixelEffects(root.transform);int pulses=0;var seen=new HashSet<int>();
            try{
                for(int tick=0;tick<200&&c.boss.move!=null;tick++){
                    c.Step();effects.Draw(c);
                    var hit=c.hits.FirstOrDefault(h=>h.owner==2);
                    var arcs=root.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.activeSelf&&r.sprite&&r.sprite.name.StartsWith("cut_fx",StringComparison.Ordinal)).ToArray();
                    if(arcs.Length==0){
                        Need(hit==null||!c.visuals.Any(v=>v.owner==2&&v.kind=="slash"&&v.frame==c.frame),"active melee contact must have a visible arc");
                        continue;
                    }
                    Need(hit!=null&&arcs.Length==1,"a visible arc must have one active melee contact");
                    Need(arcs[0].color.g==(hit.move.parry?.84f:.12f),"color follows the actual contact's parry flag");
                    if(seen.Add(hit.pulse))pulses++;
                    CheckPixels(arcs[0],hit);contacts++;
                }
                effects.Draw(c);
                Need(!root.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.gameObject.activeSelf&&r.sprite&&r.sprite.name.StartsWith("cut_fx",StringComparison.Ordinal)),"recovery has no damaging-looking lingering arc");
                var move=pattern==null?Rules.Boss[index]:pattern.opening[index];
                if(move.pulses!=null&&move.pulses.Length>0)Need(pulses==move.pulses.Length,"every authored pulse has its own matching arc");
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
    static void CheckPixels(SpriteRenderer renderer,Hitbox hit){
        string path=AssetDatabase.GetAssetPath(renderer.sprite);
        if(!textures.TryGetValue(path,out var texture)){texture=new Texture2D(2,2,TextureFormat.RGBA32,false);Need(texture.LoadImage(File.ReadAllBytes(path)),"source PNG loads independently of generated collision data");textures.Add(path,texture);}
        var pixels=texture.GetPixels32();var t=renderer.transform;
        // Pixel centres avoid ambiguous contacts along touching edges. Source PNG alpha
        // is independent evidence: no geometry helper or generated mask is used here.
        for(int y=2;y<texture.height;y+=11)for(int x=3;x<texture.width;x+=11){
            double px=t.localPosition.x*100+(x+.5-renderer.sprite.pivot.x)*t.localScale.x;
            double py=t.localPosition.y*100+95+(y+.5-renderer.sprite.pivot.y)*t.localScale.y;
            const double epsilon=.00001;
            bool touches=hit.Touches(new Box(px-epsilon,py-epsilon,epsilon*2,epsilon*2));
            bool visible=pixels[y*texture.width+x].a>0;
            Need(touches==visible,$"{renderer.sprite.name} source ({x},{y}), face {Math.Sign(t.localScale.x)}: visible={visible}, contact={touches}");samples++;
        }
        Need(!hit.Touches(new Box(hit.x+hit.w+.01,hit.y,1,hit.h)),"no damage beyond the visible far edge");
        Need(!hit.Touches(new Box(hit.x-1.01,hit.y,1,hit.h)),"no damage beyond the visible near edge");
    }
}
