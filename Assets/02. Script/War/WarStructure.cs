using Core;
using UnityEngine;
using War.Events;

namespace War
{
    /// <summary>
    /// 맵에 배치되는 War 구조물. 근처(ActivationRadius)에 아군(Player+Soldier 레이어)이 있으면
    /// ActionInterval마다 작동해 PushRadius 안의 몬스터를 밀어내고, 동시에 점령 게이지를 채운다.
    /// "밀어내기 유틸리티"와 "구조물 점령 목표"를 한 컴포넌트로 통합한 이유는, 목표가 구조물
    /// 점령이 아닌 챕터에서도 밀어내기 자체는 계속 쓸모가 있기 때문이다.
    /// </summary>
    public sealed class WarStructure : MonoBehaviour, ITickable
    {
        [SerializeField]
        private WarStructureSO definition;

        [SerializeField]
        private LayerMask allyLayerMask;

        [SerializeField]
        private LayerMask enemyLayerMask;

        private float _elapsed;

        /// <summary>
        /// 현재 점령 게이지(0~1).
        /// </summary>
        public float Control { get; private set; }

        /// <summary>
        /// 점령 완료 여부. 한 번 완료되면 되돌아가지 않는다.
        /// </summary>
        public bool IsCaptured { get; private set; }

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

        /// <summary>
        /// 새 War 시도를 위해 점령 상태를 초기화한다.
        /// </summary>
        public void ResetForNewAttempt()
        {
            Control = 0f;
            IsCaptured = false;
            _elapsed = 0f;
        }

        void ITickable.Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            if (_elapsed < definition.ActionInterval)
            {
                return;
            }

            _elapsed = 0f;

            bool allyPresent = Physics2D.OverlapCircle(transform.position, definition.ActivationRadius, allyLayerMask) != null;

            if (!allyPresent)
            {
                return;
            }

            PushNearbyEnemies();

            if (!IsCaptured)
            {
                Control = Mathf.Clamp01(Control + definition.CaptureGainPerAction);

                if (Control >= 1f)
                {
                    IsCaptured = true;
                    GameBootstrapper.Events?.Publish(new WarStructureCapturedEvent(this));
                }
            }
        }

        private void PushNearbyEnemies()
        {
            Collider2D[] enemies = Physics2D.OverlapCircleAll(transform.position, definition.PushRadius, enemyLayerMask);

            foreach (Collider2D enemy in enemies)
            {
                Vector2 direction = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
                enemy.transform.position += (Vector3)(direction * definition.PushDistance);
            }

            GameBootstrapper.Events?.Publish(new WarStructureActivatedEvent(transform.position));
        }
    }
}
