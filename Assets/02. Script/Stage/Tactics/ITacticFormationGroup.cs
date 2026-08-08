using System;

namespace Stage.Tactics
{
    /// <summary>
    /// 스폰된 전술 대형 한 벌(예: 방패병 5 + 창병 5)의 생존 상태를 추적하며 재배정하는 객체.
    /// 생성 시점에 EventBus(CharacterDiedEvent)를 구독하므로, 대형이 아직 살아있는 채로
    /// 스테이지가 강제 종료(死死 없이 몬스터가 풀에 반환됨)될 수 있어 Dispose로 명시적으로
    /// 구독을 해제할 수 있어야 한다 - MonsterSpawner.Dispose가 이를 호출한다.
    /// </summary>
    public interface ITacticFormationGroup : IDisposable
    {
    }
}
