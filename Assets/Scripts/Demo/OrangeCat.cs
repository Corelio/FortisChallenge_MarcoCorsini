#if DEMO_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.EnhancedTouch;
#endif

namespace Fortis.Demo
{
    public class OrangeCat : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _menu;
        private bool _menuVisible;

        [Header("UI")]
        public KeyCode toggleKey = KeyCode.F10;

        [Header("Zoomies")]
        [Range(10, 120)] public float durationSeconds = 60f;
        [Range(0.02f, 0.40f)] public float minInterval = 0.05f;
        [Range(0.03f, 0.50f)] public float maxInterval = 0.15f;

        // Bootstrap
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Object.FindFirstObjectByType<OrangeCat>() == null)
            {
                var go = new GameObject("OrangeCat");
                go.AddComponent<OrangeCat>();
            }
        }

        void Awake()
        {
            EnsureEventSystem();
#if ENABLE_INPUT_SYSTEM
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
#endif
            BuildUI();
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current?.f10Key.wasPressedThisFrame == true)
                ToggleMenu();
#else
            // Only if project is set to Both or Old Input:
            if (Input.GetKeyDown(toggleKey))
                ToggleMenu();
#endif
        }

        // UI building
        void BuildUI()
        {
            // Canvas overlay (top-most)
            var canvasGO = new GameObject("OrangeCatCanvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32760; // render above everything
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasGO);

            // Top-left icon
            var iconGO = new GameObject("OC_Button");
            iconGO.transform.SetParent(canvasGO.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0, 1);
            iconRT.anchorMax = new Vector2(0, 1);
            iconRT.pivot = new Vector2(0, 1);
            iconRT.anchoredPosition = new Vector2(10, -10);
            iconRT.sizeDelta = new Vector2(30, 30);

            // Icon image + button
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = Resources.Load<Sprite>("Sprites/OrangeCat");
            iconImg.color = Color.white;

            var iconBtn = iconGO.AddComponent<Button>();
            iconBtn.onClick.AddListener(ToggleMenu);

            // Menu panel (hidden)
            _menu = new GameObject("OC_Menu");
            _menu.transform.SetParent(canvasGO.transform, false);
            var menuRT = _menu.AddComponent<RectTransform>();
            menuRT.anchorMin = new Vector2(0, 1);
            menuRT.anchorMax = new Vector2(0, 1);
            menuRT.pivot     = new Vector2(0, 1);
            menuRT.anchoredPosition = new Vector2(50, -10);
            menuRT.sizeDelta = new Vector2(180, 90);
            var bg = _menu.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);

            var layout = _menu.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.padding = new RectOffset(8,8,8,8);
            layout.spacing = 6;

            AddMenuButton("Click zoomies", StartClickZoomies);
            AddMenuButton("Touch zoomies", StartTouchZoomies);

            _menu.SetActive(false);
        }

        void AddMenuButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            var b = new GameObject(label);
            b.transform.SetParent(_menu.transform, false);
            var r = b.AddComponent<RectTransform>();
            r.sizeDelta = new Vector2(160, 30);

            var img = b.AddComponent<Image>(); img.color = new Color(1f, 0.5f, 0f, 0.9f);
            var btn = b.AddComponent<Button>(); btn.onClick.AddListener(onClick);

            var tGO = new GameObject("Label");
            tGO.transform.SetParent(b.transform, false);
            var t = tGO.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            var tr = tGO.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        }

        void ToggleMenu()
        {
            _menuVisible = !_menuVisible;
            _menu.SetActive(_menuVisible);
        }

        // ZOOMIES routines
        void StartClickZoomies()
        {
            StopAllCoroutines();
            StartCoroutine(ZoomiesKeyboard());
        }

        void StartTouchZoomies()
        {
            StopAllCoroutines();
            StartCoroutine(ZoomiesTouch());
        }

        // Zoomies implementations
        IEnumerator ZoomiesKeyboard()
        {
            Debug.Log("[OrangeCat] ZoomiesKeyboard start");
#if ENABLE_INPUT_SYSTEM
            float end = Time.realtimeSinceStartup + durationSeconds;
            var leftKeys  = new[] { Key.A, Key.LeftArrow };
            var rightKeys = new[] { Key.D, Key.RightArrow };

            void Hold(params Key[] keys)
            {
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
                InputSystem.Update();
            }
            void ReleaseAll()
            {
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                InputSystem.Update();
            }
            bool TryPhysicsJump(float jumpSpeed = 5f)
            {
                // Find player in scene
                var player = FindFirstObjectByType<Platformer.Mechanics.PlayerController>();
                if (player == null) return false;

                // Apply jump via Rigidbody2D or reflection
                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    var v = rb.linearVelocity;
                    if (v.y < jumpSpeed) v.y = jumpSpeed;
                    rb.linearVelocity = v;
                    return true;
                }

                var t = player.GetType();
                System.Type cur = t;
                System.Reflection.FieldInfo velField = null;
                while (cur != null && velField == null)
                {
                    velField = cur.GetField("velocity",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.FlattenHierarchy);
                    cur = cur.BaseType;
                }

                // Apply via velocity field if found
                if (velField != null)
                {
                    object boxed = velField.GetValue(player);
                    if (boxed is Vector2 v2)
                    {
                        if (v2.y < jumpSpeed) v2.y = jumpSpeed;
                        velField.SetValue(player, v2);
                        return true;
                    }
                }

                return false;
            }

            while (Time.realtimeSinceStartup < end)
            {
                // 1) Always do a demo-physics jump at burst start (no input required)
                TryPhysicsJump(7f);
                yield return null;

                // 2) Pick a direction and run briefly
                bool goRight = Random.value < 0.5f;
                var moveKey  = goRight ? Key.D : Key.A;
                float runFor = Random.Range(0.9f, 1.6f);
                float t0     = Time.realtimeSinceStartup;
                float midAt  = t0 + Random.Range(0.35f, 0.65f);
                bool midDone = false;

                Hold(moveKey);

                while (Time.realtimeSinceStartup - t0 < runFor)
                {
                    if (!midDone && Time.realtimeSinceStartup >= midAt)
                    {
                        TryPhysicsJump();
                        midDone = true;
                        yield return null;
                        Hold(moveKey);
                    }

                    yield return null;
                }

                ReleaseAll();
                yield return null;
            }
#else
            yield return null; // Input System not enabled: do nothing
#endif
            Debug.Log("[OrangeCat] ZoomiesKeyboard end");
        }

        IEnumerator ZoomiesTouch()
        {
            Debug.Log("[OrangeCat] Touch zoomies start");
            float end = Time.realtimeSinceStartup + durationSeconds;

            while (Time.realtimeSinceStartup < end)
            {
                var pos = new Vector2(Random.Range(0, Screen.width), Random.Range(0, Screen.height));

#if ENABLE_INPUT_SYSTEM
                var ts = Touchscreen.current;
                if (ts != null)
                {
                    // Touch begin
                    var begin = new TouchState
                    {
                        touchId = 1,
                        phase = UnityEngine.InputSystem.TouchPhase.Began,
                        position = pos,
                        delta = Vector2.zero,
                        pressure = 1f
                    };
                    InputSystem.QueueStateEvent(ts, begin);
                    InputSystem.Update();

                    yield return new WaitForSecondsRealtime(0.05f);

                    // Touch end
                    var endState = new TouchState
                    {
                        touchId = 1,
                        phase = UnityEngine.InputSystem.TouchPhase.Ended,
                        position = pos,
                        delta = Vector2.zero,
                        pressure = 0f
                    };
                    InputSystem.QueueStateEvent(ts, endState);
                    InputSystem.Update();
                }
                else
                {
                    // Desktop/editor without touchscreen: click a UI element
                    SynthesizeUiClick(pos);
                }
#else
                SynthesizeUiClick(pos);
#endif
                yield return new WaitForSecondsRealtime(Random.Range(minInterval, maxInterval));
            }

            Debug.Log("[OrangeCat] Touch zoomies end");
        }

        void SynthesizeUiClick(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return;

            var data = new PointerEventData(es) { position = screenPos };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);
            if (hits.Count == 0) return;

            var target = hits[0].gameObject;
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerExitHandler);
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem").AddComponent<EventSystem>();
            es.gameObject.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es.gameObject);
        }
    }
}
#endif