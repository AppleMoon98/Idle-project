using System;
using Core;
using Equipment;
using Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 장착 팝업의 장비 한 줄(행)을 표시/제어한다. EquipmentSlotPopupUI가 보유 장비 개수만큼
    /// 이 컴포넌트가 붙은 프리팹을 Instantiate하고 Initialize로 데이터를 채운다.
    /// </summary>
    public sealed class EquipmentRowUI : MonoBehaviour
    {
        private static readonly Color LockedTextColor = new(0.55f, 0.55f, 0.55f, 1f);
        private const float LockedBackgroundAlphaMultiplier = 0.5f;

        [SerializeField]
        private Image background;

        [SerializeField]
        private Text label;

        [SerializeField]
        private Button nameButton;

        [SerializeField]
        private Button equipButton;

        [SerializeField]
        private Text equipButtonLabel;

        [SerializeField]
        private Button fuseButton;

        [SerializeField]
        private Button enhanceButton;

        private OwnedEquipment _owned;

        /// <summary>
        /// Selectable.interactable를 대입하면 Unity가 매번 Normal→Disabled 색상 전환을 fadeDuration에
        /// 걸쳐 서서히 재생한다 - 새로 Instantiate된 행은 프리팹 기본값(대개 interactable=true, "정상"
        /// 색)으로 한 번 활성화된 직후 곧바로 Initialize가 실제 값(대개 false, 미보유)으로 바꾸면서
        /// 이 전환이 시작돼, "잠깐 불이 들어왔다가 서서히 회색으로 바뀌는" 깜빡임이 매번 보였다(실사용
        /// 중 발견). fadeDuration을 이 호출 한 번만 0으로 낮춰 전환을 즉시 끝내고, 이후 실제 플레이
        /// 중 호버/클릭 피드백에는 영향이 없도록 원래 값으로 복원한다.
        /// </summary>
        private static void SetInteractableInstant(Selectable selectable, bool interactable)
        {
            if (selectable == null)
            {
                return;
            }

            ColorBlock colors = selectable.colors;
            float originalFadeDuration = colors.fadeDuration;
            colors.fadeDuration = 0f;
            selectable.colors = colors;

            selectable.interactable = interactable;

            colors.fadeDuration = originalFadeDuration;
            selectable.colors = colors;
        }

        /// <summary>
        /// 카드 기본색에 등급색을 살짝 섞어, 텍스트 가독성을 해치지 않으면서 등급을 구분할 수 있게 한다.
        /// 슬롯 팝업/전체 목록 패널이 동일한 카드 색상 규칙을 쓰도록 공용 헬퍼로 둔다.
        /// </summary>
        public static Color ComputeGradeBackground(Color cardBaseColor, EquipmentGradeSO grade, float blend)
        {
            if (grade == null)
            {
                return cardBaseColor;
            }

            Color blended = Color.Lerp(cardBaseColor, grade.TintColor, blend);
            blended.a = cardBaseColor.a;
            return blended;
        }

        /// <summary>
        /// 행 데이터를 채운다. owned가 null이면(한 번도 획득한 적 없는 장비) 이름만 흐리게 표시하고
        /// 모든 버튼을 비활성화한 "미보유" 행으로 그린다 - 아직 획득 못 한 장비도 목록에 항상
        /// 보여줘서 "무엇이 존재하는지"를 알 수 있게 하되, 실제로 가진 게 없으니 아무 동작도 할 수
        /// 없게 막는다. owned가 있으면(개수가 0이어도) 기존과 동일하게 정상적으로 상호작용 가능한
        /// 행으로 그린다 - 개수 0은 InventoryService가 더 이상 라인을 지우지 않으므로 "한 번이라도
        /// 획득했다"는 뜻이고, 장착은 개수를 소모하지 않으니 계속 장착할 수 있어야 한다.
        ///
        /// 장착은 EquipmentEquippedEvent를 통해 목록이 알아서 갱신되므로 팝업을 닫는 등의 후속
        /// 동작은 필요 없다. onDetailRequested는 이름 라벨을 탭했을 때(장착과 별개 동작) 상세
        /// 팝업을 열어달라는 요청 콜백이다(없으면 null, 이름 탭은 아무 동작 안 함).
        /// onEnhanceRequested는 강화 버튼을 눌렀을 때 강화 팝업을 열어달라는 요청 콜백이다
        /// (없으면 null, 이 경우 예전처럼 즉시 EquipmentEnhancementService.TryEnhance를 호출한다).
        /// onFuseRequested는 합성 버튼을 눌렀을 때(없으면 null, 이 경우 예전처럼 즉시
        /// EquipmentFusionService.TryFuse를 호출한다) 같은 모양의 요청 콜백이다.
        /// </summary>
        public void Initialize(EquipmentSO definition, OwnedEquipment owned, bool isEquipped, Color backgroundColor, Action<OwnedEquipment> onDetailRequested = null, Action<OwnedEquipment> onEnhanceRequested = null, Action<OwnedEquipment> onFuseRequested = null)
        {
            _owned = owned;

            // EquipmentSlotPopupUI가 같은 행 인스턴스를 재사용해(Destroy+Instantiate 대신) 반복
            // Initialize할 수 있으므로, 매번 새로 AddListener하기 전에 이전 클로저를 반드시 지운다 -
            // 그렇지 않으면 재사용 횟수만큼 리스너가 쌓여 클릭 한 번에 합성/강화가 여러 번 실행되는
            // (재료가 의도보다 더 많이 소모되는) 심각한 부작용이 생긴다.
            equipButton.onClick.RemoveAllListeners();
            nameButton?.onClick.RemoveAllListeners();
            fuseButton.onClick.RemoveAllListeners();
            enhanceButton.onClick.RemoveAllListeners();

            if (owned == null)
            {
                background.color = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, backgroundColor.a * LockedBackgroundAlphaMultiplier);
                label.color = LockedTextColor;
                label.text = definition.ItemName;

                SetInteractableInstant(equipButton, false);
                equipButtonLabel.text = "미보유";
                SetInteractableInstant(fuseButton, false);
                SetInteractableInstant(enhanceButton, false);
                SetInteractableInstant(nameButton, false);

                return;
            }

            background.color = backgroundColor;
            label.color = Color.white;

            string equippedTag = isEquipped ? "✓ " : "";
            label.text = $"{equippedTag}{owned.Definition.ItemName} x{owned.Count} (강화 {owned.EnhancementLevel})";

            SetInteractableInstant(equipButton, !isEquipped);
            equipButtonLabel.text = isEquipped ? "장착됨" : "장착";

            equipButton.onClick.AddListener(() =>
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquippedGearService equippedGear))
                {
                    equippedGear.Equip(_owned);
                }
            });

            if (nameButton != null)
            {
                nameButton.onClick.AddListener(() => onDetailRequested?.Invoke(_owned));
            }

            fuseButton.onClick.AddListener(() =>
            {
                if (onFuseRequested != null)
                {
                    onFuseRequested.Invoke(_owned);
                    return;
                }

                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquipmentFusionService fusion))
                {
                    fusion.TryFuse(_owned.Definition);
                }
            });

            enhanceButton.onClick.AddListener(() =>
            {
                if (onEnhanceRequested != null)
                {
                    onEnhanceRequested.Invoke(_owned);
                    return;
                }

                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EquipmentEnhancementService enhancement))
                {
                    enhancement.TryEnhance(_owned.Definition);
                }
            });
        }
    }
}
