using UnityEngine;

namespace Offline
{
    /// <summary>
    /// GitHub 이슈 #71 - 벽시계(DateTimeOffset.UtcNow)만으로 오프라인 경과 시간을 계산하면,
    /// 기기 시계(또는 저장된 LastActiveUnixTime 자체)를 반복적으로 미래로 조작해 동일 기간의
    /// 보상을 무한히 재수령할 수 있다. 이 클래스는 그 계산을 서버 없이도 최대한 안전하게
    /// 만드는 순수 정책 로직만 담당한다 - PlayerPrefs/AndroidJavaClass 등 실제 I/O는 전혀
    /// 모르며(Save/Core.DeviceUptime이 그쪽을 맡는다), Offline.OfflineProgressService가 이미
    /// 읽어온 값들만 인자로 받는다. 순수 함수라 RegressionChecks에서 실제 기기 없이도
    /// 완전히 검증 가능하다.
    ///
    /// 두 시각 신호를 조합한다:
    /// - 벽시계(nowUnix - lastActiveUnixTime): 사용자가 시스템 설정에서 바꿀 수 있다.
    ///   (Save.SaveService.Load()가 이미 하이워터마크로 한 번 정화해 반환하므로, 저장된
    ///   LastActiveUnixTime 값을 세이브 파일에서 직접 편집해 과거로 되돌리는 공격은 이
    ///   클래스에 도달하기 전에 이미 무력화된다.)
    /// - 기기 부팅-이후 경과시간(currentElapsedRealtimeSeconds - previousElapsedRealtimeSeconds,
    ///   Android SystemClock.elapsedRealtime 기반): 벽시계와 무관하게 흐르며 사용자가 직접
    ///   조작할 수 없다.
    ///
    /// 정책: 두 신호가 모두 유효하면 더 작은 쪽만 신뢰한다 - 벽시계를 반복적으로 미래로
    /// 돌리는 조작에서는 실제 기기 가동시간이 거의 안 흐르므로 계산된 경과 시간도 그만큼만
    /// 나온다(완료 조건: "시계를 미래로 반복 이동해도 동일 기간 보상을 중복 수령할 수 없음").
    /// 기기 부팅-이후 경과시간이 이전 관측값보다 작으면(재부팅 발생 - 정상적인 사용자 행동)
    /// 이번엔 이 신호를 신뢰할 수 없으므로 벽시계만 사용한다 - 탐지값을 처벌 근거로 쓰지
    /// 않고 오탐(재부팅)을 페널티 없이 넘기기 위함.
    /// </summary>
    internal static class OfflineElapsedTimeCalculator
    {
        /// <summary>
        /// 신뢰 가능한 오프라인 경과 시간(초)을 계산한다. 음수가 될 수 없다.
        /// </summary>
        /// <param name="nowUnix">현재 벽시계(UTC 유닉스 초).</param>
        /// <param name="lastActiveUnixTime">저장된 마지막 활동 시각(UTC 유닉스 초, 이미 하이워터마크로 정화된 값).</param>
        /// <param name="currentElapsedRealtimeSeconds">지금 이 순간 읽은 기기 부팅-이후 경과시간(초). 신호가 없으면 null.</param>
        /// <param name="previousElapsedRealtimeSeconds">저장된, 마지막으로 관측된 기기 부팅-이후 경과시간(초). 신호가 없거나 최초 실행이면 null.</param>
        public static float CalculateTrustedElapsedSeconds(
            long nowUnix,
            long lastActiveUnixTime,
            long? currentElapsedRealtimeSeconds,
            long? previousElapsedRealtimeSeconds)
        {
            float wallClockElapsed = Mathf.Max(0f, nowUnix - lastActiveUnixTime);

            if (!currentElapsedRealtimeSeconds.HasValue || !previousElapsedRealtimeSeconds.HasValue)
            {
                // 이 플랫폼에는 신호 자체가 없거나(iOS/Standalone/에디터) 이번이 첫 저장이라 비교
                // 대상이 없다 - 기존 벽시계 전용 동작으로 조용히 폴백한다.
                return wallClockElapsed;
            }

            long deviceUptimeDelta = currentElapsedRealtimeSeconds.Value - previousElapsedRealtimeSeconds.Value;

            if (deviceUptimeDelta < 0)
            {
                // 기기가 마지막 기록 이후 재부팅됐다 - elapsedRealtime은 정의상 재부팅 시에만
                // 감소할 수 있으므로, 이는 조작이 아니라 정상적인 사용자 행동이다. 이번엔 이
                // 신호를 신뢰할 수 없으므로 벽시계만 사용한다.
                return wallClockElapsed;
            }

            return Mathf.Min(wallClockElapsed, deviceUptimeDelta);
        }
    }
}
