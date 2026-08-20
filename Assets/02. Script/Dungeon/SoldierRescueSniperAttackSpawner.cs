using System.Collections.Generic;
using Character;
using Combat;
using Core;
using Managers;
using Services;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 병사 구출 던전 전용 저격 공격 반복 스포너. 던전 시도 시작~종료 동안 활성화되어, 던전 단계
    /// (N)에 따라 짧아지는 주기로 SoldierRescueSniperAttack을 하나씩 스폰한다 — 새 주기가 오면
    /// 이전 공격이 아직 진행 중이어도 개의치 않고 겹쳐서 새로 스폰한다(단계가 높을수록 여러 선이
    /// 동시에 떠 있는 압박 구조).
    /// </summary>
    public sealed class SoldierRescueSniperAttackSpawner : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GameObject attackPrefab;

        [SerializeField]
        private Transform playerTransform;

        /// <summary>
        /// 1단계 기준 반복 주기(초).
        /// </summary>
        [SerializeField]
        private float baseInterval = 1f;

        /// <summary>
        /// 단계 1당 주기가 줄어드는 양(초).
        /// </summary>
        [SerializeField]
        private float intervalDecreasePerStage = 0.1f;

        /// <summary>
        /// 아무리 단계가 높아도 이 값 밑으로는 주기가 짧아지지 않는다.
        /// </summary>
        [SerializeField]
        private float minInterval = 0.3f;

        [SerializeField]
        private int poolCapacity = 4;

        [SerializeField]
        private int poolMaxSize = 16;

        private readonly List<GameObject> _activeAttacks = new();

        private KnockbackReceiver _playerKnockback;
        private float _interval;
        private float _elapsed;
        private bool _isActive;

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        /// <summary>
        /// 던전 시도를 시작하며 스포너를 켠다. stageNumber(단계)가 높을수록 반복 주기가 짧아진다.
        /// </summary>
        public void Activate(int stageNumber)
        {
            if (playerTransform != null && _playerKnockback == null)
            {
                _playerKnockback = playerTransform.GetComponent<KnockbackReceiver>();
            }

            _interval = Mathf.Max(minInterval, baseInterval - intervalDecreasePerStage * (stageNumber - 1));
            _elapsed = 0f;
            _isActive = true;
        }

        /// <summary>
        /// 스포너를 끄고, 아직 경고/비행 중인 공격을 전부 강제로 반납한다(던전 클리어/실패/나가기
        /// 공통 — Stage.StageProgressTracker.ReleaseRemaining과 같은 이유). 반납 도중
        /// SoldierRescueSniperAttack.OnDespawned가 NotifyAttackReleased로 같은 리스트를 다시
        /// 건드리므로, 원본 리스트는 먼저 비우고 복사본을 순회한다.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            _elapsed = 0f;

            var toRelease = new List<GameObject>(_activeAttacks);
            _activeAttacks.Clear();

            if (DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                foreach (GameObject instance in toRelease)
                {
                    if (instance != null)
                    {
                        pool.Release(instance);
                    }
                }
            }
        }

        /// <summary>
        /// SoldierRescueSniperAttack이 스스로 반납될 때(명중 또는 빗나감) 추적 목록에서 뺀다.
        /// </summary>
        public void NotifyAttackReleased(GameObject instance)
        {
            _activeAttacks.Remove(instance);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isActive)
            {
                return;
            }

            _elapsed += deltaTime;

            if (_elapsed < _interval)
            {
                return;
            }

            _elapsed -= _interval;
            SpawnAttack();
        }

        /// <summary>
        /// 플레이어의 현재 위치를 관통하는 무작위 각도의 선을 만들어(줌 최소 기준 고정 범위 —
        /// Services.CameraFollowService — 양 끝까지) 저격 공격 하나를 스폰한다.
        /// </summary>
        private void SpawnAttack()
        {
            if (attackPrefab == null || playerTransform == null || !DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                return;
            }

            pool.EnsurePool(attackPrefab, poolCapacity, poolMaxSize);

            Vector3 center;
            Vector2 halfExtent;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CameraFollowService followService))
            {
                center = followService.HomeLocalPosition;
                halfExtent = followService.GetWorldBoundsHalfExtent();
            }
            else
            {
                center = Vector3.zero;
                halfExtent = new Vector2(8f, 16f);
            }

            float angleDegrees = Random.Range(0f, 360f);
            var direction = new Vector2(Mathf.Cos(angleDegrees * Mathf.Deg2Rad), Mathf.Sin(angleDegrees * Mathf.Deg2Rad));
            Vector3 origin = playerTransform.position;

            float forwardDistance = CameraVisibility.DistanceToBoundsEdge(origin, direction, center, halfExtent);
            float backwardDistance = CameraVisibility.DistanceToBoundsEdge(origin, -direction, center, halfExtent);

            Vector3 pointA = origin - (Vector3)(direction * backwardDistance);
            Vector3 pointB = origin + (Vector3)(direction * forwardDistance);

            GameObject instance = pool.Get(attackPrefab, Vector3.zero, Quaternion.identity);

            if (!instance.TryGetComponent(out SoldierRescueSniperAttack attack))
            {
                pool.Release(instance);
                return;
            }

            attack.Launch(pointA, pointB, playerTransform, _playerKnockback, center, halfExtent, this);
            _activeAttacks.Add(instance);
        }
    }
}
