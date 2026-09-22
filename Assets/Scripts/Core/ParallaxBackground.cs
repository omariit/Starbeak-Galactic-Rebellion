using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Endlessly scrolling starfield. Two background tiles chase each other down
    /// the portrait playfield and swap when fully off-screen, so the nebula never
    /// seams and repeats seamlessly (the texture crossfades its own top/bottom).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxBackground : MonoBehaviour
    {
        [Header("Drift")]
        [SerializeField] private float scrollSpeed = 46f;
        [SerializeField] private float parallaxFactor = 0.35f;

        private SpriteRenderer layerA;
        private SpriteRenderer layerB;
        private float tileHeight;
        private Camera cam;

        private void Awake()
        {
            cam = Camera.main;
            SpriteRenderer own = GetComponent<SpriteRenderer>();

            // The sprite on this object is the template; clone it for the second tile.
            layerA = own;
            GameObject bGo = new GameObject("BackgroundTileB");
            bGo.transform.SetParent(transform, false);
            layerB = bGo.AddComponent<SpriteRenderer>();
            layerB.sprite = own.sprite;
            layerB.sharedMaterial = own.sharedMaterial;
            layerB.sortingLayerName = own.sortingLayerName;
            layerB.sortingOrder = own.sortingOrder;
            layerB.drawMode = own.drawMode;

            if (own.sprite != null)
            {
                tileHeight = own.sprite.bounds.size.y;
            }
            else
            {
                tileHeight = 1920f; // fallback for the 1080x1920 reference playfield
            }

            PositionTiles(Vector3.zero);
        }

        private void PositionTiles(Vector3 basePosition)
        {
            layerA.transform.localPosition = basePosition;
            layerB.transform.localPosition = basePosition + Vector3.up * tileHeight;
        }

        private void LateUpdate()
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
            }

            float delta = scrollSpeed * parallaxFactor * Time.deltaTime;
            Vector3 pos = transform.position;
            pos.y -= delta;
            transform.position = pos;

            // When tile A has scrolled a full tile below the origin, wrap both tiles up.
            float relativeY = transform.localPosition.y;
            if (relativeY <= -tileHeight)
            {
                Vector3 wrapped = transform.localPosition;
                wrapped.y += tileHeight;
                transform.localPosition = wrapped;
            }
        }
    }
}
