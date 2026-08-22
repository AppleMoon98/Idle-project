using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 스폰 직후 MonsterSpawner가 플레이어 Transform을 주입하기 위한 공통 훅. MonsterTargetSelector/
    /// RangedKiter처럼 몬스터의 이동을 담당하는 컴포넌트는 병종마다 서로 배타적으로
    /// 하나만 붙지만, 스포너 쪽에서는 구체 타입을 몰라도 되도록 인터페이스로 통일한다.
    /// </summary>
    public interface IMonsterMovementInitializer
    {
        void Initialize(Transform playerTransform);
    }
}
