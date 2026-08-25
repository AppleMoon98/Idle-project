using Core;
using UI.Events;

namespace Gacha
{
    /// <summary>
    /// Pull() 계열 메서드가 요청한 횟수보다 적게 실행했을 때(0회 포함) 그 사실과 이유를 토스트로
    /// 알린다(GitHub 이슈 #22). 4개 Pull 계열 메서드(GachaService/EquipmentGachaService x2/
    /// SkillGachaService)가 전부 같은 형태로 이 헬퍼를 호출한다 - 이미 재화가 애초에 1회분도
    /// 안 되는 경우(각 Pull()의 CanAffordOnePull 초반 가드)는 이 헬퍼를 거치지 않고 자체적으로
    /// "재화가 모자랍니다."를 발행하고 즉시 반환하므로, 여기서는 오직 "루프를 실제로 몇 번 돌리다가
    /// 멈춘" 경우만 다룬다.
    /// </summary>
    internal static class GachaPullToast
    {
        /// <summary>
        /// succeeded가 requested보다 적을 때만(reason이 None이 아닐 때만) 토스트를 발행한다.
        /// noCandidatesMessage는 도메인마다 다른 "후보가 없다"는 문구를 호출부가 직접 채운다
        /// (예: 스킬 = "모든 스킬이 최대 레벨입니다.", 장비 = "뽑을 수 있는 장비가 없습니다.") -
        /// 한글 조사(이/가) 활용을 피하려고 완성된 문장을 그대로 받는다.
        /// </summary>
        public static void PublishIfIncomplete(
            EventBus events,
            int succeeded,
            int requested,
            GachaPullStopReason reason,
            string noCandidatesMessage)
        {
            if (events == null || reason == GachaPullStopReason.None || succeeded >= requested)
            {
                return;
            }

            string message = reason == GachaPullStopReason.NoCandidates
                ? (succeeded == 0 ? noCandidatesMessage : $"{noCandidatesMessage} ({succeeded}/{requested}회 성공)")
                : (succeeded == 0 ? "재화가 모자랍니다." : $"재화가 모자라 {succeeded}/{requested}회만 뽑았습니다.");

            events.Publish(new ToastMessageRequestedEvent(message));
        }
    }
}
