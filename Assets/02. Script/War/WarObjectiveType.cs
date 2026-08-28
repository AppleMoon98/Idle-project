namespace War
{
    /// <summary>
    /// 챕터 클라이맥스(War) 스테이지의 승리 목표 종류. WarBattleController가 챕터별로
    /// 이 값에 따라 어떤 목표 컴포넌트를 활성화할지 결정한다.
    /// </summary>
    public enum WarObjectiveType
    {
        /// <summary>
        /// 구조물 점령: 지정된 WarStructure 전부가 점령되면 클리어.
        /// </summary>
        StructureCapture,

        /// <summary>
        /// 수하물 보호: 지정된 시간 동안 수하물이 생존하면 클리어, 그전에 사망하면 실패.
        /// </summary>
        CargoProtection
    }
}
