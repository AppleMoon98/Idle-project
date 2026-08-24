using Character.Events;
using Core;
using Stage.Events;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 스테이지 진행 정책을 담당한다. "현재 스테이지"와 "역대 최고 클리어 스테이지"를
    /// 카탈로그 인덱스로 관리한다:
    ///
    /// - 클리어 시: 새 기록이면(현재 &gt; 최고) 항상 최고 기록을 갱신한다(모드와 무관 — 반복
    ///   모드에서도 진짜 기록이므로 잃지 않는다). 전진 여부는 StageModeService.CurrentMode를
    ///   따른다 — Breakthrough면 기존과 동일하게 다음 스테이지로(다음이 없으면 최고 기록 자리를
    ///   반복), Repeat이면 전진하지 않고 같은 스테이지를 계속 반복한다.
    /// - 플레이어 사망 시: 모드와 무관하게 한 칸 후퇴하되, 최고 기록 기준
    ///   maxRegressionDistance칸 밑으로는 내려가지 않는다. 이때 돌파 모드였다면 자동으로
    ///   반복 모드로 전환한다(StageModeService.SetMode) - 사망으로 후퇴한 스테이지에서 같은
    ///   자리를 계속 반복하며 재정비할 수 있도록, 별도 선택 절차 없이 곧바로 그 스테이지를
    ///   반복 대상으로 삼는다.
    ///
    /// OnStageCleared/OnCharacterDied는 CharacterDiedEvent 처리 도중 재진입으로 호출된다(마지막
    /// 처치 → StageClearedEvent → 여기 → LoadStage까지 전부 같은 호출 스택). 이 안에서 곧바로
    /// LoadStage를 부르면 StageController.EndCurrentStage가 현재 StageProgressTracker를 즉시
    /// Dispose(구독 해제)하는데, 같은 프레임·같은 호출 체인 안에서 다른 몬스터의 사망 이벤트가
    /// 그 직후에 또 발행되면(예: 광역 스킬이 여러 마리를 한 foreach에서 연달아 처치) 그 사망은
    /// 이미 사라진 옛 트래커도, 아직 구독 전인 새 트래커도 못 받아 조용히 유실된다 — 실사용 중
    /// 지적으로 발견. 그래서 LoadStage 호출 자체를 즉시 실행하지 않고 _pendingLoadStage에
    /// 적어두었다가 다음 틱(Tick)에서 실행한다 — 그러면 같은 프레임에 몰린 나머지 사망 이벤트가
    /// 전부 원래 트래커에 정상 도달한 뒤에야 트래커가 교체된다. JumpTo/JumpToHighestCleared/
    /// JumpToBreakthroughFrontier는 UI에서 직접 호출돼(EventBus 재진입이 아님) _isSuppressed가
    /// 아닌 한 그대로 즉시 LoadStage한다 - 오버레이(던전 등) 활성 중에는 OnStageCleared/
    /// OnCharacterDied와 마찬가지로 _isSuppressed를 확인해 아무 일도 하지 않는다(실사용 중 발견된
    /// 버그 - 이 세 메서드만 이 체크가 빠져 있어 던전 안에서 돌파/반복 토글을 조작하면 오버레이가
    /// 활성인 채로 LoadStage가 실행돼 스포너/트래커 상태가 꼬였다).
    /// </summary>
    public sealed class StageProgression : ITickable
    {
        private readonly StageCatalogSO _catalog;
        private readonly StageController _controller;
        private readonly EventBus _events;
        private readonly Transform _playerTransform;
        private readonly int _maxRegressionDistance;
        private readonly StageModeService _modeService;

        private int _currentIndex;
        private int _highestClearedIndex;
        private bool _isSuppressed;
        private StageSO _pendingLoadStage;

        public StageProgression(
            StageCatalogSO catalog,
            StageController controller,
            EventBus events,
            Transform playerTransform,
            int maxRegressionDistance,
            StageSO initialCurrentStage,
            StageSO initialHighestClearedStage,
            StageModeService modeService)
        {
            _catalog = catalog;
            _controller = controller;
            _events = events;
            _playerTransform = playerTransform;
            _maxRegressionDistance = maxRegressionDistance;
            _modeService = modeService;

            _currentIndex = _catalog.IndexOf(initialCurrentStage);
            _highestClearedIndex = _catalog.IndexOf(initialHighestClearedStage);

            _events.Subscribe<StageClearedEvent>(OnStageCleared);
            _events.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Register(this);
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. StageController가 파괴될 때 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<StageClearedEvent>(OnStageCleared);
            _events.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Unregister(this);
        }

        /// <summary>
        /// OnStageCleared/OnCharacterDied가 예약해둔 LoadStage를 여기서 실제로 실행한다 - 예약된
        /// 게 없으면 아무 일도 하지 않는다.
        /// </summary>
        void ITickable.Tick(float deltaTime)
        {
            if (_pendingLoadStage == null)
            {
                return;
            }

            StageSO stage = _pendingLoadStage;
            _pendingLoadStage = null;
            _controller.LoadStage(stage);
        }

        /// <summary>
        /// 현재 플레이어가 선택한 방침이 돌파인지(true) 반복인지(false). StageModeService가 없으면
        /// (구성 실수 등 방어적 상황) 기존 기본 동작인 돌파로 취급한다.
        /// </summary>
        public bool IsBreakthrough => _modeService == null || _modeService.CurrentMode == StageProgressionMode.Breakthrough;

        /// <summary>
        /// 역대 최고 클리어 스테이지의 카탈로그 인덱스. 반복 모드 스테이지 선택 팝업이 후보 목록
        /// (이 인덱스부터 내림차순)을 만들 때 쓴다.
        /// </summary>
        public int HighestClearedIndex => _highestClearedIndex;

        /// <summary>
        /// 지정한 스테이지로 현재 위치를 직접 옮긴다(반복 모드 스테이지 선택 팝업 전용). 아직
        /// 클리어하지 않은 스테이지(최고 기록보다 앞선 인덱스)로는 이동할 수 없다 - 반복 모드는
        /// 이미 증명된 난이도만 반복한다는 기존 전제(OfflineProgressService와 동일)를 그대로
        /// 따른다. 이미 그 자리라면 재로드 없이 성공만 반환한다. 성공하면 true.
        ///
        /// _isSuppressed(던전 등 오버레이 활성 중)일 때는 무조건 실패한다 - OnStageCleared/
        /// OnCharacterDied가 이미 이 플래그로 오버레이 중 LoadStage를 막고 있는 것과 같은 이유인데,
        /// 이 메서드들만 빠져 있어 던전 안에서 돌파/반복 토글을 조작하면 오버레이가 활성인 채로
        /// LoadStage가 그대로 실행돼 스포너/트래커 상태가 꼬이는 버그가 실사용 중 발견됐다.
        /// </summary>
        public bool JumpTo(StageSO stage)
        {
            if (_isSuppressed)
            {
                return false;
            }

            int index = _catalog.IndexOf(stage);

            if (index < 0 || index > _highestClearedIndex)
            {
                return false;
            }

            if (index == _currentIndex)
            {
                return true;
            }

            _currentIndex = index;
            _controller.LoadStage(_catalog.GetAt(_currentIndex));
            return true;
        }

        /// <summary>
        /// true인 동안 StageClearedEvent/Player CharacterDiedEvent를 완전히 무시한다. 던전 등
        /// 오버레이가 화면을 차지하는 동안(StageController.PauseForOverlay~ResumeAfterOverlay)
        /// 그 안에서 벌어지는 죽음/클리어가 실제 스테이지 진행도(돌파/후퇴)에 새어 들어가
        /// LoadStage를 몰래 호출해버리는 것을 막기 위한 것 — 이게 없으면 오버레이 도중 Player가
        /// 죽었을 때 여기서 스테이지를 다시 로드해 잔몹이 오버레이 위에 스폰되는 문제가 있었다.
        /// </summary>
        public void SetSuppressed(bool suppressed)
        {
            _isSuppressed = suppressed;
        }

        /// <summary>
        /// 현재 스테이지를 역대 최고 클리어 스테이지로 옮긴다(예: 랭크 승급 가능 시 자동 반복
        /// 전환, section AY/AZ 이후 추가 기능). 이미 그 자리라면(예: 방금 그 스테이지를 클리어해
        /// highest==current가 된 직후) 아무 일도 하지 않는다 - OnStageCleared 안에서 재진입으로
        /// 호출돼도 중복 LoadStage가 발생하지 않도록 하기 위함. _isSuppressed일 때는 JumpTo와
        /// 같은 이유로 아무 일도 하지 않는다.
        /// </summary>
        public void JumpToHighestCleared()
        {
            if (_isSuppressed || _currentIndex == _highestClearedIndex)
            {
                return;
            }

            _currentIndex = _highestClearedIndex;
            _controller.LoadStage(_catalog.GetAt(_currentIndex));
        }

        /// <summary>
        /// 현재 위치를 역대 최고 클리어 스테이지의 "다음" 스테이지(돌파 프론티어)로 옮긴다.
        /// 반복 모드에서 스테이지 선택 팝업으로 특정 스테이지를 골라 반복하다가 다시 돌파 모드로
        /// 되돌아갈 때 쓴다 - 방금까지 반복하던 스테이지에 그대로 머무르지 않고 실제 돌파해야 할
        /// 지점으로 복귀시킨다. 다음 스테이지가 없으면(카탈로그 마지막 스테이지가 곧 최고 기록인
        /// 경우) 최고 기록 자리 그대로 둔다 - OnStageCleared의 돌파 전진 fallback과 동일하다.
        /// _isSuppressed일 때는 JumpTo와 같은 이유로 아무 일도 하지 않는다.
        /// </summary>
        public void JumpToBreakthroughFrontier()
        {
            if (_isSuppressed)
            {
                return;
            }

            StageSO next = _catalog.GetAt(_highestClearedIndex + 1);
            int targetIndex = next != null ? _highestClearedIndex + 1 : _highestClearedIndex;

            if (targetIndex == _currentIndex)
            {
                return;
            }

            _currentIndex = targetIndex;
            _controller.LoadStage(_catalog.GetAt(_currentIndex));
        }

        private void OnStageCleared(StageClearedEvent evt)
        {
            if (_isSuppressed)
            {
                return;
            }

            if (_currentIndex > _highestClearedIndex)
            {
                _highestClearedIndex = _currentIndex;
                StageSO highest = _catalog.GetAt(_highestClearedIndex);
                _events.Publish(new HighestStageClearedEvent(highest.Chapter, highest.StageNumber));
            }

            if (IsBreakthrough)
            {
                StageSO next = _catalog.GetAt(_currentIndex + 1);
                _currentIndex = next != null ? _currentIndex + 1 : _highestClearedIndex;
            }

            _pendingLoadStage = _catalog.GetAt(_currentIndex);
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (_isSuppressed)
            {
                return;
            }

            if (evt.Character != _playerTransform.gameObject)
            {
                return;
            }

            if (IsBreakthrough)
            {
                _modeService?.SetMode(StageProgressionMode.Repeat);
            }

            int floor = Mathf.Max(0, _highestClearedIndex - _maxRegressionDistance);
            _currentIndex = Mathf.Max(_currentIndex - 1, floor);

            _pendingLoadStage = _catalog.GetAt(_currentIndex);
        }
    }
}
