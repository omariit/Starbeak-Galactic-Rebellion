using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Persistent overlay banner that survives scene transitions — shows
    /// "SECTOR CLEARED!" / "RIDER DOWN!" after each node resolves.
    /// Self-bootstraps; attached to its own DontDestroyOnLoad canvas.
    /// </summary>
    public class BannerHost : MonoBehaviour
    {
        public static BannerHost Instance { get; private set; }

        private Text banner;
        private Canvas canvas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Runs from a NATIVE callback on every scene load. An exception here is
            // fatal on IL2CPP releases (no dialog, no logcat) - so the whole body is
            // guarded and the banner is a nicety, never a boot requirement.
            try
            {
                if (Instance != null) return;
                GameObject go = new GameObject("BannerHost");
                DontDestroyOnLoad(go);
                go.AddComponent<BannerHost>();
            }
            catch (System.Exception e)
            {
                CrashLog.Error("BannerHost.Bootstrap", e);
            }
        }

        private void Awake()
        {
            try
            {
                if (Instance != null && Instance != this) { Destroy(gameObject); return; }
                Instance = this;
                BuildCanvas();
            }
            catch (System.Exception e)
            {
                // A missing banner must never cost us the run.
                CrashLog.Error("BannerHost.Awake", e);
                if (Instance == this) Instance = null;
                enabled = false;
            }
        }

        private void OnEnable() { GameManager.NodeResolved += HandleResolved; }
        private void OnDisable() { GameManager.NodeResolved -= HandleResolved; }

        private void BuildCanvas()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingLayerName = "UI";
            canvas.sortingOrder = 100;

            GraphicRaycaster raycaster = gameObject.AddComponent<GraphicRaycaster>();
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

            GameObject go = new GameObject("BannerText");
            go.transform.SetParent(transform, false);
            banner = go.AddComponent<Text>();
            banner.font = UiFont.Get();
            banner.fontSize = 120;
            banner.fontStyle = FontStyle.Bold;
            banner.alignment = TextAnchor.MiddleCenter;
            banner.raycastTarget = false;
            banner.horizontalOverflow = HorizontalWrapMode.Overflow;
            banner.verticalOverflow = VerticalWrapMode.Overflow;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(4f, -4f);

            RectTransform rect = banner.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 160f);
            rect.sizeDelta = new Vector2(1200f, 240f);

            Color c = banner.color; c.a = 0f; banner.color = c;
        }

        private void HandleResolved(bool victory)
        {
            StopAllCoroutines();
            StartCoroutine(Show(victory ? "SECTOR CLEARED!" : "RIDER DOWN!",
                               victory ? new Color(0.6f, 1f, 0.72f) : new Color(1f, 0.45f, 0.4f)));
        }

        private IEnumerator Show(string title, Color color)
        {
            canvas.enabled = true;
            banner.text = title;
            Color c = color;

            for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
            {
                c.a = Mathf.Clamp01(t / 0.22f);
                banner.color = c;
                banner.rectTransform.localScale = Vector3.one * (1.3f - 0.3f * (t / 0.22f));
                yield return null;
            }
            c.a = 1f; banner.color = c;
            banner.rectTransform.localScale = Vector3.one;

            const float hold = 1.25f;
            for (float t = Time.unscaledDeltaTime; t < hold; t += Time.unscaledDeltaTime)
                yield return null;

            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                c.a = Mathf.Clamp01(1f - t / 0.4f);
                banner.color = c;
                yield return null;
            }
            c.a = 0f; banner.color = c;
            canvas.enabled = false;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}