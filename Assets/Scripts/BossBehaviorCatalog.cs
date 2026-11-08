using System;
using UnityEngine;
namespace LingyueEchoes
{
    public static class BossBehaviorCatalog
    {
        public static OpponentPattern Pattern(string id) => OpponentPattern.FromBoss(Load(id));
        public static BossDefinition Guardian => Load("guardian");
        public static BossDefinition WrathGeneral => Load("wrath-general");
        public static BossDefinition Load(string id)
        {
            if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("Boss resource id is required",nameof(id));
            foreach(char c in id)if(!((c>='a'&&c<='z')||(c>='0'&&c<='9')||c=='-'))throw new ArgumentException("Boss resource id must contain only lowercase letters, digits and hyphens",nameof(id));
            var asset=Resources.Load<TextAsset>("Data/Bosses/"+id);
            if(asset==null)throw new InvalidOperationException("Missing boss configuration: Resources/Data/Bosses/"+id+".json");
            try { var definition=JsonUtility.FromJson<BossDefinition>(asset.text); if(definition==null||definition.id!=id)throw new InvalidOperationException("configuration id does not match resource id"); definition.Validate(); return definition.Copy(); }
            catch(Exception error) { throw new InvalidOperationException("Failed to load boss configuration '"+id+"': "+error.Message,error); }
        }
    }
}

