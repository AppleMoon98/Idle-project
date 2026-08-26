using System.Collections.Generic;
using System.Text;
using Character;
using Combat;
using Core;
using Enhancement;
using Managers;
using Soldier;
using SoldierEnhancement;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// SoldierPanel의 로스터 슬롯(SoldierRosterRow)을 탭하면 여는 상세 팝업 - 맨 위에 이 병사의
    /// 실제 Idle 애니메이션이 재생되는 미리보기, 그 아래 가운데 정렬된 스탯 목록을 보여준다.
    /// 병사 전용 장비 시스템(section FY, 완전 삭제됨)이 열던 SoldierEquipmentPopupUI를 대체하는
    /// 것이 아니라 - 그건 개별 유닛의 장착 상태를 다뤘지만, 이건 SoldierSO(병종+등급) 원형 하나를
    /// 순수 조회만 한다.
    ///
    /// **미리보기가 실제 스프라이트 시트 Animator를 그대로 재생하는 이유:** 정적 아이콘이 아니라
    /// 진짜 Idle 애니메이션을 보여달라는 요청이었다 - UI Image만으로는 빌드에서도 동작하는 진짜
    /// 애니메이션을 재현할 수 없다(AnimationClip 키프레임을 런타임에 읽는 UnityEditor.AnimationUtility는
    /// 에디터 전용이라 빌드에 못 씀). 그래서 실제 SoldierSO.Prefab을 화면 밖 먼 위치(previewStage)에
    /// Instantiate하고, 그 자리만 비추는 전용 카메라(Preview 레이어, RenderTexture 타겟)로 찍어
    /// RawImage에 띄운다 - 프리팹이 원래 갖고 있는 Animator/*AnimationController가 그대로 Idle을
    /// 재생해준다(움직일 대상이 없으면 모든 병과 컨트롤러가 기본으로 Idle 상태다).
    ///
    /// **스탯은 인스턴스를 스폰하지 않고 순수 계산으로 구한다:** 병사 등급 보너스(Soldier.
    /// SoldierGradeScaler)는 "플레이어 현재 스탯 × 등급 지분율"이고, 병사 강화(SoldierEnhancement.
    /// SoldierEnhancementService)는 전역 누적 보너스라 - 둘 다 살아있는 병사 없이도 그대로
    /// 재현 가능하다(Soldier.SquadMovementSyncService가 "본연 속도"를 BaseStats+강화에서 매번
    /// 독립적으로 재계산하는 것과 같은 관례, section DK). 미리보기 인스턴스는 순수 시각 전용이라
    /// SoldierGradeScaler/SoldierStatReceiver를 아예 비활성화해둔다 - 스탯 계산과 무관하다.
    /// </summary>
    public sealed class SoldierDetailPopupUI : MonoBehaviour, IDismissible
    {
        private static readonly EnhancementStatType[] DisplayStatOrder =
        {
            EnhancementStatType.AttackPower,
            EnhancementStatType.MaxHealth,
            EnhancementStatType.AttackSpeed,
            EnhancementStatType.MoveSpeed,
            EnhancementStatType.CriticalChance,
            EnhancementStatType.CriticalDamage
        };

        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private Text nameText;

        [SerializeField]
        private Text statsText;

        [SerializeField]
        private Transform previewStage;

        [SerializeField]
        private SoldierGradeConfigSO gradeConfig;

        private GameObject _previewInstance;

        private BackNavigationService _backNavigationService;

        private void Awake()
        {
            if (GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _backNavigationService);
            }

            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        public void Open(SoldierSO definition)
        {
            if (definition == null)
            {
                return;
            }

            nameText.text = definition.DisplayName;
            statsText.text = BuildStatsText(ComputeStats(definition));
            SpawnPreview(definition.Prefab);

            popupRoot.SetActive(true);
            _backNavigationService?.Register(this);
        }

        public void Close()
        {
            popupRoot.SetActive(false);
            _backNavigationService?.Unregister(this);
            DestroyPreview();
        }

        private void SpawnPreview(GameObject prefab)
        {
            DestroyPreview();

            if (prefab == null || previewStage == null)
            {
                return;
            }

            _previewInstance = Instantiate(prefab, previewStage.position, previewStage.rotation, previewStage);
            SetLayerRecursively(_previewInstance, previewStage.gameObject.layer);
            DisableGameplayComponents(_previewInstance);
        }

        private void DestroyPreview()
        {
            if (_previewInstance != null)
            {
                Destroy(_previewInstance);
                _previewInstance = null;
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;

            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>
        /// 미리보기 인스턴스는 순수 시각(Animator의 Idle 재생)만 필요하다 - 이동/타겟팅/공격/
        /// 스탯 재계산을 담당하는 컴포넌트를 전부 꺼서 화면 밖 무대에서 조용히 Idle만 재생하게
        /// 한다. 8개 병과가 서로 다른 컴포넌트 조합을 갖지만(예: 창병만 FormationFollower) 없는
        /// 타입은 GetComponent가 null을 반환해 자연히 건너뛴다.
        /// </summary>
        private static void DisableGameplayComponents(GameObject instance)
        {
            DisableIfPresent<EnemyTracker>(instance);
            DisableIfPresent<Attacker>(instance);
            DisableIfPresent<CharacterMover>(instance);
            DisableIfPresent<CharacterSeparation>(instance);
            DisableIfPresent<KnockbackReceiver>(instance);
            DisableIfPresent<RangedKiter>(instance);
            DisableIfPresent<RangedAttackTelegraph>(instance);
            DisableIfPresent<FormationFollower>(instance);
            DisableIfPresent<BearCharge>(instance);
            DisableIfPresent<GuardPositioner>(instance);
            DisableIfPresent<ShieldFacing>(instance);
            DisableIfPresent<SoldierBehaviorController>(instance);
            DisableIfPresent<SoldierGradeScaler>(instance);
            DisableIfPresent<SoldierStatReceiver>(instance);
            DisableIfPresent<PoolReleaseOnDeath>(instance);
        }

        private static void DisableIfPresent<T>(GameObject instance) where T : Behaviour
        {
            if (instance.TryGetComponent(out T component))
            {
                component.enabled = false;
            }
        }

        /// <summary>
        /// definition.Prefab의 원본 CharacterStatsSO에서 시작해, 병사 강화(전역 누적)와 등급 지분
        /// (플레이어 현재 스탯 대비 %, AttackPower/MaxHealth만) 순으로 얹어 최종 스탯을 계산한다 -
        /// Soldier.SoldierStatReceiver.ApplyCumulativeFromBase + Soldier.SoldierGradeScaler.Recompute를
        /// 인스턴스 없이 그대로 재현한 것.
        /// </summary>
        private RuntimeStats ComputeStats(SoldierSO definition)
        {
            CharacterStatsProvider prefabStatsProvider = definition.Prefab != null
                ? definition.Prefab.GetComponent<CharacterStatsProvider>()
                : null;

            if (prefabStatsProvider == null || prefabStatsProvider.BaseStats == null)
            {
                return new RuntimeStats(ScriptableObject.CreateInstance<CharacterStatsSO>());
            }

            CharacterStatsSO baseStats = prefabStatsProvider.BaseStats;
            var stats = new RuntimeStats(baseStats);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SoldierEnhancementService enhancement))
            {
                foreach (EnhancementStatType statType in enhancement.StatTypes)
                {
                    float cumulativeDelta = enhancement.GetValuePerLevel(statType) * enhancement.GetLevel(statType);
                    RuntimeStatApplier.Apply(stats, baseStats, statType, cumulativeDelta);
                }
            }

            if (definition.Grade != null && gradeConfig != null)
            {
                PlayerMarker playerMarker = FindFirstObjectByType<PlayerMarker>();
                CharacterStatsProvider playerStats = playerMarker != null ? playerMarker.GetComponent<CharacterStatsProvider>() : null;

                if (playerStats != null)
                {
                    float percent = gradeConfig.GetPercent(definition.Grade);
                    stats.AttackPower += playerStats.Stats.AttackPower * percent;
                    stats.MaxHealth += playerStats.Stats.MaxHealth * percent;
                }
            }

            return stats;
        }

        private static string BuildStatsText(RuntimeStats stats)
        {
            var builder = new StringBuilder();

            foreach (EnhancementStatType statType in DisplayStatOrder)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(StatDisplayNames.Get(statType));
                builder.Append(": ");
                builder.Append(FormatAbsoluteValue(statType, stats));
            }

            return builder.ToString();
        }

        /// <summary>
        /// UI.StatDisplayNames.FormatValue는 "레벨당 증분"(비율)을 포맷하는 용도라, 여기서 보여줄
        /// "최종 절대 스탯값"에는 맞지 않는다(RuntimeStats.MoveSpeed는 이미 절대 유닛/초 값이지
        /// 비율이 아니다) - 그래서 이 팝업 전용으로 별도 포맷팅을 둔다.
        /// </summary>
        private static string FormatAbsoluteValue(EnhancementStatType statType, RuntimeStats stats)
        {
            return statType switch
            {
                EnhancementStatType.AttackPower => stats.AttackPower.ToString("0.#"),
                EnhancementStatType.MaxHealth => stats.MaxHealth.ToString("0.#"),
                EnhancementStatType.AttackSpeed => $"{(stats.AttackInterval > 0f ? 1f / stats.AttackInterval : 0f):0.##}/초",
                EnhancementStatType.MoveSpeed => stats.MoveSpeed.ToString("0.##"),
                EnhancementStatType.CriticalChance => $"{stats.CriticalChance * 100f:0.#}%",
                EnhancementStatType.CriticalDamage => $"{stats.CriticalDamageMultiplier * 100f:0.#}%",
                _ => ""
            };
        }

        bool IDismissible.TryDismiss()
        {
            Close();
            return true;
        }
    }
}
