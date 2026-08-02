using Character;
using Character.Events;
using Core;
using Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 집결 깃발 슬롯. 슬롯의 아이콘(icon.sprite — 나중에 인스펙터에서 직접 채워 넣는다)을
    /// 드래그하면 화면상의 복제본이 포인터를 따라다니고, 놓으면 그 위치의 월드 좌표에
    /// RallyPointMarker를 배치하고 SquadMoveCommandEvent를 발행한다. 슬롯 자체의 아이콘은
    /// 소모되지 않으므로 매번 다시 드래그할 수 있다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class SquadRallyFlagUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField]
        private Image icon;

        [SerializeField]
        private Canvas canvas;

        [SerializeField]
        private GameObject markerPrefab;

        [SerializeField]
        private int poolCapacity = 1;

        [SerializeField]
        private int poolMaxSize = 2;

        private PoolManager _pool;
        private RectTransform _dragGhost;
        private GameObject _activeMarkerInstance;

        private void Awake()
        {
            if (icon == null)
            {
                icon = GetComponent<Image>();
            }
        }

        private void OnEnable()
        {
            if (_pool == null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;

                if (markerPrefab != null)
                {
                    _pool.EnsurePool(markerPrefab, poolCapacity, poolMaxSize);
                }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var ghostGO = new GameObject("RallyFlagDragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ghostGO.transform.SetParent(canvas.transform, false);
            ghostGO.transform.SetAsLastSibling();

            var ghostImage = ghostGO.GetComponent<Image>();
            ghostImage.sprite = icon.sprite;
            ghostImage.raycastTarget = false;

            _dragGhost = (RectTransform)ghostGO.transform;
            _dragGhost.sizeDelta = ((RectTransform)icon.transform).sizeDelta;

            UpdateGhostPosition(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateGhostPosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_dragGhost != null)
            {
                Destroy(_dragGhost.gameObject);
                _dragGhost = null;
            }

            if (_pool == null || markerPrefab == null)
            {
                return;
            }

            Camera camera = Camera.main;
            Vector3 worldPosition = camera.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, -camera.transform.position.z));
            worldPosition.z = 0f;

            if (_activeMarkerInstance != null)
            {
                _pool.Release(_activeMarkerInstance);
            }

            _activeMarkerInstance = _pool.Get(markerPrefab, worldPosition, Quaternion.identity);
            _activeMarkerInstance.GetComponent<RallyPointMarker>().SetSprite(icon.sprite);

            GameBootstrapper.Events?.Publish(new SquadMoveCommandEvent(worldPosition));
        }

        private void UpdateGhostPosition(PointerEventData eventData)
        {
            if (_dragGhost == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)canvas.transform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);

            _dragGhost.anchoredPosition = localPoint;
        }
    }
}
