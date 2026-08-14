using Character;
using Core;
using Managers;
using UnityEngine;

namespace Skill.Effects
{
    /// <summary>
    /// 시전자 주변 반경 안의 적 전체에게 (시전자 현재 공격력 + magnitude)만큼 데미지를 준다 -
    /// 스킬 수치만 고정으로 박아두면 공격력이 성장할수록 평타보다 약해져 스킬을 쓸 이유가
    /// 없어지므로, 평타(Combat.Attacker)와 같은 기준(RuntimeStats.AttackPower)에 얹는다.
    /// 반경(SkillSO.AreaRadius)과 이펙트 프리팹(SkillSO.VfxPrefab)은 장착된 스킬 데이터에서
    /// 읽는다 - 슬롯은 어느 레이어를 때릴지(targetLayerMask)만 갖고 있다.
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public sealed class AreaDamageSkillEffect : MonoBehaviour, ISkillEffect
    {
        [SerializeField]
        private LayerMask targetLayerMask;

        [SerializeField]
        private int vfxPoolCapacity = 2;

        [SerializeField]
        private int vfxPoolMaxSize = 4;

        private PoolManager _pool;
        private CharacterStatsProvider _statsProvider;

        private void Awake()
        {
            _statsProvider = GetComponentInParent<CharacterStatsProvider>();
        }

        private void OnEnable()
        {
            if (_pool == null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;
            }
        }

        public void Execute(Transform origin, SkillSO definition, float magnitude)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin.position, definition.AreaRadius, targetLayerMask);
            float damage = _statsProvider.Stats.AttackPower + magnitude;

            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out Health health) && !health.IsDead)
                {
                    health.TakeDamage(damage);
                }
            }

            // 프리팹은 radius=1 기준으로 제작하고, 실제 데미지 반경에 맞춰 스케일만 조정한다.
            SkillEffectVfx.SpawnAndPlay(_pool, definition, origin.position, vfxPoolCapacity, vfxPoolMaxSize, definition.AreaRadius);
        }

        public bool HasTargetInRange(Transform origin, SkillSO definition)
        {
            return Combat.NearestHealthScan.FindNearest(origin.position, definition.AreaRadius, targetLayerMask) != null;
        }
    }
}
