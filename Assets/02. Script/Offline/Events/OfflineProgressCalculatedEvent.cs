using System.Collections.Generic;
using Equipment;

namespace Offline.Events
{
    /// <summary>
    /// 오프라인 보상 계산이 끝나고 결과가 게임 상태에 반영된 뒤 발행되는 이벤트.
    /// UI가 이 이벤트를 구독해 결과 팝업을 표시한다.
    /// </summary>
    public readonly struct OfflineProgressCalculatedEvent
    {
        /// <summary>
        /// 오프라인으로 인정된 시간(초). 최대 인정 시간으로 clamp된 값이다.
        /// </summary>
        public float ElapsedSeconds { get; }

        /// <summary>
        /// 획득한 골드.
        /// </summary>
        public int GoldEarned { get; }

        /// <summary>
        /// 획득한 장비 목록.
        /// </summary>
        public IReadOnlyList<EquipmentSO> EquipmentEarned { get; }

        /// <summary>
        /// 처치한 것으로 계산된 몬스터 수.
        /// </summary>
        public int MonstersKilled { get; }

        /// <summary>
        /// 새로 클리어한 스테이지 수.
        /// </summary>
        public int StagesCleared { get; }

        /// <summary>
        /// 계산 종료 시점의 챕터 번호.
        /// </summary>
        public int FinalChapter { get; }

        /// <summary>
        /// 계산 종료 시점의 챕터 내 스테이지 번호.
        /// </summary>
        public int FinalStageNumber { get; }

        public OfflineProgressCalculatedEvent(
            float elapsedSeconds,
            int goldEarned,
            IReadOnlyList<EquipmentSO> equipmentEarned,
            int monstersKilled,
            int stagesCleared,
            int finalChapter,
            int finalStageNumber)
        {
            ElapsedSeconds = elapsedSeconds;
            GoldEarned = goldEarned;
            EquipmentEarned = equipmentEarned;
            MonstersKilled = monstersKilled;
            StagesCleared = stagesCleared;
            FinalChapter = finalChapter;
            FinalStageNumber = finalStageNumber;
        }
    }
}
