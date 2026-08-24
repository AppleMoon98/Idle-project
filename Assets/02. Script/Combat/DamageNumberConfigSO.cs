using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 데미지 숫자 팝업의 상승 거리/지속시간/색상 등을 데이터로 정의하는 공용 설정 애셋.
    /// </summary>
    [CreateAssetMenu(fileName = "DamageNumberConfig", menuName = "Idle Project/Combat/Damage Number Config")]
    public sealed class DamageNumberConfigSO : ScriptableObject
    {
        [SerializeField]
        private float riseDistance = 1f;

        [SerializeField]
        private float lifetime = 0.8f;

        [SerializeField]
        private float spawnHeightOffset = 0.5f;

        [SerializeField]
        private int fontSize = 32;

        [SerializeField]
        private float referenceOrthographicSize = 8f;

        [SerializeField]
        private Color normalColor = Color.white;

        [SerializeField]
        private Color criticalColor = Color.red;

        [SerializeField]
        private Color poisonColor = Color.green;

        /// <summary>
        /// 스폰 위치로부터 위로 이동하는 총 거리.
        /// </summary>
        public float RiseDistance => riseDistance;

        /// <summary>
        /// 스폰부터 소멸까지 걸리는 시간(초).
        /// </summary>
        public float Lifetime => lifetime;

        /// <summary>
        /// 대상 위치로부터 위로 띄워서 스폰할 높이.
        /// </summary>
        public float SpawnHeightOffset => spawnHeightOffset;

        /// <summary>
        /// TextMesh 폰트 크기.
        /// </summary>
        public int FontSize => fontSize;

        /// <summary>
        /// 이 폰트 크기가 화면상 의도한 크기로 보이도록 튜닝된 기준 Camera.orthographicSize.
        /// DamageNumber가 매 틱 Camera.main.orthographicSize / ReferenceOrthographicSize 비율로
        /// 자기 자신의 localScale을 보정해, 카메라 핀치 줌(UI.CameraPinchZoomUI)으로 시야를
        /// 확대/축소해도 데미지 숫자의 화면상 크기가 항상 일정하게 유지되도록 한다.
        /// </summary>
        public float ReferenceOrthographicSize => referenceOrthographicSize;

        /// <summary>
        /// 일반 데미지 색상.
        /// </summary>
        public Color NormalColor => normalColor;

        /// <summary>
        /// 치명타 데미지 색상.
        /// </summary>
        public Color CriticalColor => criticalColor;

        /// <summary>
        /// 독(지속 피해) 데미지 색상. 치명타 색상보다 우선한다(Combat.DamageNumber.Show 참고).
        /// </summary>
        public Color PoisonColor => poisonColor;
    }
}
