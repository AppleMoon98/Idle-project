using UnityEngine;

namespace Character
{
    /// <summary>
    /// "이 캐릭터가 플레이어다"를 표시하는 빈 태그 컴포넌트(Character.BossMarker와 같은 방식).
    /// Player와 Soldier가 같은 레이어(Player 레이어)를 공유해 레이어 기준만으로는 "플레이어
    /// 자신"과 "다른 병사"를 구분할 수 없는 CharacterSeparation.ignorePlayer가 이 마커로 판정한다.
    /// </summary>
    public sealed class PlayerMarker : MonoBehaviour
    {
    }
}
