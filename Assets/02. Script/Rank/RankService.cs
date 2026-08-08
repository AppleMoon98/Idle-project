using Core;
using Rank.Events;
using Stage;
using Stage.Events;

namespace Rank
{
    /// <summary>
    /// 현재 랭크와 "다음 랭크로 승급 가능한지"를 관리한다. RequiredStage를 클리어했다고 자동으로
    /// 승급하지 않는다 - 조건을 만족하면 UI(RankUpAvailableTextUI)가 "랭크 승급 가능" 버튼을 띄우고,
    /// 플레이어가 그 버튼으로 RankPromotionBattleController를 시작해 보스를 처치해야 PromoteToNext가
    /// 호출되어 실제로 승급한다. 항상 RankCatalogSO.GetNext(현재 랭크) 딱 한 단계만 다루므로, 스테이지
    /// 진행이 훨씬 앞서 있어도 중간 랭크를 건너뛸 수 없다.
    /// </summary>
    public sealed class RankService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly StageCatalogSO _stageCatalog;
        private readonly RankCatalogSO _rankCatalog;

        private RankSO _currentRank;
        private int _currentRankIndex;
        private int _highestClearedIndex = -1;

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
        /// 역대 최고 클리어 스테이지의 챕터 번호. 아직 아무것도 클리어하지 않았으면(세이브/시딩 전) 0.
        /// 랭크 판정용으로 이미 추적 중인 값을 재사용한 것 — "카탈로그에 존재하는 콘텐츠 양"이 아니라
        /// "플레이어가 실제로 클리어한 진행도"를 기준으로 삼아야 하는 소비자(예: 골드 던전의 단계 상한)를
        /// 위한 공개 접근자.
        /// </summary>
        public int HighestClearedChapter
        {
            get
            {
                StageSO stage = _stageCatalog.GetAt(_highestClearedIndex);
                return stage != null ? stage.Chapter : 0;
            }
        }

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
        /// 세이브 데이터로 현재 랭크를 조용히(이벤트 발행 없이) 맞춘다. GameBootstrapper.Awake()에서
        /// SaveService.Load() 직후 호출한다.
        /// </summary>
        public void SeedRank(int rankIndex)
        {
            RankSO rank = _rankCatalog.GetAt(rankIndex);

            if (rank == null)
            {
                return;
            }

            _currentRank = rank;
            _currentRankIndex = rankIndex;
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
        /// 세이브의 역대 최고 기록으로 "승급 가능 여부" 판정용 캐시만 시딩한다(승급 자체는
        /// 일으키지 않음 - 승급은 오직 PromoteToNext로만 일어난다). RestoreLevel 직후
        /// GameBootstrapper.Start()에서 한 번 호출한다.
        /// </summary>
        public void SeedHighestCleared(int chapter, int stageNumber)
        {
            StageSO stage = _stageCatalog.Find(chapter, stageNumber);

            if (stage != null)
            {
                _highestClearedIndex = _stageCatalog.IndexOf(stage);
            }
        }

        /// <summary>
        /// 현재 랭크의 바로 다음 랭크. 카탈로그 끝이면 null. 항상 한 단계만 반환하므로 스테이지
        /// 진행이 앞서 있어도 중간 랭크를 건너뛰는 경로가 존재하지 않는다.
        /// </summary>
        public RankSO GetNextRank()
        {
            return _rankCatalog.GetNext(_currentRank);
        }

        /// <summary>
        /// 다음 랭크로 승급전을 시작할 수 있는지: 다음 랭크가 있고, 필요 스테이지를 이미
        /// 클리어했고, 승급전에 쓸 보스 콘텐츠가 준비돼 있어야 한다.
        /// </summary>
        public bool IsNextRankAvailable()
        {
            RankSO nextRank = GetNextRank();

            if (nextRank == null || nextRank.RequiredStage == null || nextRank.BossPrefab == null)
            {
                return false;
            }

            int requiredIndex = _stageCatalog.IndexOf(nextRank.RequiredStage);
            return requiredIndex >= 0 && _highestClearedIndex >= requiredIndex;
        }

        /// <summary>
        /// stage를 역대 최고 기록으로 실제로 클리어한 적이 있는지 판정한다. IsNextRankAvailable과
        /// 같은 인덱스 비교 방식 — 골드/강화석 던전처럼 "특정 스테이지 클리어"를 입장 조건으로 삼는
        /// 소비자를 위한 공개 API. stage가 카탈로그에 없으면(콘텐츠 없음) false.
        /// </summary>
        public bool HasClearedStage(StageSO stage)
        {
            int index = _stageCatalog.IndexOf(stage);
            return index >= 0 && _highestClearedIndex >= index;
        }

        /// <summary>
        /// 다음 랭크로 딱 한 단계 승급한다. RankPromotionBattleController가 승급전 승리 시에만
        /// 호출한다.
        /// </summary>
        public void PromoteToNext()
        {
            RankSO nextRank = GetNextRank();

            if (nextRank == null)
            {
                return;
            }

            _currentRank = nextRank;
            _currentRankIndex = _rankCatalog.IndexOf(nextRank);
            _events.Publish(new RankChangedEvent(_currentRank, _currentRankIndex, isRestore: false));
        }

        private void OnHighestStageCleared(HighestStageClearedEvent evt)
        {
            StageSO clearedStage = _stageCatalog.Find(evt.Chapter, evt.StageNumber);

            if (clearedStage != null)
            {
                _highestClearedIndex = _stageCatalog.IndexOf(clearedStage);
            }
        }
    }
}
