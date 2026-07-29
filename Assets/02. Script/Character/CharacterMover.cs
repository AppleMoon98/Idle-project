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

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
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
            if (Target == null)
            {
                return;
            }

            float speed = _statsProvider.Stats.MoveSpeed;
            transform.position = Vector3.MoveTowards(transform.position, Target.position, speed * deltaTime);
        }
    }
}
