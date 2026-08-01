using Core;
using Stage.Events;
using UnityEngine;

namespace War.Objectives
{
    /// <summary>
    /// 전멸 목표. 기존 StageProgressTracker가 이미 "등록된 몬스터 전원 사망 → StageClearedEvent"를
    /// 처리하므로, 이 컴포넌트는 사실상 패스스루다 — WarBattleController는 이 타입일 때
    /// 별도로 StageClearedEvent를 강제 발행하지 않는다(자연스러운 클리어를 그대로 둔다).
    /// </summary>
    public sealed class AnnihilationObjective : MonoBehaviour, IWarObjective
    {
        public bool IsCompleted { get; private set; }

        public bool HasFailed => false;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StageClearedEvent>(OnStageCleared);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StageClearedEvent>(OnStageCleared);
        }

        public void ResetForNewAttempt()
        {
            IsCompleted = false;
        }

        private void OnStageCleared(StageClearedEvent evt)
        {
            IsCompleted = true;
        }
    }
}
