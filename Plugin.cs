using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PP2ProductionStats
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        
        private void Awake()
        {
            Log = Logger;
            
            Logger.LogInfo($"Loaded plugin {PluginInfo.PLUGIN_GUID} version {PluginInfo.PLUGIN_VERSION}. Paragon Pioneers 2 version {Application.version}.");
            
            Harmony.CreateAndPatchAll(typeof(Patcher));
        }
    }
}
