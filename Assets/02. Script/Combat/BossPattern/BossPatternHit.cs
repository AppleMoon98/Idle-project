using System;
using UnityEngine;

namespace Combat.BossPattern
{
    /// <summary>
    /// 보스 패턴 하나를 이루는 판정 한 건 - "언제 예고가 뜨고, 얼마 뒤 어떤 모양/크기로 얼마의
    /// 피해를 주는가"만 담는 평범한 데이터 행(Stage.MonsterSpawnEntry와 같은 급). 여러 개를
    /// 이어붙이면 하나의 패턴이 된다(예: 찌르기=1개, 앞뒤 가르기=2개). 위치(origin)와 기준 방향은
    /// 이 데이터에 없다 - 호출자가 캐스트 시점에 스냅샷해 실행 시점에 넘겨준다(같은 데이터를
    /// 여러 위치/방향에 재사용할 수 있도록, 예: 페이즈2의 세로줄 4개가 같은 템플릿을 랜덤 위치에
    /// 재사용).
    /// </summary>
    [Serializable]
    public sealed class BossPatternHit
    {
        [SerializeField]
        private float delay;

        [SerializeField]
        private float telegraphDuration = 1f;

        [SerializeField]
        private BossShapeKind shape;

        [SerializeField]
        private float length = 5f;

        [SerializeField]
        private float width = 2f;

        [SerializeField]
        private float facingOffsetDegrees;

        [SerializeField]
        private float damage;

        [SerializeField]
        private float damagePercentOfMaxHealth;

        [SerializeField]
        private bool reachesMapEdge;

        [SerializeField]
        private float innerRadius;

        [SerializeField]
        private bool anchorAtNearEdge;

        [SerializeField]
        private bool emitScreenSlashAndShake;

        [SerializeField]
        private bool teleportToRangeEnd;

        [SerializeField]
        private bool leaveResolveFlash;

        /// <summary>
        /// 패턴 시작 후 이 판정의 예고가 표시되기까지 걸리는 시간(초).
        /// </summary>
        public float Delay => delay;

        /// <summary>
        /// 예고 표시부터 실제 판정까지 걸리는 시간(초).
        /// </summary>
        public float TelegraphDuration => telegraphDuration;

        public BossShapeKind Shape => shape;

        /// <summary>
        /// 직사각형=전진 방향 길이, 부채꼴=반지름. ReachesMapEdge가 true면 무시된다.
        /// </summary>
        public float Length => length;

        /// <summary>
        /// 직사각형=폭, 부채꼴=전체 각도(도).
        /// </summary>
        public float Width => width;

        /// <summary>
        /// 호출자가 넘겨주는 기준 방향(보통 캐스트 시점 플레이어 방향)에 더해지는 오프셋.
        /// 0=기준 방향 그대로, 180=정반대 등.
        /// </summary>
        public float FacingOffsetDegrees => facingOffsetDegrees;

        public float Damage => damage;

        /// <summary>
        /// 0보다 크면 Damage(고정값) 대신 "맞는 대상의 최대 체력 × 이 비율"을 데미지로 쓴다(예:
        /// 0.25 = 최대 체력의 25%). 기본값 0 = 비활성화, 기존 고정 데미지 패턴은 전혀 영향받지
        /// 않는다(RankSO.RequiredStage == null과 같은 sparse opt-in 관례).
        /// </summary>
        public float DamagePercentOfMaxHealth => damagePercentOfMaxHealth;

        /// <summary>
        /// true면 실행 시점 Length 대신 Services.CameraFollowService의 고정 경계 대각선 길이로
        /// 대체한다 - "맵 끝까지 닿는" 부채꼴처럼 지도 크기에 관계없이 항상 화면을 가로지르게
        /// 해야 하는 판정에 쓴다.
        /// </summary>
        public bool ReachesMapEdge => reachesMapEdge;

        /// <summary>
        /// 부채꼴 전용(직사각형은 무시). 0보다 크면 원점에서 이 거리 안쪽은 판정/표시에서 제외돼
        /// 고리(도넛) 모양이 된다 - "이미 공격한 안쪽 범위를 제외한 바깥쪽"처럼 앞선 판정과 겹치지
        /// 않아야 하는 후속 공격에 쓴다. 기본값 0 = 기존과 동일한 꽉 찬 부채꼴.
        /// </summary>
        public float InnerRadius => innerRadius;

        /// <summary>
        /// 직사각형 전용(부채꼴은 무시 - 부채꼴은 항상 꼭짓점 기준). false(기본값)면 넘겨받은
        /// 위치가 곧 직사각형의 중심(Physics2D.OverlapBoxAll의 point와 동일한 의미) - 페이즈2의
        /// 십자/세로줄처럼 한 지점을 중심으로 대칭으로 뻗는 판정에 쓴다. true면 넘겨받은 위치가
        /// 직사각형의 "가까운 쪽 모서리"(보스 자신의 위치)이고, 실제 중심은 그 지점에서 진행
        /// 방향으로 Length의 절반만큼 앞으로 이동한 곳이 된다 - 찌르기처럼 보스 몸에서 앞으로
        /// 뻗어나가는 판정에 쓴다.
        /// </summary>
        public bool AnchorAtNearEdge => anchorAtNearEdge;

        /// <summary>
        /// true면 판정이 실제로 적용되는 순간 이 판정의 방향/위치를 따라 화면 슬래시
        /// (UI.Events.ScreenSlashRequestedEvent)와 카메라 흔들림(Skill.Events.
        /// SkillCameraShakeRequestedEvent)을 함께 요청한다. 기본값 false = 기존 판정은 전혀
        /// 영향받지 않는다(다른 sparse opt-in 필드와 같은 관례).
        /// </summary>
        public bool EmitScreenSlashAndShake => emitScreenSlashAndShake;

        /// <summary>
        /// true면(직사각형 판정 전용, 찌르기처럼 앞으로 뻗어나가는 판정에 쓴다) 판정이 적용된
        /// 직후 시전자를 이 판정 범위가 끝나는 지점(origin + 방향 × Length)으로 순간이동시킨다 -
        /// "찌르기 거리만큼 앞으로 파고드는" 돌진형 연출. 기본값 false.
        /// </summary>
        public bool TeleportToRangeEnd => teleportToRangeEnd;

        /// <summary>
        /// true면 판정이 적용되는 순간 예고 표시가 즉시 사라지지 않고 흰색+완전 불투명 잔상으로
        /// 0.1초(Combat.BossPattern.BossShapeTelegraphIndicator.PlayResolveFlash) 남았다가 사라진다.
        /// 기본값 false = 기존처럼 판정과 동시에 즉시 사라짐.
        /// </summary>
        public bool LeaveResolveFlash => leaveResolveFlash;
    }
}
