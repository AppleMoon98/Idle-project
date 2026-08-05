using System;
using System.Globalization;
using Core;

namespace UI
{
    /// <summary>
    /// BigNumber 값을 한국어 축약 표기("12.34억")로 변환하는 단일 지점.
    /// 만/억/조/경/해까지는 한글 단위를 쓰고, 그 이상(10^24, 자)부터는 실제로 아는 사람이
    /// 거의 없으므로 과학적 표기("1.23e24")로 자동 전환한다. 골드를 표시하는 모든 UI가
    /// 이 헬퍼를 공유해서, 단위 체계가 바뀌어도 손댈 곳을 한 곳으로 줄인다.
    /// </summary>
    public static class KoreanNumberFormatter
    {
        // 두 임계값은 BigNumber.TruncateToDisplayPrecision과 공유해야 하는 값이라 Core에 정의돼 있다.
        private const long PlainIntegerExponentLimit = BigNumber.DisplayPlainIntegerExponentLimit;
        private const long ScientificFallbackExponent = BigNumber.DisplayScientificFallbackExponent;

        private static readonly (long Threshold, string Unit)[] Units =
        {
            (20, "해"),
            (16, "경"),
            (12, "조"),
            (8, "억"),
            (4, "만"),
        };

        public static string Format(BigNumber value)
        {
            if (value.Exponent < PlainIntegerExponentLimit)
            {
                long whole = (long)Math.Round(value.ToDouble(), MidpointRounding.AwayFromZero);
                return whole.ToString(CultureInfo.InvariantCulture);
            }

            if (value.Exponent >= ScientificFallbackExponent)
            {
                return FormatScientific(value);
            }

            foreach ((long threshold, string unit) in Units)
            {
                if (value.Exponent < threshold)
                {
                    continue;
                }

                double scaled = value.Mantissa * Math.Pow(10.0, value.Exponent - threshold);
                return $"{FormatScaledMagnitude(scaled)}{unit}";
            }

            // 이론상 도달하지 않는 안전망(모든 Units 임계값보다 작은 경우는 위에서 이미 처리됨).
            return value.ToDouble().ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatScientific(BigNumber value)
        {
            // 과학적 표기의 가수는 정규화 규칙상 항상 [1, 10) 범위(=정수부 1자리)라
            // FormatScaledMagnitude의 "1~2자리" 분기와 항상 같은 결과(소수점 둘째 자리)가 나온다.
            return $"{FormatScaledMagnitude(value.Mantissa)}e{value.Exponent.ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// 단위(만/억/조/...) 앞에 붙는 스케일된 값을 정수부 자릿수에 따라 다르게 표시한다:
        /// 1~2자리(1~99)는 기존과 동일하게 소수점 둘째 자리까지, 3자리(100~999)는 소수점 첫째
        /// 자리까지만, 4자리(1000~9999)는 소수점 없이 정수로 표시한다 - 값이 커질수록 소수점
        /// 이하의 정밀도가 상대적으로 덜 중요해지므로 표시 폭이 과도하게 늘어나지 않게 한다.
        /// </summary>
        private static string FormatScaledMagnitude(double scaled)
        {
            if (scaled >= 1000.0)
            {
                return TruncateToDecimals(scaled, 0).ToString("0", CultureInfo.InvariantCulture);
            }

            if (scaled >= 100.0)
            {
                return TruncateToDecimals(scaled, 1).ToString("0.0", CultureInfo.InvariantCulture);
            }

            return TruncateToDecimals(scaled, 2).ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 반올림이 아니라 잘라내기. "12.3456억"을 "12.35억"이 아니라 "12.34억"으로 보여주기 위함
        /// (방치형 게임에서 실제 보유량보다 커 보이는 반올림 표기를 피하는 흔한 관례). 이미 한 번
        /// 잘린 값(예: BigNumber.TruncateToDisplayPrecision을 거친 비용)을 다시 여기서 자를 때
        /// 부동소수점 표현 오차로 한 자리 더 낮게 잘리지 않도록, Truncate 직전에 소수점 6자리로
        /// 반올림해 그 잡음을 제거한다(BigNumber.TruncateWithoutFloatingNoise와 동일한 이유).
        /// </summary>
        private static double TruncateToDecimals(double value, int decimalPlaces)
        {
            double factor = Math.Pow(10.0, decimalPlaces);
            double scaled = Math.Round(value * factor, 6, MidpointRounding.AwayFromZero);
            return Math.Truncate(scaled) / factor;
        }
    }
}
