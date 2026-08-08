using System.Collections.Generic;
using UnityEngine;

namespace Stage.Tactics
{
    /// <summary>
    /// TacticType 하나에 대응하는 전략 - 이미 스폰이 끝난 리더/추종자 인스턴스 목록을 받아
    /// 실제 페어링(리더-추종자 연결)을 수행하고, 이후 생존 상태에 따라 스스로 재배정하는
    /// ITacticFormationGroup을 만들어 반환한다. MonsterSpawner는 이 전략을 통해서만 전술을
    /// 다루므로, 새 전술을 추가할 때 MonsterSpawner의 스폰 루프 자체는 건드릴 필요가 없다.
    /// </summary>
    public interface ITacticSpawnStrategy
    {
        ITacticFormationGroup CreateFormationGroup(IReadOnlyList<GameObject> leaders, IReadOnlyList<GameObject> followers);
    }
}
