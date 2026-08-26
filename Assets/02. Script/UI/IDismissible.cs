namespace UI
{
    /// <summary>
    /// Android 시스템 뒤로가기(및 에디터 테스트용 Escape 키) 한 번으로 닫을 수 있는 화면이 구현하는
    /// 계약(GitHub 이슈 #25). BackNavigationService에 열릴 때 등록하고 닫힐 때 해제하는 방식으로
    /// 최근에 연 화면부터 스택처럼 쌓인다 - 중첩 팝업에서 Back 한 번이 최상위 화면 하나만 닫도록
    /// 하는 것이 이 인터페이스의 유일한 존재 이유다.
    /// </summary>
    public interface IDismissible
    {
        /// <summary>
        /// 이 화면을 닫을 수 있으면 닫고 true를, 이미 닫혀있는 등 닫을 게 없으면 아무 것도 하지
        /// 않고 false를 반환한다. true를 반환하면 BackNavigationService는 그 자리에서 처리를
        /// 끝내고 더 아래 스택으로 내려가지 않는다.
        /// </summary>
        bool TryDismiss();
    }
}
