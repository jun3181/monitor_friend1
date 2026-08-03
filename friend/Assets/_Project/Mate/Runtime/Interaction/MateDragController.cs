using Mate.Runtime.Core;
using UnityEngine;

namespace Mate.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class MateDragController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Vector2 viewportPadding = new Vector2(0.08f, 0.08f);
        [SerializeField] private float followSmooth = 24f;

        private MateController _controller;
        private Plane _dragPlane;
        private Vector3 _dragOffset;
        private float _viewportDepth;
        private bool _dragging;

        public bool IsDragging => _dragging;

        private void Awake()
        {
            Cache();
        }

        private void Reset()
        {
            Cache();
        }

        public void BeginDrag(Vector2 screenPosition)
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                return;
            }

            var ray = targetCamera.ScreenPointToRay(screenPosition);
            _dragPlane = new Plane(-targetCamera.transform.forward, transform.position);

            if (!_dragPlane.Raycast(ray, out var distance))
            {
                return;
            }

            var hitPoint = ray.GetPoint(distance);
            _dragOffset = transform.position - hitPoint;
            _viewportDepth = targetCamera.WorldToViewportPoint(transform.position).z;
            _dragging = true;
            _controller?.BeginDrag();
        }

        public void UpdateDrag(Vector2 screenPosition)
        {
            if (!_dragging || targetCamera == null)
            {
                return;
            }

            var ray = targetCamera.ScreenPointToRay(screenPosition);
            if (!_dragPlane.Raycast(ray, out var distance))
            {
                return;
            }

            var desired = ray.GetPoint(distance) + _dragOffset;
            desired = ClampToViewport(desired);
            var damp = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, damp);
        }

        public void EndDrag()
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            _controller?.EndDrag();
        }

        private void Cache()
        {
            _controller = GetComponent<MateController>();
            targetCamera = targetCamera != null ? targetCamera : Camera.main;
        }

        private Vector3 ClampToViewport(Vector3 worldPosition)
        {
            var viewport = targetCamera.WorldToViewportPoint(worldPosition);
            viewport.x = Mathf.Clamp(viewport.x, viewportPadding.x, 1f - viewportPadding.x);
            viewport.y = Mathf.Clamp(viewport.y, viewportPadding.y, 1f - viewportPadding.y);
            viewport.z = _viewportDepth;
            return targetCamera.ViewportToWorldPoint(viewport);
        }
    }
}
