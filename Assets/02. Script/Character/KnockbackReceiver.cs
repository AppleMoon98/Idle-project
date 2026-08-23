using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 외부 충격(기마병 돌진 등)으로 잠깐 강제로 밀려나는 범용 컴포넌트. 어떤 캐릭터든 붙일 수
    /// 있도록 병종/도메인 지식이 전혀 없다 — 호출자(예: Combat.BearCharge)가 방향/거리/시간만
    /// 넘겨주면 된다. RallyMoveReceiver와 같은 "지속시간 동안 이동 관련 컴포넌트를 꺼서 제어권을
    /// 가져왔다가 끝나면 돌려준다" 패턴이되, CharacterMover(캐릭터 자신의 이동속도)를 거치지 않고
    /// 직접 transform을 옮긴다 — 넉백은 대상의 이동속도 스탯과 무관하게 항상 일정한 세기여야
    /// 하기 때문이다(느린 유닛이 자기 이동속도로만 느릿느릿 밀려나면 타격감이 없다).
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    public sealed class KnockbackReceiver : MonoBehaviour, ITickable
    {
        [SerializeField]
        private Behaviour[] componentsToSuspend;

        private CharacterMover _mover;
        private Vector3 _knockbackVelocity;
        private bool _isKnockedBack;
        private float _remainingDuration;

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        /// <summary>
        /// 넉백 도중(_isKnockedBack=true) GameObject가 비활성화되면(예: 넉백 밀림 중에 사망해
        /// 풀로 반환됨, 또는 던전 오버레이의 SoldierRespawner.SetActiveAll(false)) Tick()이
        /// 더 이상 호출되지 않아 SetComponentsSuspended(false)/_mover.enabled = true가 영원히
        /// 실행되지 못한다 — 그 결과 componentsToSuspend(예: SoldierBehaviorController,
        /// EnemyTracker)가 disabled 상태로 풀에 반환되고, 이후 이 인스턴스가 재사용돼도 Unity는
        /// 이미 enabled=false인 컴포넌트를 GameObject 재활성화만으로 다시 켜주지 않으므로(컴포넌트
        /// 자신의 enabled가 true로 바뀌어야 OnEnable이 불림) 그 병사가 영구히 정지 상태로 남는다
        /// (실사용 중 발견). 비활성화되는 순간 넉백이 아직 진행 중이었다면 즉시 정리해 이 누수를
        /// 막는다 — 넉백 효과의 나머지 시간은 사라지지만(재활성화 시 처음부터 다시 판정), 컴포넌트가
        /// 영원히 꺼진 채로 남는 것보다 훨씬 안전하다.
        /// </summary>
        private void OnDisable()
        {
            TickerRegistration.Unregister(this);

            if (_isKnockedBack)
            {
                _isKnockedBack = false;
                _mover.enabled = true;
                SetComponentsSuspended(false);
            }
        }

        /// <summary>
        /// direction 방향으로 distance만큼, duration에 걸쳐 강제로 밀어낸다. 이미 넉백 중이면
        /// 새 값으로 덮어쓴다(중첩 누적하지 않음 — SelfBuffSkillEffect의 "재시전 시 갱신" 방향과 동일).
        /// </summary>
        public void ApplyKnockback(Vector2 direction, float distance, float duration)
        {
            if (duration <= 0f || distance <= 0f)
            {
                return;
            }

            if (!_isKnockedBack)
            {
                SetComponentsSuspended(true);
                _mover.enabled = false;
            }

            _knockbackVelocity = (Vector3)(direction.normalized * (distance / duration));
            _remainingDuration = duration;
            _isKnockedBack = true;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isKnockedBack)
            {
                return;
            }

            transform.position += _knockbackVelocity * deltaTime;
            _remainingDuration -= deltaTime;

            if (_remainingDuration <= 0f)
            {
                _isKnockedBack = false;
                _mover.enabled = true;
                SetComponentsSuspended(false);
            }
        }

        private void SetComponentsSuspended(bool suspended)
        {
            if (componentsToSuspend == null)
            {
                return;
            }

            foreach (Behaviour component in componentsToSuspend)
            {
                if (component != null)
                {
                    component.enabled = !suspended;
                }
            }
        }
    }
}
