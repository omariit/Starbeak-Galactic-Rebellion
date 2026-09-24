using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Runtime font provider that cannot kill the app.
    ///
    /// Resources.GetBuiltinResource&lt;Font&gt;("LegacyRuntime.ttf") is only guaranteed
    /// to exist in the EDITOR. In an IL2CPP release build the built-in legacy font is
    /// stripped unless something references it at build time, and calling it from a
    /// [RuntimeInitializeOnLoadMethod]/AfterSceneLoad native callback throws an
    /// exception straight into native code -> the process is killed with no logcat and
    /// no dialog. This helper degrades to "no font" (Text still renders, just invisible)
    /// instead of dying.
    /// </summary>
    public static class UiFont
    {
        private static Font cached;
        private static bool resolved;

        public static Font Get()
        {
            if (resolved) return cached;
            resolved = true;

            try
            {
                // A real font asset shipped under Resources always wins if present.
                Font asset = Resources.Load<Font>("StarbeakUI");
                if (asset != null)
                {
                    cached = asset;
                    return cached;
                }
            }
            catch (System.Exception e)
            {
                CrashLog.Error("UiFont.ResourcesLoad", e);
            }

            string[] candidates = { "LegacyRuntime.ttf", "Arial.ttf" };
            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    Font f = Resources.GetBuiltinResource<Font>(candidates[i]);
                    if (f != null)
                    {
                        cached = f;
                        return cached;
                    }
                }
                catch (System.Exception e)
                {
                    CrashLog.Error($"UiFont.GetBuiltin({candidates[i]})", e);
                }
            }

            CrashLog.Warn("UiFont: no usable font; UI text will be invisible but the game keeps running.");
            return null;
        }
    }
}
