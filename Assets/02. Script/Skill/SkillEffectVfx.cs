using Core;
using Core.Pooling;
using Managers;
using UnityEngine;

namespace Skill
{
    /// <summary>
    /// 스킬 이펙트 하나의 재생을 담당하는 범용 시각 컴포넌트. 어떤 스킬인지, 왜 재생되는지는 모르고
    /// Play()로 지시받은 자리에서 Particle System을 재생한 뒤 스스로 풀에 반납하는 것만 한다
    /// (Combat.CircleTelegraphIndicator와 동일하게 판정/타이밍은 호출자가 소유하고 이 컴포넌트는
    /// 그리기만 담당). AreaDamage/SingleTargetStrike/SelfBuff 세 스킬 이펙트가 프리팹만 바꿔 공유한다.
    /// Animator/스프라이트 시트 기반 이펙트는 대신 SkillEffectAnimationVfx가 담당하며,
    /// 어느 쪽을 쓸지는 프리팹에 실제로 붙어있는 컴포넌트로 SpawnAndPlay가 판단한다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SkillEffectVfx : MonoBehaviour, IPoolable
    {
        private ParticleSystem _particles;
        private PoolManager _pool;

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();

            // 파티클이 완전히 멎으면 OnParticleSystemStopped 메시지가 오도록 강제한다 -
            // 프리팹 쪽 Stop Action 설정을 잊어도 자동 반납이 항상 동작하게 하기 위함.
            ParticleSystem.MainModule main = _particles.main;
            main.stopAction = ParticleSystemStopAction.Callback;
        }

        /// <summary>
        /// 현재 위치에서 이펙트를 1회 재생한다. 스폰 직후(PoolManager.Get 다음) 호출한다.
        /// </summary>
        public void Play()
        {
            if (_pool == null && GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _pool);
            }

            _particles.Play();
        }

        // Unity가 Stop Action=Callback인 Particle System이 완전히 정지했을 때 자동으로 보내는 메시지.
        private void OnParticleSystemStopped()
        {
            _pool?.Release(gameObject);
        }

        /// <summary>
        /// definition.VfxPrefab을 pool에서 꺼내 position에 배치하고 재생한다. AreaDamage/SingleTargetStrike/
        /// SelfBuff 세 스킬 이펙트가 각자 들고 있던 동일한 "풀 보장 → 꺼내기 → (선택) 스케일 적용 → 재생"
        /// 절차를 공유한다. pool을 아직 못 구했거나 definition에 VfxPrefab이 없으면 조용히 아무 일도 하지 않는다.
        /// followTarget이 주어지면 스폰 직후 그 트랜스폼의 자식으로 재부모화한다(월드 위치는 유지) —
        /// 이후 매 프레임 추적 코드 없이 트랜스폼 계층만으로 시전자를 따라다니게 된다. 반납 시엔
        /// PoolManager.Release가 항상 풀 루트로 다시 부모를 되돌리므로 별도 정리가 필요 없다.
        /// </summary>
        public static void SpawnAndPlay(PoolManager pool, SkillSO definition, Vector3 position, int poolCapacity, int poolMaxSize, float? uniformScale = null, Transform followTarget = null)
        {
            if (pool == null || definition == null || definition.VfxPrefab == null)
            {
                return;
            }

            pool.EnsurePool(definition.VfxPrefab, poolCapacity, poolMaxSize);
            GameObject instance = pool.Get(definition.VfxPrefab, position, Quaternion.identity);

            if (followTarget != null)
            {
                instance.transform.SetParent(followTarget, worldPositionStays: true);
            }

            if (uniformScale.HasValue)
            {
                instance.transform.localScale = Vector3.one * uniformScale.Value;
            }

            if (instance.TryGetComponent(out SkillEffectVfx particleVfx))
            {
                particleVfx.Play();
            }
            else if (instance.TryGetComponent(out SkillEffectAnimationVfx animationVfx))
            {
                animationVfx.Play();
            }
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
