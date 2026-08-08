using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 외부 충격(기마병 돌진 등)으로 잠깐 강제로 밀려나는 범용 컴포넌트. 어떤 캐릭터든 붙일 수
    /// 있도록 병종/도메인 지식이 전혀 없다 — 호출자(예: Combat.CavalryCharge)가 방향/거리/시간만
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

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
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
