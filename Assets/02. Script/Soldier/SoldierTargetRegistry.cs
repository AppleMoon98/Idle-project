using System.Collections.Generic;
using Character;
using Core;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 어떤 Soldier가 어떤 적을 추적 중인지 기록하는 등록부.
    /// Soldier끼리 같은 적을 중복 추적하지 않도록(대체 대상이 있는 한) 조율하는 데 쓰인다.
    /// </summary>
    public sealed class SoldierTargetRegistry : IManager, IService
    {
        private readonly Dictionary<GameObject, Health> _claims = new();

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            _claims.Clear();
        }

        /// <summary>
        /// candidate가 claimant 자신이 아닌 다른 Soldier에 의해 이미 추적 중인 대상인지 반환한다.
        /// </summary>
        public bool IsClaimedByOther(Health candidate, GameObject claimant)
        {
            foreach (KeyValuePair<GameObject, Health> claim in _claims)
            {
                if (claim.Key != claimant && claim.Value == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// claimant의 현재 추적 대상을 갱신한다. target이 null이면 claim을 해제한다.
        /// </summary>
        public void SetClaim(GameObject claimant, Health target)
        {
            if (target == null)
            {
                _claims.Remove(claimant);
                return;
            }

            _claims[claimant] = target;
        }
    }
}
