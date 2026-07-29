using Character.Events;
using Core;
using Managers;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 이 캐릭터가 사망하면 PoolManager를 통해 풀로 반납한다.
    /// 풀링되는 캐릭터(몬스터 등) 프리팹에만 붙여서 사용한다.
    /// </summary>
    public sealed class PoolReleaseOnDeath : MonoBehaviour
    {
        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (evt.Character != gameObject)
            {
                return;
            }

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                pool.Release(gameObject);
            }
        }
    }
}
