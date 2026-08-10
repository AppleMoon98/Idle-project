using System.Collections.Generic;
using Combat;
using Core;
using UnityEngine;
using War;

namespace Character
{
    /// <summary>
    /// 병사 구출 던전(War.SoldierRescueDungeonSessionController가 소유) 진행 중, 자동 모드일 때
    /// 플레이어가 가장 가까운 미점령 구역(WarStructure)으로 스스로 이동해 그 판정 범위 안에
    /// 서 있도록 한다. Activate()/Deactivate()는 세션 컨트롤러가 시도 시작/종료 시 호출한다.
    /// Manual 모드일 때는 아무것도 하지 않는다 - 기존 PlayerManualMover의 탭 이동이 그대로 동작한다.
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(EnemyTracker))]
    public sealed class CaptureZoneAutoNavigator : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float retargetInterval = 0.5f;

        private CharacterMover _mover;
        private EnemyTracker _enemyTracker;
        private IReadOnlyList<WarStructure> _zones;
        private float _elapsed;
        private bool _isActive;

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _enemyTracker = GetComponent<EnemyTracker>();
        }

        /// <summary>
        /// 이번 시도의 점령 구역 목록을 받아 자동 이동을 시작한다. 목록 참조를 그대로 들고 있으므로
        /// (매 틱 복사하지 않음) 세션 컨트롤러가 시도 종료 시 반드시 Deactivate()를 먼저 호출해
        /// 해제된 구역을 계속 들고 있지 않도록 해야 한다.
        /// </summary>
        public void Activate(IReadOnlyList<WarStructure> zones)
        {
            _zones = zones;
            _isActive = true;
            _elapsed = retargetInterval;
            TickerRegistration.Register(this);
        }

        /// <summary>
        /// 자동 이동을 멈추고 EnemyTracker를 원래대로 되돌린다.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            _zones = null;
            TickerRegistration.Unregister(this);

            if (_enemyTracker != null)
            {
                _enemyTracker.enabled = true;
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isActive)
            {
                return;
            }

            bool isAuto = GameBootstrapper.Services != null
                && GameBootstrapper.Services.TryGet(out PlayerControlModeService controlModeService)
                && controlModeService.CurrentMode == PlayerControlMode.Auto;

            if (!isAuto)
            {
                if (_enemyTracker != null && !_enemyTracker.enabled)
                {
                    _enemyTracker.enabled = true;
                }

                return;
            }

            if (_enemyTracker != null)
            {
                _enemyTracker.enabled = false;
            }

            _elapsed += deltaTime;

            if (_elapsed < retargetInterval)
            {
                return;
            }

            _elapsed = 0f;
            Retarget();
        }

        private void Retarget()
        {
            WarStructure nearest = FindNearestUncaptured();

            if (nearest == null)
            {
                _mover.Target = null;
                return;
            }

            _mover.Target = nearest.transform;
            _mover.StoppingDistance = nearest.ActivationRadius * 0.5f;
        }

        private WarStructure FindNearestUncaptured()
        {
            if (_zones == null)
            {
                return null;
            }

            WarStructure nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (WarStructure zone in _zones)
            {
                if (zone == null || zone.IsCaptured)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, zone.transform.position);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = zone;
                }
            }

            return nearest;
        }
    }
}
