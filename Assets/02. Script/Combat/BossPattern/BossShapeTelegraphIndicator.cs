using System.Collections.Generic;
using Core;
using Core.Pooling;
using Managers;
using UnityEngine;

namespace Combat.BossPattern
{
    /// <summary>
    /// 보스 패턴의 직사각형/부채꼴 예고(텔레그래프) 표시를 담당하는 순수 시각 컴포넌트. 판정/
    /// 데미지/타이밍은 전부 호출자(Rank.Boss.PromotionBossController 등)가 소유하며, 이 컴포넌트는
    /// Show*()/SetProgress01()로 지시받은 대로 그리기만 한다(War.Boss.WarBossTelegraphIndicator와
    /// 동일한 철학을 직사각형/부채꼴로 확장한 것). 스프라이트는 별도 아트 에셋 없이 최초 1회
    /// 코드로 생성해 모든 인스턴스가 공유한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossShapeTelegraphIndicator : MonoBehaviour, IPoolable, ITickable
    {
        private const int RectangleTextureSize = 8;

        // 부채꼴(특히 "맵 끝까지" 닿는 부채꼴 패턴)은 실제 게임에서 반지름 1유닛짜리 베이스
        // 스프라이트를 30~40배까지 확대해서 쓴다 - 128px 해상도로는 그 배율에서 각도 경계의
        // 페더링 폭 자체가 화면에서 텍스셀 하나하나가 눈에 보일 만큼 넓게 늘어나(실사용 중
        // 실제 게임 내 최대 줌(narrowOrthographicSize=6)에서도 계단 현상이 뚜렷이 보임),
        // 512로 올려 텍셀 크기를 4분의 1로 줄인다.
        private const int WedgeTextureSize = 512;

        // 부채꼴 베이스 스프라이트는 꼭짓점(pivot)에서 먼 쪽 가장자리까지의 거리가 정확히 1
        // 월드 유닛이 되도록 생성한다 - 그래야 ShowSector가 반지름을 곱하기만 하면 된다.
        private const float WedgeUnitRadius = 1f;

        private static readonly Color NormalFillColor = new(1f, 0.15f, 0.1f, 1f);

        // 판정이 실제로 적용되는 순간 공격 범위가 바로 사라지지 않고 흰색/완전 불투명으로 잠깐
        // 남아있게 하는 잔상 연출의 지속 시간 - 어떤 패턴(찌르기/앞뒤 가르기/부채꼴/페이즈2 등)이든
        // PlayResolveFlash()만 부르면 이 잔상을 그대로 재사용할 수 있다.
        private const float ResolveFlashDuration = 0.1f;

        private static Sprite _sharedRectangleSprite;
        private static Sprite _sharedRectangleFlashSprite;
        private static readonly Dictionary<string, Sprite> SharedWedgeSprites = new();
        private static readonly Dictionary<string, Sprite> SharedWedgeFlashSprites = new();

        private enum LastShapeKind
        {
            Rectangle,
            Sector
        }

        [SerializeField]
        private SpriteRenderer spriteRenderer;

        private LastShapeKind _lastShapeKind;
        private float _lastWedgeAngleDeg;
        private float _lastWedgeInnerRadiusRatio;
        private bool _isFlashing;
        private float _flashElapsed;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
            _isFlashing = false;
        }

        /// <summary>
        /// center를 중심으로 angleDeg만큼 회전된 length×width 직사각형 예고를 표시한다.
        /// angleDeg=0이면 length 방향이 월드 +X를 향한다(Combat.BossPatternShapes.FindHitsInRectangle과
        /// 동일한 좌표 규약).
        /// </summary>
        public void ShowRectangle(Vector3 center, float length, float width, float angleDeg)
        {
            _isFlashing = false;
            _lastShapeKind = LastShapeKind.Rectangle;

            spriteRenderer.sprite = GetOrCreateRectangleSprite(NormalFillColor, ref _sharedRectangleSprite);
            transform.position = center;
            transform.eulerAngles = new Vector3(0f, 0f, angleDeg);
            transform.localScale = new Vector3(length, width, 1f);
            SetProgress01(0f);
        }

        /// <summary>
        /// origin을 꼭짓점으로, facingDeg 방향을 중심으로 angleDeg만큼 벌어진 반지름 radius
        /// 부채꼴 예고를 표시한다(Combat.BossPatternShapes.FindHitsInSector와 동일한 좌표 규약).
        /// innerRadius가 0보다 크면 그 안쪽을 비워 고리(도넛) 모양으로 그린다.
        /// </summary>
        public void ShowSector(Vector3 origin, float radius, float angleDeg, float facingDeg, float innerRadius = 0f)
        {
            float innerRadiusRatio = radius > 0f ? Mathf.Clamp01(innerRadius / radius) : 0f;

            _isFlashing = false;
            _lastShapeKind = LastShapeKind.Sector;
            _lastWedgeAngleDeg = angleDeg;
            _lastWedgeInnerRadiusRatio = innerRadiusRatio;

            spriteRenderer.sprite = GetOrCreateWedgeSprite(angleDeg, innerRadiusRatio, NormalFillColor, SharedWedgeSprites);
            transform.position = origin;
            transform.eulerAngles = new Vector3(0f, 0f, facingDeg);
            float scale = radius / WedgeUnitRadius;
            transform.localScale = new Vector3(scale, scale, 1f);
            SetProgress01(0f);
        }

        /// <summary>
        /// 예고 경과 비율(0~1)에 따라 위험도가 커지는 것처럼 보이도록 불투명도를 올린다.
        /// </summary>
        public void SetProgress01(float progress01)
        {
            Color color = spriteRenderer.color;
            color.a = Mathf.Lerp(0.25f, 0.85f, Mathf.Clamp01(progress01));
            spriteRenderer.color = color;
        }

        /// <summary>
        /// 판정이 실제로 적용되는 순간 호출한다 - 지금 표시 중이던 모양(직사각형이든 부채꼴이든,
        /// 마지막 Show*() 호출 그대로) 그대로 흰색+완전 불투명으로 바꾸고 ResolveFlashDuration
        /// (0.1초) 뒤 스스로 풀에 반납한다. 호출자는 이제 이 메서드만 부르면 되고, 직접
        /// PoolManager.Release를 즉시 호출할 필요가 없다(Combat.DamageNumber와 동일한 "스스로
        /// 타이머를 관리하다 스스로 반납"하는 자기완결형 패턴 - 호출자가 몇 초 뒤 반납할지 따로
        /// 챙기지 않아도 된다).
        /// </summary>
        public void PlayResolveFlash()
        {
            spriteRenderer.sprite = _lastShapeKind == LastShapeKind.Sector
                ? GetOrCreateWedgeSprite(_lastWedgeAngleDeg, _lastWedgeInnerRadiusRatio, Color.white, SharedWedgeFlashSprites)
                : GetOrCreateRectangleSprite(Color.white, ref _sharedRectangleFlashSprite);

            spriteRenderer.color = Color.white;
            _isFlashing = true;
            _flashElapsed = 0f;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isFlashing)
            {
                return;
            }

            _flashElapsed += deltaTime;

            if (_flashElapsed < ResolveFlashDuration)
            {
                return;
            }

            _isFlashing = false;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                pool.Release(gameObject);
            }
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _isFlashing = false;
        }

        private static Sprite GetOrCreateRectangleSprite(Color fillColor, ref Sprite cache)
        {
            if (cache != null)
            {
                return cache;
            }

            var texture = new Texture2D(RectangleTextureSize, RectangleTextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < RectangleTextureSize; y++)
            {
                for (int x = 0; x < RectangleTextureSize; x++)
                {
                    texture.SetPixel(x, y, fillColor);
                }
            }

            texture.Apply();

            cache = Sprite.Create(
                texture,
                new Rect(0f, 0f, RectangleTextureSize, RectangleTextureSize),
                new Vector2(0.5f, 0.5f),
                RectangleTextureSize);

            return cache;
        }

        private static Sprite GetOrCreateWedgeSprite(float angleDeg, float innerRadiusRatio, Color fillColor, Dictionary<string, Sprite> cacheDictionary)
        {
            int key = Mathf.RoundToInt(angleDeg);
            int holeKey = Mathf.RoundToInt(innerRadiusRatio * 100f);
            string cacheKey = key + "_" + holeKey;

            if (cacheDictionary.TryGetValue(cacheKey, out Sprite cached))
            {
                return cached;
            }

            // 캔버스는 정사각형이 아니라 세로가 가로의 2배인 직사각형이어야 한다 - 부채꼴은
            // 꼭짓점에서 반지름만큼 가로로 뻗어나가는 동시에, 각도가 90도에 가까워질수록 세로로도
            // 반지름만큼(즉 위아래 합쳐 지름만큼) 필요하다. 가로/세로 픽셀 스케일을
            // WedgeTextureSize(가로 픽셀 수)로 동일하게 맞춰야, 세로 여유가 가로 절반밖에 없어
            // 실제 원호에 닿기 전에 캔버스 위아래 끝에 먼저 잘리는 문제(180도가 거의 직사각형
            // 모양으로 보이던 버그)가 생기지 않는다.
            int canvasWidth = WedgeTextureSize;
            int canvasHeight = WedgeTextureSize * 2;

            var texture = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float halfAngle = key * 0.5f;
            float centerY = (canvasHeight - 1) * 0.5f;
            float outerRadius = canvasWidth;
            // 페더링 폭(픽셀)은 해상도에 비례해야 한다 - 고정 3px로 두면 해상도를 올릴수록
            // 반지름 대비 페더링 비율이 오히려 얇아져(3/512 < 3/128) 원래 의도한 시각적 두께를
            // 잃는다.
            float edgeFeatherPixels = WedgeTextureSize / 128f * 3f;
            float outerEdgeFeatherStart = outerRadius - edgeFeatherPixels;

            // 고리(도넛) 모양 - innerRadiusRatio가 0보다 크면 원점 쪽 holeRadiusPixels 안쪽을
            // 바깥 경계와 동일한 방식(페더링 포함)으로 비운다. 0이면 기존과 완전히 동일(구멍 없음).
            float holeRadiusPixels = outerRadius * holeKey / 100f;

            // 반지름 경계(innerRadius~outerRadius)는 원래도 부드럽게 페더링되어 있었지만, 각도
            // 경계(부채꼴의 대각선 변)는 "각도가 halfAngle 이하면 그리고, 아니면 안 그린다"는
            // 이분법 하드컷이라 anti-aliasing이 전혀 없었다 - 대각선을 하드컷으로 래스터화하면
            // 항상 계단식(jaggy) 경계가 생기고, 특히 부채꼴처럼 큰 배율로 확대되는 패턴에서는
            // 그 계단이 눈에 띄게 깨져 보인다(실사용 중 발견). 반지름과 동일한 방식(InverseLerp
            // 기반 페더링)을 각도 축에도 적용해, 두 경계 모두 부드럽게 만든다.
            const float AngleFeatherDegrees = 2f;

            for (int y = 0; y < canvasHeight; y++)
            {
                for (int x = 0; x < canvasWidth; x++)
                {
                    float dx = x;
                    float dy = y - centerY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float angleFromForward = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);

                    float radialAlpha = Mathf.Clamp01(1f - Mathf.InverseLerp(outerEdgeFeatherStart, outerRadius, distance));
                    float angularAlpha = Mathf.Clamp01(1f - Mathf.InverseLerp(halfAngle - AngleFeatherDegrees, halfAngle, angleFromForward));
                    float holeAlpha = holeRadiusPixels > 0f
                        ? Mathf.Clamp01(Mathf.InverseLerp(holeRadiusPixels, holeRadiusPixels + edgeFeatherPixels, distance))
                        : 1f;
                    float alpha = radialAlpha * angularAlpha * holeAlpha;

                    Color pixel = fillColor;
                    pixel.a = alpha;
                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply();

            // pixelsPerUnit은 가로 기준(canvasWidth)으로 고정한다 - 세로 픽셀 수가 2배라도
            // 같은 픽셀당-월드유닛 비율을 쓰므로, 완성된 스프라이트는 가로 1유닛(=WedgeUnitRadius)
            // × 세로 2유닛(반지름 1일 때 위아래 합쳐 지름)이 되고, ShowSector의 균등 스케일이
            // 그대로 두 축 모두에 올바르게 반영된다.
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, canvasWidth, canvasHeight),
                new Vector2(0f, 0.5f),
                canvasWidth);

            cacheDictionary[cacheKey] = sprite;
            return sprite;
        }
    }
}
