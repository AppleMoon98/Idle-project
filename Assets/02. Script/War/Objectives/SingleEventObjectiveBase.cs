using Core;
using UnityEngine;

namespace War.Objectives
{
    /// <summary>
    /// "이벤트 하나를 구독하다가, 그 이벤트가 완료 조건을 만족하면 IsCompleted=true가 된다"는
    /// 패턴을 공유하는 War 목표의 공통 베이스. AnnihilationObjective(StageClearedEvent)/
    /// BossDefeatObjective(CharacterDiedEvent)가 각자 들고 있던 동일한 구독/해제/리셋
    /// 보일러플레이트를 여기로 모은다. 연속적 진행도가 있는 목표(CargoProtection/StructureCapture)는
    /// 이 베이스를 쓰지 않는다.
    /// </summary>
    public abstract class SingleEventObjectiveBase<TEvent> : MonoBehaviour, IWarObjective
    {
        public bool IsCompleted { get; private set; }

        public virtual bool HasFailed => false;

        public virtual float Progress01 => IsCompleted ? 1f : 0f;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<TEvent>(OnEvent);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<TEvent>(OnEvent);
        }

        public virtual void ResetForNewAttempt()
        {
            IsCompleted = false;
        }

        private void OnEvent(TEvent evt)
        {
            if (IsCompletionEvent(evt))
            {
                IsCompleted = true;
            }
        }

        /// <summary>
        /// 구독한 이벤트가 이 목표를 완료시키는 이벤트인지 판단한다.
        /// </summary>
        protected abstract bool IsCompletionEvent(TEvent evt);
    }
}
