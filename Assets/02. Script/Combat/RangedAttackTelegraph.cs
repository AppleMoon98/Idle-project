using Character;
using Core;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Attacker.AttackWindupStarted를 구독해, 실제 발사 attackWindupLeadTime초 전부터 예상 타겟까지
    /// 빨간 선(LineRenderer)으로 예고한다. 판정에는 전혀 관여하지 않는 순수 시각 컴포넌트 —
    /// War.Boss.WarBossTelegraphIndicator와 동일한 "판정은 Attacker/RangedAttackBehavior가, 그리기는
    /// 이 컴포넌트가" 분리 원칙을 따른다.
    /// </summary>
    [RequireComponent(typeof(Attacker))]
    public sealed class RangedAttackTelegraph : MonoBehaviour, ITickable
    {
        [SerializeField]
        private Color lineColor = new Color(1f, 0.15f, 0.1f, 0.85f);

        [SerializeField]
        private float lineWidth = 0.05f;

        private Attacker _attacker;
        private LineRenderer _line;
        private Health _target;

        private void Awake()
        {
            _attacker = GetComponent<Attacker>();
            _line = CreateLine();
        }

        private void OnEnable()
        {
            _attacker.AttackWindupStarted += OnWindupStarted;
            _attacker.AttackPerformed += OnAttackPerformed;
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            _attacker.AttackWindupStarted -= OnWindupStarted;
            _attacker.AttackPerformed -= OnAttackPerformed;
            TickerRegistration.Unregister(this);
            HideLine();
        }

        private void OnWindupStarted(Health target)
        {
            _target = target;
            _line.enabled = true;
        }

        private void OnAttackPerformed()
        {
            HideLine();
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_target == null || _target.IsDead)
            {
                HideLine();
                return;
            }

            _line.SetPosition(0, transform.position);
            _line.SetPosition(1, _target.transform.position);
        }

        private void HideLine()
        {
            _target = null;
            _line.enabled = false;
        }

        private LineRenderer CreateLine()
        {
            var lineObject = new GameObject("AttackTelegraphLine");
            lineObject.transform.SetParent(transform, false);

            var renderer = lineObject.AddComponent<LineRenderer>();
            renderer.positionCount = 2;
            renderer.useWorldSpace = true;
            renderer.startWidth = lineWidth;
            renderer.endWidth = lineWidth;
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.startColor = lineColor;
            renderer.endColor = lineColor;
            renderer.sortingOrder = 5;
            renderer.enabled = false;

            return renderer;
        }
    }
}
