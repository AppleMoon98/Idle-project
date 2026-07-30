using System.Collections.Generic;
using Character.Events;
using Core;
using Managers;
using Stage.Events;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 스폰된 몬스터의 처치 여부를 추적해 스테이지 클리어를 판정한다.
    /// </summary>
    public sealed class StageProgressTracker
    {
        private readonly HashSet<GameObject> _aliveMonsters = new();
        private readonly StageSO _stage;
        private readonly EventBus _events;
        private readonly int _totalToClear;
        private int _killCount;

        public StageProgressTracker(StageSO stage, EventBus events)
        {
            _stage = stage;
            _events = events;
            _totalToClear = CalculateTotal(stage);

            _events.Subscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        /// <summary>
        /// 스포너가 새로 스폰한 몬스터를 추적 대상으로 등록한다.
        /// </summary>
        public void RegisterSpawned(GameObject monster)
        {
            _aliveMonsters.Add(monster);
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. 스테이지 종료 시 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        /// <summary>
        /// 아직 살아있는(=클리어에 실패하고 스테이지가 전환된) 몬스터를 보상 없이 풀로 반납한다.
        /// 정상적으로 클리어된 경우엔 이미 전부 죽어있어 사실상 아무 일도 하지 않는다.
        /// </summary>
        public void ReleaseRemaining(PoolManager pool)
        {
            foreach (GameObject monster in _aliveMonsters)
            {
                if (monster != null)
                {
                    pool.Release(monster);
                }
            }

            _aliveMonsters.Clear();
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (!_aliveMonsters.Remove(evt.Character))
            {
                return;
            }

            _killCount++;

            if (_killCount >= _totalToClear)
            {
                _events.Publish(new StageClearedEvent(_stage));
            }
        }

        private static int CalculateTotal(StageSO stage)
        {
            int total = 0;

            foreach (MonsterSpawnEntry entry in stage.SpawnEntries)
            {
                total += entry.Count;
            }

            return total;
        }
    }
}
