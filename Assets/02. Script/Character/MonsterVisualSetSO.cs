using UnityEngine;

namespace Character
{
    /// <summary>
    /// 몬스터 스폰 시 무작위로 고를 후보 스프라이트 묶음. 여러 StageSO의 MonsterSpawnEntry가
    /// 같은 세트를 공유해서 참조할 수 있어, 스테이지마다 스프라이트 배열을 중복 기입하지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterVisualSet", menuName = "Idle Project/Character/Monster Visual Set")]
    public sealed class MonsterVisualSetSO : ScriptableObject
    {
        [SerializeField]
        private Sprite[] sprites;

        /// <summary>
        /// 후보 스프라이트 목록.
        /// </summary>
        public Sprite[] Sprites => sprites;
    }
}
