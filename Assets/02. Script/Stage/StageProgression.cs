using Core;
using Stage.Events;

namespace Stage
{
    /// <summary>
    /// StageClearedEvent를 구독해 StageCatalogSO 상의 다음 스테이지를 자동으로 로드한다.
    /// 마지막 스테이지를 클리어한 경우(다음 스테이지 없음)는 아무 동작도 하지 않는다.
    /// </summary>
    public sealed class StageProgression
    {
        private readonly StageCatalogSO _catalog;
        private readonly StageController _controller;
        private readonly EventBus _events;

        public StageProgression(StageCatalogSO catalog, StageController controller, EventBus events)
        {
            _catalog = catalog;
            _controller = controller;
            _events = events;

            _events.Subscribe<StageClearedEvent>(OnStageCleared);
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. StageController가 파괴될 때 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<StageClearedEvent>(OnStageCleared);
        }

        private void OnStageCleared(StageClearedEvent evt)
        {
            StageSO next = _catalog.GetNext(evt.Stage);

            if (next != null)
            {
                _controller.LoadStage(next);
            }
        }
    }
}
