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
    }
}
