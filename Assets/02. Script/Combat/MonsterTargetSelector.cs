using Character;
using Core;
using Soldier;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 몬스터의 이동 대상을 주기적으로 재평가한다. 우선순위:
    /// 1) 플레이어가 나를 타겟팅 중이면 플레이어
    /// 2) 나를 타겟팅 중인 병사가 있으면 그 병사
    /// 3) 둘 다 아니면 플레이어(기본값)
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(Health))]
    public sealed class MonsterTargetSelector : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float retargetInterval = 0.2f;

        private CharacterMover _mover;
        private Health _self;
        private PlayerTargetTracker _playerTargetTracker;
        private SoldierTargetRegistry _soldierTargetRegistry;
        private float _elapsed;

        /// <summary>
        /// 아무도 나를 타겟팅하지 않을 때의 기본 이동 대상(플레이어). MonsterSpawner가 스폰 직후 주입한다.
        /// </summary>
        public Transform PlayerTransform { get; private set; }

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _self = GetComponent<Health>();
            GameBootstrapper.Services?.TryGet(out _playerTargetTracker);
            GameBootstrapper.Services?.TryGet(out _soldierTargetRegistry);
        }

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        /// <summary>
        /// 플레이어 Transform을 주입하고 즉시 한 번 재평가한다. 스폰 직후 MonsterSpawner가 호출한다.
        /// </summary>
        public void Initialize(Transform playerTransform)
        {
            PlayerTransform = playerTransform;
            Retarget();
        }

        void ITickable.Tick(float deltaTime)
        {
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
            _mover.Target = ChooseTarget();
        }

        private Transform ChooseTarget()
        {
            if (_playerTargetTracker != null && _playerTargetTracker.CurrentTarget == _self)
            {
                return PlayerTransform;
            }

            if (_soldierTargetRegistry != null && _soldierTargetRegistry.TryGetClaimant(_self, out GameObject soldier) && soldier != null)
            {
                return soldier.transform;
            }

            return PlayerTransform;
        }
    }
}
