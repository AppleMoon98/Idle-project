using UnityEngine;

namespace War
{
    /// <summary>
    /// War 구조물 하나의 동작을 정의하는 데이터 에셋.
    /// </summary>
    [CreateAssetMenu(fileName = "WarStructure", menuName = "Idle Project/War/War Structure")]
    public sealed class WarStructureSO : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private float activationRadius = 3f;

        [SerializeField]
        private float pushRadius = 5f;

        [SerializeField]
        private float pushDistance = 1.5f;

        [SerializeField]
        private float actionInterval = 1f;

        [SerializeField]
        [Range(0f, 1f)]
        private float captureGainPerAction = 0.1f;

        /// <summary>
        /// 구조물 이름(UI 표시용).
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 이 반경 안에 아군이 있어야 구조물이 작동한다.
        /// </summary>
        public float ActivationRadius => activationRadius;

        /// <summary>
        /// 작동 시 이 반경 안의 몬스터를 밀어낸다.
        /// </summary>
        public float PushRadius => pushRadius;

        /// <summary>
        /// 한 번 작동할 때 몬스터를 밀어내는 거리.
        /// </summary>
        public float PushDistance => pushDistance;

        /// <summary>
        /// 작동 판정 주기(초).
        /// </summary>
        public float ActionInterval => actionInterval;

        /// <summary>
        /// 한 번 작동할 때마다 채워지는 점령 게이지(0~1 중 비율).
        /// </summary>
        public float CaptureGainPerAction => captureGainPerAction;
    }
}
