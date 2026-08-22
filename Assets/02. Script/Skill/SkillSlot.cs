using System.Collections.Generic;
using Core;
using Skill.Effects;
using Skill.Events;
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
    ///
    /// 발동 경로는 둘이다 - 자동(Tick, SkillLoadoutService.IsEnabled가 켜져 있고 쿨다운이 다 찼고
    /// ISkillEffect.HasTargetInRange일 때만) / 수동(TryManualCast, UI.SkillCooldownHudUI의 탭 요청 -
    /// 쿨다운만 확인하고 대상 존재 여부는 확인하지 않는다, 대상이 없어도 일단 사용된다). 두 경로
    /// 모두 같은 Cast() 헬퍼로 수렴해 쿨다운 리셋/이펙트 실행/카메라 흔들림 로직이 갈라지지 않는다.
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
                { SkillEffectType.SelfBuff, GetComponent<SelfBuffSkillEffect>() },
                { SkillEffectType.Poison, GetComponent<PoisonSkillEffect>() },
                { SkillEffectType.Whirlwind, GetComponent<WhirlwindSkillEffect>() },
                { SkillEffectType.Meteor, GetComponent<MeteorSkillEffect>() },
                { SkillEffectType.Debuff, GetComponent<DebuffSkillEffect>() },
                { SkillEffectType.Curse, GetComponent<CurseSkillEffect>() },
                { SkillEffectType.SoldierBuff, GetComponent<SoldierBuffSkillEffect>() },
                { SkillEffectType.PartyHeal, GetComponent<PartyHealBuffSkillEffect>() }
            };
        }

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillLoadoutService loadoutService))
            {
                loadoutService.RegisterSlot(this);
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillLoadoutService loadoutService))
            {
                loadoutService.UnregisterSlot(this);
            }
        }

        /// <summary>
        /// 이 슬롯의 쿨다운을 즉시 "다 찬"(발동 가능) 상태로 되돌린다. 던전 입장 시점에
        /// SkillLoadoutService.ResetAllCooldowns()가 등록된 슬롯 전부에 호출한다 - 장착된 스킬이
        /// 없으면(definition == null) 조용히 아무 일도 하지 않는다.
        /// </summary>
        public void ResetCooldownReady()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout))
            {
                return;
            }

            SkillSO definition = loadout.GetEquipped(slotIndex);

            if (definition == null)
            {
                return;
            }

            _elapsed = definition.Cooldown;
            CooldownProgress01 = 1f;
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

            // 비어있거나(장착 안 함) 방어적으로 레벨이 0이면 쿨다운 자체가 의미 없으니 매 틱
            // 리셋해둔다 - 나중에 장착/레벨업되면 쿨다운이 처음부터 새로 시작한다.
            if (definition == null || skillService.GetLevel(definition) <= 0)
            {
                _elapsed = 0f;
                CooldownProgress01 = 0f;
                return;
            }

            _elapsed = Mathf.Min(_elapsed + deltaTime, definition.Cooldown);
            CooldownProgress01 = Mathf.Clamp01(_elapsed / definition.Cooldown);

            if (_elapsed < definition.Cooldown)
            {
                return;
            }

            // 자동(IsEnabled) 상태가 아니면(수동) 쿨다운이 다 찼어도 스스로 발동하지 않는다 - HUD
            // 탭(TryManualCast)으로만 발동한다. 쿨다운 진행도 자체는 자동/수동과 무관하게 계속
            // 차오르므로, 수동 모드에서도 HUD 게이지가 정확한 발동 가능 여부를 보여준다.
            if (!loadout.IsEnabled(slotIndex))
            {
                return;
            }

            if (!_effectsByType.TryGetValue(definition.EffectType, out ISkillEffect effect) || effect == null)
            {
                return;
            }

            // 쿨다운은 다 찼지만 발동할 대상이 사거리 안에 없으면 소모하지 않고 대기한다(진행도는
            // 가득 찬 채로 유지) - 다음 틱에 다시 확인해 대상이 나타나는 즉시 발동한다.
            if (!effect.HasTargetInRange(transform, definition))
            {
                return;
            }

            Cast(definition, effect, skillService);
        }

        /// <summary>
        /// HUD 슬롯을 탭하는 등 플레이어가 명시적으로 요청했을 때 즉시 발동을 시도한다. 자동(Tick)과
        /// 달리 ISkillEffect.HasTargetInRange를 확인하지 않는다 - 대상이 없어도 일단 사용된다(각
        /// ISkillEffect.Execute 구현체는 대상이 없을 때도 안전하게 아무 효과 없이 끝난다, 예:
        /// AreaDamageSkillEffect는 범위 안에 아무도 없으면 그냥 아무도 안 맞고 넘어감). 쿨다운이
        /// 덜 찼으면 그대로 실패(false) - 수동이라고 쿨다운까지 무시하지는 않는다(스팸 방지).
        /// </summary>
        public bool TryManualCast()
        {
            if (GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout)
                || !GameBootstrapper.Services.TryGet(out SkillService skillService))
            {
                return false;
            }

            SkillSO definition = loadout.GetEquipped(slotIndex);

            if (definition == null || skillService.GetLevel(definition) <= 0 || CooldownProgress01 < 1f)
            {
                return false;
            }

            if (!_effectsByType.TryGetValue(definition.EffectType, out ISkillEffect effect) || effect == null)
            {
                return false;
            }

            Cast(definition, effect, skillService);
            return true;
        }

        private void Cast(SkillSO definition, ISkillEffect effect, SkillService skillService)
        {
            _elapsed = 0f;
            CooldownProgress01 = 0f;

            effect.Execute(transform, definition, definition.GetMagnitude(skillService.GetLevel(definition)));

            if (definition.ShakeCamera)
            {
                GameBootstrapper.Events?.Publish(new SkillCameraShakeRequestedEvent(definition.ShakeDuration, definition.ShakeMagnitude));
            }
        }
    }
}
