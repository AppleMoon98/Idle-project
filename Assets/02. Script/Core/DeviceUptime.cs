using System;

namespace Core
{
    /// <summary>
    /// 벽시계(<see cref="DateTimeOffset.UtcNow"/>)와 무관하게 기기 부팅 이후 단조 증가하는
    /// 시간을 제공한다 - 사용자가 시스템 설정에서 날짜/시각을 바꿔도 이 값은 전혀 영향받지
    /// 않는다(GitHub 이슈 #71 - 오프라인 보상이 벽시계 차이만으로 계산돼, 시계를 반복적으로
    /// 미래로 이동하면 같은 기간의 보상을 무한히 재수령할 수 있는 문제).
    ///
    /// Android에서만 <c>android.os.SystemClock.elapsedRealtime()</c>(순정 Android SDK, API
    /// 레벨 1부터 존재)를 별도 네이티브 플러그인 없이 <see cref="global::UnityEngine.AndroidJavaClass"/>로
    /// 직접 호출해 제공한다. 이 신호 자체가 없는 플랫폼(iOS/Standalone/에디터)이나 네이티브
    /// 호출이 실패하는 극히 드문 경우(일부 커스텀 ROM 등)에는 항상 실패를 반환한다 - 그 경우
    /// 호출자(Offline.OfflineElapsedTimeCalculator)는 이 신호 없이 기존처럼 벽시계만 신뢰하는
    /// 동작으로 조용히 폴백해야 한다(그 이상 보호는 하지 못하지만, 최소한 기존 동작보다
    /// 나빠지지는 않는다).
    /// </summary>
    public static class DeviceUptime
    {
        /// <summary>
        /// 기기가 마지막으로 부팅된 이후 경과한 시간(초)을 반환한다. 이 신호를 제공할 수 없는
        /// 플랫폼이거나 네이티브 호출이 실패하면 false를 반환하고 <paramref name="elapsedSeconds"/>는
        /// 0이다.
        /// </summary>
        public static bool TryGetElapsedRealtimeSeconds(out long elapsedSeconds)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var systemClock = new UnityEngine.AndroidJavaClass("android.os.SystemClock"))
                {
                    long elapsedMillis = systemClock.CallStatic<long>("elapsedRealtime");
                    elapsedSeconds = elapsedMillis / 1000L;
                    return true;
                }
            }
            catch (Exception)
            {
                // 일부 기기/커스텀 ROM에서 예기치 못한 실패가 있을 수 있다 - 예외를 앱 전체로
                // 새어나가게 하는 대신 "신호 없음"으로 안전하게 폴백한다.
                elapsedSeconds = 0;
                return false;
            }
#else
            elapsedSeconds = 0;
            return false;
#endif
        }
    }
}
