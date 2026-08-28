using Combat;
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

        [SerializeField]
        private CircleTelegraphIndicator rangeIndicator;

        private SpriteRenderer _spriteRenderer;
        private float _elapsed;

        /// <summary>
        /// 현재 점령 게이지(0~1).
        /// </summary>
        public float Control { get; private set; }

        /// <summary>
        /// 점령 완료 여부. 한 번 완료되면 되돌아가지 않는다.
        /// </summary>
        public bool IsCaptured { get; private set; }

        /// <summary>
        /// 점령 판정 반경. 자동 이동(Character.CaptureZoneAutoNavigator) 등 외부에서 "이 범위
        /// 안에 서면 된다"를 알아야 하는 소비자를 위해 노출한다.
        /// </summary>
        public float ActivationRadius => definition != null ? definition.ActivationRadius : 0f;

        /// <summary>
        /// 공유 원형 예고 시각 컴포넌트(Combat.CircleTelegraphIndicator)를 그대로 재사용해, 점령
        /// 판정 반경을 위험 범위처럼 항상 표시해둔다 - ActivationRadius는 구조물이 존재하는 동안 변하지
        /// 않으므로 한 번만 Show()하면 된다(자식이라 위치는 부모를 따라 자동으로 맞는다).
        /// </summary>
        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (rangeIndicator != null && definition != null)
            {
                rangeIndicator.Show(transform.position, definition.ActivationRadius);
            }
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

        /// <summary>
        /// 새 War 시도를 위해 점령 상태를 초기화한다. 직전 시도에서 점령 완료로 숨겨져 있었을 수
        /// 있는 몸체/판정 범위 표시도 함께 되돌린다.
        /// </summary>
        public void ResetForNewAttempt()
        {
            Control = 0f;
            IsCaptured = false;
            _elapsed = 0f;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = true;
            }

            rangeIndicator?.SetVisible(true);
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

                    // 점령이 끝난 구조물은 몸체/판정 범위 표시를 숨긴다 - 여러 구역을 동시에
                    // 상대하는 콘텐츠(병사 구출 던전 등)에서 이미 끝난 구역이 화면에 계속 남아있으면
                    // 아직 진행해야 할 구역과 구분이 안 돼 확인하기 어렵다는 문제가 있었다(실사용
                    // 중 발견). 판정/틱 자체는 멈추지 않는다 - 오직 시각적으로만 사라진다.
                    if (_spriteRenderer != null)
                    {
                        _spriteRenderer.enabled = false;
                    }

                    rangeIndicator?.SetVisible(false);

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
