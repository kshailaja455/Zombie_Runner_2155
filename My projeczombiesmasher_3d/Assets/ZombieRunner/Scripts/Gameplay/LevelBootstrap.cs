using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace ZombieRunner
{
    /// <summary>
    /// The only component a level scene needs. The setup tool creates it in each level scene with the
    /// theme set and the camera/sun assigned; missing references are created at runtime.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LevelBootstrap : MonoBehaviour
    {
        public ThemeId theme = ThemeId.Forest;
        public Camera mainCamera;
        public Light sun;

        void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            EnsureEventSystem();
            var gm = gameObject.GetOrAdd<GameManager>();
            gm.Begin(theme, mainCamera, sun);
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
