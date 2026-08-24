using Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Character
{
    /// <summary>
    /// 캐릭터의 월드 Y좌표를 기준으로 UnityEngine.Rendering.SortingGroup.sortingOrder를 매 틱
    /// 갱신한다 - Y가 낮을수록(화면 아래쪽/앞쪽) 더 앞에, 높을수록(화면 위쪽/뒤쪽) 더 뒤에
    /// 그려지도록 부호를 반전한다. SortingGroup을 쓰는 이유: 몸통(WeaponAnchor의 부모)/무기
    /// (WeaponVisual)/방패(ShieldVisual) 등 자식 SpriteRenderer들이 이미 갖고 있는 상대적
    /// sortingOrder(무기가 몸통 위에 그려지는 것 등, section AP)는 그대로 유지한 채, 캐릭터
    /// 전체를 하나의 단위로만 다른 캐릭터와 앞뒤를 비교한다 - 자식 sortingOrder를 직접 건드리면
    /// 이 관계가 깨진다.
    ///
    /// HealthBar(월드 스페이스 Canvas, Character 섹션 E)는 SpriteRenderer가 아니라 Canvas라
    /// SortingGroup의 영향을 받지 않는다 - 항상 몸통 위에 그려지는 기존 동작이 그대로 유지된다.
    /// </summary>
    [RequireComponent(typeof(SortingGroup))]
    public sealed class YSortRenderer : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float precisionMultiplier = 100f;

        private SortingGroup _sortingGroup;

        private void Awake()
        {
            _sortingGroup = GetComponent<SortingGroup>();
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        void ITickable.Tick(float deltaTime)
        {
            _sortingGroup.sortingOrder = Mathf.RoundToInt(-transform.position.y * precisionMultiplier);
        }
    }
}
