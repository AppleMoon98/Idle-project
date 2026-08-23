using System;
using System.Collections.Generic;
using System.Linq;
using Character;
using Character.Events;
using Combat;
using Combat.BossPattern;
using Core;
using Core.Pooling;
using Managers;
using Services;
using Skill.Events;
using UI;
using UI.Events;
using UnityEngine;

namespace Rank.Boss
{
    /// <summary>
    /// 병사 랭크(1-10) 승급전 전용 보병 보스의 패턴 진행을 관리한다. 평시에는 patterns를
    /// 순환하며 각 PromotionBossPatternSO의 BossPatternHit들을 TimedActionSequence로 재생하고,
    /// 자기 체력이 50%를 처음 밑도는 순간 페이즈2(RunPhaseTwoSequence, 이후 단계에서 채움)를
    /// 1회 발동한다. 이 컴포넌트는 평타를 전혀 갖지 않는다(Combat.Attacker/MeleeAttackBehavior
    /// 없음) - 이동/사거리 정지는 같은 오브젝트의 Combat.MonsterTargetSelector가 기존과 동일하게
    /// 담당한다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class PromotionBossController : MonoBehaviour, ITickable, IPoolable
    {
        [SerializeField]
        private PromotionBossPatternSO[] patterns;

        [SerializeField]
        private float intervalBetweenPatterns = 4f;

        [SerializeField]
        private float detectionRange = 30f;

        [SerializeField]
        private LayerMask allyLayerMask;

        [SerializeField]
        private GameObject telegraphIndicatorPrefab;

        [SerializeField]
        private int telegraphPoolCapacity = 8;

        [SerializeField]
        private int telegraphPoolMaxSize = 12;

        [Header("체력 50% 페이즈")]

        [SerializeField]
        private float phaseTwoCrossDelay = 2f;

        [SerializeField]
        private BossPatternHit crossHitTemplate;

        [SerializeField]
        private BossPatternHit verticalLineHitTemplate;

        private const int VerticalLineRepeatCount = 3;
        private const int VerticalLinesPerVolley = 4;
        private static readonly float[] CrossAngles = { 0f, 90f, 45f, 135f };

        private Health _health;
        private MonsterTargetSelector _targetSelector;
        private CharacterMover _mover;
        private Collider2D _collider;
        private SpriteRenderer _spriteRenderer;
        private HealthBarUI _healthBarUI;
        private Character.Animation.UnitAnimationControllerBase _animationController;
        private PoolManager _pool;
        private CameraFollowService _cameraFollowService;
        private CameraZoomSliderUI _cameraZoomSlider;
        private Camera _camera;
        private float _savedOrthographicSize;
        private bool _zoomOverridden;

        private readonly TimedActionSequence _sequence = new();
        private readonly List<GameObject> _activeIndicators = new();

        // 표시 중인 텔레그래프마다 "언제 표시됐고 얼마나 오래 지속되는지"를 기록해 두고, 매 틱
        // 경과율에 따라 SetProgress01을 갱신한다(War.Boss.WarBossPatternRunner.TickTelegraph와
        // 동일한 의도) - ShowTelegraphForHit가 Show*() 호출 시 딱 한 번만 SetProgress01(0f)을
        // 부르고 그 뒤로 다시 갱신하지 않으면, 텔레그래프가 표시되는 내내 가장 흐린 알파(0.25)에
        // 고정돼 사실상 안 보이는 것처럼 보인다(실사용 중 발견 - "패턴이 안 보이면서 피만 깎인다").
        private readonly List<(BossShapeTelegraphIndicator Indicator, float ShowTime, float Duration)> _telegraphProgress = new();

        private int _patternIndex;
        private float _elapsedSinceLastCast;
        private bool _phaseTwoTriggered;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _targetSelector = GetComponent<MonsterTargetSelector>();
            _mover = GetComponent<CharacterMover>();
            _collider = GetComponent<Collider2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _healthBarUI = GetComponentInChildren<HealthBarUI>(includeInactive: true);
            _animationController = GetComponent<Character.Animation.UnitAnimationControllerBase>();
            GameBootstrapper.Services?.TryGet(out _cameraFollowService);
            _cameraZoomSlider = UnityEngine.Object.FindFirstObjectByType<CameraZoomSliderUI>();
            _camera = Camera.main;
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
            GameBootstrapper.Events?.Subscribe<CharacterHealthChangedEvent>(OnHealthChanged);

            if (_pool == null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;

                if (telegraphIndicatorPrefab != null)
                {
                    _pool.EnsurePool(telegraphIndicatorPrefab, telegraphPoolCapacity, telegraphPoolMaxSize);
                }
            }
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
            GameBootstrapper.Events?.Unsubscribe<CharacterHealthChangedEvent>(OnHealthChanged);
        }

        public void OnSpawned()
        {
            _patternIndex = 0;
            _elapsedSinceLastCast = 0f;
            _phaseTwoTriggered = false;
            _sequence.Cancel();
            ReleaseAllIndicators();

            // 이전 시도가 체력 50% 페이즈 도중(무적/충돌판정 OFF/숨김 상태) 강제 반납됐을 수
            // 있다(예: 그 사이 플레이어가 죽어 RankPromotionBattleController.HandleFailure()가
            // 이 보스를 풀에 반납) - 재사용 시 항상 평시 상태로 되돌린다.
            _health.SetInvulnerable(false);

            if (_collider != null)
            {
                _collider.enabled = true;
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = true;
            }

            _healthBarUI?.SetForceHidden(false);

            UnfreezeMovement();
        }

        public void OnDespawned()
        {
            _sequence.Cancel();
            ReleaseAllIndicators();

            // 페이즈2 도중(카메라가 맵 중앙에 강제 고정된 상태) 보스가 강제 반납되는 경우(예: 그
            // 사이 플레이어가 죽어 RankPromotionBattleController.HandleFailure()가 이 보스를 풀에
            // 반납) EndPhaseTwo()가 끝까지 실행되지 못해 override가 영원히 안 풀릴 수 있다.
            _cameraFollowService?.SetOverrideTarget(null);

            // 같은 이유로 최광각 줌 강제 전환도 되돌린다 - _zoomOverridden으로 가드한다(페이즈2가
            // 아예 시작 전이었던 정상 사망까지 0으로 덮어써버리는 것을 방지).
            if (_zoomOverridden && _camera != null)
            {
                _camera.orthographicSize = _savedOrthographicSize;
                _zoomOverridden = false;
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            // GameTicker의 등록 해제는 그 프레임의 순회가 다 끝난 뒤에야 적용된다 - 같은 프레임
            // 안에서 이 보스가 죽어 OnDespawned()로 이미 정리(비활성화)된 뒤에도 한 번 더
            // Tick()을 받을 수 있다(War.Boss.WarBossPatternRunner와 동일한 함정).
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            _sequence.Tick(deltaTime);
            UpdateTelegraphProgress();

            // _sequence.IsRunning 하나만으로 평시 순환 정지가 충분하다 - RunPhaseTwoSequence()는
            // 자기 안에서 동기적으로 _sequence.Play(...)까지 호출하므로 OnHealthChanged가 끝나는
            // 시점엔 이미 IsRunning=true다. _phaseTwoTriggered는 OnHealthChanged 쪽의 "1회만
            // 트리거" 가드일 뿐, 페이즈2가 끝난 뒤(_sequence.IsRunning이 다시 false가 됨)에는
            // 평시 순환이 정상적으로 재개돼야 하므로 여기서는 검사하지 않는다.
            if (_sequence.IsRunning)
            {
                return;
            }

            if (patterns == null || patterns.Length == 0)
            {
                return;
            }

            _elapsedSinceLastCast += deltaTime;

            if (_elapsedSinceLastCast < intervalBetweenPatterns)
            {
                return;
            }

            TryStartPattern();
        }

        private void OnHealthChanged(CharacterHealthChangedEvent evt)
        {
            if (_phaseTwoTriggered || evt.Character != gameObject || evt.Max <= 0f)
            {
                return;
            }

            if (evt.Current / evt.Max > 0.5f)
            {
                return;
            }

            _phaseTwoTriggered = true;
            _sequence.Cancel();
            ReleaseAllIndicators();
            RunPhaseTwoSequence();
        }

        /// <summary>
        /// 패턴(평시 3종이든 페이즈2든) 진행 중에는 이동이 완전히 멈춰야 한다 - MonsterTargetSelector
        /// 비활성화만으로는 부족하다: Character.CharacterMover는 별도 ITickable로 자기 Target을
        /// 향해 계속 걸어가므로, MonsterTargetSelector가 더 이상 Target을 갱신하지 않아도
        /// CharacterMover는 마지막으로 받은 Target을 향해 계속 움직인다. 그래서 Target 자체를
        /// null로 비워야 실제로 제자리에 멈춘다.
        /// </summary>
        private void FreezeMovement()
        {
            if (_targetSelector != null)
            {
                _targetSelector.enabled = false;
            }

            if (_mover != null)
            {
                _mover.Target = null;
            }

            _animationController?.SetExternalAttacking(true);
        }

        /// <summary>
        /// MonsterTargetSelector를 다시 켜기만 하면 된다 - 재활성화 후 첫 재평가(retargetInterval
        /// 이내)에서 스스로 새 Target을 찾아 CharacterMover에 넣어준다.
        /// </summary>
        private void UnfreezeMovement()
        {
            if (_targetSelector != null)
            {
                _targetSelector.enabled = true;
            }

            _animationController?.SetExternalAttacking(false);
        }

        /// <summary>
        /// 체력 50% 트리거(1회) 시퀀스. 무적 ON + 충돌판정 OFF + 타겟팅 정지 + 맵 중앙으로 즉시
        /// 순간이동한 뒤, 그 순간부터 시간을 세는 하나의 플랫 스텝 목록을 지어 _sequence로
        /// 재생한다: 2초 뒤 X자+십자 동시 텔레그래프 → 1초 뒤 동시 피해 + 보스 숨김 + 첫 세로줄
        /// 볼리 텔레그래프 → 2초 뒤 피해 + 다음 볼리 텔레그래프(3회 반복) → 마지막 볼리 피해 직후
        /// 재등장 + 무적/충돌판정/타겟팅 원복.
        /// </summary>
        private void RunPhaseTwoSequence()
        {
            // CameraFollowService.HomeLocalPosition은 카메라 자신의 로컬 좌표(z가 카메라 depth,
            // 보통 -10)다 - 그 z를 그대로 스프라이트 위치에 쓰면 카메라와 같은 z, 즉 근평면(near
            // clip) 안쪽에 놓이게 되어 아예 렌더링되지 않는다(Services.CameraFollowService.
            // GetRandomPointWithinBounds가 이미 문서화한 것과 동일한 함정 - 그 메서드는 z를 0으로
            // 강제한다). 게임 평면(z=0)의 XY만 취한다.
            Vector3 homePosition = _cameraFollowService != null ? _cameraFollowService.HomeLocalPosition : transform.position;
            Vector3 center = new(homePosition.x, homePosition.y, 0f);

            _health.SetInvulnerable(true);

            if (_collider != null)
            {
                _collider.enabled = false;
            }

            FreezeMovement();

            transform.position = center;

            // 기본 줌 상태에서는 Services.CameraFollowService가 여전히 플레이어를 따라간다 -
            // 보스가 맵 중앙(플레이어와 멀리 떨어진 곳)으로 순간이동해도 카메라를 그대로 두면
            // 페이즈2 전체(보스+텔레그래프)가 화면 밖에서 벌어진다. 페이즈2가 끝날 때까지
            // 카메라를 강제로 이 위치에 고정한다(EndPhaseTwo에서 해제). 카메라 자신은 게임
            // 평면(z=0)이 아니라 원래 자기 depth(homePosition.z, 보통 -10)를 유지해야 한다 -
            // center(z=0)를 그대로 넘기면 카메라 자신이 게임 평면 위로 옮겨져 렌더링이 깨진다.
            _cameraFollowService?.SetOverrideTarget(homePosition);

            // 플레이어가 확대해둔 상태였다면 십자/세로줄 패턴(맵 전체를 관통하는 판정)의 상당
            // 부분이 화면 밖에서 벌어져 안 보인다 - 페이즈2 동안만 최광각(UI.CameraZoomSliderUI.
            // WideOrthographicSize)으로 강제 전환하고, 끝나면(EndPhaseTwo) 플레이어가 원래
            // 맞춰뒀던 확대/축소 값으로 되돌린다. 슬라이더 자신의 값(UI 표시)은 건드리지 않는다 -
            // Camera.main.orthographicSize만 직접 덮어쓰고 복원하므로, 슬라이더를 실제로 만지면
            // 그 시점 값 기준으로 다시 정상 동기화된다.
            if (_camera != null && _cameraZoomSlider != null)
            {
                _savedOrthographicSize = _camera.orthographicSize;
                _camera.orthographicSize = _cameraZoomSlider.WideOrthographicSize;
                _zoomOverridden = true;
            }

            List<(float delaySinceStart, Action execute)> steps = new();
            float t = phaseTwoCrossDelay;

            AppendCrossSteps(steps, center, t);
            t += crossHitTemplate.TelegraphDuration;

            for (int rep = 0; rep < VerticalLineRepeatCount; rep++)
            {
                bool isLastVolley = rep == VerticalLineRepeatCount - 1;
                AppendVerticalLineVolleySteps(steps, center, t, isLastVolley);
                t += verticalLineHitTemplate.TelegraphDuration;
            }

            _sequence.Play(steps.OrderBy(step => step.delaySinceStart).ToList());
        }

        private void AppendCrossSteps(List<(float delaySinceStart, Action execute)> steps, Vector3 center, float showTime)
        {
            GameObject[] indicators = new GameObject[CrossAngles.Length];

            for (int i = 0; i < CrossAngles.Length; i++)
            {
                int index = i;
                float angle = CrossAngles[i];
                steps.Add((showTime, () => indicators[index] = ShowTelegraphForHit(crossHitTemplate, center, angle)));
            }

            float resolveTime = showTime + crossHitTemplate.TelegraphDuration;

            steps.Add((resolveTime, () =>
            {
                List<(BossPatternHit Hit, Vector3 Origin, float FacingDeg, GameObject Indicator)> group = new();

                for (int i = 0; i < CrossAngles.Length; i++)
                {
                    group.Add((crossHitTemplate, center, CrossAngles[i], indicators[i]));
                }

                // 세로줄 볼리(AppendVerticalLineVolleySteps)와 동일하게, 판정과 같은 순간에 각
                // 방향의 실제 각도로 화면 슬래시를 긋고 카메라를 흔든다 - 십자는 4방향이 전부 같은
                // center에서 뻗어나가므로 위치는 공유하고 각도만 CrossAngles로 다르게 준다.
                for (int i = 0; i < CrossAngles.Length; i++)
                {
                    GameBootstrapper.Events?.Publish(new ScreenSlashRequestedEvent(CrossAngles[i], center));
                }

                GameBootstrapper.Events?.Publish(new SkillCameraShakeRequestedEvent(0.2f, 0.35f));

                ResolveSimultaneousHits(group);

                // 십자 판정이 끝나는 순간 보스가 사라진다 - 세로줄 볼리가 진행되는 동안 계속 숨어있는다.
                // 체력바(HealthBarUI)는 보스 자신과 별개의 자식 Canvas라 SpriteRenderer만 꺼서는
                // 같이 안 숨겨진다(실사용 중 발견 - 몸은 사라졌는데 체력바만 허공에 남음). fillAmount
                // 상태는 그대로 유지한 채(SetForceHidden) 표시만 같이 끈다.
                if (_spriteRenderer != null)
                {
                    _spriteRenderer.enabled = false;
                }

                _healthBarUI?.SetForceHidden(true);
            }));
        }

        private void AppendVerticalLineVolleySteps(List<(float delaySinceStart, Action execute)> steps, Vector3 center, float showTime, bool isLastVolley)
        {
            Vector3[] origins = new Vector3[VerticalLinesPerVolley];
            GameObject[] indicators = new GameObject[VerticalLinesPerVolley];

            for (int i = 0; i < VerticalLinesPerVolley; i++)
            {
                int index = i;

                // 4줄의 X좌표는 볼리 전체를 스케줄링하는 이 시점에 한 번에 미리 뽑아둔다(개별
                // 스텝 실행 시점마다 따로 뽑으면 서로의 위치를 몰라 겹침 방지가 불가능하다) -
                // showTime에 실행되는 각 스텝은 이미 정해진 좌표로 텔레그래프만 띄운다.
                steps.Add((showTime, () =>
                {
                    indicators[index] = ShowTelegraphForHit(verticalLineHitTemplate, origins[index], 90f);
                }));
            }

            AssignNonOverlappingVerticalLineOrigins(origins, center);

            float resolveTime = showTime + verticalLineHitTemplate.TelegraphDuration;

            steps.Add((resolveTime, () =>
            {
                List<(BossPatternHit Hit, Vector3 Origin, float FacingDeg, GameObject Indicator)> group = new();

                for (int i = 0; i < VerticalLinesPerVolley; i++)
                {
                    group.Add((verticalLineHitTemplate, origins[i], 90f, indicators[i]));
                }

                // "화면이 베이는" 슬래시 연출을 판정과 같은 순간에, 4줄 각각의 실제 위치에서
                // 발행한다(볼리당 1번이 아니라 줄마다 1번 - 이 4연격 패턴 자체를 표현하는 것이
                // 목적이라 각 줄이 저마다 자기 위치에서 그어져야 한다). UI.ScreenSlashEffectUI는
                // 도메인을 몰라도 되도록 이벤트만 구독한다(Skill.Events.SkillCameraShakeRequestedEvent와
                // 같은 방향). 세로줄 패턴이므로 각도는 90도(수직) 고정.
                for (int i = 0; i < VerticalLinesPerVolley; i++)
                {
                    GameBootstrapper.Events?.Publish(new ScreenSlashRequestedEvent(90f, origins[i]));
                }

                // 화면 흔들림도 같은 순간에 함께 요청한다 - 4줄이 동시에 판정되므로 볼리당 한 번만
                // 발행한다(CameraShakeService.OnShakeRequested는 재요청 시 누적하지 않고 갱신만 하므로
                // 여러 번 불러도 안전하지만, 굳이 4번 반복할 이유가 없다). 이 서비스는 원래 Skill
                // 도메인의 SkillCameraShakeRequestedEvent만 구독하도록 만들어졌지만 Duration/Magnitude
                // 뿐인 순수 데이터 이벤트라 다른 도메인에서도 그대로 재사용한다.
                GameBootstrapper.Events?.Publish(new SkillCameraShakeRequestedEvent(0.2f, 0.35f));

                ResolveSimultaneousHits(group);

                if (isLastVolley)
                {
                    EndPhaseTwo(center);
                }
            }));
        }

        /// <summary>
        /// 같은 순간에 여러 도형이 동시에 열리는 판정(X자+십자, 세로줄 4줄)을 처리한다. 같은
        /// 대상이 여러 도형에 동시에 걸려도 피해가 1회만 들어가도록 대상을 먼저 전부 모은 뒤
        /// 적용한다. 그룹의 모든 항목은 같은 데미지를 공유한다는 전제(페이즈2 설계상 항상 그렇다).
        /// </summary>
        private void ResolveSimultaneousHits(List<(BossPatternHit Hit, Vector3 Origin, float FacingDeg, GameObject Indicator)> group)
        {
            HashSet<Health> targets = new();

            foreach ((BossPatternHit hit, Vector3 origin, float facingDeg, GameObject _) in group)
            {
                float angle = facingDeg + hit.FacingOffsetDegrees;
                float length = ResolveLength(hit);

                IEnumerable<Health> hits;

                if (hit.Shape == BossShapeKind.Rectangle)
                {
                    Vector3 rectCenter = ResolveRectangleCenter(hit, origin, angle, length);
                    hits = BossPatternShapes.FindHitsInRectangle(rectCenter, length, hit.Width, angle, allyLayerMask);
                }
                else
                {
                    Vector2 forward = BossPatternShapes.AngleToDirection(angle);
                    hits = BossPatternShapes.FindHitsInSector(origin, length, hit.Width, forward, allyLayerMask, hit.InnerRadius);
                }

                foreach (Health health in hits)
                {
                    targets.Add(health);
                }
            }

            BossPatternHit sharedHit = group.Count > 0 ? group[0].Hit : null;

            // 데미지 적용 전에 인디케이터부터 전부 해제한다(ResolveHit과 동일한 재진입 방어 이유).
            foreach ((BossPatternHit groupHit, Vector3 _, float _, GameObject indicator) in group)
            {
                ReleaseIndicator(indicator, groupHit.LeaveResolveFlash);
            }

            if (sharedHit == null)
            {
                return;
            }

            foreach (Health health in targets)
            {
                health.TakeDamage(ResolveDamage(sharedHit, health));
            }
        }

        /// <summary>
        /// DamagePercentOfMaxHealth가 설정돼 있으면(0보다 크면) 맞는 대상 자신의 최대 체력에
        /// 비례한 데미지를, 아니면 기존처럼 고정 Damage를 반환한다.
        /// </summary>
        private static float ResolveDamage(BossPatternHit hit, Health target)
        {
            return hit.DamagePercentOfMaxHealth > 0f ? target.MaxHealth * hit.DamagePercentOfMaxHealth : hit.Damage;
        }

        /// <summary>
        /// 한 볼리의 세로줄 개수(count)만큼 맵 가로 폭을 균등 구간으로 나눠, 각 구간 안에서만
        /// 무작위 X를 뽑아 origins에 채운다(구간 경계에서 줄 폭(verticalLineHitTemplate.Width)의
        /// 절반씩 안쪽으로 들여 뽑아, 인접 구간 줄과도 겹치지 않는다) - 순수하게 독립적으로
        /// 뽑으면(예전 RandomVerticalLineOrigin) 두 줄이 우연히 거의 같은 X에 겹쳐 뜰 수 있어,
        /// 구간을 나눠 애초에 겹칠 수 없는 자리에서만 뽑도록 했다.
        /// </summary>
        private void AssignNonOverlappingVerticalLineOrigins(Vector3[] origins, Vector3 center)
        {
            if (_cameraFollowService == null)
            {
                for (int i = 0; i < origins.Length; i++)
                {
                    origins[i] = center;
                }

                return;
            }

            Vector2 halfExtent = _cameraFollowService.GetWorldBoundsHalfExtent();
            float minX = center.x - halfExtent.x;
            float segmentWidth = (halfExtent.x * 2f) / origins.Length;
            float halfLineWidth = verticalLineHitTemplate.Width * 0.5f;

            for (int i = 0; i < origins.Length; i++)
            {
                float segmentStart = minX + segmentWidth * i;
                float low = segmentStart + halfLineWidth;
                float high = segmentStart + segmentWidth - halfLineWidth;
                float x = low < high ? UnityEngine.Random.Range(low, high) : segmentStart + segmentWidth * 0.5f;
                origins[i] = new Vector3(x, center.y, center.z);
            }
        }

        private void EndPhaseTwo(Vector3 center)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = true;
            }

            _healthBarUI?.SetForceHidden(false);

            transform.position = center;
            _health.SetInvulnerable(false);

            if (_collider != null)
            {
                _collider.enabled = true;
            }

            UnfreezeMovement();

            _cameraFollowService?.SetOverrideTarget(null);

            if (_zoomOverridden && _camera != null)
            {
                _camera.orthographicSize = _savedOrthographicSize;
                _zoomOverridden = false;
            }

            _elapsedSinceLastCast = 0f;
        }

        private void TryStartPattern()
        {
            if (_pool == null || telegraphIndicatorPrefab == null)
            {
                return;
            }

            Health target = FindPlayer();

            if (target == null)
            {
                return;
            }

            PromotionBossPatternSO pattern = patterns[_patternIndex];
            _patternIndex = (_patternIndex + 1) % patterns.Length;
            _elapsedSinceLastCast = 0f;

            if (pattern == null || pattern.Hits == null || pattern.Hits.Length == 0)
            {
                return;
            }

            Vector3 origin = transform.position;
            Vector2 toTarget = (Vector2)target.transform.position - (Vector2)origin;
            float facingDeg = toTarget.sqrMagnitude > Mathf.Epsilon ? BossPatternShapes.DirectionToAngle(toTarget) : 0f;

            List<(float delaySinceStart, Action execute)> steps = new();
            float maxResolveTime = 0f;

            foreach (BossPatternHit hit in pattern.Hits)
            {
                BossPatternHit capturedHit = hit;
                GameObject[] indicatorSlot = new GameObject[1];
                float resolveTime = capturedHit.Delay + capturedHit.TelegraphDuration;
                maxResolveTime = Mathf.Max(maxResolveTime, resolveTime);

                steps.Add((capturedHit.Delay, () => indicatorSlot[0] = ShowTelegraphForHit(capturedHit, origin, facingDeg)));
                steps.Add((resolveTime, () => ResolveHit(capturedHit, origin, facingDeg, indicatorSlot[0])));
            }

            // 패턴이 진행되는 동안(마지막 판정이 끝날 때까지) 보스는 완전히 제자리에 멈춘다 -
            // 텔레그래프가 캐스트 시점 위치/방향을 스냅샷해 고정하는데, 그동안 보스가 움직이면
            // 예고 표시와 실제 보스 위치가 어긋나 보인다.
            FreezeMovement();
            steps.Add((maxResolveTime, UnfreezeMovement));

            _sequence.Play(steps.OrderBy(step => step.delaySinceStart).ToList());
        }

        private GameObject ShowTelegraphForHit(BossPatternHit hit, Vector3 origin, float facingDeg)
        {
            if (_pool == null || telegraphIndicatorPrefab == null)
            {
                return null;
            }

            GameObject instance = _pool.Get(telegraphIndicatorPrefab, origin, Quaternion.identity);
            _activeIndicators.Add(instance);

            BossShapeTelegraphIndicator indicator = instance.GetComponent<BossShapeTelegraphIndicator>();
            float angle = facingDeg + hit.FacingOffsetDegrees;
            float length = ResolveLength(hit);

            if (hit.Shape == BossShapeKind.Rectangle)
            {
                Vector3 center = ResolveRectangleCenter(hit, origin, angle, length);
                indicator.ShowRectangle(center, length, hit.Width, angle);
            }
            else
            {
                indicator.ShowSector(origin, length, hit.Width, angle, hit.InnerRadius);
            }

            _telegraphProgress.Add((indicator, _sequence.Elapsed, hit.TelegraphDuration));

            return instance;
        }

        /// <summary>
        /// 표시 중인 텔레그래프마다 경과율(0~1)을 계산해 SetProgress01에 반영한다 - 표시 시점부터
        /// 판정까지 갈수록 점점 진해지도록.
        /// </summary>
        private void UpdateTelegraphProgress()
        {
            float elapsed = _sequence.Elapsed;

            for (int i = 0; i < _telegraphProgress.Count; i++)
            {
                (BossShapeTelegraphIndicator indicator, float showTime, float duration) = _telegraphProgress[i];

                if (indicator == null)
                {
                    continue;
                }

                float progress = duration > 0f ? Mathf.Clamp01((elapsed - showTime) / duration) : 1f;
                indicator.SetProgress01(progress);
            }
        }

        private void ResolveHit(BossPatternHit hit, Vector3 origin, float facingDeg, GameObject indicatorInstance)
        {
            float angle = facingDeg + hit.FacingOffsetDegrees;
            float length = ResolveLength(hit);

            List<Health> targets;

            if (hit.Shape == BossShapeKind.Rectangle)
            {
                Vector3 center = ResolveRectangleCenter(hit, origin, angle, length);
                targets = new List<Health>(BossPatternShapes.FindHitsInRectangle(center, length, hit.Width, angle, allyLayerMask));
            }
            else
            {
                Vector2 forward = BossPatternShapes.AngleToDirection(angle);
                targets = new List<Health>(BossPatternShapes.FindHitsInSector(origin, length, hit.Width, forward, allyLayerMask, hit.InnerRadius));
            }

            // 데미지 적용 전에 이 판정 자신의 상태(인디케이터 해제)부터 정리한다 - TakeDamage가
            // 플레이어를 죽이면 Rank.RankPromotionBattleController.HandleFailure()가 동기적으로
            // 이 보스 자신을 풀에 반납해(ReleaseBoss) OnDespawned()가 이 시퀀스를 취소시킬 수
            // 있다(War.Boss.WarBossPatternRunner.ResolvePattern과 동일한 재진입 함정).
            ReleaseIndicator(indicatorInstance, hit.LeaveResolveFlash);

            if (hit.EmitScreenSlashAndShake)
            {
                Vector3 slashPosition = hit.Shape == BossShapeKind.Rectangle
                    ? ResolveRectangleCenter(hit, origin, angle, length)
                    : origin;

                GameBootstrapper.Events?.Publish(new ScreenSlashRequestedEvent(angle, slashPosition));
                GameBootstrapper.Events?.Publish(new SkillCameraShakeRequestedEvent(0.2f, 0.35f));
            }

            foreach (Health health in targets)
            {
                health.TakeDamage(ResolveDamage(hit, health));
            }

            // 대상에게 피해를 다 입힌 뒤 시전자 자신을 옮긴다 - 순서를 바꾸면(텔레포트 먼저) 위의
            // targets가 이미 옛 위치 기준으로 스캔을 끝낸 뒤라 영향은 없지만, "찌르기가 맞고 나서
            // 파고든다"는 연출 순서와 코드 순서를 일치시켜 두는 편이 이해하기 쉽다.
            if (hit.TeleportToRangeEnd && hit.Shape == BossShapeKind.Rectangle)
            {
                Vector2 direction = BossPatternShapes.AngleToDirection(angle);
                transform.position = origin + (Vector3)(direction * length);
            }
        }

        private float ResolveLength(BossPatternHit hit)
        {
            if (!hit.ReachesMapEdge)
            {
                return hit.Length;
            }

            if (_cameraFollowService == null)
            {
                return hit.Length;
            }

            Vector2 halfExtent = _cameraFollowService.GetWorldBoundsHalfExtent();
            return halfExtent.magnitude * 2f;
        }

        private static Vector3 ResolveRectangleCenter(BossPatternHit hit, Vector3 originPoint, float angleDeg, float length)
        {
            if (!hit.AnchorAtNearEdge)
            {
                return originPoint;
            }

            Vector2 direction = BossPatternShapes.AngleToDirection(angleDeg);
            return originPoint + (Vector3)(direction * (length * 0.5f));
        }

        private Health FindPlayer()
        {
            return _cameraFollowService != null
                ? NearestHealthScan.FindNearestInBounds(transform.position, _cameraFollowService.HomeLocalPosition, _cameraFollowService.GetWorldBoundsHalfExtent(), allyLayerMask)
                : NearestHealthScan.FindNearest(transform.position, detectionRange, allyLayerMask);
        }

        /// <summary>
        /// leaveFlash가 true면 즉시 풀에 반납하는 대신 BossShapeTelegraphIndicator.PlayResolveFlash()로
        /// 흰색 잔상을 남기고 그 인디케이터 스스로 알아서 반납하도록 맡긴다(자기완결형 - 이후 이
        /// 컨트롤러는 더 이상 그 인스턴스를 신경 쓰지 않는다).
        /// </summary>
        private void ReleaseIndicator(GameObject instance, bool leaveFlash = false)
        {
            if (instance == null)
            {
                return;
            }

            _activeIndicators.Remove(instance);
            _telegraphProgress.RemoveAll(entry => entry.Indicator != null && entry.Indicator.gameObject == instance);

            if (leaveFlash && instance.TryGetComponent(out BossShapeTelegraphIndicator indicator))
            {
                indicator.PlayResolveFlash();
                return;
            }

            if (_pool != null)
            {
                _pool.Release(instance);
            }
        }

        private void ReleaseAllIndicators()
        {
            if (_pool != null)
            {
                foreach (GameObject instance in _activeIndicators)
                {
                    _pool.Release(instance);
                }
            }

            _activeIndicators.Clear();
            _telegraphProgress.Clear();
        }
    }
}
