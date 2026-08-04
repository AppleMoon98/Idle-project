using Core;
using UnityEngine;

namespace Skill
{
    /// <summary>
    /// 고정 스킬 슬롯 하나. 같은 오브젝트의 ISkillEffect 구현체를 캐싱해두고, SkillSO.Cooldown마다
    /// SkillService에서 현재 레벨을 조회해 Execute를 호출한다. 어떤 효과인지는 전혀 모른다.
    /// </summary>
    public sealed class SkillSlot : MonoBehaviour, ITickable
    {
        [SerializeField]
        private SkillSO definition;

        private ISkillEffect _effect;
        private float _elapsed;

        public SkillSO Definition => definition;

        private void Awake()
        {
            _effect = GetComponent<ISkillEffect>();
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

        void ITickable.Tick(float deltaTime)
        {
            if (_effect == null || definition == null)
            {
                return;
            }

            int level = 0;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillService service))
            {
                level = service.GetLevel(definition);
            }

            // 0레벨(강화 전)에는 아직 습득하지 않은 것으로 보고 자동 시전하지 않는다.
            if (level <= 0)
            {
                return;
            }

            _elapsed += deltaTime;

            if (_elapsed < definition.Cooldown)
            {
                return;
            }

            _elapsed = 0f;

            _effect.Execute(transform, definition.GetMagnitude(level));
        }
    }
}
