using System;
using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 다른 캐릭터와 겹치지 않도록 매 틱마다 서로 밀어낸다. 기본적으로 진영(적/아군) 구분 없이
    /// Player/Soldier/Monster 전부에 동일하게 적용된다. CharacterMover의 목표 추적 이동과는 완전히
    /// 독립적인 별도 ITickable이라 이동 로직(목표 추적/카이팅/집결 등) 자체는 전혀 건드리지 않고,
    /// 그 결과 위치에 겹침 보정만 얹는다. 몸 크기는 CircleCollider2D.radius × 월드 스케일에서 그대로
    /// 읽어와 별도 수치를 새로 정의하지 않는다 — Monster_Elite/Monster_Boss처럼 localScale이 다른
    /// 경우도 자동으로 반영된다. ignoreLayerMask(기본값 0=Nothing, 대부분의 유닛은 비워둠)에 포함된
    /// 레이어의 상대는 이 인스턴스 자신의 스캔에서 제외한다 — 예를 들어 기마병/기마궁수는 자기 편
    /// 레이어를 여기 넣어, 아군 무리를 뚫고 지나갈 때 밀려나지 않고 그대로 통과한다(오직 이 오브젝트
    /// 자신의 계산만 영향받는다 — 통과당하는 아군 쪽의 CharacterSeparation은 그대로 자기 자신을
    /// 밀어내려 할 수 있다). ignorePlayer(기본값 false)는 PlayerMarker로 식별되는 플레이어 자신만
    /// 콕 집어 제외한다 — Player/Soldier가 같은 레이어를 공유해 ignoreLayerMask만으로는 "플레이어
    /// 자신"과 "다른 병사"를 구분할 수 없기 때문에(레이어 기준 제외는 병사끼리도 함께 무시하게
    /// 되어버린다) 별도 필드로 뒀다.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class CharacterSeparation : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float separationStrength = 6f;

        [SerializeField]
        private LayerMask ignoreLayerMask;

        [SerializeField]
        private bool ignorePlayer;

        private CircleCollider2D _collider;
        private float _bodyRadius;

        // 34개 캐릭터 프리팹 전체가 매 틱 이 컴포넌트를 하나씩 갖고 도는데, 기존 코드는 틱마다
        // Physics2D.OverlapCircleAll로 새 배열을 할당하고 있었다(GitHub 이슈 #23 - 지속적인 GC
        // 압력의 핵심 원인). GameTicker.Update()가 모든 ITickable.Tick()을 한 스레드에서 순차
        // 호출하므로(재진입 없음), 인스턴스마다 배열을 따로 두지 않고 static 버퍼 하나를 전부가
        // 공유해도 안전하다 - 이렇게 하면 34개 인스턴스가 아니라 게임 전체에서 배열 하나만 존재한다.
        private static Collider2D[] _overlapBuffer = new Collider2D[16];

        /// <summary>
        /// _overlapBuffer가 지금까지 실제로 확장된 횟수(진단용, GitHub 이슈 #23) - 정상 밀집도에서는
        /// 0(또는 세션 초반 한두 번)에서 멈춰야 한다. 계속 늘어난다면 시작 크기(16)를 키우는 게 낫다.
        /// </summary>
        public static int BufferGrowthCount { get; private set; }

        /// <summary>
        /// 넉백 등 외부 시스템이 순간적으로 밀어내는 양을 계산할 때 같은 기준(콜라이더 반지름)을
        /// 쓸 수 있도록 공개한다.
        /// </summary>
        public float BodyRadius => _bodyRadius;

        private void Awake()
        {
            _collider = GetComponent<CircleCollider2D>();
        }

        private void OnEnable()
        {
            _bodyRadius = _collider.radius * transform.lossyScale.x;
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        void ITickable.Tick(float deltaTime)
        {
            Vector2 position = transform.position;
            Vector2 push = Vector2.zero;

            // 레이어 마스크를 안 넘기던 기존 OverlapCircleAll(position, radius) 2-인자 오버로드와
            // 동일하게(GitHub 이슈 #23), useLayerMask는 기본값 false로 둬 전 레이어를 그대로
            // 스캔한다. useTriggers는 프로젝트 전역 설정(Physics2D.queriesHitTriggers)을 그대로
            // 따라야 레거시 All 계열 오버로드와 정확히 같은 결과를 낸다.
            var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
            int count = Physics2D.OverlapCircle(position, _bodyRadius * 2f, filter, _overlapBuffer);

            while (count == _overlapBuffer.Length)
            {
                Array.Resize(ref _overlapBuffer, _overlapBuffer.Length * 2);
                BufferGrowthCount++;
                count = Physics2D.OverlapCircle(position, _bodyRadius * 2f, filter, _overlapBuffer);
            }

            for (int i = 0; i < count; i++)
            {
                Collider2D other = _overlapBuffer[i];

                if (other == null || other.gameObject == gameObject || !other.TryGetComponent(out CharacterSeparation otherSeparation))
                {
                    continue;
                }

                if ((ignoreLayerMask.value & (1 << other.gameObject.layer)) != 0)
                {
                    continue;
                }

                if (ignorePlayer && other.TryGetComponent(out PlayerMarker _))
                {
                    continue;
                }

                Vector2 otherPosition = other.transform.position;
                Vector2 delta = position - otherPosition;
                float distance = delta.magnitude;
                float minDistance = _bodyRadius + otherSeparation._bodyRadius;

                if (distance >= minDistance)
                {
                    continue;
                }

                Vector2 direction = distance > 0.0001f ? delta / distance : UnityEngine.Random.insideUnitCircle.normalized;
                push += direction * (minDistance - distance);
            }

            if (push != Vector2.zero)
            {
                transform.position += (Vector3)(push * separationStrength * deltaTime);
            }
        }
    }
}
