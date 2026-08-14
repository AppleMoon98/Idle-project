using Character;
using Core;
using Core.Pooling;
using Managers;
using UnityEngine;

namespace War.Boss
{
    /// <summary>
    /// War 보스의 광역 공격 패턴을 순환 실행한다. 근처 아군(Player/Soldier 레이어) 중 최근접
    /// 위치를 스냅샷해 예고를 표시하고, TelegraphDuration이 지나면 그 자리에 남아있는 대상에게
    /// 데미지를 준다. 판정/타이밍은 이 컴포넌트가 전부 소유하고, WarBossTelegraphIndicator는
    /// 지시받은 대로 그리기만 한다(Combat.Attacker/WeaponSwing과 동일한 분리 철학).
    /// </summary>
    public sealed class WarBossPatternRunner : MonoBehaviour, ITickable, IPoolable
    {
        [SerializeField]
        private WarBossPatternSO[] patterns;

        [SerializeField]
        private float intervalBetweenPatterns = 4f;

        [SerializeField]
        private float detectionRange = 20f;

        [SerializeField]
        private LayerMask allyLayerMask;

        [SerializeField]
        private GameObject telegraphIndicatorPrefab;

        [SerializeField]
        private int poolCapacity = 2;

        [SerializeField]
        private int poolMaxSize = 4;

        private PoolManager _pool;
        private int _patternIndex;
        private float _elapsedSinceLastCast;
        private bool _isTelegraphing;
        private float _telegraphElapsed;
        private Vector3 _telegraphPosition;
        private WarBossPatternSO _activePattern;
        private GameObject _activeIndicatorInstance;
        private WarBossTelegraphIndicator _activeIndicatorComponent;

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            if (_pool == null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;

                if (telegraphIndicatorPrefab != null)
                {
                    _pool.EnsurePool(telegraphIndicatorPrefab, poolCapacity, poolMaxSize);
                }
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        public void OnSpawned()
        {
            _patternIndex = 0;
            _elapsedSinceLastCast = 0f;
            CancelActivePattern();
        }

        public void OnDespawned()
        {
            CancelActivePattern();
        }

        void ITickable.Tick(float deltaTime)
        {
            // GameTicker의 등록 해제는 그 프레임의 순회가 다 끝난 뒤에야 적용된다(section AP의
            // WeaponSwing과 동일한 함정) - 같은 프레임 안에서 이 보스가 죽어 OnDespawned()로
            // 이미 정리(및 비활성화)된 뒤에도, 아직 _tickables에 남아있는 이 컴포넌트가 그 프레임
            // 안에서 한 번 더 Tick()을 받을 수 있다. 그 상태로 아래 로직을 그대로 타면 이미
            // 반납된(비활성화된) 보스에서 TryStartPattern()이 실행돼 아무도 정리하지 않는 새
            // 예고 표시가 생기거나, 이미 null이 된 _activePattern을 참조해 예외가 날 수 있다.
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (patterns == null || patterns.Length == 0)
            {
                return;
            }

            if (_isTelegraphing)
            {
                TickTelegraph(deltaTime);
                return;
            }

            _elapsedSinceLastCast += deltaTime;

            if (_elapsedSinceLastCast < intervalBetweenPatterns)
            {
                return;
            }

            TryStartPattern();
        }

        private void TickTelegraph(float deltaTime)
        {
            _telegraphElapsed += deltaTime;

            float progress = _activePattern.TelegraphDuration <= 0f
                ? 1f
                : Mathf.Clamp01(_telegraphElapsed / _activePattern.TelegraphDuration);

            if (_activeIndicatorComponent != null)
            {
                _activeIndicatorComponent.SetProgress01(progress);
            }

            if (_telegraphElapsed >= _activePattern.TelegraphDuration)
            {
                ResolvePattern();
            }
        }

        private void TryStartPattern()
        {
            if (_pool == null || telegraphIndicatorPrefab == null)
            {
                return;
            }

            Transform target = FindNearestAlly();

            if (target == null)
            {
                return;
            }

            _activePattern = patterns[_patternIndex];
            _patternIndex = (_patternIndex + 1) % patterns.Length;

            _telegraphPosition = target.position;
            _telegraphElapsed = 0f;
            _isTelegraphing = true;

            _activeIndicatorInstance = _pool.Get(telegraphIndicatorPrefab, _telegraphPosition, Quaternion.identity);
            _activeIndicatorComponent = _activeIndicatorInstance.GetComponent<WarBossTelegraphIndicator>();
            _activeIndicatorComponent.Show(_telegraphPosition, _activePattern.Radius);
        }

        private void ResolvePattern()
        {
            // 데미지 계산에 필요한 값을 지역 변수로 미리 스냅샷해두고, 실제 피해 적용보다 먼저
            // 이 컴포넌트 자신의 상태(_activePattern/_isTelegraphing/인디케이터)부터 정리한다.
            // TakeDamage가 대상을 죽이면 CharacterDiedEvent가 동기적으로 재진입해(이 보스 자신이
            // 강제로 풀에 반납되는 경로를 타면) OnDespawned()가 같은 프레임 안에서 이 메서드가
            // 아직 참조 중인 _activePattern을 먼저 null로 만들어버릴 수 있다 - 순서를 바꾸지
            // 않으면 hits 배열의 두 번째 이후 대상을 처리할 때 이미 null이 된 _activePattern을
            // 읽어 NullReferenceException이 나고(그 프레임에 남아있던 뒤쪽 대상은 피해도 못 받음),
            // 그 예외가 Tick()까지 새어나가 GameTicker의 안전장치(section CH)에 걸려 이 컴포넌트가
            // 그 전투 내내 조용히 멈춰버린다.
            Vector3 position = _telegraphPosition;
            float radius = _activePattern.Radius;
            float damage = _activePattern.Damage;

            ReleaseIndicator();
            _activePattern = null;
            _isTelegraphing = false;
            _elapsedSinceLastCast = 0f;

            Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius, allyLayerMask);

            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out Health health) && !health.IsDead)
                {
                    health.TakeDamage(damage);
                }
            }
        }

        private void CancelActivePattern()
        {
            ReleaseIndicator();
            _activePattern = null;
            _isTelegraphing = false;
            _telegraphElapsed = 0f;
        }

        private void ReleaseIndicator()
        {
            if (_pool != null && _activeIndicatorInstance != null)
            {
                _pool.Release(_activeIndicatorInstance);
            }

            _activeIndicatorInstance = null;
            _activeIndicatorComponent = null;
        }

        private Transform FindNearestAlly()
        {
            Collider2D[] candidates = Physics2D.OverlapCircleAll(transform.position, detectionRange, allyLayerMask);

            Transform nearest = null;
            float nearestSqrDistance = float.MaxValue;

            foreach (Collider2D candidate in candidates)
            {
                if (!candidate.TryGetComponent(out Health health) || health.IsDead)
                {
                    continue;
                }

                float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = candidate.transform;
                }
            }

            return nearest;
        }
    }
}
