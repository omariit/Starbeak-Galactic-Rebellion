using System.Collections.Generic;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Depth backdrop: slowly scrolling parallax asteroids in front of the nebula,
    /// built from the pre-baked 3D-shaded belt sprites. Self-bootstraps one
    /// DontDestroyOnLoad instance; no builder wiring required. Sits behind all
    /// gameplay entities (Background sorting layer, negative order).
    /// </summary>
    public class BeltField : MonoBehaviour
    {
        public static BeltField Instance { get; private set; }

        [SerializeField] private int rockCount = 16;

        private class Rock
        {
            public SpriteRenderer renderer;
            public float x;
            public float y;
            public float parallax;
            public float spin;
            public float spinSpeed;
            public float halfSize;
        }

        private readonly List<Rock> rocks = new List<Rock>(24);
        private Sprite[] sprites;
        private Camera cam;
        private float halfHeight = 1660f;
        private float halfWidth = 960f;
        private float wrapLength;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            CrashLog.Info("BeltField.Bootstrap:enter");
            if (CrashLog.HasArgument("-starbeakNoBelt")) return;
            if (Instance != null) return;
            GameObject go = new GameObject("BeltField");
            DontDestroyOnLoad(go);
            go.AddComponent<BeltField>();
            CrashLog.Info("BeltField.Bootstrap:leave");
        }

        private void Awake()
        {
            CrashLog.Info("BeltField.Awake:enter");
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            CrashLog.Info("BeltField.Awake:leave");
        }

        private void Start()
        {
            CrashLog.Info("BeltField.Start:enter");
            cam = Camera.main;
            if (cam != null)
            {
                halfHeight = cam.orthographicSize + 700f;
                halfWidth = cam.orthographicSize * cam.aspect * 1.15f + 220f;
            }
            wrapLength = halfHeight * 2f;

            sprites = Resources.LoadAll<Sprite>("Sprites");
            if (sprites == null || sprites.Length == 0)
            {
                // No belt art (editor-only or missing import): render nothing.
                return;
            }

            for (int i = 0; i < rockCount && i < 24; i++)
            {
                Sprite sprite = sprites[i % sprites.Length];
                GameObject go = new GameObject($"Belt_{i:00}");
                go.transform.SetParent(transform, false);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingLayerName = "Background";
                sr.sortingOrder = -12 - (i & 1);

                bool moon = sprites.Length > 0 && sprite.name.Contains("moon");
                float baseScale = moon ? Random.Range(0.75f, 1.15f) : Random.Range(0.4f, 0.85f);
                go.transform.localScale = Vector3.one * baseScale;
                sr.color = Color.Lerp(new Color(0.78f, 0.82f, 0.95f), Color.white, Random.value * 0.25f);

                Rock rock = new Rock
                {
                    renderer = sr,
                    parallax = moon ? 0.5f : Random.Range(0.22f, 0.65f),
                    spinSpeed = Random.Range(-0.16f, 0.16f),
                    halfSize = sprite.bounds.extents.y * baseScale
                };
                rock.x = Random.Range(-halfWidth, halfWidth);
                rock.y = Random.Range(-halfHeight, halfHeight);
                rocks.Add(rock);
            }
            CrashLog.Info($"BeltField.Start:leave:{rocks.Count}");
        }

        private void LateUpdate()
        {
            if (rocks.Count == 0) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            float dt = Time.deltaTime;
            float camY = cam.transform.position.y;
            float camX = cam.transform.position.x;
            if (float.IsNaN(camY) || float.IsInfinity(camY)) return;
            if (wrapLength < 1f) wrapLength = Mathf.Max(1f, halfHeight * 2f);

            for (int i = 0; i < rocks.Count; i++)
            {
                Rock r = rocks[i];
                if (r.renderer == null) continue;

                r.y -= (8f + 30f * r.parallax) * dt;   // slow drift down
                r.spin += r.spinSpeed * dt;

                // Wrap relative to the camera viewport. Bounded arithmetic instead of a
                // while-loop: a zero-length wrap would hang the main thread and Android
                // would kill the app as an ANR.
                if (r.y < camY - halfHeight || r.y > camY + halfHeight)
                {
                    float offset = Mathf.Repeat(r.y - (camY - halfHeight), wrapLength);
                    r.y = camY - halfHeight + offset;
                }

                r.renderer.transform.SetPositionAndRotation(
                    new Vector3(r.x + camX * (1f - r.parallax), r.y, 420f - r.parallax * 180f),
                    Quaternion.Euler(0f, 0f, r.spin * Mathf.Rad2Deg));
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
