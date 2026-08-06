using UnityEngine;

namespace Character
{
    /// <summary>
    /// 이 프로젝트에 존재하는 몬스터 비주얼 세트 전체 목록. Content Editor의 Catalog Editor가
    /// 다른 카탈로그(EquipmentCatalogSO 등)와 동일한 "SO 배열 하나 든 컨테이너" 형태로 다룬다.
    /// MonsterSpawnEntry는 이 배열이 아니라 MonsterVisualSetSO를 직접 참조하므로,
    /// 세이브에 쓰이는 인덱스 조회(IndexOf/GetAt)는 필요 없다.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterVisualSetCatalog", menuName = "Idle Project/Character/Monster Visual Set Catalog")]
    public sealed class MonsterVisualSetCatalogSO : ScriptableObject
    {
        [SerializeField]
        private MonsterVisualSetSO[] sets;

        public MonsterVisualSetSO[] Sets => sets;
    }
}
