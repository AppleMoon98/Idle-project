using System.Collections.Generic;
using Combat;
using Core;
using Soldier.Events;
using Stage;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// SquadTacticType.LeftRightRaid/RearRaid가 배정된 부대를 스테이지 시작 시 완전히 숨겼다가,
    /// 부대 구성(전원 기마병인지)에 따라 정해진 시간 뒤 좌/우(또는 상단) 스폰 지점에서
    /// 등장시킨다. SoldierSpawner가 그 스테이지의 모든 슬롯을 (재)스폰한 직후 OnStageStarted를
    /// 명시적으로 호출해준다 - EventBus 구독 순서에 기대지 않고 "스폰 완료 후"를 항상 보장하기
    /// 위함(반대로 SoldierSpawner보다 먼저 반응하면 아직 스폰되지 않은 빈 목록을 숨기는 꼴이 된다).
    /// </summary>
    public sealed class SquadRaidCoordinator : MonoBehaviour, ITickable
    {
        [SerializeField]
        private StageController stageController;

        [SerializeField]
        private float leftRightRaidDelayBear = 4f;

        [SerializeField]
        private float leftRightRaidDelayOther = 8f;

        [SerializeField]
        private float rearRaidDelayBear = 8f;

        [SerializeField]
        private float rearRaidDelayOther = 16f;

        private readonly float[] _remaining = new float[SoldierDeploymentService.SquadCount];
        private readonly bool[] _isPending = new bool[SoldierDeploymentService.SquadCount];

        private SquadTacticService _tactics;
        private SquadMovementSyncService _movementSync;

        private void Awake()
        {
            GameBootstrapper.Services?.TryGet(out _tactics);
            GameBootstrapper.Services?.TryGet(out _movementSync);
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
            GameBootstrapper.Events?.Subscribe<SquadTacticChangedEvent>(OnTacticChanged);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
            GameBootstrapper.Events?.Unsubscribe<SquadTacticChangedEvent>(OnTacticChanged);
        }

        /// <summary>
        /// SoldierSpawner가 스폰(초기 스폰/스테이지 전환 재배치)을 마친 직후 호출한다. 습격 전술이
        /// 배정된 부대만 그 자리에서 숨기고 카운트다운을 시작한다 - 전술이 없는 부대는 건드리지 않는다.
        /// </summary>
        public void OnStageStarted()
        {
            if ((_tactics == null && (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out _tactics)))
                || (_movementSync == null && (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out _movementSync))))
            {
                return;
            }

            for (int squadIndex = 0; squadIndex < SoldierDeploymentService.SquadCount; squadIndex++)
            {
                SquadTacticType tactic = _tactics.GetTactic(squadIndex);

                if (!IsRaidTactic(tactic))
                {
                    _isPending[squadIndex] = false;
                    continue;
                }

                ArmSquad(squadIndex, tactic);
            }
        }

        /// <summary>
        /// 부대 구성(전원 기마병인지)에 따라 카운트다운을 시작하고 부대원 전원을 즉시 숨긴다.
        /// </summary>
        private void ArmSquad(int squadIndex, SquadTacticType tactic)
        {
            IReadOnlyList<GameObject> members = _movementSync.GetSquadMembers(squadIndex);

            if (members.Count == 0)
            {
                _isPending[squadIndex] = false;
                return;
            }

            bool allBear = true;

            foreach (GameObject member in members)
            {
                if (member == null)
                {
                    continue;
                }

                if (member.GetComponent<BearCharge>() == null)
                {
                    allBear = false;
                    break;
                }
            }

            _remaining[squadIndex] = GetDelay(tactic, allBear);
            _isPending[squadIndex] = true;

            foreach (GameObject member in members)
            {
                if (member != null)
                {
                    member.SetActive(false);
                }
            }
        }

        private float GetDelay(SquadTacticType tactic, bool allBear)
        {
            return tactic switch
            {
                SquadTacticType.LeftRightRaid => allBear ? leftRightRaidDelayBear : leftRightRaidDelayOther,
                SquadTacticType.RearRaid => allBear ? rearRaidDelayBear : rearRaidDelayOther,
                _ => 0f,
            };
        }

        private static bool IsRaidTactic(SquadTacticType tactic)
        {
            return tactic == SquadTacticType.LeftRightRaid || tactic == SquadTacticType.RearRaid;
        }

        void ITickable.Tick(float deltaTime)
        {
            for (int squadIndex = 0; squadIndex < SoldierDeploymentService.SquadCount; squadIndex++)
            {
                if (!_isPending[squadIndex])
                {
                    continue;
                }

                _remaining[squadIndex] -= deltaTime;

                if (_remaining[squadIndex] <= 0f)
                {
                    _isPending[squadIndex] = false;
                    ExecuteRaid(squadIndex);
                }
            }
        }

        /// <summary>
        /// 대기(숨김) 중이던 부대의 전술이 습격 계열이 아닌 걸로 바뀌면, 카운트다운을 취소하고
        /// 즉시 다시 보이게 한다 - 그대로 두면 다음 습격 시점까지 영원히 숨어있게 된다.
        /// </summary>
        private void OnTacticChanged(SquadTacticChangedEvent evt)
        {
            // GitHub 이슈 #26 - SquadTacticService.SetTactic이 이제 범위 밖 SquadIndex를 발행하지
            // 않지만, 이 컴포넌트도 이벤트 경계에서 한 번 더 방어한다(_isPending을 바로 인덱싱하면
            // 범위 밖 값에서 IndexOutOfRangeException이 났던 게 실제로 재현된 버그였다).
            if (evt.SquadIndex < 0 || evt.SquadIndex >= SoldierDeploymentService.SquadCount)
            {
                return;
            }

            if (!_isPending[evt.SquadIndex] || IsRaidTactic(evt.Tactic))
            {
                return;
            }

            _isPending[evt.SquadIndex] = false;
            Reveal(evt.SquadIndex);
        }

        private void Reveal(int squadIndex)
        {
            if (_movementSync == null)
            {
                return;
            }

            foreach (GameObject member in _movementSync.GetSquadMembers(squadIndex))
            {
                if (member != null)
                {
                    member.SetActive(true);
                }
            }
        }

        /// <summary>
        /// 카운트다운이 끝난 부대를 실제로 등장시킨다. 좌우 습격은 부대 내 상대 슬롯 번호
        /// 홀수(1,3,5...)를 좌측, 짝수(2,4,6...)를 우측 스폰 지점에, 후방 습격은 전원을 상단
        /// 스폰 지점에 순환 배치한다.
        /// </summary>
        private void ExecuteRaid(int squadIndex)
        {
            if (_tactics == null || _movementSync == null || stageController == null)
            {
                return;
            }

            SquadTacticType tactic = _tactics.GetTactic(squadIndex);
            IReadOnlyList<GameObject> members = _movementSync.GetSquadMembers(squadIndex);

            int leftCursor = 0;
            int rightCursor = 0;
            int topCursor = 0;

            foreach (GameObject member in members)
            {
                if (member == null || !_movementSync.TryGetSlotIndex(member, out int slotIndex))
                {
                    continue;
                }

                Vector3? spawnPosition;

                if (tactic == SquadTacticType.RearRaid)
                {
                    spawnPosition = stageController.GetTopEdgePosition(topCursor++);
                }
                else
                {
                    int relativeNumber = (slotIndex % SoldierDeploymentService.SlotsPerSquad) + 1;
                    bool isOdd = relativeNumber % 2 == 1;
                    spawnPosition = isOdd ? stageController.GetLeftEdgePosition(leftCursor++) : stageController.GetRightEdgePosition(rightCursor++);
                }

                if (spawnPosition.HasValue)
                {
                    member.transform.SetPositionAndRotation(spawnPosition.Value, Quaternion.identity);
                }

                member.SetActive(true);
            }
        }
    }
}
