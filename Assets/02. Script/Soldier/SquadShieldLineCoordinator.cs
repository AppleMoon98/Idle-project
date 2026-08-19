using System.Collections.Generic;
using Character.Events;
using Core;
using Soldier.Events;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 여러 부대가 SquadTacticType.ShieldWall이며, 그 부대의 1열(배치 그리드의 relative row 0 —
    /// SoldierDeploymentService.GridColumns칸)이 전원 방패보병(Character.ShieldGuard)으로 채워져
    /// 있으면, 조건을 만족하는 부대들의 1열 유닛만 플레이어 리셋 위치 뒤에 부대 번호 순으로 나란히
    /// 한 줄 세운다. 2/3/4열은 손대지 않는다 — SoldierDeploymentService.GetProtectorSlotIndex는
    /// 항상 "자기 행 + 1"만 가리키므로 1열(row 0)은 애초에 아무도 따라오지 않는 독립적인 행이라,
    /// 1열만 옮겨도 2/3/4열은 자연히 영향받지 않는다(Soldier.SquadShieldWallCoordinator의 방어자
    /// 페어링과 완전히 독립적으로 동작). 배치 UI로 언제든 구성이 바뀔 수 있어 고정 그룹을 만들지
    /// 않고, 관련 이벤트가 올 때마다 매번 전체를 다시 계산한다(이 프로젝트 전반의 "매번 깨끗하게
    /// 재계산" 관례, Stage.Tactics.ShieldWallFormationGroup.Rebalance/SquadShieldWallCoordinator.
    /// RecomputeSquad와 동일한 방향).
    /// </summary>
    public sealed class SquadShieldLineCoordinator : IManager, IService
    {
        /// <summary>부대 블록 하나가 차지하는 가로 폭.</summary>
        private const float SquadSpacing = 3f;

        /// <summary>부대 블록 안에서 1열 유닛끼리의 간격.</summary>
        private const float UnitSpacing = 1f;

        /// <summary>플레이어 리셋 위치로부터 -Y 방향으로 떨어지는 거리.</summary>
        private const float BehindDistance = 3f;

        private readonly EventBus _events;
        private readonly SquadTacticService _tactics;
        private readonly SquadMovementSyncService _movementSync;
        private readonly Transform _playerTransform;

        private readonly Dictionary<GameObject, Vector3> _linePositions = new();
        private Vector3? _formationOrigin;

        public SquadShieldLineCoordinator(EventBus events, SquadTacticService tactics, SquadMovementSyncService movementSync, Transform playerTransform)
        {
            _events = events;
            _tactics = tactics;
            _movementSync = movementSync;
            _playerTransform = playerTransform;
        }

        public void Initialize()
        {
            _events?.Subscribe<SquadTacticChangedEvent>(OnTacticChanged);
            _events?.Subscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            _events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        public void Shutdown()
        {
            _events?.Unsubscribe<SquadTacticChangedEvent>(OnTacticChanged);
            _events?.Unsubscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            _events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
            _linePositions.Clear();
        }

        /// <summary>
        /// Soldier.SoldierSpawner가 그 스테이지의 모든 슬롯을 (재)스폰한 직후 호출한다
        /// (Soldier.SquadRaidCoordinator.OnStageStarted와 같은 이유 - EventBus 구독 순서에 기대지
        /// 않고 "스폰 완료 후"를 항상 보장하기 위함). 이 시점의 플레이어 위치를 대형의 기준점으로
        /// 캐싱하고 다시 계산한다.
        /// </summary>
        public void OnStageStarted()
        {
            _formationOrigin = _playerTransform != null ? _playerTransform.position : (Vector3?)null;
            Recompute();
        }

        /// <summary>
        /// instance가 지금 이 대형의 1열 구성원이면 목표 위치를 반환한다. 대형이 아직 계산되지
        /// 않았거나(스테이지 시작 전) 조건을 만족하는 부대가 없거나, instance가 조건을 만족하는
        /// 부대의 1열이 아니면 false.
        /// </summary>
        public bool TryGetLinePosition(GameObject instance, out Vector3 position)
        {
            return _linePositions.TryGetValue(instance, out position);
        }

        private void OnTacticChanged(SquadTacticChangedEvent evt)
        {
            Recompute();
        }

        private void OnDeploymentChanged(SoldierDeploymentChangedEvent evt)
        {
            Recompute();
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            Recompute();
        }

        private void Recompute()
        {
            _linePositions.Clear();

            if (!_formationOrigin.HasValue)
            {
                return;
            }

            var qualifyingFrontRows = new List<List<GameObject>>();

            for (int squadIndex = 0; squadIndex < SoldierDeploymentService.SquadCount; squadIndex++)
            {
                if (_tactics.GetTactic(squadIndex) == SquadTacticType.ShieldWall && TryGetFrontRow(squadIndex, out List<GameObject> frontRow))
                {
                    qualifyingFrontRows.Add(frontRow);
                }
            }

            if (qualifyingFrontRows.Count == 0)
            {
                return;
            }

            Vector3 origin = _formationOrigin.Value;
            float lineY = origin.y - BehindDistance;
            float totalWidth = qualifyingFrontRows.Count * SquadSpacing;
            float firstBlockCenterX = origin.x - totalWidth / 2f + SquadSpacing / 2f;

            for (int blockIndex = 0; blockIndex < qualifyingFrontRows.Count; blockIndex++)
            {
                List<GameObject> frontRow = qualifyingFrontRows[blockIndex];
                float blockCenterX = firstBlockCenterX + blockIndex * SquadSpacing;
                float rowStartX = blockCenterX - (frontRow.Count - 1) * UnitSpacing / 2f;

                for (int column = 0; column < frontRow.Count; column++)
                {
                    float x = rowStartX + column * UnitSpacing;
                    _linePositions[frontRow[column]] = new Vector3(x, lineY, origin.z);
                }
            }
        }

        /// <summary>
        /// squadIndex의 1열(relative row 0, SoldierDeploymentService.GridColumns칸)이 빠짐없이
        /// 방패보병(Character.ShieldGuard)으로 채워져 있으면 그 인스턴스들을 열 순서대로 반환한다.
        /// 한 칸이라도 비어있거나 방패보병이 아니면 false.
        /// </summary>
        private bool TryGetFrontRow(int squadIndex, out List<GameObject> frontRow)
        {
            frontRow = null;

            IReadOnlyList<GameObject> members = _movementSync.GetSquadMembers(squadIndex);
            var bySlot = new Dictionary<int, GameObject>();

            foreach (GameObject member in members)
            {
                if (member != null && _movementSync.TryGetSlotIndex(member, out int slotIndex))
                {
                    bySlot[slotIndex] = member;
                }
            }

            int squadStart = squadIndex * SoldierDeploymentService.SlotsPerSquad;
            var result = new List<GameObject>(SoldierDeploymentService.GridColumns);

            for (int column = 0; column < SoldierDeploymentService.GridColumns; column++)
            {
                int slotIndex = squadStart + column;

                if (!bySlot.TryGetValue(slotIndex, out GameObject member) || member.GetComponent<Character.ShieldGuard>() == null)
                {
                    return false;
                }

                result.Add(member);
            }

            frontRow = result;
            return true;
        }
    }
}
