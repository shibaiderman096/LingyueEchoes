using System;
using UnityEngine;

namespace LingyueEchoes
{
    // Global presentation preference, independent of the selected save slot.
    public static class SceneParticleSettings
    {
        public const string Key="lingyue.sceneParticles";
        public static event Action<bool> Changed;
        public static bool Enabled=>PlayerPrefs.GetInt(Key,1)!=0;
        public static void Toggle(){
            bool enabled=!Enabled;
            PlayerPrefs.SetInt(Key,enabled?1:0);PlayerPrefs.Save();
            Changed?.Invoke(enabled);
        }
    }
}
