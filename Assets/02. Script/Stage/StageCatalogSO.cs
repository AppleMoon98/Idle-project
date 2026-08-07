using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 스테이지를 진행 순서대로 나열한 데이터 에셋. 실시간 자동 진행(StageProgression)과
    /// 오프라인 보상 시뮬레이션이 "다음 스테이지가 무엇인지" 판단할 때 공통으로 참조한다.
    /// </summary>
    [CreateAssetMenu(fileName = "StageCatalog", menuName = "Idle Project/Stage/Stage Catalog")]
    public sealed class StageCatalogSO : ScriptableObject
    {
        [SerializeField]
        private StageSO[] stages;

        /// <summary>
        /// 진행 순서대로 나열된 스테이지 목록.
        /// </summary>
        public StageSO[] Stages => stages;

        /// <summary>
        /// current 다음 순서의 스테이지를 반환한다. current가 목록에 없거나 마지막이면 null.
        /// </summary>
        public StageSO GetNext(StageSO current)
        {
            if (stages == null)
            {
                return null;
            }

            for (int i = 0; i < stages.Length - 1; i++)
            {
                if (stages[i] == current)
                {
                    return stages[i + 1];
                }
            }

            return null;
        }

        /// <summary>
        /// 챕터/스테이지 번호로 카탈로그에서 스테이지를 찾는다. 없으면 null.
        /// 저장 데이터는 StageSO 참조가 아닌 챕터/스테이지 번호만 들고 있어 필요하다.
        /// </summary>
        public StageSO Find(int chapter, int stageNumber)
        {
            if (stages == null)
            {
                return null;
            }

            foreach (StageSO stage in stages)
            {
                if (stage != null && stage.Chapter == chapter && stage.StageNumber == stageNumber)
                {
                    return stage;
                }
            }

            return null;
        }

        /// <summary>
        /// stage가 목록에서 몇 번째(0부터)인지 반환한다. null이거나 목록에 없으면 -1.
        /// StageProgression이 "현재/최고 기록" 위치를 정수 인덱스로 비교·연산할 때 사용한다.
        /// </summary>
        public int IndexOf(StageSO stage)
        {
            if (stages == null || stage == null)
            {
                return -1;
            }

            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] == stage)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// index 위치의 스테이지를 반환한다. 범위를 벗어나면 null.
        /// </summary>
        public StageSO GetAt(int index)
        {
            if (stages == null || index < 0 || index >= stages.Length)
            {
                return null;
            }

            return stages[index];
        }

        /// <summary>
        /// 카탈로그에 실제로 존재하는 가장 높은 챕터 번호를 반환한다. 스테이지가 없으면 0.
        /// 콘텐츠가 줄어들 때(예: 테스트 빌드용 챕터 축소, section BD) "지금 존재하는 마지막
        /// 챕터가 몇 번인지"를 매번 다시 훑지 않고 조회하기 위한 헬퍼.
        /// </summary>
        public int GetMaxChapter()
        {
            int maxChapter = 0;

            if (stages == null)
            {
                return maxChapter;
            }

            foreach (StageSO stage in stages)
            {
                if (stage != null && stage.Chapter > maxChapter)
                {
                    maxChapter = stage.Chapter;
                }
            }

            return maxChapter;
        }
    }
}
