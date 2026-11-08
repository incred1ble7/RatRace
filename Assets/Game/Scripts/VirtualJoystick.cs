using UnityEngine;
using UnityEngine.EventSystems;

namespace MouseGame
{
    /// <summary>
    /// Floating on-screen joystick. Put it on a transparent full-size Image (touch zone).
    /// The stick appears where the finger touches and returns to its rest position on release.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform background;
        [SerializeField] RectTransform handle;
        [Tooltip("Max handle offset in canvas units.")]
        [SerializeField] float radius = 110f;
        [Range(0f, 0.5f)] [SerializeField] float deadZone = 0.12f;

        RectTransform zone;
        Vector2 restPosition;
        int pointerId = int.MinValue;

        /// <summary>Stick direction, magnitude 0..1.</summary>
        public Vector2 Direction { get; private set; }

        void Awake()
        {
            zone = (RectTransform)transform;
            if (background != null) restPosition = background.anchoredPosition;
        }

        void OnDisable() => Release();

        /// <summary>Wire up when the UI is built from code.</summary>
        public void Setup(RectTransform bg, RectTransform stick, float maxRadius)
        {
            zone = (RectTransform)transform;
            background = bg;
            handle = stick;
            radius = maxRadius;
            restPosition = bg.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (pointerId != int.MinValue) return;           // ignore second finger
            pointerId = e.pointerId;
            if (background != null && ScreenToLocal(zone, e, out var local))
            {
                // local is relative to the zone pivot; convert to the background's anchor reference
                Vector2 anchorRef = zone.rect.min + Vector2.Scale(zone.rect.size, background.anchorMin);
                background.anchoredPosition = local - anchorRef;
            }
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointerId || background == null) return;
            if (!ScreenToLocal(background, e, out var local)) return;

            Vector2 offset = Vector2.ClampMagnitude(local, radius);
            if (handle != null) handle.anchoredPosition = offset;

            Vector2 dir = offset / radius;
            float m = dir.magnitude;
            Direction = m < deadZone ? Vector2.zero : dir.normalized * Mathf.InverseLerp(deadZone, 1f, m);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointerId) Release();
        }

        void Release()
        {
            pointerId = int.MinValue;
            Direction = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            if (background != null) background.anchoredPosition = restPosition;
        }

        static bool ScreenToLocal(RectTransform rt, PointerEventData e, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out local);
    }
}
