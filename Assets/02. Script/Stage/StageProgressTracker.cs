using System.Collections.Generic;
using Character;
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
        private readonly Dictionary<GameObject, Vector3> _aliveMonsters = new();
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
            _events.Publish(new StageProgressChangedEvent(_totalToClear - _killCount, _totalToClear));
        }

        /// <summary>
        /// 스포너가 새로 스폰한 몬스터를 추적 대상으로 등록한다. spawnPosition은 오버레이(던전 등)에서
        /// 돌아올 때 위치를 되돌리기 위해 함께 기억해둔다(SetActiveAll 참고).
        /// </summary>
        public void RegisterSpawned(GameObject monster, Vector3 spawnPosition)
        {
            _aliveMonsters[monster] = spawnPosition;
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. 스테이지 종료 시 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        /// <summary>
        /// 살아있는 몬스터를 죽음 이벤트/보상 없이 전부 활성/비활성 전환한다. 던전 같은 오버레이가
        /// 잠깐 화면을 차지하는 동안 스테이지를 "일시정지 + 숨김" 상태로 두었다가 그대로 되돌리기
        /// 위한 것으로, 추적 중인 살아있는 개체 집합 자체는 건드리지 않는다.
        /// 다시 활성화할 때(active=true)는 위치를 스폰 당시 좌표로, 체력을 최대치로 되돌린다 -
        /// 오버레이 도중 플레이어가 입힌 피해나 몬스터 자신의 이동이 그대로 남아있으면(실사용 중
        /// 발견) 오버레이 진입 전과 다른 상태로 스테이지가 재개된다. Character.StagePositionResetter가
        /// Player/Soldier에 대해 이미 하는 것과 같은 방향의 리셋을 몬스터 쪽에도 적용한 것이다.
        /// </summary>
        public void SetActiveAll(bool active)
        {
            foreach (KeyValuePair<GameObject, Vector3> entry in _aliveMonsters)
            {
                GameObject monster = entry.Key;

                if (monster == null)
                {
                    continue;
                }

                if (active)
                {
                    monster.transform.position = entry.Value;

                    if (monster.TryGetComponent(out Health health))
                    {
                        health.Revive();
                    }
                }

                monster.SetActive(active);
            }
        }

        /// <summary>
        /// 아직 살아있는(=클리어에 실패하고 스테이지가 전환된) 몬스터를 보상 없이 풀로 반납한다.
        /// 정상적으로 클리어된 경우엔 이미 전부 죽어있어 사실상 아무 일도 하지 않는다.
        /// </summary>
        public void ReleaseRemaining(PoolManager pool)
        {
            foreach (GameObject monster in _aliveMonsters.Keys)
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

            _events.Publish(new StageProgressChangedEvent(_totalToClear - _killCount, _totalToClear));

            if (_killCount >= _totalToClear)
            {
                _events.Publish(new StageClearedEvent(_stage));
            }
        }

        /// <summary>
        /// 이 스테이지에서 실제로 스폰될 총 마릿수를 계산한다. SpawnEntries(일반/엘리트/보스)뿐
        /// 아니라 TacticEntries(전술 대형)도 반드시 포함해야 한다 - 빠뜨리면 대형 전체를 잡지
        /// 않고도 스테이지가 클리어로 잘못 판정될 수 있다(_killCount는 대형 유닛의 죽음도
        /// 그대로 세지만, 이 합계가 그만큼 낮게 잡히면 그보다 훨씬 적게 죽여도 조건을
        /// 만족해버린다). 전술 쌍의 수는 TacticSpawnEntry.PairCount(GitHub 이슈 #33 - Stage.
        /// MonsterSpawner.TickTactics/Offline.OfflineStageSimulator와 공유하는 단일 진실 공급원)를
        /// 그대로 써야 실제로 스폰되는 마릿수와 어긋나지 않는다 - 홀수 총량일 때 원래 값을 그대로
        /// 더하면 스포너보다 1 많게 잡혀 영원히 클리어할 수 없는 스테이지가 된다.
        /// </summary>
        private static int CalculateTotal(StageSO stage)
        {
            int total = 0;

            foreach (MonsterSpawnEntry entry in stage.SpawnEntries)
            {
                total += entry.Count;
            }

            if (stage.TacticEntries != null)
            {
                foreach (TacticSpawnEntry tacticEntry in stage.TacticEntries)
                {
                    total += tacticEntry.PairCount * 2;
                }
            }

            return total;
        }
    }
}
