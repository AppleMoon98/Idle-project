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
        private const long PlainIntegerExponentLimit = 4; // 10^4(만) 미만은 정수 그대로 표기
        private const long ScientificFallbackExponent = 24; // 10^24(자) 이상은 과학적 표기로 폴백

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
                return $"{TruncateToTwoDecimals(scaled).ToString("0.00", CultureInfo.InvariantCulture)}{unit}";
            }

            // 이론상 도달하지 않는 안전망(모든 Units 임계값보다 작은 경우는 위에서 이미 처리됨).
            return value.ToDouble().ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatScientific(BigNumber value)
        {
            return $"{TruncateToTwoDecimals(value.Mantissa).ToString("0.00", CultureInfo.InvariantCulture)}e{value.Exponent.ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// 반올림이 아니라 잘라내기. "12.3456억"을 "12.35억"이 아니라 "12.34억"으로 보여주기 위함
        /// (방치형 게임에서 실제 보유량보다 커 보이는 반올림 표기를 피하는 흔한 관례).
        /// </summary>
        private static double TruncateToTwoDecimals(double value)
        {
            return Math.Truncate(value * 100.0) / 100.0;
        }
    }
}
