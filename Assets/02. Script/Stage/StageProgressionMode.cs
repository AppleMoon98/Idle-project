namespace Stage
{
    /// <summary>
    /// 플레이어가 직접 선택하는 스테이지 진행 방침. Breakthrough(돌파)는 기존 기본 동작(클리어 시
    /// 다음 스테이지로 전진, 최고 기록까지는 한 칸씩 따라잡음) 그대로이고, Repeat(반복)는 클리어해도
    /// 전진하지 않고 같은 스테이지를 계속 반복한다.
    /// </summary>
    public enum StageProgressionMode
    {
        Breakthrough,
        Repeat
    }
}
