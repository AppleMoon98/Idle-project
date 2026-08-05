using Character;
using Character.Events;
using UnityEngine;

namespace War.Objectives
{
    /// <summary>
    /// 보스 처치 목표. BossMarker가 붙은 대상이 사망하면 완료된다(다른 몬스터 생존 여부 무관).
    /// </summary>
    public sealed class BossDefeatObjective : SingleEventObjectiveBase<CharacterDiedEvent>
    {
        protected override bool IsCompletionEvent(CharacterDiedEvent evt)
        {
            return evt.Character.TryGetComponent(out BossMarker _);
        }
    }
}
