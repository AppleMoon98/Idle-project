using System;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 계단식 스테이지 난이도 배율 구간 하나. StageIndexThreshold 이상인 카탈로그 인덱스부터
    /// 스테이지 1칸당 MultiplierPerIndex만큼 배율이 증가한다. Enhancement.CostIncrementTier와
    /// 같은 "구간별 기울기" 개념이지만 그쪽은 레벨/정수 증가폭이고 이쪽은 스테이지 인덱스/실수
    /// 기울기라 필드 타입이 달라 별도 클래스로 뒀다(SoldierEnhancement가 Enhancement와 구조만
    /// 같고 독립 구현인 것과 같은 이유).
    /// </summary>
    [Serializable]
    public sealed class StageDifficultyTier
    {
        [SerializeField]
        private int stageIndexThreshold;

        [SerializeField]
        private float multiplierPerIndex;

        /// <summary>
        /// 이 구간이 시작되는 카탈로그 인덱스(0부터).
        /// </summary>
        public int StageIndexThreshold => stageIndexThreshold;

        /// <summary>
        /// 이 구간에서 스테이지 인덱스 1칸당 늘어나는 배율.
        /// </summary>
        public float MultiplierPerIndex => multiplierPerIndex;
    }
}
