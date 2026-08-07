using System.Collections.Generic;
using Character;
using Character.Events;
using Core;
using Dungeon.Events;
using Loot.Events;
using Managers;
using Stage;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 골드 던전 한 판의 진행을 관리한다. 화면 안 랜덤 위치에 비공격 몬스터를 스폰하고,
    /// 처치할 때마다 고정 골드(GoldPerKillPerStage × 선택 단계)를 지급하며, 전멸하거나
    /// 제한시간이 끝나면 원래 스테이지로 복귀시킨다. StageSO/StageProgression 파이프라인은
    /// 전혀 건드리지 않는다 — StageController.PauseForOverlay/ResumeAfterOverlay로 기존
    /// 스테이지를 잠깐 숨기고 되돌릴 뿐이다. 죽은 몬스터 자신의 반납은 PoolReleaseOnDeath가
    /// 처리하므로, 여기서는 시간 종료로 남아있는 몬스터만 직접 반납한다.
    /// </summary>
    public sealed class GoldDungeonSessionController : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GoldDungeonConfigSO config;

        [SerializeField]
        private StageController stageController;

        private readonly HashSet<GameObject> _aliveMonsters = new();

        private int _stageNumber;
        private float _remainingTime;
        private bool _isActive;

        /// <summary>
        /// 현재 던전이 진행 중인지 여부.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// 골드 던전을 시작한다. stageNumber는 보상 계산에 쓰인다. 이미 진행 중이면 무시한다.
        /// </summary>
        public void Enter(int stageNumber)
        {
            if (_isActive || config == null || config.MonsterPrefab == null)
            {
                return;
            }

            _isActive = true;
            _stageNumber = stageNumber;
            _remainingTime = config.TimeLimitSeconds;

            stageController?.PauseForOverlay();

            SpawnMonsters();

            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Register(this);

            GameBootstrapper.Events?.Publish(new GoldDungeonSessionStartedEvent(_stageNumber, _aliveMonsters.Count, _remainingTime));
            GameBootstrapper.Events?.Publish(new GoldDungeonProgressChangedEvent(_aliveMonsters.Count));
        }

        private void SpawnMonsters()
        {
            if (!DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                return;
            }

            pool.EnsurePool(config.MonsterPrefab, config.MonsterCount, config.MonsterCount);

            for (int i = 0; i < config.MonsterCount; i++)
            {
                Vector3 spawnPosition = DungeonSpawnUtility.RandomOnScreenPosition(config.SpawnViewportMargin);
                GameObject instance = pool.Get(config.MonsterPrefab, spawnPosition, Quaternion.identity);

                if (instance.TryGetComponent(out StageMonsterScaler scaler))
                {
                    scaler.ApplyScale(config.CalculateMonsterStatMultiplier(_stageNumber));
                }

                _aliveMonsters.Add(instance);
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isActive)
            {
                return;
            }

            _remainingTime -= deltaTime;

            if (_remainingTime <= 0f)
            {
                EndSession(cleared: false);
            }
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (!_aliveMonsters.Remove(evt.Character))
            {
                return;
            }

            GameBootstrapper.Events?.Publish(new GoldEarnedEvent(config.GoldPerKillPerStage * _stageNumber));
            GameBootstrapper.Events?.Publish(new GoldDungeonProgressChangedEvent(_aliveMonsters.Count));

            if (_aliveMonsters.Count <= 0)
            {
                EndSession(cleared: true);
            }
        }

        private void EndSession(bool cleared)
        {
            _isActive = false;

            GameBootstrapper.Events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Unregister(this);

            ReleaseRemainingMonsters();

            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new GoldDungeonSessionEndedEvent(cleared));
        }

        private void ReleaseRemainingMonsters()
        {
            if (DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                foreach (GameObject monster in _aliveMonsters)
                {
                    if (monster != null)
                    {
                        pool.Release(monster);
                    }
                }
            }

            _aliveMonsters.Clear();
        }

        private void OnDestroy()
        {
            if (_isActive)
            {
                EndSession(cleared: false);
            }
        }
    }
}
