using UnityEngine;

namespace Skill
{
    /// <summary>
    /// 스킬 하나(고정 슬롯)의 데이터 정의. 레벨 0(미강화 상태)은 아직 습득하지 않은 것으로 취급돼
    /// SkillSlot이 자동 발동시키지 않는다(SkillSlot.Tick 참고) — 레벨 1부터 EffectValueBase +
    /// EffectValuePerLevel × 레벨 수치로 발동한다. 비용은 골드/강화석을 둘 다 요구하며 레벨에
    /// 비례해 선형으로 증가한다(EquipmentEnhancementConfigSO와 동일한 형태).
    /// </summary>
    [CreateAssetMenu(fileName = "Skill", menuName = "Idle Project/Skill/Skill")]
    public sealed class SkillSO : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private Sprite icon;

        [SerializeField]
        private Color iconTint = Color.white;

        [SerializeField]
        private float cooldown = 8f;

        [SerializeField]
        private int maxLevel = 20;

        [SerializeField]
        private int goldCostBase = 100;

        [SerializeField]
        private int goldCostIncreasePerLevel = 50;

        [SerializeField]
        private int stoneCostBase = 5;

        [SerializeField]
        private int stoneCostIncreasePerLevel = 2;

        [SerializeField]
        private float effectValueBase = 10f;

        [SerializeField]
        private float effectValuePerLevel = 2f;

        [SerializeField]
        private SkillEffectType effectType;

        [SerializeField]
        private float areaRadius = 3f;

        [SerializeField]
        private float strikeRange = 2.5f;

        [SerializeField]
        private float buffDuration = 5f;

        [SerializeField]
        private GameObject vfxPrefab;

        [SerializeField]
        private bool vfxFollowCaster;

        [SerializeField]
        private float vfxHeightOffset;

        [SerializeField]
        private bool shakeCamera;

        [SerializeField]
        private float shakeDuration = 0.3f;

        [SerializeField]
        private float shakeMagnitude = 0.2f;

        /// <summary>
        /// 스킬 이름(UI 표시용).
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 슬롯 아이콘. 실제 스킬 전용 아트가 없는 동안은 재사용 스프라이트를 IconTint로
        /// 다르게 물들여 임시 아이콘으로 쓴다(Player/Soldier/Cargo와 동일한 재사용 방식).
        /// </summary>
        public Sprite Icon => icon;

        /// <summary>
        /// 아이콘에 곱해질 색상.
        /// </summary>
        public Color IconTint => iconTint;

        /// <summary>
        /// 자동 발동 주기(초).
        /// </summary>
        public float Cooldown => cooldown;

        /// <summary>
        /// 최대 레벨.
        /// </summary>
        public int MaxLevel => maxLevel;

        /// <summary>
        /// 레벨(0부터 시작)에 따른 효과 수치. ISkillEffect 구현체가 데미지/버프량 등으로 해석한다.
        /// </summary>
        public float GetMagnitude(int level)
        {
            return effectValueBase + effectValuePerLevel * level;
        }

        /// <summary>
        /// 이 스킬이 실행할 효과 종류. SkillSlot이 장착 슬롯의 여러 ISkillEffect 구현체 중
        /// 어느 것을 실행할지 고르는 데 쓴다.
        /// </summary>
        public SkillEffectType EffectType => effectType;

        /// <summary>
        /// AreaDamage 스킬의 데미지 반경. 다른 EffectType의 스킬에서는 쓰이지 않는다.
        /// </summary>
        public float AreaRadius => areaRadius;

        /// <summary>
        /// SingleTargetStrike 스킬의 탐지 사거리. 다른 EffectType의 스킬에서는 쓰이지 않는다.
        /// </summary>
        public float StrikeRange => strikeRange;

        /// <summary>
        /// SelfBuff 스킬의 버프 지속시간(초). 다른 EffectType의 스킬에서는 쓰이지 않는다.
        /// </summary>
        public float BuffDuration => buffDuration;

        /// <summary>
        /// 시전 시 재생할 이펙트 프리팹(Skill.SkillEffectVfx가 붙어있어야 한다).
        /// </summary>
        public GameObject VfxPrefab => vfxPrefab;

        /// <summary>
        /// 이 스킬의 이펙트가 시전자(캐스터)를 따라다닐지 여부. 기본 false(시전 위치에 고정) —
        /// AreaDamage/SingleTargetStrike처럼 타격 지점에 고정돼야 하는 이펙트는 끄고,
        /// SelfBuff처럼 시전자 본인에게 붙는 이펙트만 켠다.
        /// </summary>
        public bool VfxFollowCaster => vfxFollowCaster;

        /// <summary>
        /// 이펙트 스폰 위치에 더할 높이(시전자 기준 위쪽). VfxFollowCaster가 켜져 있으면
        /// 재부모화 이후의 로컬 오프셋으로도 그대로 쓰인다(따라다니는 동안에도 이 높이 유지).
        /// </summary>
        public float VfxHeightOffset => vfxHeightOffset;

        /// <summary>
        /// 시전 시 카메라 흔들림 연출을 재생할지 여부. 기본 false — 이 스킬만의 부가 옵션이라
        /// 켜둔 스킬에서만 의미가 있다(AreaRadius 등과 같은 "일부 스킬만 쓰는 필드" 관례).
        /// </summary>
        public bool ShakeCamera => shakeCamera;

        /// <summary>
        /// 카메라 흔들림 지속시간(초). ShakeCamera가 false면 쓰이지 않는다.
        /// </summary>
        public float ShakeDuration => shakeDuration;

        /// <summary>
        /// 카메라 흔들림 강도. ShakeCamera가 false면 쓰이지 않는다.
        /// </summary>
        public float ShakeMagnitude => shakeMagnitude;

        /// <summary>
        /// 다음 레벨(level -> level+1)로 올리는 데 필요한 골드 비용.
        /// </summary>
        public int GetGoldCost(int level)
        {
            return goldCostBase + goldCostIncreasePerLevel * level;
        }

        /// <summary>
        /// 다음 레벨(level -> level+1)로 올리는 데 필요한 강화석 비용.
        /// </summary>
        public int GetStoneCost(int level)
        {
            return stoneCostBase + stoneCostIncreasePerLevel * level;
        }
    }
}
