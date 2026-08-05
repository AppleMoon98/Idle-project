using Character;
using Core;
using Equipment;
using Equipment.Events;
using Loot;
using Loot.Events;
using Skill;
using Skill.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 스킬 하나의 상세 정보(아이콘/레벨/스펙/다음 강화 재료)와 레벨업/장착 버튼을 보여주는 팝업.
    /// SkillGridUI가 칸을 탭하면 이 팝업을 연다. 장착 버튼은 SkillSlotBarUI에서 현재 선택된
    /// (테두리 있는) 슬롯에 이 스킬을 장착한다.
    /// </summary>
    public sealed class SkillDetailPopupUI : MonoBehaviour
    {
        private const string InsufficientColorHex = "#ff6b6b";

        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Image icon;

        [SerializeField]
        private Text nameText;

        [SerializeField]
        private Text levelText;

        [SerializeField]
        private Text statsText;

        [SerializeField]
        private Text materialText;

        [SerializeField]
        private Button levelUpButton;

        [SerializeField]
        private Button equipButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private CharacterStatsProvider playerStats;

        [SerializeField]
        private SkillSlotBarUI slotBar;

        private SkillSO _definition;
        private bool _isOpen;

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
            levelUpButton.onClick.AddListener(OnLevelUpClicked);
            equipButton.onClick.AddListener(OnEquipClicked);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameBootstrapper.Events?.Subscribe<GoldChangedEvent>(OnCurrencyChanged);
            GameBootstrapper.Events?.Subscribe<EnhancementStoneChangedEvent>(OnCurrencyChanged);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameBootstrapper.Events?.Unsubscribe<GoldChangedEvent>(OnCurrencyChanged);
            GameBootstrapper.Events?.Unsubscribe<EnhancementStoneChangedEvent>(OnCurrencyChanged);
        }

        /// <summary>
        /// 지정된 스킬의 정보를 채워 팝업을 연다.
        /// </summary>
        public void Open(SkillSO definition)
        {
            _definition = definition;
            _isOpen = true;
            popupRoot.SetActive(true);
            Refresh();
        }

        /// <summary>
        /// 팝업을 닫는다. SkillSlotBarUI가 자신이 비활성화될 때(스킬 탭을 닫을 때) 같이 닫기 위해
        /// 외부에서 호출할 수 있어야 한다(EquipmentSlotPopupUI.Close와 동일한 이유).
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            popupRoot.SetActive(false);
        }

        private void OnSkillLeveledUp(SkillLeveledUpEvent evt)
        {
            if (_isOpen && evt.Definition == _definition)
            {
                Refresh();
            }
        }

        // 팝업이 열려있는 동안 전투 등으로 골드/강화석이 바뀌면 보유량 표시가 바로 최신 상태를
        // 따라가도록 한다 - 어느 재화가 바뀌었는지는 구분할 필요 없이 통째로 다시 그린다.
        private void OnCurrencyChanged(GoldChangedEvent evt)
        {
            RefreshIfOpen();
        }

        private void OnCurrencyChanged(EnhancementStoneChangedEvent evt)
        {
            RefreshIfOpen();
        }

        private void RefreshIfOpen()
        {
            if (_isOpen)
            {
                Refresh();
            }
        }

        private void OnLevelUpClicked()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillService service))
            {
                service.TryLevelUp(_definition);
            }
        }

        // SkillSlotBarUI에서 현재 선택된(테두리 있는) 슬롯에 이 스킬을 장착한다. 그리드에서
        // 칸을 탭했을 때 바로 장착되던 것을 이 버튼으로 옮겼다 - 어떤 스킬인지 먼저 상세를 보고
        // 나서 장착 여부를 결정할 수 있게 하기 위함이다.
        private void OnEquipClicked()
        {
            if (slotBar == null || slotBar.SelectedSlotIndex < 0
                || GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout))
            {
                return;
            }

            if (loadout.TryEquip(slotBar.SelectedSlotIndex, _definition))
            {
                Close();
            }
        }

        private void Refresh()
        {
            if (_definition == null
                || GameBootstrapper.Services == null
                || !GameBootstrapper.Services.TryGet(out SkillService service))
            {
                return;
            }

            icon.sprite = _definition.Icon;
            icon.color = _definition.IconTint;
            nameText.text = _definition.DisplayName;

            int level = service.GetLevel(_definition);
            bool isMax = level >= _definition.MaxLevel;
            float magnitude = _definition.GetMagnitude(level);

            levelText.text = $"Lv. {level} / {_definition.MaxLevel}";
            statsText.text = BuildStatsText(magnitude);
            materialText.text = BuildMaterialText(level, isMax);
            levelUpButton.interactable = !isMax;

            // 미습득(레벨 0) 스킬이거나 선택된 슬롯이 없으면 장착할 수 없다.
            equipButton.interactable = level >= 1 && slotBar != null && slotBar.SelectedSlotIndex >= 0;
        }

        // 효과 타입에 따라 의미 있는 스펙만 나열한다 - 예를 들어 지속시간은 SelfBuff에만 있는
        // 값이라 다른 타입에는 표시하지 않는다. 데미지 계열(AreaDamage/SingleTargetStrike)은
        // 실전에서 (공격력 + magnitude)로 들어가므로(각 이펙트의 Execute와 동일한 공식), 그 합계를
        // 내역과 함께 보여줘 스킬이 평타보다 항상 세다는 걸 바로 확인할 수 있게 한다.
        private string BuildStatsText(float magnitude)
        {
            float attackPower = playerStats != null ? playerStats.Stats.AttackPower : 0f;

            switch (_definition.EffectType)
            {
                case SkillEffectType.AreaDamage:
                    return $"데미지 {attackPower + magnitude:F0} (공격력 {attackPower:F0} + {magnitude:F0})\n범위 {_definition.AreaRadius:F1}";
                case SkillEffectType.SingleTargetStrike:
                    return $"데미지 {attackPower + magnitude:F0} (공격력 {attackPower:F0} + {magnitude:F0})\n공격 거리 {_definition.StrikeRange:F1}";
                case SkillEffectType.SelfBuff:
                    return $"공격력 증가 {magnitude:F0}\n지속시간 {_definition.BuffDuration:F1}초";
                default:
                    return "";
            }
        }

        // 다음 레벨에 필요한 재화와 현재 보유량을 나란히 보여주고, 부족한 쪽만 빨간색으로 강조한다.
        private string BuildMaterialText(int level, bool isMax)
        {
            if (isMax)
            {
                return "MAX";
            }

            int goldNeeded = _definition.GetGoldCost(level);
            int stoneNeeded = _definition.GetStoneCost(level);
            BigNumber goldOwned = BigNumber.Zero;
            int stoneOwned = 0;

            if (GameBootstrapper.Services != null)
            {
                if (GameBootstrapper.Services.TryGet(out CurrencyService currency))
                {
                    goldOwned = currency.CurrentGold;
                }

                if (GameBootstrapper.Services.TryGet(out EnhancementStoneService stones))
                {
                    stoneOwned = stones.CurrentStones;
                }
            }

            string goldLine = FormatMaterialLine("골드", goldNeeded, goldOwned);
            string stoneLine = FormatMaterialLine("강화석", stoneNeeded, stoneOwned);

            return $"{goldLine}\n{stoneLine}";
        }

        private static string FormatMaterialLine(string label, int needed, int owned)
        {
            string line = $"{label} {needed} / 보유 {owned}";
            return owned < needed ? $"<color={InsufficientColorHex}>{line}</color>" : line;
        }

        private static string FormatMaterialLine(string label, int needed, BigNumber owned)
        {
            string line = $"{label} {needed} / 보유 {KoreanNumberFormatter.Format(owned)}";
            return owned < needed ? $"<color={InsufficientColorHex}>{line}</color>" : line;
        }
    }
}
