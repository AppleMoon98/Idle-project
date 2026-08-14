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
    /// 맵 안 무작위 위치(줌 배율과 무관한 고정 플레이 범위 기준, Dungeon.DungeonSpawnUtility 재사용)
    /// 에 SkillSO.MeteorShellCount개의 포탄을 동시에 떨어뜨린다. 예고 표시는 War.Boss.
    /// WarBossTelegraphIndicator를 그대로 재사용한다(War 전용 컴포넌트가 아니라 "위치/반경을 받아
    /// 원형 경고를 그리는" 순수 시각 컴포넌트라 도메인 의존 없이 재사용 가능). SkillSO.
    /// MeteorTelegraphDuration 뒤 그 자리에 남아있는 적 전체에게 (시전자 현재 공격력 + magnitude)
    /// 만큼의 피해를 준다. 여러 포탄이 동시에 서로 다른 진행도로 카운트다운되므로
    /// War.Boss.WarBossPatternRunner(포탄 하나만 순차 처리)와 달리 리스트로 여러 개를 동시에 추적한다.
    /// </summary>
    [RequireComponent(typeof(SkillSlot))]
    public sealed class MeteorSkillEffect : MonoBehaviour, ISkillEffect, ITickable
    {
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

            for (int i = 0; i < definition.MeteorShellCount; i++)
            {
                Vector3 position = DungeonSpawnUtility.RandomWithinPlayAreaPosition(spawnMargin);

                GameObject instance = _pool.Get(telegraphIndicatorPrefab, position, Quaternion.identity);
                var indicator = instance.GetComponent<WarBossTelegraphIndicator>();
                indicator.Show(position, definition.AreaRadius);

                _activeShells.Add(new Shell
                {
                    Position = position,
                    Elapsed = 0f,
                    IndicatorInstance = instance,
                    IndicatorComponent = indicator
                });
            }
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

            ReleaseShell(shell);
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
