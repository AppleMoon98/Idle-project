using Soldier;

namespace UI
{
    /// <summary>
    /// SquadTacticType → 한글 표시 이름. SquadTacticDropdownUI(현재 선택 라벨)와
    /// SquadTacticOptionPopupUI(선택 목록) 둘 다 같은 매핑을 쓰도록 공유한다(UI.StatDisplayNames와
    /// 같은 "한 곳에서만 고치면 되는 매핑 테이블" 관례).
    /// </summary>
    public static class SquadTacticDisplayNames
    {
        public static string Get(SquadTacticType tactic)
        {
            return tactic switch
            {
                SquadTacticType.ShieldWall => "방패벽",
                SquadTacticType.LeftRightRaid => "좌우 습격",
                SquadTacticType.RearRaid => "후방 습격",
                _ => "없음",
            };
        }
    }
}
