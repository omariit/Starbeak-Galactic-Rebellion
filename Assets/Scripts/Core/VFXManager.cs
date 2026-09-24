using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Punchy combat feedback: pooled explosion particles, shockwave rings, hit
    /// sparks, engine trails, and a decaying screen shake driven by per-event
    /// trauma. Every burst is built procedurally at runtime (no asset coupling).
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("Shared particle texture")]
        [SerializeField] public Sprite glowSprite;

        [Header("Screen shake")]
        [SerializeField] private float maxShakeAngle = 1.4f;
        [SerializeField] private float maxShakeOffset = 34f;
        [SerializeField] private float traumaDecayPerSecond = 1.6f;

        [Header("Pool")]
        [SerializeField] private int particlePoolSize = 24;
        [SerializeField] private int ringPoolSize = 16;

        private readonly List<ParticleSystem> particlePool = new List<ParticleSystem>();
        private readonly List<SpriteRenderer> ringPool = new List<SpriteRenderer>();
        private int particleCursor;
        private int ringCursor;

        private float trauma;
        private Camera shakeCamera;
        private Vector3 cameraRestPosition;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildPools();
        }

        private void Start()
        {
            CacheCamera();
        }

        private void BuildPools()
        {
            Material additive = NewAdditiveSpriteMaterial();

            for (int i = 0; i < particlePoolSize; i++)
            {
                GameObject go = new GameObject($"VfxExplosion_{i:00}");
                go.transform.SetParent(transform, false);
                go.SetActive(false);

                ParticleSystem ps = go.AddComponent<ParticleSystem>();
                ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
                ConfigureExplosionRenderer(renderer, additive);
                particlePool.Add(ps);
            }

            for (int i = 0; i < ringPoolSize; i++)
            {
                GameObject go = new GameObject($"VfxRing_{i:00}");
                go.transform.SetParent(transform, false);
                go.SetActive(false);

                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = "Effects";
                renderer.sortingOrder = 30;
                renderer.sharedMaterial = additive;
                ringPool.Add(renderer);
            }
        }

        private Material NewAdditiveSpriteMaterial()
        {
            // Sprites/Default with additive blending gives bright, glowing particles
            // that never darken the background (essential for explosions).
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/StarbeakTransparentGlow");
            if (shader == null)
            {
                Debug.LogWarning("[VFXManager] No additive sprite shader found; VFX use default material.");
                return null;
            }

            Material mat = new Material(shader);
            if (shader.name == "Sprites/Default")
            {
                mat.SetFloat("_Mode", 1f); // Legacy additive mode
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHABLEND_ON");
            }
            return mat;
        }

        private void ConfigureExplosionRenderer(ParticleSystemRenderer renderer, Material additive)
        {
            renderer.sortingLayerName = "Effects";
            renderer.sortingOrder = 25;
            renderer.sharedMaterial = additive;
            renderer.alignment = ParticleSystemRenderSpace.Facing;
        }

        /// <summary>Caches the gameplay camera; called on start and on scene load.</summary>
        public void CacheCamera()
        {
            shakeCamera = Camera.main;
            if (shakeCamera != null) cameraRestPosition = shakeCamera.transform.localPosition;
        }

        // ------------------------------------------------------------------ explosions
        /// <summary>Detonates an explosion at <paramref name="position"/>. Use <see cref="ExplosionScale"/>.</summary>
        public void SpawnExplosion(Vector3 position, ExplosionScale scale)
        {
            bool big = scale >= ExplosionScale.Large;
            bool huge = scale >= ExplosionScale.Boss;

            ParticleSystem ps = particlePool[particleCursor];
            particleCursor = (particleCursor + 1) % particlePool.Count;

            ConfigureExplosion(ps, position, scale);
            ps.gameObject.SetActive(true);
            ps.Play();

            // Shockwave ring expands and fades.
            SpawnShockwave(position, huge ? 1.6f : (big ? 1.2f : 1f));

            // Camera trauma: small pops nudge, boss deaths jolt.
            AddTrauma(huge ? 0.9f : (big ? 0.45f : 0.22f));

            // Sound matches the scale.
            AudioManager audio = AudioManager.Instance;
            if (audio != null)
            {
                string clip = huge ? "sfx_boss_explosion" : "sfx_explosion";
                audio.PlaySfxJittered(clip, huge ? 1f : 0.8f, huge ? 0.08f : 0.12f);
            }
        }

        private void ConfigureExplosion(ParticleSystem ps, Vector3 position, ExplosionScale scale)
        {
            Transform tf = ps.transform;
            tf.position = position;
            tf.rotation = Quaternion.identity;
            tf.localScale = Vector3.one;

            float scaleF = scale == ExplosionScale.Small ? 1f
                         : scale == ExplosionScale.Medium ? 1.7f
                         : scale == ExplosionScale.Large ? 2.6f : 4.2f;

            // Bind the soft glow sprite through the texture-sheet module (the only
            // supported way to put a Sprite on a particle system).
            if (glowSprite != null)
            {
                var sheet = ps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Sprites;
                sheet.SetSprite(0, glowSprite);
            }

            var main = ps.main;
            main.duration = 1.4f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(90f * scaleF, 420f * scaleF);
            main.startSize = new ParticleSystem.MinMaxCurve(26f * scaleF, 96f * scaleF);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Clamp((int)(60 * scaleF), 24, 320);
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            // Fire palette: white-hot core -> yellow -> orange -> red -> smoke.
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 1f, 1f), 0.00f),
                    new GradientColorKey(new Color(1f, 0.95f, 0.45f), 0.15f),
                    new GradientColorKey(new Color(1f, 0.62f, 0.12f), 0.45f),
                    new GradientColorKey(new Color(0.82f, 0.16f, 0.10f), 0.75f),
                    new GradientColorKey(new Color(0.25f, 0.22f, 0.30f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.00f, 0.00f),
                    new GradientAlphaKey(0.95f, 0.20f),
                    new GradientAlphaKey(0.60f, 0.55f),
                    new GradientAlphaKey(0.15f, 0.85f),
                    new GradientAlphaKey(0.00f, 1.00f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            // Burst everything at once.
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            var burst = new ParticleSystem.Burst(0f, (short)Mathf.Clamp((int)(34 * scaleF), 12, 240));
            emission.burstCount = 1;
            emission.SetBurst(0, burst);

            // Radial blast.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 12f * scaleF;
            shape.angle = 0f;
            shape.radiusMode = ParticleSystemShapeMultiModeValue.Random;
            shape.arc = 360f;

            // Particles shrink as they die.
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.25f, 3.2f, 3.2f),
                new Keyframe(0.12f, 1f, 0f, 0f),
                new Keyframe(1f, 0.15f, -1.4f, -1.4f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Drag slows shrapnel for a meaty feel.
            var limitVelocity = ps.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.limit = 60f * scaleF;
            limitVelocity.dampen = 0.16f;

            // Additive fade-out at the tail.
            var trails = ps.trails;
            trails.enabled = scale >= ExplosionScale.Medium;
            trails.ratio = 0.45f;
            trails.colorOverLifetime = new Color(1f, 0.75f, 0.3f, 0.45f);
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 0.1f)));
            trails.worldSpace = true;

            var collision = ps.collision;
            collision.enabled = false;
        }

        private void SpawnShockwave(Vector3 position, float scale)
        {
            SpriteRenderer renderer = ringPool[ringCursor];
            ringCursor = (ringCursor + 1) % ringPool.Count;

            Transform tf = renderer.transform;
            tf.position = position;
            tf.rotation = Quaternion.identity;
            tf.localScale = Vector3.one * (0.25f * scale);
            if (renderer.sprite == null) renderer.sprite = ringSprite != null ? ringSprite : glowSprite;

            renderer.gameObject.SetActive(true);
            renderer.color = new Color(1f, 0.95f, 0.8f, 0.85f);
            StartCoroutine(AnimateShockwave(renderer, scale));
        }

        private IEnumerator AnimateShockwave(SpriteRenderer renderer, float scale)
        {
            if (renderer == null) yield break;

            float duration = 0.42f;
            float elapsed = 0f;
            Transform tf = renderer.transform;

            while (elapsed < duration && renderer != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                tf.localScale = Vector3.one * (0.25f * scale + 3.4f * scale * eased);
                renderer.color = new Color(1f, 0.95f - 0.25f * eased, 0.8f - 0.4f * eased,
                                           0.85f * (1f - eased));
                yield return null;
            }

            if (renderer != null) renderer.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ hit sparks
        /// <summary>Small directional spark burst used when a projectile connects.</summary>
        public void SpawnHitSpark(Vector3 position, Vector2 normal, float intensity = 1f)
        {
            ParticleSystem ps = particlePool[particleCursor];
            particleCursor = (particleCursor + 1) % particlePool.Count;

            ConfigureHitSpark(ps, position, normal, intensity);
            ps.gameObject.SetActive(true);
            ps.Play();

            AudioManager.Instance?.PlaySfxJittered("sfx_hit", 0.5f * intensity, 0.18f);
            AddTrauma(0.05f * intensity);
        }

        private void ConfigureHitSpark(ParticleSystem ps, Vector3 position, Vector2 normal, float intensity)
        {
            Transform tf = ps.transform;
            tf.position = position;
            tf.rotation = Quaternion.identity;
            tf.localScale = Vector3.one;

            float s = Mathf.Max(0.4f, intensity);

            var main = ps.main;
            main.duration = 0.45f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(60f * s, 260f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(8f * s, 26f * s);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Clamp((int)(14 * s), 6, 40);
            main.playOnAwake = false;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.35f), 0.3f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.5f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.burstCount = 1;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, (short)Mathf.Clamp((int)(12 * s), 4, 30)));

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 4f * s;
            shape.arc = 60f;
            // Cone-free 2D: aim along the surface normal.
            float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
            tf.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 0.05f)));

            var trails = ps.trails;
            trails.enabled = false;
        }

        // ------------------------------------------------------------------ muzzle flash
        /// <summary>Quick directional flash at the muzzle when the player fires.</summary>
        public void SpawnMuzzleFlash(Vector3 position, Vector2 direction)
        {
            ParticleSystem ps = particlePool[particleCursor];
            particleCursor = (particleCursor + 1) % particlePool.Count;

            Transform tf = ps.transform;
            tf.position = position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            tf.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
            tf.localScale = Vector3.one;

            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(40f, 160f);
            main.startSize = new ParticleSystem.MinMaxCurve(14f, 34f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 10;
            main.playOnAwake = false;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.75f, 0.95f, 1f), 0f),
                    new GradientColorKey(new Color(0.35f, 0.7f, 1f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.burstCount = 1;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, (short)8));

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 6f;
            shape.arc = 35f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 0.05f)));

            var trails = ps.trails;
            trails.enabled = false;

            ps.gameObject.SetActive(true);
            ps.Play();
        }

        // ------------------------------------------------------------------ shake
        /// <summary>Adds trauma (0..1). Squared falloff keeps small hits subtle and big hits violent.</summary>
        public void AddTrauma(float amount)
        {
            if (amount <= 0f) return;
            trauma = Mathf.Clamp01(trauma + amount);
            if (!enabled) enabled = true;
        }

        private void LateUpdate()
        {
            if (shakeCamera == null)
            {
                CacheCamera();
                if (shakeCamera == null) return;
            }

            if (trauma <= 0.001f)
            {
                if (shakeCamera.transform.localPosition != cameraRestPosition)
                {
                    shakeCamera.transform.localPosition = cameraRestPosition;
                }
                return;
            }

            trauma = Mathf.Max(0f, trauma - traumaDecayPerSecond * Time.deltaTime);
            float shake = trauma * trauma;

            float seed = Time.unscaledTime * 24f;
            float offsetX = (Mathf.PerlinNoise(seed, 0f) * 2f - 1f) * maxShakeOffset * shake;
            float offsetY = (Mathf.PerlinNoise(0f, seed) * 2f - 1f) * maxShakeOffset * shake;
            float angle = (Mathf.PerlinNoise(seed * 0.7f, seed * 0.7f) * 2f - 1f) * maxShakeAngle * shake;

            shakeCamera.transform.localPosition = cameraRestPosition + new Vector3(offsetX, offsetY, 0f);
            shakeCamera.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>Clears residual trauma (called on scene transitions).</summary>
        public void ClearTrauma()
        {
            trauma = 0f;
            if (shakeCamera != null)
            {
                shakeCamera.transform.localPosition = cameraRestPosition;
                shakeCamera.transform.localRotation = Quaternion.identity;
            }
        }

        [SerializeField] public Sprite ringSprite;

        // ------------------------------------------------------------------ hit flash
        private struct Flasher
        {
            public SpriteRenderer renderer;
            public Color baseColor;
            public Color tint;
            public float timer;
            public float duration;
        }

        private readonly List<Flasher> flashers = new List<Flasher>(96);

        /// <summary>White (or tinted) pop on a sprite when it takes a hit; re-armable.</summary>
        public void AddHitFlash(GameObject target, Color tint, float duration = 0.12f)
        {
            if (target == null) return;

            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            if (renderer == null) return;

            for (int i = 0; i < flashers.Count; i++)
            {
                if (flashers[i].renderer == renderer)
                {
                    Flasher f = flashers[i];
                    f.timer = duration;
                    f.duration = duration;
                    flashers[i] = f;
                    return;
                }
            }

            flashers.Add(new Flasher
            {
                renderer = renderer,
                baseColor = renderer.color,
                tint = tint,
                timer = duration,
                duration = duration
            });
        }

        private void Update()
        {
            if (flashers.Count == 0) return;

            float dt = Time.deltaTime;
            for (int i = flashers.Count - 1; i >= 0; i--)
            {
                Flasher f = flashers[i];
                if (f.renderer == null)
                {
                    flashers.RemoveAt(i);
                    continue;
                }

                f.timer -= dt;
                if (f.timer <= 0f)
                {
                    f.renderer.color = f.baseColor;
                    flashers.RemoveAt(i);
                    continue;
                }

                float t = Mathf.Clamp01(f.timer / f.duration);
                f.renderer.color = Color.Lerp(f.baseColor, f.tint, t);
                // Pool resets re-arm flashes automatically (ResetState clears color).
            }
        }

        // ------------------------------------------------------------------ engine trails
        private readonly HashSet<int> trailedShips = new HashSet<int>();
        private Material trailMaterial;

        /// <summary>Additive exhaust trail behind a ship; safe to call repeatedly.</summary>
        public void AttachEngineTrail(GameObject ship)
        {
            if (ship == null) return;
            int id = ship.GetInstanceID();
            if (!trailedShips.Add(id)) return;

            if (trailMaterial == null)
            {
                Shader trailShader = Shader.Find("Hidden/StarbeakTrail");
                if (trailShader == null) trailShader = Shader.Find("Sprites/Default");
                if (trailShader == null) return;

                trailMaterial = new Material(trailShader);
                trailMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            Transform emitter = ship.transform.Find("EngineTrail");
            if (emitter == null)
            {
                GameObject trailGo = new GameObject("EngineTrail");
                trailGo.transform.SetParent(ship.transform, false);
                trailGo.transform.localPosition = new Vector3(0f, -120f, -1f);
                emitter = trailGo.transform;
            }

            TrailRenderer trail = emitter.GetComponent<TrailRenderer>();
            if (trail == null) trail = emitter.gameObject.AddComponent<TrailRenderer>();

            trail.sharedMaterial = trailMaterial;
            trail.time = 0.28f;
            trail.startWidth = 46f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 8f;
            trail.numCapVertices = 2;
            trail.sortingLayerName = "Effects";
            trail.sortingOrder = 20;
            Color hot = new Color(0.55f, 0.9f, 1f, 0.85f);
            Color cold = new Color(0.2f, 0.5f, 1f, 0f);
            trail.colorGradient = new Gradient();
            trail.colorGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(hot, 0f),
                    new GradientColorKey(new Color(0.8f, 0.5f, 1f), 0.5f),
                    new GradientColorKey(cold, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.35f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
        }

        // ------------------------------------------------------------------ boss telegraph
        /// <summary>Expanding warning pulse before the boss fleet arrives.</summary>
        public void SpawnBossTelegraph(Vector3 position, float radius)
        {
            if (ringPool.Count == 0) return;

            SpriteRenderer renderer = ringPool[ringCursor];
            ringCursor = (ringCursor + 1) % ringPool.Count;

            Transform tf = renderer.transform;
            tf.position = position;
            tf.rotation = Quaternion.identity;
            tf.localScale = Vector3.one * 0.2f;
            if (renderer.sprite == null && ringSprite != null) renderer.sprite = ringSprite;

            renderer.gameObject.SetActive(true);
            renderer.color = new Color(1f, 0.35f, 0.2f, 0.9f);
            StartCoroutine(AnimateTelegraph(renderer, radius));
        }

        private IEnumerator AnimateTelegraph(SpriteRenderer renderer, float radius)
        {
            const float duration = 0.9f;
            float elapsed = 0f;
            while (elapsed < duration && renderer != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                renderer.transform.localScale = Vector3.one * (0.2f + radius * 0.006f * eased);
                renderer.color = new Color(1f, 0.35f, 0.2f, 0.9f * (1f - t));
                yield return null;
            }
            if (renderer != null) renderer.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    public enum ExplosionScale
    {
        Small,
        Medium,
        Large,
        Boss
    }
}
