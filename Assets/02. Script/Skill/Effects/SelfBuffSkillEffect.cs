using Character;
using Core;
using Managers;
using UnityEngine;

namespace Skill.Effects
{
    /// <summary>
    /// 시전자 자신의 공격력을 magnitude만큼 SkillSO.BuffDuration초 동안 올렸다가 원복한다. 재시전 시
    /// 이전 보너스를 먼저 제거한 뒤 새 보너스를 적용해(중첩 대신 갱신) 값이 어긋나지 않게 한다.
    /// SkillSlot이 GetComponent&lt;ISkillEffect&gt;()로 자기 자신을 정확히 찾도록 스킬마다 별도의
    /// 자식 오브젝트에 배치하는 구조라(Combat.Attacker처럼 캐릭터당 효과가 하나뿐이지 않음),
    /// CharacterStatsProvider는 같은 오브젝트가 아니라 부모(캐릭터 루트)에서 찾는다.
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public sealed class SelfBuffSkillEffect : MonoBehaviour, ISkillEffect, ITickable
    {
        [SerializeField]
        private int vfxPoolCapacity = 2;

        [SerializeField]
        private int vfxPoolMaxSize = 4;

        private CharacterStatsProvider _statsProvider;
        private PoolManager _pool;
        private float _appliedBonus;
        private float _remaining;

        private void Awake()
        {
            _statsProvider = GetComponentInParent<CharacterStatsProvider>();
        }

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            if (_pool == null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        public void Execute(Transform origin, SkillSO definition, float magnitude)
        {
            RevertBonus();

            _appliedBonus = magnitude;
            _statsProvider.Stats.AttackPower += _appliedBonus;
            _remaining = definition.BuffDuration;

            // 버프 지속시간 내내 도는 루프 이펙트가 아니라 시전 순간의 1회성 버스트만 재생한다 -
            // 지속시간과 이펙트 수명을 동기화하려면 별도 해제 로직이 필요해져 범위를 넘어선다.
            Vector3 spawnPosition = origin.position + Vector3.up * definition.VfxHeightOffset;
            Transform followTarget = definition.VfxFollowCaster ? origin : null;
            SkillEffectVfx.SpawnAndPlay(_pool, definition, spawnPosition, vfxPoolCapacity, vfxPoolMaxSize, followTarget: followTarget);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_remaining <= 0f)
            {
                return;
            }

            _remaining -= deltaTime;

            if (_remaining <= 0f)
            {
                RevertBonus();
            }
        }

        private void RevertBonus()
        {
            if (_appliedBonus <= 0f)
            {
                return;
            }

            _statsProvider.Stats.AttackPower -= _appliedBonus;
            _appliedBonus = 0f;
        }
    }
}
