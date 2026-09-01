using Character;
using Core;
using Enhancement;
using Enhancement.Events;
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
    /// (테두리 있는) 슬롯이 있으면 그 슬롯에 바로 장착하고, 선택된 슬롯이 없으면 SkillSlotBarUI에
    /// "대기 중인 스킬"로 넘겨 사용자가 다음에 탭하는 슬롯에 장착되도록 한다(OnEquipClicked 참고).
    /// </summary>
    public sealed class SkillDetailPopupUI : MonoBehaviour, IDismissible
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
        private GameObject targetSlotRow;

        [SerializeField]
        private Image targetSlotIcon;

        [SerializeField]
        private Text targetSlotText;

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

        private BackNavigationService _backNavigationService;

        private void Awake()
        {
            if (GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _backNavigationService);
            }

            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
            levelUpButton.onClick.AddListener(OnLevelUpClicked);
            equipButton.onClick.AddListener(OnEquipClicked);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameBootstrapper.Events?.Subscribe<SkillCountChangedEvent>(OnSkillCountChanged);
            GameBootstrapper.Events?.Subscribe<GoldChangedEvent>(OnCurrencyChanged);
            GameBootstrapper.Events?.Subscribe<StatEnhancedEvent>(OnAttackPowerChanged);
            GameBootstrapper.Events?.Subscribe<EquipmentStatsChangedEvent>(OnAttackPowerChanged);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameBootstrapper.Events?.Unsubscribe<SkillCountChangedEvent>(OnSkillCountChanged);
            GameBootstrapper.Events?.Unsubscribe<GoldChangedEvent>(OnCurrencyChanged);
            GameBootstrapper.Events?.Unsubscribe<StatEnhancedEvent>(OnAttackPowerChanged);
            GameBootstrapper.Events?.Unsubscribe<EquipmentStatsChangedEvent>(OnAttackPowerChanged);
        }

        /// <summary>
        /// 지정된 스킬의 정보를 채워 팝업을 연다.
        /// </summary>
        public void Open(SkillSO definition)
        {
            _definition = definition;
            _isOpen = true;
            popupRoot.SetActive(true);
            _backNavigationService?.Register(this);
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
            _backNavigationService?.Unregister(this);
        }

        private void OnSkillLeveledUp(SkillLeveledUpEvent evt)
        {
            if (_isOpen && evt.Definition == _definition)
            {
                Refresh();
            }
        }

        private void OnSkillCountChanged(SkillCountChangedEvent evt)
        {
            if (_isOpen && evt.Definition == _definition)
            {
                Refresh();
            }
        }

        // 팝업이 열려있는 동안 전투 등으로 골드가 바뀌면 보유량 표시가 바로 최신 상태를
        // 따라가도록 한다.
        private void OnCurrencyChanged(GoldChangedEvent evt)
        {
            RefreshIfOpen();
        }

        // statsText의 데미지 프리뷰가 playerStats.Stats.AttackPower를 직접 읽어 계산하므로,
        // 팝업이 열려 있는 동안 강화/장비 착용으로 공격력이 바뀌면 다시 그려야 값이 어긋나지 않는다.
        private void OnAttackPowerChanged(StatEnhancedEvent evt)
        {
            if (evt.StatType == EnhancementStatType.AttackPower)
            {
                RefreshIfOpen();
            }
        }

        private void OnAttackPowerChanged(EquipmentStatsChangedEvent evt)
        {
            if (evt.StatType == EnhancementStatType.AttackPower)
            {
                RefreshIfOpen();
            }
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

        // SkillSlotBarUI에서 현재 선택된(테두리 있는) 슬롯이 있으면 이 스킬을 바로 그 슬롯에
        // 장착한다. 선택된 슬롯이 없으면 즉시 실패하는 대신, 이 스킬을 SkillSlotBarUI에 "대기 중인
        // 스킬"로 넘겨(RequestEquipTarget) 안내 텍스트를 띄우고 팝업을 닫는다 - 이후 사용자가
        // 슬롯 바에서 어떤 슬롯이든 탭하면 그 슬롯에 장착된다(SkillSlotBarUI.OnSlotTapped 참고).
        private void OnEquipClicked()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout))
            {
                return;
            }

            if (slotBar != null && slotBar.SelectedSlotIndex >= 0)
            {
                if (loadout.TryEquip(slotBar.SelectedSlotIndex, _definition))
                {
                    // 장착 완료 후 선택을 해제한다 - 남겨두면 다음에 다른 스킬을 장착하려고
                    // 다른 슬롯을 탭했을 때 "새 선택"이 아니라 "자리 교환"으로 잘못 해석된다
                    // (SkillSlotBarUI.ClearSelection 참고).
                    slotBar.ClearSelection();
                    Close();
                }

                return;
            }

            slotBar?.RequestEquipTarget(_definition);
            Close();
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
            nameText.text = $"{_definition.DisplayName} [{SkillGradeDisplayNames.Get(_definition.Grade)}]";

            int level = service.GetLevel(_definition);
            bool isMax = level >= _definition.MaxLevel;
            float magnitude = _definition.GetMagnitude(level);
            int count = service.GetCount(_definition);
            int requiredCount = service.GetRequiredCount(_definition);

            levelText.text = $"Lv. {level} / {_definition.MaxLevel} (보유 {count}개)";
            statsText.text = BuildStatsText(magnitude, level);
            materialText.text = BuildMaterialText(level, isMax, count, requiredCount);
            levelUpButton.interactable = !isMax && count >= requiredCount
                && (level == 0 || HasEnoughCurrency(level));

            // 미습득(레벨 0) 스킬만 장착할 수 없다 - 슬롯 미선택은 더 이상 버튼을 막지 않고,
            // OnEquipClicked에서 "슬롯 선택 대기" 흐름으로 넘어간다.
            equipButton.interactable = level >= 1;

            RefreshTargetSlotRow();
        }

        // 이 팝업(Card)이 화면 중앙을 넓게 덮어, 이미 선택된(테두리 있는) 슬롯이 뒤에 가려져
        // 안 보이는 문제가 실사용 중 발견됐다 - 어느 슬롯에 장착될지를 팝업 안에서 아이콘+테두리로
        // 직접 보여준다. 슬롯이 아직 선택되지 않았으면(장착 버튼을 누른 뒤에야 슬롯을 고르는
        // RequestEquipTarget 흐름, OnEquipClicked 참고) 장착 대상이 아직 정해지지 않았으므로 숨긴다.
        private void RefreshTargetSlotRow()
        {
            if (targetSlotRow == null)
            {
                return;
            }

            int selectedSlotIndex = slotBar != null ? slotBar.SelectedSlotIndex : -1;

            if (selectedSlotIndex < 0)
            {
                targetSlotRow.SetActive(false);
                return;
            }

            targetSlotRow.SetActive(true);
            targetSlotText.text = $"{selectedSlotIndex + 1}번 슬롯에 장착됩니다.";

            SkillSO equipped = null;
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout))
            {
                equipped = loadout.GetEquipped(selectedSlotIndex);
            }

            if (equipped != null)
            {
                targetSlotIcon.sprite = equipped.Icon;
                targetSlotIcon.color = equipped.IconTint;
            }
            else
            {
                targetSlotIcon.sprite = null;
                targetSlotIcon.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            }
        }

        // 효과 타입에 따라 의미 있는 스펙만 나열한다 - 예를 들어 지속시간은 SelfBuff에만 있는
        // 값이라 다른 타입에는 표시하지 않는다. 데미지 계열(AreaDamage/SingleTargetStrike)은
        // 실전에서 (공격력 + magnitude)로 들어가므로(각 이펙트의 Execute와 동일한 공식), 그 합계를
        // 내역과 함께 보여줘 스킬이 평타보다 항상 세다는 걸 바로 확인할 수 있게 한다.
        // 모든 케이스의 첫 줄은 예외 없이 "대상: X" 형식으로 통일한다 - 데미지/디버프 계열은
        // 화면에 굳이 안 적어도 "적"이 대상인 게 자명해 보이지만, SelfBuff/PartyHeal처럼 실제로는
        // 플레이어 본인뿐 아니라 병사 전체에게도 같은 비율로 적용되는 경우(SkillSelfBuffAppliedEvent/
        // SkillPartyHealAppliedEvent를 Soldier.SoldierStatReceiver가 구독)는 수치만 보면 플레이어
        // 전용처럼 오해하기 쉬웠다 - 한쪽만 명시하면 형식이 들쭉날쭉해 오히려 더 헷갈리므로,
        // 모든 효과 타입에 예외 없이 대상 줄을 넣는다.
        private string BuildStatsText(float magnitude, int level)
        {
            float attackPower = playerStats != null ? playerStats.Stats.AttackPower : 0f;

            switch (_definition.EffectType)
            {
                case SkillEffectType.AreaDamage:
                    return $"대상: 적\n데미지 {attackPower + magnitude:F0} (공격력 {attackPower:F0} + {magnitude:F0})\n범위 {_definition.AreaRadius:F1}";
                case SkillEffectType.SingleTargetStrike:
                    return $"대상: 적\n데미지 {attackPower + magnitude:F0} (공격력 {attackPower:F0} + {magnitude:F0})\n공격 거리 {_definition.StrikeRange:F1}";
                case SkillEffectType.SelfBuff:
                    return $"대상: 플레이어+병사\n공격력 증가 {magnitude * 100f:F0}%\n지속시간 {_definition.GetBuffDuration(level):F1}초";
                case SkillEffectType.Poison:
                    return $"대상: 적\n독 데미지 {attackPower + magnitude:F0}/{_definition.TickInterval:F1}초 (공격력 {attackPower:F0} + {magnitude:F0})\n지속시간 {_definition.GetBuffDuration(level):F1}초\n사거리 {_definition.StrikeRange:F1}";
                case SkillEffectType.Whirlwind:
                    return $"대상: 적\n데미지 {attackPower + magnitude:F0}/{_definition.TickInterval:F1}초 (공격력 {attackPower:F0} + {magnitude:F0})\n범위 {_definition.AreaRadius:F1}\n지속시간 {_definition.GetBuffDuration(level):F1}초";
                case SkillEffectType.Meteor:
                    return $"대상: 적\n포탄당 데미지 {attackPower + magnitude:F0} (공격력 {attackPower:F0} + {magnitude:F0})\n포탄 수 {_definition.MeteorShellCount}개\n범위 {_definition.AreaRadius:F1}\n예고 시간 {_definition.MeteorTelegraphDuration:F1}초";
                case SkillEffectType.Debuff:
                    return $"대상: 적\n이동속도/공격속도 감소 {magnitude * 100f:F0}%\n지속시간 {_definition.GetBuffDuration(level):F1}초\n사거리 {_definition.StrikeRange:F1}";
                case SkillEffectType.Curse:
                    return $"대상: 적\n최대체력/공격력 감소 {magnitude * 100f:F0}%\n지속시간 {_definition.GetBuffDuration(level):F1}초\n사거리 {_definition.StrikeRange:F1}";
                case SkillEffectType.SoldierBuff:
                    return $"대상: 병사\n이동속도/공격속도 증가 {magnitude * 100f:F0}%\n지속시간 {_definition.GetBuffDuration(level):F1}초";
                case SkillEffectType.PartyHeal:
                    return $"대상: 플레이어+병사\n공격력 증가 {magnitude * 100f:F0}%\n초당 회복 {_definition.GetHealPercentPerSecond(level) * 100f:F1}%\n지속시간 {_definition.GetBuffDuration(level):F1}초";
                default:
                    return "";
            }
        }

        // 다음 레벨에 필요한 재료(중복 스킬 개수 + 0강 구간이 아니면 골드)와 현재 보유량을 나란히
        // 보여주고, 부족한 쪽만 빨간색으로 강조한다. 0강 -> 1강은 중복 스킬 1개만 있으면 무료라
        // 골드 줄 자체를 보여줄 필요가 없다. requiredCount는 SkillService.GetRequiredCount가
        // 이미 레벨업마다 1개씩 늘어나는 값을 계산해준다.
        private string BuildMaterialText(int level, bool isMax, int count, int requiredCount)
        {
            if (isMax)
            {
                return "MAX";
            }

            string countLine = FormatMaterialLine("중복 스킬", requiredCount, count);

            if (level == 0)
            {
                return $"{countLine}\n(무료 습득 — 골드 불필요)";
            }

            int goldNeeded = _definition.GetGoldCost(level);
            BigNumber goldOwned = BigNumber.Zero;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CurrencyService currency))
            {
                goldOwned = currency.CurrentGold;
            }

            string goldLine = FormatMaterialLine("골드", goldNeeded, goldOwned);

            return $"{countLine}\n{goldLine}";
        }

        // levelUpButton의 활성 조건 중 재화 충분 여부만 따로 뗀 헬퍼 — 복제본 개수는 Refresh에서
        // 이미 확인했으므로 여기서는 골드만 본다(0강 구간은 이 메서드 자체를 호출하지 않는다).
        private bool HasEnoughCurrency(int level)
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out CurrencyService currency))
            {
                return false;
            }

            return currency.CurrentGold >= _definition.GetGoldCost(level);
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

        bool IDismissible.TryDismiss()
        {
            Close();
            return true;
        }
    }
}
