using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// On-screen mirror of <see cref="CrashLog"/>. A user with no USB cable and no
    /// logcat still has to be able to say WHY the app died, so anything fatal is drawn
    /// over the game view: one screenshot is a complete diagnosis.
    /// Top-level type on purpose - Unity cannot instantiate nested MonoBehaviours.
    /// </summary>
    public class CrashOverlay : MonoBehaviour
    {
        private GUIStyle style;
        private Color prevColor;

        private void OnGUI()
        {
            string text = CrashLog.OverlayText;
            if (string.IsNullOrEmpty(text)) return;

            float age = CrashLog.OverlayAge;
            if (age > 60f)
            {
                CrashLog.ClearOverlay();
                return;
            }

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(16, Mathf.RoundToInt(Screen.dpi > 0f ? Screen.dpi * 0.032f : 22f)),
                    wordWrap = true,
                    richText = false
                };
                style.normal.textColor = new Color(1f, 0.45f, 0.45f);
            }

            float alpha = age > 50f ? Mathf.Clamp01((60f - age) / 10f) : 1f;
            prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Box(new Rect(10f, 10f, Screen.width - 20f, 260f), GUIContent.none);
            GUI.Label(new Rect(24f, 20f, Screen.width - 60f, 240f), text, style);
            GUI.color = prevColor;
        }
    }
}
