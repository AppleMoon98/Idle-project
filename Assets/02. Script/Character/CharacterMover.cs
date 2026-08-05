using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 지정된 대상(Target)을 향해 이동한다. GameTicker에 등록되어 매 프레임 갱신된다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class CharacterMover : MonoBehaviour, ITickable
    {
        private CharacterStatsProvider _statsProvider;

        /// <summary>
        /// 이동 목표. null이면 이동하지 않는다.
        /// </summary>
        public Transform Target { get; set; }

        /// <summary>
        /// Target까지 남은 거리가 이 값 이하이면 더 이상 접근하지 않고 멈춘다.
        /// 기본값 0이면 기존과 동일하게 Target 위치까지 계속 이동한다.
        /// </summary>
        public float StoppingDistance { get; set; }

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
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
            if (Target == null)
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, Target.position);

            if (distance <= StoppingDistance)
            {
                return;
            }

            float speed = _statsProvider.Stats.MoveSpeed;
            transform.position = Vector3.MoveTowards(transform.position, Target.position, speed * deltaTime);
        }
    }
}
