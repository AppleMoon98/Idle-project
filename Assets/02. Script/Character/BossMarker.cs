using UnityEngine;

namespace Character
{
    /// <summary>
    /// "이 캐릭터가 보스다"를 표시하는 빈 태그 컴포넌트. War.Objectives.BossDefeatObjective가
    /// CharacterDiedEvent를 받았을 때 사망한 대상이 보스인지 판정하는 데 사용한다
    /// (MonsterLootProvider, PoolReleaseOnDeath와 같은 태그 컴포넌트 방식).
    /// </summary>
    public sealed class BossMarker : MonoBehaviour
    {
    }
}
