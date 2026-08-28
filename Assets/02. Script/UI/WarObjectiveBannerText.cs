using War;

namespace UI
{
    /// <summary>
    /// WarObjectiveType별 한글 안내 문구를 반환하는 공유 헬퍼. WarBattleHudUI/WarClimaxWarningUI가
    /// 동일한 문구를 각자 중복 정의하지 않도록 한 곳에서 관리한다 (StatDisplayNames와 동일한 목적).
    /// </summary>
    public static class WarObjectiveBannerText
    {
        public static string Resolve(WarObjectiveType type)
        {
            return type switch
            {
                WarObjectiveType.StructureCapture => "구조물을 점령하세요!",
                WarObjectiveType.CargoProtection => "수하물을 보호하세요!",
                _ => "목표를 완료하세요!"
            };
        }
    }
}
