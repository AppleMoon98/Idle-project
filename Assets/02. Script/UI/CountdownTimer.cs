using UnityEngine;

namespace UI
{
    /// <summary>
    /// "남은 시간을 deltaTime만큼 줄이되 0 밑으로 내려가지 않게 한다"는 카운트다운 보일러플레이트를
    /// GoldDungeonHudUI/StoneDungeonHudUI/WarClimaxWarningUI가 각자 반복하던 것을 공유한다.
    /// 텍스트 포맷(초만 표시할지 mm:ss로 표시할지)은 화면마다 달라 별도 포맷 헬퍼로 분리한다.
    /// </summary>
    public static class CountdownTimer
    {
        /// <summary>
        /// remaining에서 deltaTime을 뺀 값을 0으로 클램프해 반환한다.
        /// </summary>
        public static float Tick(float remaining, float deltaTime)
        {
            return Mathf.Max(0f, remaining - deltaTime);
        }

        /// <summary>
        /// 초 단위 남은 시간을 "mm:ss" 형식으로 포맷한다.
        /// </summary>
        public static string FormatMinutesSeconds(float remainingSeconds)
        {
            int totalSeconds = Mathf.CeilToInt(remainingSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }
}
