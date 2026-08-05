using System;
using System.Globalization;

namespace Core
{
    /// <summary>
    /// 가수(Mantissa)와 지수(Exponent)로 값을 표현하는 임의 크기 숫자 타입.
    /// long의 한계(약 922경)를 넘어서는 재화 성장을 다루기 위해, 값이 아무리 커져도
    /// 연산 비용이 항상 일정한 가수+지수 방식(방치형 게임 업계 표준, break_infinity류)을 사용한다.
    /// 유효자리는 double 수준(15~17자리)까지만 보장하며, 지수 차이가 아주 큰 두 값을 더하면
    /// 작은 값은 버려진다 — 이 장르에서는 버그가 아니라 의도된 동작이다.
    /// </summary>
    public readonly struct BigNumber : IComparable<BigNumber>, IEquatable<BigNumber>
    {
        private const double NormalizeBase = 10.0;

        /// <summary>
        /// 이 차이를 넘어서는 지수 차 덧셈은 작은 쪽을 무시한다(double 유효자리 한계).
        /// </summary>
        private const int MaxSignificantExponentGap = 17;

        /// <summary>
        /// 이 지수 미만은 단위(만/억/...) 없이 정수 그대로 표시한다. UI.KoreanNumberFormatter와
        /// TruncateToDisplayPrecision이 공유하는 임계값이라 여기(Core)에 둔다.
        /// </summary>
        public const long DisplayPlainIntegerExponentLimit = 4;

        /// <summary>
        /// 이 지수 이상은 한글 단위 대신 과학적 표기(예: "1.23e24")로 전환한다. UI.KoreanNumberFormatter와
        /// TruncateToDisplayPrecision이 공유하는 임계값이라 여기(Core)에 둔다.
        /// </summary>
        public const long DisplayScientificFallbackExponent = 24;

        public double Mantissa { get; }
        public long Exponent { get; }

        public static readonly BigNumber Zero = default;

        public BigNumber(double mantissa, long exponent)
        {
            (Mantissa, Exponent) = Normalize(mantissa, exponent);
        }

        private static (double mantissa, long exponent) Normalize(double mantissa, long exponent)
        {
            if (mantissa == 0.0)
            {
                return (0.0, 0);
            }

            double sign = Math.Sign(mantissa);
            double abs = Math.Abs(mantissa);

            while (abs >= NormalizeBase)
            {
                abs /= NormalizeBase;
                exponent++;
            }

            while (abs < 1.0)
            {
                abs *= NormalizeBase;
                exponent--;
            }

            return (sign * abs, exponent);
        }

        public static implicit operator BigNumber(long value)
        {
            return value == 0 ? Zero : new BigNumber(value, 0);
        }

        public static implicit operator BigNumber(int value)
        {
            return (BigNumber)(long)value;
        }

        public static BigNumber operator +(BigNumber a, BigNumber b)
        {
            if (a.Mantissa == 0.0)
            {
                return b;
            }

            if (b.Mantissa == 0.0)
            {
                return a;
            }

            if (a.Exponent < b.Exponent)
            {
                (a, b) = (b, a);
            }

            long gap = a.Exponent - b.Exponent;

            if (gap > MaxSignificantExponentGap)
            {
                return a;
            }

            double combinedMantissa = a.Mantissa + (b.Mantissa / Math.Pow(NormalizeBase, gap));
            return new BigNumber(combinedMantissa, a.Exponent);
        }

        public static BigNumber operator -(BigNumber a, BigNumber b)
        {
            return a + new BigNumber(-b.Mantissa, b.Exponent);
        }

        public static BigNumber operator *(BigNumber a, BigNumber b)
        {
            if (a.Mantissa == 0.0 || b.Mantissa == 0.0)
            {
                return Zero;
            }

            return new BigNumber(a.Mantissa * b.Mantissa, a.Exponent + b.Exponent);
        }

        public static BigNumber operator *(BigNumber a, double scalar)
        {
            return new BigNumber(a.Mantissa * scalar, a.Exponent);
        }

        public static bool operator <(BigNumber a, BigNumber b) => a.CompareTo(b) < 0;
        public static bool operator >(BigNumber a, BigNumber b) => a.CompareTo(b) > 0;
        public static bool operator <=(BigNumber a, BigNumber b) => a.CompareTo(b) <= 0;
        public static bool operator >=(BigNumber a, BigNumber b) => a.CompareTo(b) >= 0;
        public static bool operator ==(BigNumber a, BigNumber b) => a.CompareTo(b) == 0;
        public static bool operator !=(BigNumber a, BigNumber b) => a.CompareTo(b) != 0;

        public int CompareTo(BigNumber other)
        {
            double aSign = Math.Sign(Mantissa);
            double bSign = Math.Sign(other.Mantissa);

            if (aSign != bSign)
            {
                return aSign.CompareTo(bSign);
            }

            if (aSign == 0.0)
            {
                return 0;
            }

            int magnitudeComparison = Exponent != other.Exponent
                ? Exponent.CompareTo(other.Exponent)
                : Math.Abs(Mantissa).CompareTo(Math.Abs(other.Mantissa));

            return aSign > 0.0 ? magnitudeComparison : -magnitudeComparison;
        }

        public bool Equals(BigNumber other) => CompareTo(other) == 0;

        public override bool Equals(object obj) => obj is BigNumber other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Mantissa.GetHashCode() * 397) ^ Exponent.GetHashCode();
            }
        }

        /// <summary>
        /// 저장(PlayerPrefs)용 라운드트립 포맷("1.234567890123E15"). 화면 표기는
        /// UI.KoreanNumberFormatter를 쓴다 — 이 문자열은 사람이 읽는 용도가 아니다.
        /// </summary>
        public override string ToString()
        {
            return $"{Mantissa.ToString("R", CultureInfo.InvariantCulture)}E{Exponent.ToString(CultureInfo.InvariantCulture)}";
        }

        public static BigNumber Parse(string text)
        {
            int splitIndex = text.IndexOf('E');
            double mantissa = double.Parse(text.Substring(0, splitIndex), CultureInfo.InvariantCulture);
            long exponent = long.Parse(text.Substring(splitIndex + 1), CultureInfo.InvariantCulture);
            return new BigNumber(mantissa, exponent);
        }

        public static bool TryParse(string text, out BigNumber result)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('E') < 0)
            {
                result = Zero;
                return false;
            }

            try
            {
                result = Parse(text);
                return true;
            }
            catch (FormatException)
            {
                result = Zero;
                return false;
            }
        }

        /// <summary>
        /// 지수가 충분히 작을 때만 안전한 double 변환. 지수가 크면 정밀도가 깨지거나
        /// 무한대가 나올 수 있으므로 실제 연산이 아니라 표기 등 보조 용도로만 쓴다.
        /// </summary>
        public double ToDouble() => Mantissa * Math.Pow(NormalizeBase, Exponent);

        /// <summary>
        /// UI.KoreanNumberFormatter가 이 값을 표시할 때 실제로 보여주는 자리 밑을 전부 버린(0 처리)
        /// 값을 반환한다. 강화/스탯업 비용처럼 "화면에 보이는 숫자가 곧 실제로 차감되는 금액"이어야
        /// 하는 곳에서 GetNextCost 같은 계산 함수가 결과를 반환하기 직전에 호출한다 - 그렇지 않으면
        /// 자릿수가 큰 비용일수록 화면엔 안 보이는 소수점/끝자리만큼 유저가 예상 못 한 채로 더
        /// 차감되는 것처럼 느껴질 수 있다. 표시 단위 임계값(만/억/조/...)이 전부 4의 배수이므로
        /// 소수점 자리수는 Exponent % 4만으로 결정된다(과학적 표기 구간은 가수가 항상 1자리라 별도 처리).
        /// KoreanNumberFormatter.Format과 완전히 같은 반올림/버림 방식을 그대로 재사용해야
        /// "표시된 값 == 실제 차감된 값"이 항상 보장된다 - 한쪽만 고치면 다시 어긋난다.
        /// </summary>
        public BigNumber TruncateToDisplayPrecision()
        {
            if (Exponent < DisplayPlainIntegerExponentLimit)
            {
                return new BigNumber(Math.Round(ToDouble(), MidpointRounding.AwayFromZero), 0);
            }

            if (Exponent >= DisplayScientificFallbackExponent)
            {
                // 과학적 표기는 만/억/조 단위로 재분류(re-bracket)하지 않고 가수를 그대로
                // 2자리까지만 보여준다(FormatScientific과 동일).
                double truncatedMantissa = TruncateWithoutFloatingNoise(Mantissa, 100.0);
                return new BigNumber(truncatedMantissa, Exponent);
            }

            // Units 임계값이 전부 4의 배수라, 이 값이 속한 단위(만/억/...) 안에서 몇 번째 자리인지(d)는
            // Exponent % 4로 정해진다 - Format이 스케일한 값(Mantissa * 10^d)과 반드시 같은 값을 잘라야
            // "표시된 값 == 실제 차감된 값"이 성립하므로, Mantissa 자체가 아니라 이 스케일된 값을 자른다.
            int d = (int)(Exponent % 4);
            int decimalPlaces = d switch { 2 => 1, 3 => 0, _ => 2 };

            double scaledWithinBracket = Mantissa * Math.Pow(NormalizeBase, d);
            double decimalFactor = Math.Pow(NormalizeBase, decimalPlaces);
            double truncatedScaled = TruncateWithoutFloatingNoise(scaledWithinBracket, decimalFactor);

            long bracketExponent = Exponent - d;
            return new BigNumber(truncatedScaled, bracketExponent);
        }

        /// <summary>
        /// value*factor를 자른 뒤 factor로 나눈다. 이미 한 번 이 방식으로 잘린 값(예: 4.56)은 부동소수점
        /// 표현상 정확히 4.56이 아니라 4.559999999999994처럼 저장될 수 있어, 그 값을 다시 그대로
        /// Truncate하면 한 자리 더 낮게(4.55) 잘리는 이중 절삭 오차가 생긴다. Truncate 직전에 소수점
        /// 6자리로 반올림해 그 부동소수점 잡음을 제거한 뒤 자른다.
        /// </summary>
        private static double TruncateWithoutFloatingNoise(double value, double factor)
        {
            double scaled = Math.Round(value * factor, 6, MidpointRounding.AwayFromZero);
            return Math.Truncate(scaled) / factor;
        }
    }
}
