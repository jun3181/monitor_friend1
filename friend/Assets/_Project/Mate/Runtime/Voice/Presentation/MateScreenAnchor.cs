using UnityEngine;

namespace Mate.Runtime.Voice.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MateScreenAnchor : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform worldAnchor;
        [SerializeField] private GameObject mateRoot;
        [SerializeField] private RectTransform targetRect;
        [SerializeField] private Vector2 pixelOffset = new Vector2(0f, 46f);
        [SerializeField] private Vector2 screenPadding = new Vector2(24f, 24f);

        public bool IsAnchorVisible { get; private set; }

        private void LateUpdate()
        {
            UpdatePosition();
        }

        public void Configure(Camera camera, Transform anchor, RectTransform rect)
        {
            targetCamera = camera;
            worldAnchor = anchor;
            targetRect = rect;
        }

        public void SetTargetRect(RectTransform rect)
        {
            targetRect = rect;
        }

        public bool UpdatePosition()
        {
            var cameraToUse = targetCamera != null ? targetCamera : Camera.main;
            if (cameraToUse == null || targetRect == null)
            {
                IsAnchorVisible = false;
                return false;
            }

            var anchorPosition = ResolveAnchorPosition();
            var screenPosition = cameraToUse.WorldToScreenPoint(anchorPosition);
            if (screenPosition.z <= 0f)
            {
                IsAnchorVisible = false;
                return false;
            }

            var width = targetRect.rect.width > 1f ? targetRect.rect.width : targetRect.sizeDelta.x;
            var height = targetRect.rect.height > 1f ? targetRect.rect.height : targetRect.sizeDelta.y;
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;

            var clamped = new Vector2(screenPosition.x, screenPosition.y) + pixelOffset;
            clamped.x = Mathf.Clamp(clamped.x, screenPadding.x + halfWidth, Screen.width - screenPadding.x - halfWidth);
            clamped.y = Mathf.Clamp(clamped.y, screenPadding.y + halfHeight, Screen.height - screenPadding.y - halfHeight);

            targetRect.position = clamped;
            IsAnchorVisible = true;
            return true;
        }

        private Vector3 ResolveAnchorPosition()
        {
            if (worldAnchor != null)
            {
                return worldAnchor.position;
            }

            if (mateRoot == null)
            {
                mateRoot = GameObject.Find("Mate_VRM");
            }

            if (mateRoot == null)
            {
                return Vector3.zero;
            }

            var renderers = mateRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return mateRoot.transform.position + Vector3.up * 1.8f;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return new Vector3(bounds.center.x, bounds.max.y + 0.12f, bounds.center.z);
        }
    }
}
