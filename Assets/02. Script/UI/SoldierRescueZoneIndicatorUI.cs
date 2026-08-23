using System.Collections.Generic;
using Combat;
using Core;
using Dungeon;
using UnityEngine;
using UnityEngine.UI;
using War;

namespace UI
{
    /// <summary>
    /// 병사 구출 던전 진행 중(SoldierRescueDungeonSessionController.IsFighting), 아직 점령하지
    /// 못한 구역이 화면 밖에 있으면 화면 가장자리에 그 방향을 가리키는 화살표를 표시한다. 화면
    /// 안에 들어와 있거나(Combat.CameraVisibility.IsOnScreen — 줌 배율과 무관한 고정 범위가 아니라
    /// "지금 실제로 보이는 화면" 기준을 일부러 쓴다. 이 기능 자체가 "지금 화면에 안 보이는 걸
    /// 알려주는 것"이라, 다른 곳(적 탐지 범위 등)과 달리 실시간 카메라 뷰포트가 정답이다) 이미
    /// 점령됐으면(War.WarStructure.IsCaptured) 그 구역의 화살표는 숨긴다.
    ///
    /// 화살표 스프라이트는 War.Boss.WarBossTelegraphIndicator.GetOrCreateCircleSprite와 같은 이유로
    /// 별도 아트 에셋 없이 코드로 한 번 생성해 공유한다(흰색+알파 모양만 담고 Image.color로 틴트).
    /// 인디케이터 GameObject 자체도 미리 만든 프리팹 없이 필요한 개수만큼(최대 zoneCount) 그때그때
    /// 코드로 생성해 재사용하는 풀 - 매 시도 구역 수가 항상 같지만, 하드코딩 없이 실제 활성 구역
    /// 수에 맞춰 자연스럽게 늘어나도록 했다.
    /// </summary>
    public sealed class SoldierRescueZoneIndicatorUI : MonoBehaviour, ITickable
    {
        private const int ArrowTextureSize = 64;

        [SerializeField]
        private SoldierRescueDungeonSessionController session;

        [SerializeField]
        private RectTransform canvasRect;

        [SerializeField]
        private RectTransform indicatorContainer;

        /// <summary>
        /// 화살표가 화면 가장자리에서 안쪽으로 얼마나 떨어져 표시될지(캔버스 로컬 단위).
        /// </summary>
        [SerializeField]
        private float edgeMargin = 80f;

        /// <summary>
        /// Combat.CameraVisibility.IsOnScreen에 넘기는 뷰포트 마진 - 0보다 크게 잡아, 구역이
        /// 화면 가장자리에 딱 걸친 순간(화살표 표시 여부가 매 프레임 깜빡일 수 있는 경계)보다
        /// 조금 더 안쪽에 들어와야 "화면 안"으로 인정한다.
        /// </summary>
        [SerializeField]
        private float onScreenHideMargin = 0.05f;

        [SerializeField]
        private float arrowSize = 60f;

        [SerializeField]
        private Color arrowColor = new Color(1f, 0.84f, 0f, 1f);

        private static Sprite _sharedArrowSprite;

        private readonly List<RectTransform> _pool = new();

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
            HideAll();
        }

        void ITickable.Tick(float deltaTime)
        {
            Camera cam = Camera.main;

            if (session == null || !session.IsFighting || cam == null)
            {
                HideAll();
                return;
            }

            IReadOnlyList<WarStructure> zones = session.ActiveZones;
            int visibleCount = 0;

            for (int i = 0; i < zones.Count; i++)
            {
                WarStructure zone = zones[i];

                if (zone == null || zone.IsCaptured)
                {
                    continue;
                }

                if (CameraVisibility.IsOnScreen(cam, zone.transform.position, onScreenHideMargin))
                {
                    continue;
                }

                RectTransform indicator = GetOrCreateIndicator(visibleCount);
                visibleCount++;
                PositionIndicator(indicator, cam, zone.transform.position);
            }

            for (int i = visibleCount; i < _pool.Count; i++)
            {
                _pool[i].gameObject.SetActive(false);
            }
        }

        private void HideAll()
        {
            foreach (RectTransform indicator in _pool)
            {
                if (indicator != null)
                {
                    indicator.gameObject.SetActive(false);
                }
            }
        }

        private RectTransform GetOrCreateIndicator(int index)
        {
            if (index < _pool.Count)
            {
                RectTransform existing = _pool[index];
                existing.gameObject.SetActive(true);
                return existing;
            }

            var go = new GameObject("ZoneIndicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(indicatorContainer != null ? indicatorContainer : canvasRect, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(arrowSize, arrowSize);

            Image image = go.GetComponent<Image>();
            image.sprite = GetOrCreateArrowSprite();
            image.color = arrowColor;
            image.raycastTarget = false;

            _pool.Add(rt);
            return rt;
        }

        private void PositionIndicator(RectTransform indicator, Camera cam, Vector3 worldPosition)
        {
            Vector3 viewportPos = cam.WorldToViewportPoint(worldPosition);

            if (viewportPos.z < 0f)
            {
                // 2D 직교 카메라에서는 사실상 발생하지 않지만, 카메라 뒤쪽으로 판정될 경우를 위한
                // 방어적 반전 처리(Combat.CameraVisibility.IsOnScreen도 z<=0을 "화면 밖"으로 본다).
                viewportPos.x = 1f - viewportPos.x;
                viewportPos.y = 1f - viewportPos.y;
            }

            Vector2 canvasSize = canvasRect.rect.size;
            var targetLocal = new Vector2((viewportPos.x - 0.5f) * canvasSize.x, (viewportPos.y - 0.5f) * canvasSize.y);
            Vector2 direction = targetLocal.sqrMagnitude > 0.0001f ? targetLocal.normalized : Vector2.up;

            Vector2 halfSize = canvasSize * 0.5f - new Vector2(edgeMargin, edgeMargin);
            Vector2 clampedPos = ClampDirectionToRectEdge(direction, halfSize);

            indicator.anchoredPosition = clampedPos;
            indicator.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// 화면 중심에서 direction 방향으로 나아갈 때, halfSize 크기 사각형의 테두리와 만나는
        /// 지점을 구한다 - Combat.CameraVisibility.DistanceToBoundsEdge와 같은 발상(레이-사각형
        /// 교차)이지만, 여기선 캔버스 로컬 2D 좌표계 안에서 원점(화면 중심) 기준으로 직접 계산한다.
        /// </summary>
        private static Vector2 ClampDirectionToRectEdge(Vector2 direction, Vector2 halfSize)
        {
            if (Mathf.Approximately(direction.x, 0f))
            {
                return new Vector2(0f, Mathf.Sign(direction.y) * halfSize.y);
            }

            if (Mathf.Approximately(direction.y, 0f))
            {
                return new Vector2(Mathf.Sign(direction.x) * halfSize.x, 0f);
            }

            float slope = Mathf.Abs(direction.y / direction.x);
            float cornerSlope = halfSize.y / halfSize.x;

            if (slope < cornerSlope)
            {
                float x = Mathf.Sign(direction.x) * halfSize.x;
                return new Vector2(x, x * (direction.y / direction.x));
            }

            float y = Mathf.Sign(direction.y) * halfSize.y;
            return new Vector2(y * (direction.x / direction.y), y);
        }

        /// <summary>
        /// 오른쪽(0도)을 가리키는 이등변삼각형을 코드로 한 번 생성해 모든 인스턴스가 공유한다.
        /// </summary>
        private static Sprite GetOrCreateArrowSprite()
        {
            if (_sharedArrowSprite != null)
            {
                return _sharedArrowSprite;
            }

            var texture = new Texture2D(ArrowTextureSize, ArrowTextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < ArrowTextureSize; y++)
            {
                for (int x = 0; x < ArrowTextureSize; x++)
                {
                    texture.SetPixel(x, y, IsInsideArrow(x, y) ? Color.white : new Color(1f, 1f, 1f, 0f));
                }
            }

            texture.Apply();

            _sharedArrowSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, ArrowTextureSize, ArrowTextureSize),
                new Vector2(0.5f, 0.5f),
                ArrowTextureSize);

            return _sharedArrowSprite;
        }

        /// <summary>
        /// 꼭짓점이 텍스처 오른쪽 가장자리, 밑변이 왼쪽 가장자리인 이등변삼각형.
        /// </summary>
        private static bool IsInsideArrow(int x, int y)
        {
            float nx = x / (float)(ArrowTextureSize - 1);
            float ny = y / (float)(ArrowTextureSize - 1) - 0.5f;

            float halfWidthAtX = 0.5f * (1f - nx);
            return Mathf.Abs(ny) <= halfWidthAtX;
        }
    }
}
