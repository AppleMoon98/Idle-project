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
    }
}
