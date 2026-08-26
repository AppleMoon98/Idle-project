using System;
using System.Collections.Generic;
using Core;
using Soldier.Events;

namespace Soldier
{
    /// <summary>
    /// 부대(0 ~ SquadCount-1)마다 어떤 전술이 배정돼 있는지만 저장한다. 기본값은 전 부대
    /// None(전술 없음). 실제 전술 효과(대형 재배치 등)는 이 서비스가 아니라 그 전술을 구독하는
    /// 별도 조율자(예: SquadShieldWallCoordinator)가 담당한다 — Enhancement.EnhancementService가
    /// 레벨/비용만 갖고 실제 스탯 적용은 StatEnhancementReceiver에게 맡기는 것과 같은 "서비스는
    /// 상태만, 적용은 구독자가" 분리.
    ///
    /// Save.SaveService가 ExportSnapshot/RestoreSnapshot으로 세이브한다(과거엔 Stage.StageModeService와
    /// 같은 이유로 세이브하지 않는 세션 단위 설정으로 취급했으나, 실사용 중 "설정한 전술이 재시작하면
    /// 사라진다"는 문제로 지적돼 영구 저장으로 전환했다).
    /// </summary>
    public sealed class SquadTacticService : IManager, IService
    {
        /// <summary>
        /// 전술이 배정된 부대 하나를 세이브 데이터로 직렬화하기 위한 형태. None인 부대는
        /// ExportSnapshot이 아예 담지 않으므로(하나도 없으면 빈 배열), None 자체는 나타나지 않는다.
        /// </summary>
        [Serializable]
        public struct SquadTacticSnapshotEntry
        {
            public int SquadIndex;
            public SquadTacticType Tactic;
        }

        private readonly EventBus _events;
        private readonly Dictionary<int, SquadTacticType> _tactics = new();

        public SquadTacticService(EventBus events)
        {
            _events = events;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            _tactics.Clear();
        }

        /// <summary>
        /// squadIndex에 배정된 전술. 아직 아무것도 고르지 않았으면 None.
        /// </summary>
        public SquadTacticType GetTactic(int squadIndex)
        {
            return _tactics.TryGetValue(squadIndex, out SquadTacticType tactic) ? tactic : SquadTacticType.None;
        }

        /// <summary>
        /// squadIndex의 전술을 바꾼다. 실제로 값이 바뀔 때만 SquadTacticChangedEvent를 발행한다.
        /// GitHub 이슈 #26 - squadIndex가 범위(0 ~ SquadCount-1) 밖이거나 tactic이 정의되지 않은
        /// enum 값이면 아무 것도 저장하지 않고 이벤트도 발행하지 않는다(조용히 무시) - 이 유일한
        /// 공개 진입점에서 막아두면, 어떤 호출부(UI/세이브 복원)가 잘못된 값을 넘겨도
        /// SquadTacticChangedEvent 소비자(SquadRaidCoordinator 등)가 범위 밖 인덱스를 절대
        /// 받지 않는다.
        /// </summary>
        public void SetTactic(int squadIndex, SquadTacticType tactic)
        {
            if (!IsValid(squadIndex, tactic))
            {
                return;
            }

            if (GetTactic(squadIndex) == tactic)
            {
                return;
            }

            _tactics[squadIndex] = tactic;
            _events?.Publish(new SquadTacticChangedEvent(squadIndex, tactic));
        }

        private static bool IsValid(int squadIndex, SquadTacticType tactic)
        {
            return squadIndex >= 0
                && squadIndex < SoldierDeploymentService.SquadCount
                && Enum.IsDefined(typeof(SquadTacticType), tactic);
        }

        /// <summary>
        /// 6개 백엔드 부대 전체에 같은 전술을 적용한다 — 배치 UI가 더 이상 부대를 개별 선택하지
        /// 않으므로(단일 풀), 전술 선택도 부대별이 아니라 전체 단위 하나로 통합됐다. 내부적으로는
        /// SetTactic을 부대마다 호출할 뿐이라 부대별 저장 구조(_tactics)는 그대로 유지된다.
        /// </summary>
        public void SetTacticForAll(SquadTacticType tactic)
        {
            for (int i = 0; i < SoldierDeploymentService.SquadCount; i++)
            {
                SetTactic(i, tactic);
            }
        }

        /// <summary>
        /// 현재 전술이 배정된 부대(None 제외) 전체를 세이브용 스냅샷으로 내보낸다.
        /// </summary>
        public SquadTacticSnapshotEntry[] ExportSnapshot()
        {
            var snapshot = new List<SquadTacticSnapshotEntry>();

            foreach (KeyValuePair<int, SquadTacticType> pair in _tactics)
            {
                if (pair.Value != SquadTacticType.None)
                {
                    snapshot.Add(new SquadTacticSnapshotEntry { SquadIndex = pair.Key, Tactic = pair.Value });
                }
            }

            return snapshot.ToArray();
        }

        /// <summary>
        /// RestoreSnapshot이 발견한 문제를 요약한 진단 결과(GitHub 이슈 #26) - 다른 두 병사 도메인
        /// 서비스(SoldierRosterService/SoldierDeploymentService)의 RestoreResult와 같은 모양.
        /// </summary>
        public readonly struct RestoreResult
        {
            public readonly int RestoredCount;
            public readonly int DiscardedInvalidEntry;

            public RestoreResult(int restoredCount, int discardedInvalidEntry)
            {
                RestoredCount = restoredCount;
                DiscardedInvalidEntry = discardedInvalidEntry;
            }

            public bool HasDiscardedEntries => DiscardedInvalidEntry > 0;
        }

        /// <summary>
        /// 세이브 스냅샷으로 부대별 전술을 복원한다. 이벤트를 발행하지 않는다(시딩이지 게임플레이
        /// 변화가 아니다 - InventoryService.RestoreSnapshot과 같은 관례). GitHub 이슈 #26 - 기존
        /// 배정을 먼저 비워 재복원 시 잔존 항목이 남지 않게 하고, SquadIndex 범위(0 ~ SquadCount-1
        /// 밖) 또는 정의되지 않은 Tactic enum 값을 가진 항목 하나가 손상돼 있어도 그 항목만 버리고
        /// 나머지 유효한 항목은 그대로 복원한다(하나가 깨졌다고 전체를 포기하지 않음).
        /// </summary>
        public RestoreResult RestoreSnapshot(SquadTacticSnapshotEntry[] snapshot)
        {
            _tactics.Clear();

            if (snapshot == null)
            {
                return new RestoreResult(0, 0);
            }

            int discarded = 0;

            foreach (SquadTacticSnapshotEntry entry in snapshot)
            {
                if (!IsValid(entry.SquadIndex, entry.Tactic))
                {
                    discarded++;
                    continue;
                }

                _tactics[entry.SquadIndex] = entry.Tactic;
            }

            return new RestoreResult(_tactics.Count, discarded);
        }
    }
}
