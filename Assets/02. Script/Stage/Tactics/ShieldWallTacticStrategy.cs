using System.Collections.Generic;
using UnityEngine;

namespace Stage.Tactics
{
    /// <summary>
    /// TacticType.ShieldWall에 대응하는 전략 - ShieldWallFormationGroup을 만들어 반환하기만 한다.
    /// </summary>
    public sealed class ShieldWallTacticStrategy : ITacticSpawnStrategy
    {
        public ITacticFormationGroup CreateFormationGroup(IReadOnlyList<GameObject> leaders, IReadOnlyList<GameObject> followers)
        {
            return new ShieldWallFormationGroup(leaders, followers);
        }
    }
}
