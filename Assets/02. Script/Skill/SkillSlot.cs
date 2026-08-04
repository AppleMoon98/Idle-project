using System.Collections.Generic;
using Core;
using Skill.Effects;
using UnityEngine;

namespace Skill
{
    /// <summary>
    /// 고정 장착 슬롯 하나(0~SkillLoadoutService.SlotCount-1). 자신의 slotIndex에 무엇이
    /// 장착됐는지는 전혀 캐시하지 않고 매 틱 SkillLoadoutService에 물어본다 — 플레이어가 언제든
    /// 자유롭게 스킬을 바꿔 끼울 수 있어야 하기 때문이다. 장착된 스킬이 없으면 아무 일도 하지 않는다.
    /// 어떤 스킬이든 장착될 수 있으므로 세 가지 ISkillEffect 구현체를 전부 들고 있다가
    /// SkillSO.EffectType으로 그때그때 알맞은 것을 골라 실행한다(Character.RuntimeStatApplier와
    /// 같은 "enum → 구현체" 테이블 패턴).
    /// </summary>
    public sealed class SkillSlot : MonoBehaviour, ITickable
    {
        [SerializeField]
        private int slotIndex;

        private Dictionary<SkillEffectType, ISkillEffect> _effectsByType;
        private float _elapsed;

        /// <summary>
        /// 이 슬롯이 대응하는 SkillLoadoutService 상의 인덱스.
        /// </summary>
        public int SlotIndex => slotIndex;

        /// <summary>
        /// 현재 쿨다운 진행률(0~1). 장착된 스킬이 없으면 0. 쿨타임 HUD가 매 틱 읽어 표시한다.
        /// </summary>
        public float CooldownProgress01 { get; private set; }

        private void Awake()
        {
            _effectsByType = new Dictionary<SkillEffectType, ISkillEffect>
            {
                { SkillEffectType.AreaDamage, GetComponent<AreaDamageSkillEffect>() },
                { SkillEffectType.SingleTargetStrike, GetComponent<SingleTargetStrikeSkillEffect>() },
                { SkillEffectType.SelfBuff, GetComponent<SelfBuffSkillEffect>() }
            };
        }

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            if (GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout)
                || !GameBootstrapper.Services.TryGet(out SkillService skillService))
            {
                return;
            }

            SkillSO definition = loadout.GetEquipped(slotIndex);

            // 비어있거나(장착 안 함), 방어적으로 레벨이 0이거나, 플레이어가 HUD에서 꺼둔 슬롯은
            // 발동하지 않고, 다음에 다시 조건이 충족됐을 때 쿨다운이 처음부터 새로 시작하도록
            // 진행도를 리셋해둔다.
            if (definition == null || skillService.GetLevel(definition) <= 0 || !loadout.IsEnabled(slotIndex))
            {
                _elapsed = 0f;
                CooldownProgress01 = 0f;
                return;
            }

            _elapsed += deltaTime;
            CooldownProgress01 = Mathf.Clamp01(_elapsed / definition.Cooldown);

            if (_elapsed < definition.Cooldown)
            {
                return;
            }

            _elapsed = 0f;
            CooldownProgress01 = 0f;

            if (_effectsByType.TryGetValue(definition.EffectType, out ISkillEffect effect) && effect != null)
            {
                effect.Execute(transform, definition, definition.GetMagnitude(skillService.GetLevel(definition)));
            }
        }
    }
}
