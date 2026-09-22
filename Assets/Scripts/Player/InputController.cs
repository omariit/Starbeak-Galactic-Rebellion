using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Touchscreen input controller for portrait Android gameplay. Supports two schemes:
    /// direct absolute-finger-tracking drag (ship follows finger 1:1 with LERP smoothing)
    /// or a virtual joystick anchored at first touch. Reads the movement/drag zone and
    /// the fire zone (bottom half of the screen by default).
    /// </summary>
    public class InputController : MonoBehaviour
    {
        public static InputController Instance { get; private set; }

        [Header("Zones (0..1 of screen height, bottom-anchored)")]
        [Tooltip("Touch y below this fraction steers the ship.")]
        [SerializeField] private float movementZoneTop = 0.62f;
        [SerializeField] private float fireZoneTop = 0.28f;

        [Header("Smoothing")]
        [SerializeField] private float movementLerp = 14f;
        [SerializeField] private float joystickRadiusPixels = 160f;
        [SerializeField] private float joystickDeadZone = 0.12f;

        [Header("Editor emulation")]
        [SerializeField] private bool useKeyboardInEditor = true;

        /// <summary>Normalized movement intent (-1..1 per axis).</summary>
        public Vector2 Movement { get; private set; }
        public bool IsFiring { get; private set; }

        private Camera mainCamera;
        private int steeringPointerId = -1;
        private int firePointerId = -1;
        private Vector2 joystickOriginScreen;
        private Vector2 smoothedMovement;
        private bool joystickMode;

        // Screen dimensions in pixels, cached per frame to avoid repeated API calls.
        private int lastScreenWidth;
        private int lastScreenHeight;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            GameManager.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameManager.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            // Only process gameplay input while actually in combat.
            bool gameplay = state == GameState.Gameplay;
            enabled = gameplay;
            if (!gameplay)
            {
                Movement = Vector2.zero;
                smoothedMovement = Vector2.zero;
                IsFiring = false;
                steeringPointerId = -1;
                firePointerId = -1;
            }
        }

        private void Update()
        {
            CacheScreenDimensions();

            if (useKeyboardInEditor && Application.isEditor)
            {
                PollKeyboard();
            }
            else
            {
                PollTouches();
            }

            // Input interpolation (LERP) toward the raw intent for smooth motion.
            smoothedMovement = Vector2.Lerp(smoothedMovement, Movement,
                Mathf.Clamp01(movementLerp * Time.deltaTime));

            PlayerShip.Instance?.ApplyMovementInput(smoothedMovement, Time.deltaTime);
        }

        private void CacheScreenDimensions()
        {
            int width = Screen.width;
            int height = Screen.height;
            if (width == lastScreenWidth && height == lastScreenHeight) return;
            lastScreenWidth = width;
            lastScreenHeight = height;
            if (mainCamera == null) mainCamera = Camera.main;
        }

        // ---------------------------------------------------------------- Touch polling
        private void PollTouches()
        {
            IsFiring = false;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    HandleTouchBegan(touch);
                }
                else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    HandleTouchMoved(touch);
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    HandleTouchEnded(touch);
                }
            }

            if (steeringPointerId == -1) Movement = Vector2.zero;
        }

        private void HandleTouchBegan(Touch touch)
        {
            float normalizedY = (float)touch.position.y / lastScreenHeight;

            if (normalizedY < fireZoneTop)
            {
                firePointerId = touch.fingerId;
                IsFiring = true;
                return;
            }

            if (normalizedY < movementZoneTop && steeringPointerId == -1)
            {
                steeringPointerId = touch.fingerId;
                joystickOriginScreen = touch.position;
                joystickMode = SaveSystem.Instance?.Profile?.settings.absoluteDragInput == false;
            }
        }

        private void HandleTouchMoved(Touch touch)
        {
            if (touch.fingerId == firePointerId)
            {
                IsFiring = true;
                return;
            }
            if (touch.fingerId != steeringPointerId) return;

            Movement = joystickMode
                ? ReadVirtualJoystick(touch.position)
                : ReadAbsoluteDrag(touch.position);
        }

        private void HandleTouchEnded(Touch touch)
        {
            if (touch.fingerId == steeringPointerId)
            {
                steeringPointerId = -1;
                Movement = Vector2.zero;
            }
            if (touch.fingerId == firePointerId)
            {
                firePointerId = -1;
                IsFiring = false;
            }
        }

        /// <summary>Direct absolute-finger-tracking: ship target sits under the finger.</summary>
        private Vector2 ReadAbsoluteDrag(Vector2 touchPosition)
        {
            if (lastScreenWidth <= 0 || lastScreenHeight <= 0) return Vector2.zero;

            // Center-relative normalized intent (-1..1).
            float x = (touchPosition.x / lastScreenWidth) * 2f - 1f;
            float y = (touchPosition.y / lastScreenHeight) * 2f - 1f;
            Vector2 intent = new Vector2(x, y);
            return Vector2.ClampMagnitude(intent, 1f);
        }

        /// <summary>Anchored virtual joystick: intent from displacement off the origin.</summary>
        private Vector2 ReadVirtualJoystick(Vector2 touchPosition)
        {
            Vector2 delta = touchPosition - joystickOriginScreen;
            float magnitude = delta.magnitude / joystickRadiusPixels;
            if (magnitude < joystickDeadZone) return Vector2.zero;

            return Vector2.ClampMagnitude(delta.normalized * Mathf.Min(magnitude, 1f), 1f);
        }

        // ---------------------------------------------------------------- Editor emulation
        private void PollKeyboard()
        {
            IsFiring = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);

            float x = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) x += 1f;

            float y = 0f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) y -= 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) y += 1f;

            Movement = new Vector2(x, y);
        }

        /// <summary>Flips between absolute drag and virtual joystick at runtime.</summary>
        public void SetInputScheme(bool absoluteDrag)
        {
            GameSettings settings = SaveSystem.Instance?.Profile?.settings;
            if (settings != null)
            {
                settings.absoluteDragInput = absoluteDrag;
                SaveSystem.Instance.MarkDirty();
            }
            joystickMode = !absoluteDrag;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
