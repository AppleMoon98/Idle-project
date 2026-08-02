using Character;
using Character.Events;
using Core;
using UnityEngine;

namespace War.Objectives
{
    /// <summary>
    /// 보스 처치 목표. BossMarker가 붙은 대상이 사망하면 완료된다(다른 몬스터 생존 여부 무관).
    /// </summary>
    public sealed class BossDefeatObjective : MonoBehaviour, IWarObjective
    {
        public bool IsCompleted { get; private set; }

        public bool HasFailed => false;

        public float Progress01 => IsCompleted ? 1f : 0f;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        public void ResetForNewAttempt()
        {
            IsCompleted = false;
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (evt.Character.TryGetComponent(out BossMarker _))
            {
                IsCompleted = true;
            }
        }
    }
}
