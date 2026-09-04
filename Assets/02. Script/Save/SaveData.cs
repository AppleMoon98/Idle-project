using Core;

namespace Save
{
    /// <summary>
    /// PlayerPrefs에 저장/로드되는 최소 게임 상태 스냅샷.
    /// 오프라인 보상 계산에 필요한 값만 담는다(전체 게임 상태 저장이 아님).
    /// </summary>
    public readonly struct SaveData
    {
        /// <summary>
        /// 마지막으로 저장된 보유 골드.
        /// </summary>
        public BigNumber Gold { get; }

        /// <summary>
        /// 마지막으로 저장된 보유 강화석.
        /// </summary>
        public int EnhancementStones { get; }

        /// <summary>
        /// 마지막으로 저장된, 현재 진행 중인(도전/반복 대상) 챕터 번호.
        /// </summary>
        public int Chapter { get; }

        /// <summary>
        /// 마지막으로 저장된, 현재 진행 중인(도전/반복 대상) 챕터 내 스테이지 번호.
        /// </summary>
        public int StageNumber { get; }

        /// <summary>
        /// 역대 최고로 클리어한 챕터 번호. 사망으로 후퇴해도 낮아지지 않는다. 기록이 없으면 0.
        /// </summary>
        public int HighestClearedChapter { get; }

        /// <summary>
        /// 역대 최고로 클리어한 챕터 내 스테이지 번호. 기록이 없으면 0.
        /// </summary>
        public int HighestClearedStageNumber { get; }

        /// <summary>
        /// 마지막 저장 시각(UTC 유닉스 타임, 초). 저장 기록이 없으면 0.
        /// </summary>
        public long LastActiveUnixTime { get; }

        /// <summary>
        /// 마지막으로 저장된 공격력 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int AttackPowerLevel { get; }

        /// <summary>
        /// 마지막으로 저장된 최대체력 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int MaxHealthLevel { get; }

        /// <summary>
        /// 마지막으로 저장된 공격속도 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int AttackSpeedLevel { get; }

        /// <summary>
        /// 마지막으로 저장된 이동속도 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int MoveSpeedLevel { get; }

        /// <summary>
        /// 마지막으로 저장된 크리티컬 확률 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int CriticalChanceLevel { get; }

        /// <summary>
        /// 마지막으로 저장된 크리티컬 피해량 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int CriticalDamageLevel { get; }

        /// <summary>
        /// 보유 장비/슬롯별 장착 상태를 직렬화한 JSON. 가변 길이 컬렉션이라 개별 필드가 아니라
        /// 통째로 문자열 하나로 저장한다(SaveService.RestoreInventory가 파싱/복원한다). 기록이 없으면 빈 문자열.
        /// </summary>
        public string InventoryJson { get; }

        /// <summary>
        /// 마지막으로 저장된 랭크의 RankCatalogSO 상 인덱스. 기록이 없으면 0(시작 랭크).
        /// </summary>
        public int RankIndex { get; }

        /// <summary>
        /// 마지막으로 저장된 보유 병사 소환권. 기록이 없으면 0.
        /// </summary>
        public int SoldierTicketCount { get; }

        /// <summary>
        /// 보유 병사 로스터(개별 유닛 + 다음 발급 번호)를 직렬화한 JSON. InventoryJson과 같은 이유로
        /// 통째로 문자열 하나로 저장한다(SaveService.RestoreSoldierRoster가 파싱/복원한다). 기록이 없으면 빈 문자열.
        /// </summary>
        public string SoldierRosterJson { get; }

        /// <summary>
        /// 스킬별 레벨을 직렬화한 JSON. InventoryJson과 같은 이유로 통째로 문자열 하나로 저장한다
        /// (SaveService.RestoreSkills가 파싱/복원한다). 기록이 없으면 빈 문자열.
        /// </summary>
        public string SkillLevelsJson { get; }

        /// <summary>
        /// 장착 슬롯별 스킬을 직렬화한 JSON. InventoryJson과 같은 이유로 통째로 문자열 하나로
        /// 저장한다(SaveService.RestoreSkillLoadout이 파싱/복원한다). 기록이 없으면 빈 문자열.
        /// </summary>
        public string SkillLoadoutJson { get; }

        /// <summary>
        /// 꺼진(자동 발동하지 않는) 장착 슬롯 인덱스 목록을 직렬화한 JSON. InventoryJson과 같은
        /// 이유로 통째로 문자열 하나로 저장한다(SaveService.RestoreSkillEnabled가 파싱/복원한다).
        /// 기록이 없으면 빈 문자열(= 전부 켜짐).
        /// </summary>
        public string SkillEnabledJson { get; }

        /// <summary>
        /// 마지막으로 저장된 병사 공격력 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int SoldierAttackPowerLevel { get; }

        /// <summary>
        /// 마지막으로 저장된 병사 최대체력 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int SoldierMaxHealthLevel { get; }

        /// <summary>
        /// 마지막으로 저장된 병사 공격속도 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int SoldierAttackSpeedLevel { get; }

        /// <summary>
        /// 마지막으로 저장된 병사 이동속도 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int SoldierMoveSpeedLevel { get; }

        /// <summary>
        /// 마지막으로 저장된 병사 크리티컬 확률 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int SoldierCriticalChanceLevel { get; }

        /// <summary>
        /// 마지막으로 저장된 병사 크리티컬 피해량 강화 레벨. 기록이 없으면 0.
        /// </summary>
        public int SoldierCriticalDamageLevel { get; }

        /// <summary>
        /// 마지막으로 저장된 보유 스킬 주문서. 기록이 없으면 0.
        /// </summary>
        public int SkillScrollCount { get; }

        /// <summary>
        /// 스킬별 보유 개수(레벨업 재료)를 직렬화한 JSON. InventoryJson과 같은 이유로 통째로
        /// 문자열 하나로 저장한다(SaveService.RestoreSkillCounts가 파싱/복원한다). 기록이 없으면 빈 문자열.
        /// </summary>
        public string SkillCountsJson { get; }

        /// <summary>
        /// 마지막으로 저장된 보유 무기 뽑기권. 기록이 없으면 0.
        /// </summary>
        public int EquipmentGachaTicketCount { get; }

        /// <summary>
        /// 부대별 배정된 전술(None 제외)을 직렬화한 JSON. InventoryJson과 같은 이유로 통째로
        /// 문자열 하나로 저장한다(SaveService.RestoreSquadTactics가 파싱/복원한다). 기록이 없으면
        /// 빈 문자열(= 전 부대 전술 없음).
        /// </summary>
        public string SquadTacticsJson { get; }

        /// <summary>
        /// 병사 골드 뽑기(GachaService) 테이블별 누적 뽑기 횟수를 직렬화한 JSON. InventoryJson과
        /// 같은 이유로 통째로 문자열 하나로 저장한다(SaveService.RestoreGachaPullCounts가
        /// 파싱/복원한다). 기록이 없으면 빈 문자열(= 전부 0회).
        /// </summary>
        public string SoldierGachaGoldPullCountsJson { get; }

        /// <summary>
        /// 스킬 골드 뽑기(SkillGachaService) 테이블별 누적 뽑기 횟수를 직렬화한 JSON. 나머지는
        /// SoldierGachaGoldPullCountsJson과 같다.
        /// </summary>
        public string SkillGachaGoldPullCountsJson { get; }

        /// <summary>
        /// 마지막으로 저장된 보유 보스 토벌 증표. 기록이 없으면 0.
        /// </summary>
        public int BossTokenCount { get; }

        /// <summary>
        /// 마지막으로 관측된 기기 부팅-이후 경과시간(초, Android SystemClock.elapsedRealtime
        /// 기반). 벽시계와 무관하게 흐르는 신호라 GitHub 이슈 #71(오프라인 보상 시계 조작
        /// 방지, Offline.OfflineElapsedTimeCalculator 참고)에 쓰인다. 이 신호가 없는
        /// 플랫폼(iOS/Standalone/에디터)이거나 기록이 없으면 0.
        /// </summary>
        public long LastElapsedRealtimeSeconds { get; }

        /// <summary>
        /// 게임 최초 실행 인트로 스토리(Story.StorySO)를 끝까지 보거나 스킵해 완료했는지 여부.
        /// 기록이 없으면 false(아직 안 봄) - Core.GameBootstrapper.Start()가 이 값이 false일 때만
        /// 인트로 스토리를 재생한다.
        /// </summary>
        public bool HasSeenIntroStory { get; }

        public SaveData(
            BigNumber gold,
            int enhancementStones,
            int chapter,
            int stageNumber,
            int highestClearedChapter,
            int highestClearedStageNumber,
            long lastActiveUnixTime,
            int attackPowerLevel,
            int maxHealthLevel,
            int attackSpeedLevel,
            int moveSpeedLevel,
            int criticalChanceLevel,
            int criticalDamageLevel,
            string inventoryJson,
            int rankIndex,
            int soldierTicketCount,
            string soldierRosterJson,
            string skillLevelsJson,
            int soldierAttackPowerLevel,
            int soldierMaxHealthLevel,
            int soldierAttackSpeedLevel,
            int soldierMoveSpeedLevel,
            int soldierCriticalChanceLevel,
            int soldierCriticalDamageLevel,
            string skillLoadoutJson,
            string skillEnabledJson,
            int skillScrollCount,
            string skillCountsJson,
            int equipmentGachaTicketCount,
            string squadTacticsJson,
            string soldierGachaGoldPullCountsJson,
            string skillGachaGoldPullCountsJson,
            int bossTokenCount,
            long lastElapsedRealtimeSeconds,
            bool hasSeenIntroStory)
        {
            Gold = gold;
            EnhancementStones = enhancementStones;
            Chapter = chapter;
            StageNumber = stageNumber;
            HighestClearedChapter = highestClearedChapter;
            HighestClearedStageNumber = highestClearedStageNumber;
            LastActiveUnixTime = lastActiveUnixTime;
            AttackPowerLevel = attackPowerLevel;
            MaxHealthLevel = maxHealthLevel;
            AttackSpeedLevel = attackSpeedLevel;
            MoveSpeedLevel = moveSpeedLevel;
            CriticalChanceLevel = criticalChanceLevel;
            CriticalDamageLevel = criticalDamageLevel;
            InventoryJson = inventoryJson;
            RankIndex = rankIndex;
            SoldierTicketCount = soldierTicketCount;
            SoldierRosterJson = soldierRosterJson;
            SkillLevelsJson = skillLevelsJson;
            SoldierAttackPowerLevel = soldierAttackPowerLevel;
            SoldierMaxHealthLevel = soldierMaxHealthLevel;
            SoldierAttackSpeedLevel = soldierAttackSpeedLevel;
            SoldierMoveSpeedLevel = soldierMoveSpeedLevel;
            SoldierCriticalChanceLevel = soldierCriticalChanceLevel;
            SoldierCriticalDamageLevel = soldierCriticalDamageLevel;
            SkillLoadoutJson = skillLoadoutJson;
            SkillEnabledJson = skillEnabledJson;
            SkillScrollCount = skillScrollCount;
            SkillCountsJson = skillCountsJson;
            EquipmentGachaTicketCount = equipmentGachaTicketCount;
            SquadTacticsJson = squadTacticsJson;
            SoldierGachaGoldPullCountsJson = soldierGachaGoldPullCountsJson;
            SkillGachaGoldPullCountsJson = skillGachaGoldPullCountsJson;
            BossTokenCount = bossTokenCount;
            LastElapsedRealtimeSeconds = lastElapsedRealtimeSeconds;
            HasSeenIntroStory = hasSeenIntroStory;
        }
    }
}
