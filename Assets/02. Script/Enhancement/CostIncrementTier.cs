using System;
using System.Collections.Generic;
using UnityEngine;

namespace Enhancement
{
    /// <summary>
    /// 계단식 강화 비용 구간 하나. LevelThreshold 이상인 레벨부터 강화 1회당 Increment만큼
    /// 비용이 증가한다. EnhancementConfigSO.CostIncrementTiers가 비어있으면 이 구간 방식 대신
    /// 기존 복리(BaseCost * CostMultiplier^level) 방식을 사용한다.
    /// </summary>
    [Serializable]
    public sealed class CostIncrementTier
    {
        [SerializeField]
        [Min(0)]
        private int levelThreshold;

        [SerializeField]
        [Min(0)]
        private int increment;

        /// <summary>
        /// 이 구간이 시작되는 레벨.
        /// </summary>
        public int LevelThreshold => levelThreshold;

        /// <summary>
        /// 이 구간에서 강화 1회당 늘어나는 비용.
        /// </summary>
        public int Increment => increment;

        /// <summary>
        /// 구간별로 1회당 비용 증가폭이 달라지는 계단식 누적 비용. 각 구간은 자기 시작 지점부터
        /// 다음 구간 시작 지점(또는 마지막 구간이면 끝)까지 걸쳐있는 횟수만큼만 기여한다.
        /// tiers가 비어있으면(레거시 방식) baseValue 그대로 반환한다. Enhancement.EnhancementService,
        /// Gacha.GachaTableSO/SkillGachaTableSO가 공유하는 순수 계산 로직 — 세 곳 모두 이 형태의
        /// 계단식 누적을 각자 복제해서 갖고 있던 것을 하나로 뽑았다.
        ///
        /// **tiers의 LevelThreshold가 오름차순으로 정렬돼 있다고 가정한다** - 이 계산 자체는
        /// 정렬 여부를 검증하지 않는다(런타임 성능 경로라 순수 계산만 담당). 배열이 역순이거나
        /// 임계값이 중복되면 조용히 잘못된(작거나 0인) 결과를 낼 수 있다 - 콘텐츠 단계의 검증은
        /// Editor.ContentCostValidation.ValidateCostIncrementTiers가 담당한다(GitHub 이슈 #67).
        ///
        /// **long 경계 오버플로는 포화 처리한다(GitHub 이슈 #67):** 누산 도중 long 범위를 넘으면
        /// 음수로 반전되는 대신 long.MaxValue에서 멈춘다 - Core.BigNumber.SaturatingAdd와 같은
        /// "checked 연산 + OverflowException 포화" 관례를 그대로 재사용했다. Increment/count는
        /// 항상 0 이상이므로(음수는 CheckNonNegative가 이미 콘텐츠 단계에서 거부) 오버플로 방향은
        /// 항상 양의 방향뿐이다.
        /// </summary>
        public static long CalculateTotal(long baseValue, IReadOnlyList<CostIncrementTier> tiers, int count)
        {
            if (tiers == null || tiers.Count == 0)
            {
                return baseValue;
            }

            long total = baseValue;

            for (int i = 0; i < tiers.Count; i++)
            {
                int tierStart = tiers[i].LevelThreshold;
                int tierEnd = i + 1 < tiers.Count ? tiers[i + 1].LevelThreshold : int.MaxValue;
                int countInTier = Mathf.Max(0, Mathf.Min(count, tierEnd) - tierStart);

                total = SaturatingAdd(total, SaturatingMultiply(countInTier, tiers[i].Increment));
            }

            return total;
        }

        private static long SaturatingMultiply(long left, long right)
        {
            try
            {
                return checked(left * right);
            }
            catch (OverflowException)
            {
                return (left >= 0) == (right >= 0) ? long.MaxValue : long.MinValue;
            }
        }

        private static long SaturatingAdd(long left, long right)
        {
            try
            {
                return checked(left + right);
            }
            catch (OverflowException)
            {
                return right >= 0 ? long.MaxValue : long.MinValue;
            }
        }
    }
}
