using Core;
using Dungeon;
using Rank;
using Stage;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    /// <summary>
    /// Android 시스템 뒤로가기(및 에디터/PC 테스트용 Escape 키 - 새 Input System에서 Android
    /// 하드웨어/제스처 백 버튼은 Keyboard.current.escapeKey로 노출된다)를 한곳에서만 수신해 우선순위
    /// 순서대로 딱 하나의 대상에 전달한다(GitHub 이슈 #25). Keyboard.current가 아예 없는 플랫폼/기기
    /// (터치만 있는 실기기 일부)에서도 안전하도록 null 체크 후 진행한다.
    ///
    /// <para>
    /// 세 단계 우선순위(먼저 적용되는 것이 이긴다):
    /// </para>
    /// <list type="number">
    /// <item>열려있는 IDismissible 팝업 스택의 최상위 하나(BackNavigationService.TryDismissTop) -
    /// 이슈가 재현한 "중첩 팝업" 시나리오가 여기서 해결된다.</item>
    /// <item>던전/랭크 승급전 오버레이(TryHandleOverlay) - 실패 대기 상태(전투 중이 아님)면 해당
    /// 컨트롤러의 ExitToOriginalStage()를 그대로 호출해 "나가기" 버튼과 동일하게 처리한다. 전투
    /// 중이거나(자발적 이탈 기능 자체가 아직 없음, 의도적 범위 제한) 골드 던전처럼 애초에
    /// 이탈 메서드가 없는 오버레이는 StageController.IsOverlayActive만으로 조용히 소비해 3번
    /// (종료 확인)으로 새지 않게 막는다. 연습 스테이지(PracticeStageController)는 언제든 자유롭게
    /// 나갈 수 있어(section GK) IsActive만으로 바로 Exit()한다.</item>
    /// <item>루트 화면(닫을 팝업도, 활성 오버레이도 없음) - ConfirmationPopupUI로 종료 확인 후
    /// Application.Quit(). 에디터에서는 Application.Quit()이 Play Mode를 끝내지 않으므로
    /// EditorApplication.isPlaying을 직접 끈다.</item>
    /// </list>
    /// </summary>
    public sealed class BackInputRouter : MonoBehaviour, ITickable
    {
        [SerializeField]
        private StageController stageController;

        [SerializeField]
        private BossDungeonSessionController bossDungeon;

        [SerializeField]
        private SkillDungeonSessionController skillDungeon;

        [SerializeField]
        private StoneDungeonSessionController stoneDungeon;

        [SerializeField]
        private SoldierRescueDungeonSessionController soldierRescueDungeon;

        [SerializeField]
        private RankPromotionBattleController rankPromotion;

        [SerializeField]
        private PracticeStageController practiceStage;

        [SerializeField]
        private ConfirmationPopupUI confirmationPopup;

        private BackNavigationService _backNavigationService;

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _backNavigationService);
            }

            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            HandleBack();
        }

        private void HandleBack()
        {
            if (_backNavigationService != null && _backNavigationService.TryDismissTop())
            {
                return;
            }

            if (TryHandleOverlay())
            {
                return;
            }

            RequestQuitConfirmation();
        }

        /// <summary>
        /// 연습 스테이지 → 5개 던전/승급전 컨트롤러(실패 대기 상태면 나가기, 전투 중이면 소비만) →
        /// 그 외에도 StageController.IsOverlayActive가 켜져 있으면(War 클라이맥스 워밍업 등, 별도
        /// 컨트롤러가 없는 오버레이) 조용히 소비 - 순서대로 검사해 처음 해당하는 것 하나만 처리한다.
        /// </summary>
        private bool TryHandleOverlay()
        {
            if (practiceStage != null && practiceStage.IsActive)
            {
                practiceStage.Exit();
                return true;
            }

            if (TryExitWaitingDungeon(bossDungeon != null && bossDungeon.IsActive, bossDungeon != null && bossDungeon.IsFighting, () => bossDungeon.ExitToOriginalStage()))
            {
                return true;
            }

            if (TryExitWaitingDungeon(skillDungeon != null && skillDungeon.IsActive, skillDungeon != null && skillDungeon.IsFighting, () => skillDungeon.ExitToOriginalStage()))
            {
                return true;
            }

            if (TryExitWaitingDungeon(stoneDungeon != null && stoneDungeon.IsActive, stoneDungeon != null && stoneDungeon.IsFighting, () => stoneDungeon.ExitToOriginalStage()))
            {
                return true;
            }

            if (TryExitWaitingDungeon(soldierRescueDungeon != null && soldierRescueDungeon.IsActive, soldierRescueDungeon != null && soldierRescueDungeon.IsFighting, () => soldierRescueDungeon.ExitToOriginalStage()))
            {
                return true;
            }

            if (TryExitWaitingDungeon(rankPromotion != null && rankPromotion.IsActive, rankPromotion != null && rankPromotion.IsFighting, () => rankPromotion.ExitToOriginalStage()))
            {
                return true;
            }

            if (stageController != null && stageController.IsOverlayActive)
            {
                // 위 5곳 어디에도 안 걸렸지만 오버레이는 여전히 활성 상태(War 클라이맥스 워밍업,
                // 골드 던전처럼 애초에 이탈 메서드가 없는 오버레이 등) - 무엇을 해야 할지 정의돼
                // 있지 않으므로 조용히 소비만 하고, 아무 효과 없이 종료 확인으로 새지 않게 막는다.
                return true;
            }

            return false;
        }

        /// <summary>
        /// isActive/isFighting 두 bool을 미리 평가해서 넘겨받는다(널 체크를 매 호출부가 반복하지
        /// 않도록) - isActive가 아니면 애초에 해당 없음(false), isFighting이면 아직 나가는 방법이
        /// 없으니 조용히 소비만(true, exit 호출 안 함), 둘 다 아니면(실패 대기 상태) 실제로
        /// exitAction을 호출한다.
        /// </summary>
        private static bool TryExitWaitingDungeon(bool isActive, bool isFighting, System.Action exitAction)
        {
            if (!isActive)
            {
                return false;
            }

            if (!isFighting)
            {
                exitAction();
            }

            return true;
        }

        private void RequestQuitConfirmation()
        {
            if (confirmationPopup == null)
            {
                return;
            }

            confirmationPopup.RequestConfirm("QuitApp", "게임을 종료하시겠습니까?", QuitApp);
        }

        private static void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
