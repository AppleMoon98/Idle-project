using UnityEngine;

namespace Story
{
    /// <summary>
    /// 스토리 한 편을 구성하는 컷(만화 한 장면) 하나의 데이터. StorySO.Cuts 배열의 원소로만
    /// 쓰이며, 화면 탭 한 번마다 이 컷 하나가 표시된다.
    /// </summary>
    [CreateAssetMenu(fileName = "StoryCut", menuName = "Idle Project/Story/Story Cut")]
    public sealed class StoryCutSO : ScriptableObject
    {
        [SerializeField]
        private Sprite cutImage;

        [SerializeField]
        [TextArea(2, 6)]
        private string cutText;

        /// <summary>
        /// 이 컷의 만화 이미지. null이면(콘텐츠 미비 placeholder) StoryPopupUI가 이미지 영역을
        /// 숨기고 텍스트만 표시한다(Skill.SkillSO.VfxPrefab 등과 동일한 sparse opt-in 관례).
        /// </summary>
        public Sprite CutImage => cutImage;

        /// <summary>
        /// 이 컷의 대사/나레이션 텍스트. 비어 있으면 텍스트 영역을 숨긴다.
        /// </summary>
        public string CutText => cutText;
    }
}
