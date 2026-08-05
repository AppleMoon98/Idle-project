using Character.Events;
using Combat;
using Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Character
{
    /// <summary>
    /// PlayerControlMode가 Manual일 때 화면 탭 위치로 플레이어를 직접 이동시킨다. Manual 진입 시
    /// 같은 오브젝트의 EnemyTracker를 꺼서 이동 제어권을 가져오고(Soldier.SoldierBehaviorController가
    /// 원거리 카이팅에 쓰는 것과 동일한 패턴), Auto로 돌아가면 다시 켜서 반납한다. 공격(Attacker)은
    /// 이동 방식과 무관하게 항상 자동으로 동작한다.
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(EnemyTracker))]
    public sealed class PlayerManualMover : MonoBehaviour, ITickable
    {
        private CharacterMover _mover;
        private EnemyTracker _enemyTracker;
        private Transform _tapAnchor;
        private Camera _camera;
        private bool _isManualActive;

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _enemyTracker = GetComponent<EnemyTracker>();
            _camera = Camera.main;

            var anchorGO = new GameObject("PlayerManualMoveAnchor");
            _tapAnchor = anchorGO.transform;
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<PlayerControlModeChangedEvent>(OnControlModeChanged);
            TickerRegistration.Register(this);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PlayerControlModeService controlModeService))
            {
                ApplyMode(controlModeService.CurrentMode);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<PlayerControlModeChangedEvent>(OnControlModeChanged);
            TickerRegistration.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_tapAnchor != null)
            {
                Destroy(_tapAnchor.gameObject);
            }
        }

        private void OnControlModeChanged(PlayerControlModeChangedEvent evt)
        {
            ApplyMode(evt.Mode);
        }

        private void ApplyMode(PlayerControlMode mode)
        {
            _isManualActive = mode == PlayerControlMode.Manual;
            _enemyTracker.enabled = !_isManualActive;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isManualActive)
            {
                return;
            }

            Pointer pointer = Pointer.current;

            if (pointer == null || !pointer.press.wasPressedThisFrame)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 screenPosition = pointer.position.ReadValue();
            Vector3 worldPosition = _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -_camera.transform.position.z));
            worldPosition.z = 0f;

            _tapAnchor.position = worldPosition;
            _mover.Target = _tapAnchor;
            _mover.StoppingDistance = 0f;
        }
    }
}
