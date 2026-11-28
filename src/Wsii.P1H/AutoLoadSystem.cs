using System;
using System.IO;
using System.Linq;
using Colossal.IO.AssetDatabase;
using Game;
using Game.SceneFlow;

namespace Wsii.P1H
{
    /// <summary>
    /// Optional in-game automation: when an <c>autoload.flag</c> file sits beside the mod, load the
    /// newest save through the game's own scene flow instead of a menu click. This exists because
    /// the Unity window exposes no UI Automation buttons, so the menu cannot be driven from outside.
    /// It is gated by the flag so normal play never auto-loads.
    /// </summary>
    public sealed class AutoLoadSystem : PhaseProbeSystem
    {
        private bool _attempted;

        protected override string Phase
        {
            get { return "AutoLoad"; }
        }

        protected override int Heartbeat
        {
            get { return 600; }
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (_attempted)
            {
                return;
            }

            string dir = Mod.ResolveModDirectory();
            if (!File.Exists(Path.Combine(dir, "autoload.flag")))
            {
                return;
            }

            _attempted = true;

            try
            {
                var manager = GameManager.instance;
                if (manager == null)
                {
                    Mod.Log("autoload: GameManager.instance is null");
                    return;
                }

                string save = NewestSave(Path.Combine(Mod.DataDirectory(), "Saves"));
                if (save == null)
                {
                    Mod.Log("autoload: no save found");
                    return;
                }

                Mod.Log("autoload: newest save " + Path.GetFileName(save));

                IAssetDatabase user = AssetDatabase.user;
                if (user == null)
                {
                    Mod.Log("autoload: AssetDatabase.user is null");
                    return;
                }

                IAssetData asset = Lookup(user, save);
                if (asset == null)
                {
                    Mod.Log("autoload: asset lookup failed for " + Path.GetFileName(save));
                    return;
                }

                Mod.Log("autoload: asset " + asset.GetType().Name + " name=" + asset.name);
                InvokeLoader(manager, asset);
            }
            catch (Exception ex)
            {
                Mod.Log("autoload-ERROR " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void InvokeLoader(GameManager manager, IAssetData asset)
        {
            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            var byAsset = typeof(GameManager).GetMethod("AutoLoad", Flags, null, new[] { typeof(IAssetData) }, null);
            if (byAsset != null)
            {
                object result = byAsset.Invoke(manager, new object[] { asset });
                Mod.Log("autoload: AutoLoad(IAssetData) invoked -> " + (result == null ? "null" : result.GetType().Name));
                return;
            }

            Mod.Log("autoload: no AutoLoad(IAssetData) overload found by reflection");
        }

        private static IAssetData Lookup(IAssetDatabase user, string savePath)
        {
            foreach (string key in new[] { Path.GetFileName(savePath), Path.GetFileNameWithoutExtension(savePath), savePath })
            {
                try
                {
                    var asset = user.GetAsset(key);
                    if (asset != null)
                    {
                        return asset;
                    }
                }
                catch (Exception ex)
                {
                    Mod.Log("autoload: GetAsset(" + key + ") threw " + ex.GetType().Name);
                }
            }

            return null;
        }

        private static string NewestSave(string savesRoot)
        {
            if (!Directory.Exists(savesRoot))
            {
                return null;
            }

            return Directory.GetFiles(savesRoot, "*.cok", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
    }
}
