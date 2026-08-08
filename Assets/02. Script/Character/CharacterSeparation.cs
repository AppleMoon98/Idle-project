using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 다른 캐릭터와 겹치지 않도록 매 틱마다 서로 밀어낸다. 진영(적/아군) 구분 없이 Player/Soldier/
    /// Monster 전부에 동일하게 적용된다. CharacterMover의 목표 추적 이동과는 완전히 독립적인 별도
    /// ITickable이라 이동 로직(목표 추적/카이팅/집결 등) 자체는 전혀 건드리지 않고, 그 결과 위치에
    /// 겹침 보정만 얹는다. 레이어와 무관하게 CharacterSeparation을 가진 상대라면 전부 대상으로
    /// 삼는다(진영 필터링 없음). 몸 크기는 CircleCollider2D.radius × 월드 스케일에서 그대로 읽어와
    /// 별도 수치를 새로 정의하지 않는다 — Monster_Elite/Monster_Boss처럼 localScale이 다른 경우도
    /// 자동으로 반영된다.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class CharacterSeparation : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float separationStrength = 6f;

        private CircleCollider2D _collider;
        private float _bodyRadius;

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

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(position, _bodyRadius * 2f);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider2D other = overlaps[i];

                if (other == null || other.gameObject == gameObject || !other.TryGetComponent(out CharacterSeparation otherSeparation))
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

                Vector2 direction = distance > 0.0001f ? delta / distance : Random.insideUnitCircle.normalized;
                push += direction * (minDistance - distance);
            }

            if (push != Vector2.zero)
            {
                transform.position += (Vector3)(push * separationStrength * deltaTime);
            }
        }
    }
}
