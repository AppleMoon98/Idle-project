using System;
using Core;

namespace Gacha
{
    /// <summary>
    /// "지금 잔액으로 이 티어를 몇 회 연속으로 뽑을 수 있는가"를 계산하는 공용 헬퍼(GitHub 이슈
    /// #22 - 버튼/재화 텍스트에 정확한 실행 가능 횟수를 표시). 골드 티어처럼 회차마다 비용이
    /// CostIncrementTier로 계속 오르는 경우 단순 나눗셈으로는 구할 수 없어, 실제로 한 회씩
    /// 차감해가며 세야 한다(시뮬레이션). 비용이 고정이면(가장 흔한 경우 - 무기 뽑기, 소환권/
    /// 주문서/뽑기권 전부) 나눗셈 한 번으로 정확히 계산한다.
    /// </summary>
    public static class GachaAffordabilityCalculator
    {
        /// <summary>
        /// 회차마다 비용이 오르는 시뮬레이션의 반복 상한. 이 값 이상 계속 살 수 있으면 그 이상은
        /// 세지 않고 이 값 그대로 반환한다(호출부가 "{N}회 이상"으로 표시) - 이 프로젝트의 모든
        /// 뽑기 버튼 중 가장 큰 값(300)보다 충분히 여유 있게 잡았다.
        /// </summary>
        public const int MaxSimulatedPulls = 10000;

        /// <summary>
        /// getCostForPull(pullsSoFar + i)로 회차별 비용을 조회하며 balance가 버티는 한 계속
        /// 카운트한다. 비용이 매 회 똑같다면(가장 흔한 "고정 비용" 케이스) 반복 없이 한 번의
        /// 나눗셈으로 계산한다 - BigNumber에는 나눗셈 연산자가 없어 ToDouble()(문서화된 "표시/근사
        /// 용도")로 근사한다. 실제 재화 차감(TrySpendGold)은 뽑기 시점에 그대로 정확한 BigNumber
        /// 비교로 이뤄지므로, 여기서의 근사는 오직 이 화면 표시값에만 영향을 준다. 비용이 회차마다
        /// 바뀌면(CostIncrementTier가 채워진 골드 티어) MaxSimulatedPulls까지 실제로 시뮬레이션한다.
        /// </summary>
        public static int CalculateMaxAffordableGoldPulls(BigNumber balance, int pullsSoFar, Func<int, int> getCostForPull)
        {
            int firstCost = getCostForPull(pullsSoFar);

            if (firstCost <= 0)
            {
                return 0;
            }

            if (getCostForPull(pullsSoFar + 1) == firstCost)
            {
                double approxCount = Math.Floor(Math.Max(0.0, balance.ToDouble() / firstCost));
                return approxCount >= MaxSimulatedPulls ? MaxSimulatedPulls : (int)approxCount;
            }

            int affordable = 0;
            BigNumber remaining = balance;

            while (affordable < MaxSimulatedPulls)
            {
                int cost = getCostForPull(pullsSoFar + affordable);

                if (cost <= 0 || remaining < (BigNumber)cost)
                {
                    break;
                }

                remaining -= cost;
                affordable++;
            }

            return affordable;
        }

        /// <summary>
        /// 소환권/주문서/뽑기권처럼 회차와 무관하게 항상 같은 정수 비용인 재화의 실행 가능 횟수.
        /// </summary>
        public static int CalculateMaxAffordableFixedCostPulls(int balance, int costPerPull)
        {
            return costPerPull > 0 ? balance / costPerPull : 0;
        }
    }
}
