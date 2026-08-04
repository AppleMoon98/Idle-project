using Core;
using Core.Pooling;
using Managers;
using UnityEngine;

namespace Skill
{
    /// <summary>
    /// 스킬 이펙트 하나의 재생을 담당하는 범용 시각 컴포넌트. 어떤 스킬인지, 왜 재생되는지는 모르고
    /// Play()로 지시받은 자리에서 Particle System을 재생한 뒤 스스로 풀에 반납하는 것만 한다
    /// (War.Boss.WarBossTelegraphIndicator와 동일하게 판정/타이밍은 호출자가 소유하고 이 컴포넌트는
    /// 그리기만 담당). AreaDamage/SingleTargetStrike/SelfBuff 세 스킬 이펙트가 프리팹만 바꿔 공유한다.
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

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
