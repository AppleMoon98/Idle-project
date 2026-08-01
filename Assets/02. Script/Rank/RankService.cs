using Core;
using Rank.Events;
using Stage;
using Stage.Events;

namespace Rank
{
    /// <summary>
    /// Stage.Events.HighestStageClearedEvent(역대 최고 기록 갱신)를 구독해 랭크 승급을 판정한다.
    /// 오프라인 진행처럼 한 번에 여러 스테이지를 건너뛸 수도 있으므로, 이번 갱신으로 여러 랭크를
    /// 한꺼번에 승급할 수 있는지 반복 확인한다. Stage 서비스를 직접 참조하지 않고 이벤트와
    /// StageCatalogSO(데이터 조회용)만 사용한다.
    /// </summary>
    public sealed class RankService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly StageCatalogSO _stageCatalog;
        private readonly RankCatalogSO _rankCatalog;

        private RankSO _currentRank;
        private int _currentRankIndex;

        public RankService(EventBus events, StageCatalogSO stageCatalog, RankCatalogSO rankCatalog)
        {
            _events = events;
            _stageCatalog = stageCatalog;
            _rankCatalog = rankCatalog;
            _currentRank = rankCatalog.GetAt(0);
            _currentRankIndex = 0;
        }

        /// <summary>
        /// 현재 랭크.
        /// </summary>
        public RankSO CurrentRank => _currentRank;

        /// <summary>
        /// 현재 랭크의 RankCatalogSO 상 인덱스.
        /// </summary>
        public int CurrentRankIndex => _currentRankIndex;

        /// <summary>
        /// 현재 랭크가 rank 이상인지 판정한다. rank가 null이면 조건 없음으로 간주해 항상 true
        /// (해금 조건을 아직 안 정한 시스템이 이 값을 그대로 써도 항상 열려 있도록).
        /// rank가 카탈로그에 없으면(설정 실수) false.
        /// </summary>
        public bool IsAtLeast(RankSO rank)
        {
            if (rank == null)
            {
                return true;
            }

            int requiredIndex = _rankCatalog.IndexOf(rank);
            return requiredIndex >= 0 && _currentRankIndex >= requiredIndex;
        }

        public void Initialize()
        {
            _events.Subscribe<HighestStageClearedEvent>(OnHighestStageCleared);
        }

        public void Shutdown()
        {
            _events.Unsubscribe<HighestStageClearedEvent>(OnHighestStageCleared);
        }

        /// <summary>
        /// 세이브 데이터로 현재 랭크를 복원한다. RankChangedEvent를 재발행해 이미 구독 중인
        /// UI/게이트(SoldierSpawner 등)가 시작 시점 상태를 놓치지 않게 한다.
        /// </summary>
        public void RestoreLevel(int rankIndex)
        {
            RankSO rank = _rankCatalog.GetAt(rankIndex);

            if (rank == null)
            {
                return;
            }

            _currentRank = rank;
            _currentRankIndex = rankIndex;
            _events.Publish(new RankChangedEvent(_currentRank, _currentRankIndex, isRestore: true));
        }

        /// <summary>
        /// 세이브의 역대 최고 기록 기준으로 밀린 승급이 있으면 조용히(팝업 없이) 따라잡는다.
        /// Rank 시스템이 생기기 전에 이미 그 스테이지를 클리어해둔 세이브이거나, 이미 클리어한
        /// 스테이지가 나중에 마일스톤으로 지정된 경우 랭크가 영영 안 올라가는 걸 방지한다.
        /// RestoreLevel 직후 GameBootstrapper.Start()에서 한 번 호출한다.
        /// </summary>
        public void CatchUpFromHighestStage(int chapter, int stageNumber)
        {
            StageSO stage = _stageCatalog.Find(chapter, stageNumber);

            if (stage == null)
            {
                return;
            }

            PromoteUpTo(_stageCatalog.IndexOf(stage), isRestore: true);
        }

        private void OnHighestStageCleared(HighestStageClearedEvent evt)
        {
            StageSO clearedStage = _stageCatalog.Find(evt.Chapter, evt.StageNumber);

            if (clearedStage == null)
            {
                return;
            }

            PromoteUpTo(_stageCatalog.IndexOf(clearedStage), isRestore: false);
        }

        /// <summary>
        /// clearedIndex(스테이지 카탈로그 인덱스)가 요구치를 만족하는 한 연속으로 승급시킨다.
        /// </summary>
        private void PromoteUpTo(int clearedIndex, bool isRestore)
        {
            while (true)
            {
                RankSO nextRank = _rankCatalog.GetNext(_currentRank);

                if (nextRank == null || nextRank.RequiredStage == null)
                {
                    break;
                }

                int requiredIndex = _stageCatalog.IndexOf(nextRank.RequiredStage);

                if (clearedIndex < requiredIndex)
                {
                    break;
                }

                _currentRank = nextRank;
                _currentRankIndex = _rankCatalog.IndexOf(nextRank);
                _events.Publish(new RankChangedEvent(_currentRank, _currentRankIndex, isRestore));
            }
        }
    }
}
