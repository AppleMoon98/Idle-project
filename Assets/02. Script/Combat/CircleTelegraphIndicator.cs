using Core.Pooling;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 원형 예고(텔레그래프) 표시를 담당하는 순수 시각 컴포넌트. 판정/데미지/타이밍은 호출자가
    /// 전부 소유하며, 이 컴포넌트는 Show()/SetProgress01()로 지시받은 대로 그리기만 한다
    /// (Combat.WeaponSwing이 Attacker의 판정과 분리되어 있는 것과 동일한 철학). 원래 War 보스
    /// 전용 패턴(War.Boss.WarBossPatternRunner, 삭제됨)의 부속이었으나 War.WarStructure(점령
    /// 범위)/Skill.Effects.MeteorSkillEffect(포탄 낙하 예고)/Combat.SiegeMortarAttackBehavior+
    /// MortarShell(공성병 박격포 착탄) 등 여러 도메인이 공유하는 범용 유틸리티가 된 지 오래라
    /// War.Boss에서 Combat으로 옮기고 이름에서도 "War"/"Boss"를 뺐다 - 직사각형/부채꼴을 그리는
    /// 형제 컴포넌트 Combat.BossPattern.BossShapeTelegraphIndicator와 "{모양}TelegraphIndicator"
    /// 네이밍을 맞춘 것이기도 하다. 원형 스프라이트는 별도 아트 에셋 없이 최초 1회 코드로 생성해
    /// 모든 인스턴스가 공유한다 — 스프라이트 텍스처 자체는 흰색+알파(모양)만 담고, 실제 색조는
    /// SpriteRenderer.color로 입힌다(스프라이트가 색을 곱연산으로 틴트하므로 흰색 스프라이트 ×
    /// 임의 색 = 그 색 그대로). 이렇게 분리해야 같은 스프라이트를 공유하면서도 호출자마다 다른
    /// 색(적 공격=빨강, 플레이어 스킬=파랑 등)을 쓸 수 있다.
    ///
    /// 프리팹의 SpriteRenderer.sortingOrder는 바닥 장식처럼 배경(GroundTilemap, -100)보다는 위,
    /// 그 외 모든 오브젝트보다는 아래여야 한다(실사용 중 "공격 범위 표시가 캐릭터를 가린다"는
    /// 제보로 발견 - 기존 값 1은 캐릭터보다 위였다). Character.YSortRenderer가 화면 안 캐릭터에
    /// 부여하는 sortingOrder 실질 범위가 약 [-80, -20](baseSortingOrder=-50 ± 가시 Y 범위)이라,
    /// 그보다 낮은 -90으로 고정해뒀다 - 예고 위치/캐릭터 위치와 무관하게 항상 배경보다는 위,
    /// 캐릭터를 포함한 나머지 전부보다는 아래를 유지한다(캐릭터처럼 Y에 따라 동적으로 재계산할
    /// 필요가 없다 - 바닥 장식은 원근에 따라 다른 오브젝트를 가리거나 가려질 이유가 없으므로
    /// 고정값 하나로 항상 충분하다).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CircleTelegraphIndicator : MonoBehaviour, IPoolable
    {
        private const int TextureSize = 128;

        /// <summary>
        /// Show()에 색을 안 넘긴 호출자(WarStructure 점령 범위 등, 전부 적/위험 표시)가 쓰는 기본색.
        /// </summary>
        private static readonly Color DefaultColor = new Color(1f, 0.15f, 0.1f, 1f);

        private static Sprite _sharedCircleSprite;

        [SerializeField]
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = GetOrCreateCircleSprite();
        }

        /// <summary>
        /// 지정된 위치/반경에 예고 표시를 시작한다. tintColor를 생략하면 기존과 동일한 빨간색
        /// (적 위험 표시)을 쓴다 - 플레이어 자신의 공격(예: Skill.Effects.MeteorSkillEffect의
        /// 포탄 낙하)처럼 성격이 다른 예고는 다른 색을 넘겨 구분한다.
        /// </summary>
        public void Show(Vector3 position, float radius, Color? tintColor = null)
        {
            transform.position = position;
            transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

            Color tint = tintColor ?? DefaultColor;
            Color current = spriteRenderer.color;
            spriteRenderer.color = new Color(tint.r, tint.g, tint.b, current.a);

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
        /// 표시 여부를 직접 켜고 끈다(War.WarStructure가 점령 완료 시 자기 판정 범위 표시를 숨기는
        /// 용도 등) - Show()처럼 위치/색을 다시 설정하지 않는다.
        /// </summary>
        public void SetVisible(bool visible)
        {
            spriteRenderer.enabled = visible;
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
        }

        private static Sprite GetOrCreateCircleSprite()
        {
            if (_sharedCircleSprite != null)
            {
                return _sharedCircleSprite;
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float center = (TextureSize - 1) * 0.5f;
            float outerRadius = TextureSize * 0.5f;
            float innerRadius = outerRadius - 3f;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = 1f - Mathf.InverseLerp(innerRadius, outerRadius, distance);
                    alpha = Mathf.Clamp01(alpha);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            _sharedCircleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                TextureSize);

            return _sharedCircleSprite;
        }
    }
}
