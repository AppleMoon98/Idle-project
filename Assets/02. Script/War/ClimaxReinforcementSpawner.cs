using System.Collections.Generic;
using Character.Events;
using Core;
using Managers;
using Stage.Events;
using UnityEngine;

namespace War
{
    /// <summary>
    /// 1-40(챕터1, 스테이지40)에 한정해 근접 지원병을 플레이어 주변에 스폰한다. 기존
    /// Soldier 로스터/배치/강화/저장 시스템과는 완전히 무관한 일회성 지원 유닛으로,
    /// 세이브되지 않으며 이 스테이지를 벗어나면 생존자는 즉시 풀로 반환된다.
    /// </summary>
    public sealed class ClimaxReinforcementSpawner : MonoBehaviour
    {
        private const int TargetChapter = 1;
        private const int TargetStageNumber = 40;

        [SerializeField]
        private GameObject reinforcementPrefab;

        [SerializeField]
        private Transform playerTarget;

        [SerializeField]
        private int unitCount = 20;

        [SerializeField]
        private float spawnRadius = 3f;

        private readonly List<GameObject> _activeUnits = new();

        private Vector3 _anchorPosition;

        /// <summary>
        /// 클라이맥스 진입 시 플레이어를 리셋 위치로 되돌리는 ClimaxStagePositionResetter와
        /// 같은 StageChangedEvent를 각자 구독하므로, 실행 순서에 따라 이 스포너가 먼저 실행되면
        /// 플레이어가 아직 리셋되기 전(전 스테이지에서 이동해 있던) 위치를 기준으로 배치될 수
        /// 있다. Awake 시점에 씬에 배치된 원래 좌표를 한 번 캐싱해두고 항상 이 좌표를 기준으로
        /// 배치하면(ClimaxStagePositionResetter가 캐싱하는 좌표와 사실상 동일한 값) 구독
        /// 순서와 무관하게 항상 올바른 위치에 스폰된다.
        /// </summary>
        private void Awake()
        {
            if (playerTarget != null)
            {
                _anchorPosition = playerTarget.position;
            }
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);
            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StageChangedEvent>(OnStageChanged);
            GameBootstrapper.Events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            ReleaseActiveUnits();

            if (evt.Chapter == TargetChapter && evt.StageNumber == TargetStageNumber)
            {
                SpawnReinforcements();
            }
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            _activeUnits.Remove(evt.Character);
        }

        /// <summary>
        /// 플레이어 주변 원형으로 unitCount만큼 균등하게 배치해 스폰한다. 죽으면 부활하지
        /// 않는다 - 이 스테이지에 다시 진입할 때(재도전 포함) OnStageChanged가 다시 호출되어
        /// 매번 새로 unitCount만큼 스폰된다.
        /// </summary>
        private void SpawnReinforcements()
        {
            if (reinforcementPrefab == null || playerTarget == null)
            {
                return;
            }

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                return;
            }

            pool.EnsurePool(reinforcementPrefab, unitCount, unitCount);

            for (int i = 0; i < unitCount; i++)
            {
                float angle = i * Mathf.PI * 2f / unitCount;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spawnRadius;
                GameObject instance = pool.Get(reinforcementPrefab, _anchorPosition + offset, Quaternion.identity);
                _activeUnits.Add(instance);
            }
        }

        /// <summary>
        /// 현재 추적 중인 생존자를 보상/이벤트 없이 즉시 풀로 반환한다. 1-40을 벗어날 때(클리어,
        /// 사망 후퇴 등) 호출되며, 죽지 않고 남은 지원병이 다음 스테이지로 새어 들어가는 것을 막는다.
        /// </summary>
        private void ReleaseActiveUnits()
        {
            if (_activeUnits.Count == 0)
            {
                return;
            }

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                foreach (GameObject unit in _activeUnits)
                {
                    if (unit != null)
                    {
                        pool.Release(unit);
                    }
                }
            }

            _activeUnits.Clear();
        }
    }
}
