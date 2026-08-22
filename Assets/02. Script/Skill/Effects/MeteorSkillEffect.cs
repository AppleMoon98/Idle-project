using System.Collections.Generic;
using Character;
using Combat;
using Core;
using Dungeon;
using Managers;
using Services;
using UnityEngine;
using War.Boss;

namespace Skill.Effects
{
    /// <summary>
    /// 맵 안(줌 배율과 무관한 고정 플레이 범위 기준, Dungeon.DungeonSpawnUtility 재사용)에
    /// SkillSO.MeteorShellCount개의 포탄을 동시에 떨어뜨린다. 낙하 위치는 ResolvePositions가
    /// 정한다 - 그 절반은 적을 노리고 나머지는 완전 무작위이되, 어떤 경우든 포탄끼리 범위(반지름
    /// AreaRadius인 원)가 서로 겹치지 않는다. 예고 표시는 War.Boss.WarBossTelegraphIndicator를
    /// 그대로 재사용한다(War 전용 컴포넌트가 아니라 "위치/반경을 받아 원형 경고를 그리는" 순수
    /// 시각 컴포넌트라 도메인 의존 없이 재사용 가능). SkillSO.MeteorTelegraphDuration 뒤 그
    /// 자리에 남아있는 적 전체에게 (시전자 현재 공격력 + magnitude)만큼의 피해를 준다. 여러
    /// 포탄이 동시에 서로 다른 진행도로 카운트다운되므로 War.Boss.WarBossPatternRunner(포탄
    /// 하나만 순차 처리)와 달리 리스트로 여러 개를 동시에 추적한다.
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public sealed class MeteorSkillEffect : MonoBehaviour, ISkillEffect, ITickable
    {
        /// <summary>
        /// 플레이어 자신이 시전하는 포탄 낙하 예고는 적의 공격 예고(빨강, WarBossTelegraphIndicator
        /// 기본색)와 구분되도록 파란색을 쓴다.
        /// </summary>
        private static readonly Color TelegraphColor = new Color(0.2f, 0.5f, 1f, 1f);

        /// <summary>
        /// 겹치지 않는 무작위 낙하 위치를 찾기 위한 최대 재시도 횟수.
        /// </summary>
        private const int MaxPlacementAttempts = 30;

        private sealed class Shell
        {
            public Vector3 Position;
            public float Elapsed;
            public GameObject IndicatorInstance;
            public WarBossTelegraphIndicator IndicatorComponent;
        }

        [SerializeField]
        private LayerMask targetLayerMask;

        [SerializeField]
        private GameObject telegraphIndicatorPrefab;

        [SerializeField]
        [Range(0f, 0.5f)]
        private float spawnMargin = 0.1f;

        [SerializeField]
        private int telegraphPoolCapacity = 4;

        [SerializeField]
        private int telegraphPoolMaxSize = 8;

        [SerializeField]
        private int vfxPoolCapacity = 2;

        [SerializeField]
        private int vfxPoolMaxSize = 4;

        [SerializeField]
        private GameObject explosionPrefab;

        [SerializeField]
        private int explosionPoolCapacity = 2;

        [SerializeField]
        private int explosionPoolMaxSize = 4;

        private PoolManager _pool;
        private CharacterStatsProvider _statsProvider;
        private SkillSO _definition;
        private float _magnitude;
        private readonly List<Shell> _activeShells = new();

        private void Awake()
        {
            _statsProvider = GetComponentInParent<CharacterStatsProvider>();
        }

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;

                if (telegraphIndicatorPrefab != null)
                {
                    _pool.EnsurePool(telegraphIndicatorPrefab, telegraphPoolCapacity, telegraphPoolMaxSize);
                }

                if (explosionPrefab != null)
                {
                    _pool.EnsurePool(explosionPrefab, explosionPoolCapacity, explosionPoolMaxSize);
                }
            }

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

            ReleaseAllShells();
        }

        /// <summary>
        /// 사거리 개념이 없는 광역 스킬이라, 최광각 고정 범위(줌 배율과 무관) 안에 살아있는 적이
        /// 하나라도 있으면 발동 가능으로 취급한다.
        /// </summary>
        public bool HasTargetInRange(Transform origin, SkillSO definition)
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out CameraFollowService cameraFollow))
            {
                return false;
            }

            Health found = null;
            NearestHealthScan.ForEachAliveCandidateInBounds(cameraFollow.HomeLocalPosition, cameraFollow.GetWorldBoundsHalfExtent(), targetLayerMask, (candidate, health) => found = health);
            return found != null;
        }

        public void Execute(Transform origin, SkillSO definition, float magnitude)
        {
            if (_pool == null || telegraphIndicatorPrefab == null)
            {
                return;
            }

            _definition = definition;
            _magnitude = magnitude;

            List<Vector3> positions = ResolvePositions(definition);

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 position = positions[i];

                GameObject instance = _pool.Get(telegraphIndicatorPrefab, position, Quaternion.identity);
                var indicator = instance.GetComponent<WarBossTelegraphIndicator>();
                indicator.Show(position, definition.AreaRadius, TelegraphColor);

                _activeShells.Add(new Shell
                {
                    Position = position,
                    Elapsed = 0f,
                    IndicatorInstance = instance,
                    IndicatorComponent = indicator
                });
            }
        }

        /// <summary>
        /// 포탄 MeteorShellCount개의 낙하 위치를 정한다 — 어떤 경우든 서로의 범위(반지름
        /// AreaRadius인 원)는 절대 겹치지 않는다. 그 절반(정수 나눗셈, 4발→2발/5발→2발/6발→3발)은
        /// 최광각 고정 범위 안에 살아있는 적 중 무작위로 고른 위치를 노리고, 나머지는 완전
        /// 무작위 위치다. 적을 노리는 포탄이라도 이미 확정된 다른 포탄과 겹치면(예: 두 적이
        /// 서로 붙어있음) 그 적 위치를 포기하고 대신 겹치지 않는 무작위 위치로 대체한다 — "적을
        /// 노린다"보다 "겹치지 않는다"가 우선순위가 더 높은 조건이기 때문이다.
        /// </summary>
        private List<Vector3> ResolvePositions(SkillSO definition)
        {
            int shellCount = definition.MeteorShellCount;
            float minDistance = definition.AreaRadius * 2f;
            List<Vector3> positions = new List<Vector3>(shellCount);

            int targetedCount = shellCount / 2;
            List<Vector3> enemyPositions = CollectRandomEnemyPositions(targetedCount);

            foreach (Vector3 enemyPosition in enemyPositions)
            {
                Vector3 position = OverlapsAny(enemyPosition, positions, minDistance)
                    ? FindNonOverlappingRandomPosition(positions, minDistance)
                    : enemyPosition;

                positions.Add(position);
            }

            int remaining = shellCount - positions.Count;

            for (int i = 0; i < remaining; i++)
            {
                positions.Add(FindNonOverlappingRandomPosition(positions, minDistance));
            }

            return positions;
        }

        /// <summary>
        /// 최광각 고정 범위 안의 살아있는 적 전체를 모은 뒤 무작위로 섞어 최대 count개까지
        /// 반환한다(적이 count보다 적으면 있는 만큼만 - 나머지는 ResolvePositions가 완전 무작위
        /// 포탄으로 자연스럽게 채운다).
        /// </summary>
        private List<Vector3> CollectRandomEnemyPositions(int count)
        {
            List<Vector3> positions = new List<Vector3>();

            if (count <= 0 || GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out CameraFollowService cameraFollow))
            {
                return positions;
            }

            NearestHealthScan.ForEachAliveCandidateInBounds(cameraFollow.HomeLocalPosition, cameraFollow.GetWorldBoundsHalfExtent(), targetLayerMask, (candidate, health) =>
            {
                positions.Add(candidate.transform.position);
            });

            for (int i = positions.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (positions[i], positions[j]) = (positions[j], positions[i]);
            }

            if (positions.Count > count)
            {
                positions.RemoveRange(count, positions.Count - count);
            }

            return positions;
        }

        /// <summary>
        /// existing의 어느 좌표와도 minDistance 이상 떨어진 무작위 위치를 찾는다. 최대
        /// MaxPlacementAttempts번 시도해도 못 찾으면(포탄 수가 지나치게 많아 겹치지 않게 들어갈
        /// 자리 자체가 부족한 극단적인 경우) 마지막 시도 좌표를 그대로 쓴다 - 최선 노력 배치로,
        /// 스킬 발동 자체는 항상 성공해야 한다(Soldier.SoldierRescueDungeonConfigSO의 구역 배치와
        /// 동일한 관례).
        /// </summary>
        private Vector3 FindNonOverlappingRandomPosition(List<Vector3> existing, float minDistance)
        {
            Vector3 candidate = DungeonSpawnUtility.RandomWithinPlayAreaPosition(spawnMargin);

            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                if (!OverlapsAny(candidate, existing, minDistance))
                {
                    return candidate;
                }

                candidate = DungeonSpawnUtility.RandomWithinPlayAreaPosition(spawnMargin);
            }

            return candidate;
        }

        private static bool OverlapsAny(Vector3 position, List<Vector3> existing, float minDistance)
        {
            foreach (Vector3 other in existing)
            {
                if (Vector2.Distance(position, other) < minDistance)
                {
                    return true;
                }
            }

            return false;
        }

        void ITickable.Tick(float deltaTime)
        {
            for (int i = _activeShells.Count - 1; i >= 0; i--)
            {
                Shell shell = _activeShells[i];
                shell.Elapsed += deltaTime;

                float progress = _definition.MeteorTelegraphDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(shell.Elapsed / _definition.MeteorTelegraphDuration);
                shell.IndicatorComponent.SetProgress01(progress);

                if (shell.Elapsed >= _definition.MeteorTelegraphDuration)
                {
                    ResolveShell(shell);
                    _activeShells.RemoveAt(i);
                }
            }
        }

        private void ResolveShell(Shell shell)
        {
            float damage = _statsProvider.Stats.AttackPower + _magnitude;

            NearestHealthScan.ForEachAliveCandidate(shell.Position, _definition.AreaRadius, targetLayerMask, (candidate, health) =>
            {
                health.TakeDamage(damage);
            });

            SkillEffectVfx.SpawnAndPlay(_pool, _definition, shell.Position, vfxPoolCapacity, vfxPoolMaxSize);
            SpawnExplosion(shell.Position);

            ReleaseShell(shell);
        }

        /// <summary>
        /// Combat.ExplosionEffect(공성병 포탄/광역 공격이 쓰는 것과 같은 이펙트, sparse opt-in)를
        /// AreaRadius 크기로 재생한다 - 기존 SkillEffectVfx(SkillSO.VfxPrefab, 스킬별 커스텀
        /// 이펙트)와는 별개의 추가 연출이다.
        /// </summary>
        private void SpawnExplosion(Vector3 position)
        {
            if (explosionPrefab == null || _pool == null)
            {
                return;
            }

            GameObject instance = _pool.Get(explosionPrefab, position, Quaternion.identity);

            if (instance.TryGetComponent(out Combat.ExplosionEffect explosion))
            {
                explosion.Play(position, _definition.AreaRadius);
            }
        }

        private void ReleaseShell(Shell shell)
        {
            if (_pool != null && shell.IndicatorInstance != null)
            {
                _pool.Release(shell.IndicatorInstance);
            }
        }

        private void ReleaseAllShells()
        {
            foreach (Shell shell in _activeShells)
            {
                ReleaseShell(shell);
            }

            _activeShells.Clear();
        }
    }
}
