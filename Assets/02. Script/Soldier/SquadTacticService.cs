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
        /// </summary>
        public void SetTactic(int squadIndex, SquadTacticType tactic)
        {
            if (GetTactic(squadIndex) == tactic)
            {
                return;
            }

            _tactics[squadIndex] = tactic;
            _events?.Publish(new SquadTacticChangedEvent(squadIndex, tactic));
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
        /// 세이브 스냅샷으로 부대별 전술을 복원한다. 이벤트를 발행하지 않는다(시딩이지 게임플레이
        /// 변화가 아니다 - InventoryService.RestoreSnapshot과 같은 관례).
        /// </summary>
        public void RestoreSnapshot(SquadTacticSnapshotEntry[] snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (SquadTacticSnapshotEntry entry in snapshot)
            {
                _tactics[entry.SquadIndex] = entry.Tactic;
            }
        }
    }
}
