using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 무기 소켓이 공격 시 재생하는 시각 모션의 공통 기반. 타격 판정에는 관여하지 않는다.
    /// 휘두르기(<see cref="WeaponSwing"/>), 찌르기(<see cref="WeaponThrust"/>) 등
    /// 병과별로 다른 모션을 같은 방식(무기 소켓에 붙여 Play 호출)으로 재생하기 위한 추상화다.
    /// </summary>
    public abstract class WeaponMotion : MonoBehaviour
    {
        /// <summary>
        /// 모션을 처음부터 재생한다.
        /// </summary>
        public abstract void Play();
    }
}
