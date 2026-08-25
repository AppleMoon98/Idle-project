using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 매 틱 자신의 MaxHealth 대비 일정 비율만큼 체력을 회복하는 범용 컴포넌트.
    /// 특정 오브젝트 전용이 아니라, "초당 N% 회복"이 필요한 어떤 캐릭터에도 붙여 쓸 수 있다
    /// (첫 사용처는 연습 스테이지 허수아비, Stage.PracticeStageController).
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class PercentHealthRegen : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float regenPercentPerSecond = 0.01f;

        private Health _health;
        private CharacterStatsProvider _statsProvider;

        private void Awake()
        {
            _health = GetComponent<Health>();
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

        public void Tick(float deltaTime)
        {
            _health.Heal(_statsProvider.Stats.MaxHealth * regenPercentPerSecond * deltaTime);
        }
    }
}
