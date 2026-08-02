using Core.Pooling;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 집결 명령 위치를 표시하는 순수 시각 컴포넌트. 생명주기(스폰/교체/해제)는 전부
    /// UI.SquadRallyFlagUI가 관리하며, 이 컴포넌트 자신은 스프라이트를 반영하기만 한다
    /// (War.Boss.WarBossTelegraphIndicator와 동일한 "판정/생명주기는 소유자가, 시각은 순수하게"
    /// 분리 철학).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RallyPointMarker : MonoBehaviour, IPoolable
    {
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        /// <summary>
        /// 집결 슬롯 아이콘의 스프라이트를 그대로 이어받아 표시한다.
        /// </summary>
        public void SetSprite(Sprite sprite)
        {
            spriteRenderer.sprite = sprite;
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
        }
    }
}
