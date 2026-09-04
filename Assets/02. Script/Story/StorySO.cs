using UnityEngine;

namespace Story
{
    /// <summary>
    /// 스토리 한 편(인트로 1개, 랭크 승급마다 1개)의 데이터 정의. Cuts를 순서대로 한 컷씩
    /// 탭으로 넘겨보는 만화 형식 - StoryPopupUI.Play(StorySO, onComplete)가 소비한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Story", menuName = "Idle Project/Story/Story")]
    public sealed class StorySO : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private StoryCutSO[] cuts;

        /// <summary>
        /// 에디터 식별용 이름(현재 UI에는 표시되지 않는다).
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 순서대로 표시할 컷 목록(보통 2~3개). 비어 있으면 StoryPopupUI.Play가 즉시
        /// onComplete를 호출하고 종료한다.
        /// </summary>
        public StoryCutSO[] Cuts => cuts;
    }
}
