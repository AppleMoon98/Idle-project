using Character;
using Core;
using Stage;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 몬스터가 자기 Character.CharacterMover.Target에 아직 도달하지 못한(사거리 밖) "행군 중"
    /// 상태인지 주기적으로 폴링해 Stage.MonsterSquadMovementSyncService에 보고한다.
    /// Character.CharacterSeparation과 같은 방향의 완전히 독립적인 추가 계층이라, 어떤 이동
    /// 컴포넌트(MonsterTargetSelector/RangedKiter/FormationFollower/GuardPositioner)가
    /// Target/StoppingDistance를 설정했는지는 몰라도 된다 — CharacterMover 하나만 폴링한다.
    ///
    /// Combat.CavalryCharge/OrbitKiter(목표에 다가가 멈춘다는 개념 자체가 없는 자체 상태 기계 —
    /// 기마병은 돌진 중 CharacterMover.Target을 직접 null로 비우고 transform을 스스로 몰고,
    /// 기마궁수는 계속 움직이는 궤도점을 쫓아 영원히 도달하지 못함)를 가진 몬스터는 Awake에서
    /// 감지해 등록 자체를 건너뛴다 — Soldier.SoldierBehaviorController가 기마병/기마궁수를
    /// 부대 이동속도 동기화에서 제외하는 것과 정확히 같은 이유.
    ///
    /// 등록/해제는 Managers.PoolManager의 Get/Release가 항상 거치는 OnEnable/OnDisable에 맞춰
    /// 스스로 수행한다 — 죽어서 반환되든, 스테이지 전환으로 강제 반환되든 동일하게 처리되므로
    /// 별도의 사망 이벤트 구독이나 스포너 쪽 명시적 해제 호출이 필요 없다.
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class MonsterMarchingTracker : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float pollInterval = 0.2f;

        private CharacterMover _mover;
        private MonsterSquadMovementSyncService _squadSync;
        private bool _isExempt;
        private float _elapsed;

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _isExempt = GetComponent<CavalryCharge>() != null || GetComponent<OrbitKiter>() != null;

            if (!_isExempt)
            {
                GameBootstrapper.Services?.TryGet(out _squadSync);
            }
        }

        private void OnEnable()
        {
            if (_isExempt || _squadSync == null)
            {
                return;
            }

            _elapsed = 0f;
            _squadSync.Register(gameObject);
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            if (_isExempt || _squadSync == null)
            {
                return;
            }

            TickerRegistration.Unregister(this);
            _squadSync.Unregister(gameObject);
        }

        void ITickable.Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            if (_elapsed < pollInterval)
            {
                return;
            }

            _elapsed = 0f;

            bool isMarching = _mover.Target != null
                && Vector3.Distance(transform.position, _mover.Target.position) > _mover.StoppingDistance;

            _squadSync.SetMarching(gameObject, isMarching);
        }
    }
}
