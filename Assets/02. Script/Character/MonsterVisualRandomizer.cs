using UnityEngine;

namespace Character
{
    /// <summary>
    /// 스폰 시점에 몬스터의 스프라이트를 후보군 중 무작위로 고른다. Monster 전용 컴포넌트.
    /// StageMonsterScaler와 동일한 이유로, 세트가 없을 때도 "적용 안 함"이 아니라 프리팹의
    /// 원래(기본) 스프라이트로 명시적으로 되돌린다 — 풀링으로 재사용되는 오브젝트라 이전
    /// 스폰의 스킨이 그대로 남아있으면 안 되기 때문이다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MonsterVisualRandomizer : MonoBehaviour
    {
        private SpriteRenderer _spriteRenderer;
        private Sprite _defaultSprite;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _defaultSprite = _spriteRenderer.sprite;
        }

        /// <summary>
        /// visualSet에 후보가 있으면 그중 하나를 무작위로 적용하고, 없으면(null 또는 빈 배열)
        /// 프리팹의 기본 스프라이트로 되돌린다.
        /// </summary>
        public void ApplyVisualSet(MonsterVisualSetSO visualSet)
        {
            Sprite[] candidates = visualSet != null ? visualSet.Sprites : null;

            if (candidates == null || candidates.Length == 0)
            {
                _spriteRenderer.sprite = _defaultSprite;
                return;
            }

            _spriteRenderer.sprite = candidates[Random.Range(0, candidates.Length)];
        }
    }
}
