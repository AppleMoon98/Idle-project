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
    /// **baseSortingOrder/precisionMultiplier 기본값이 좁은 이유(실제로 겪은 버그):** 이 씬의
    /// GroundTilemap은 sortingOrder=-100으로 고정돼 있고, HealthBar(월드 스페이스 Canvas,
    /// Character 섹션 E)는 SpriteRenderer가 아니라 SortingGroup의 그룹화 대상은 아니지만 자기
    /// 자신의 Canvas.sortingOrder=5로 여전히 같은 Default 정렬 레이어의 같은 숫자 공간을
    /// 공유한다(DamageNumber=10/ExplosionEffect=5/MortarShell=3 등도 마찬가지) - 즉 "Canvas라
    /// 영향을 안 받는다"는 SortingGroup 그룹화 여부에만 해당하고, sortingOrder 숫자 경쟁
    /// 자체에서는 자유롭지 않다. 처음 이 값들을 baseSortingOrder=0/precisionMultiplier=100으로
    /// 뒀더니, 캐릭터가 화면 중앙보다 Y로 1만 높아져도 sortingOrder가 -100 밑으로 내려가
    /// GroundTilemap 뒤에 완전히 숨어버렸다(화면 위쪽 절반이 통째로 안 보이는 것처럼 보임). 이후
    /// baseSortingOrder를 캐릭터 몸통이 항상 GroundTilemap(-100)보다는 위, HealthBar/
    /// DamageNumber 등 "항상 캐릭터 위에 떠야 하는" 요소들(가장 작은 값 5)보다는 아래를 유지하도록
    /// 좁은 구간(-100, 5) 안에서만 움직이게 재조정했다 - 이 구간 폭(105)을 카메라 최광각 기준
    /// 가시 Y 범위(약 ±24~30, Services.CameraFollowService)에 맞추려면 precisionMultiplier가
    /// 반드시 2 미만이어야 해서(105 / (2×30) ≈ 1.75) 기본값을 1로, baseSortingOrder를 그 구간의
    /// 중간 지점(-50)으로 낮췄다. 화면 밖 깊숙한 스폰 지점(Y가 이 범위를 크게 벗어나는 경우)에서
    /// 값이 -100/5 경계를 넘어가는 것은 상관없다 - 화면 밖이라 어차피 안 보이고, 실제로 화면
    /// 안으로 들어오는 시점엔 매 틱 다시 계산되므로 그 시점 기준으로 항상 올바른 값이 나온다.
    /// </summary>
    [RequireComponent(typeof(SortingGroup))]
    public sealed class YSortRenderer : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float precisionMultiplier = 1f;

        [SerializeField]
        private int baseSortingOrder = -50;

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
            _sortingGroup.sortingOrder = baseSortingOrder + Mathf.RoundToInt(-transform.position.y * precisionMultiplier);
        }
    }
}
