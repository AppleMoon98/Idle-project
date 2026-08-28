using Core;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 데미지 숫자를 표시하고 위로 떠오르며 페이드아웃되다가 스스로 풀로 반납되는 순수 시각 컴포넌트.
    /// 데미지 계산/치명타 판정은 전부 Health/Attacker가 소유하며, 이 컴포넌트는 전달받은 값을
    /// 그리기만 한다(Combat.WeaponSwing, Combat.CircleTelegraphIndicator와 동일한 분리 철학).
    /// Projectile과 마찬가지로 스스로 타이머를 관리하다 스스로 풀에 반납되는 자기완결형 컴포넌트다.
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class DamageNumber : MonoBehaviour, ITickable
    {
        [SerializeField]
        private DamageNumberConfigSO config;

        /// <summary>
        /// 이 config 에셋 참조. Combat.DamageNumberSpawner가 (프리팹 하나당 에셋도 하나뿐이므로)
        /// 별도 GameBootstrapper 필드 없이 프리팹의 이 컴포넌트에서 그대로 읽어 쓴다.
        /// </summary>
        public DamageNumberConfigSO Config => config;

        /// <summary>
        /// 이 숫자가 현재 표시 중인 데미지의 대상. DamageNumberSpawner가 "풀에서 재사용되면서
        /// 다른 대상 몫으로 바뀌었는지"를 판별하는 데 쓴다(병합 대상 오판 방지) — 같은 인스턴스가
        /// Release 후 곧바로 다른 대상에게 Get()될 수 있어, 단순히 인스턴스가 활성 상태인지만으로는
        /// "아직 내 대상 몫인지"를 알 수 없다.
        /// </summary>
        public GameObject OwnerTarget { get; private set; }

        private TextMesh _textMesh;
        private MeshRenderer _meshRenderer;
        private Camera _camera;
        private Vector3 _startPosition;
        private float _elapsed;

        private void Awake()
        {
            _textMesh = GetComponent<TextMesh>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _camera = Camera.main;

            // 폰트가 동적(Dynamic) 폰트라 아직 쓰인 적 없는 문자가 요청되면 내부 아틀라스
            // 텍스처가 통째로 재생성될 수 있다 - 그 경우 아웃라인 머티리얼이 들고 있던 텍스처
            // 참조가 낡아버리므로, 항상 폰트의 현재 텍스처로 다시 맞춰둔다(숫자만 표시하는
            // 컴포넌트라 실제로 재생성이 일어날 일은 거의 없지만, 방어적으로 한 번 동기화).
            _meshRenderer.sharedMaterial.mainTexture = _textMesh.font.material.mainTexture;
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        /// <summary>
        /// 지정된 위치에서 데미지 숫자 표시를 시작한다(또는 병합 갱신 시 다시 시작한다). 풀에서
        /// 꺼낸 직후 호출되어야 한다. isPoison이 true면 isCritical과 무관하게 독(지속 피해)
        /// 색상으로 표시한다 - 평소(흰색/치명타 빨간색)와 다르게 초록색으로 보여서 지속 피해임을
        /// 한눈에 구분할 수 있게 한다. owner는 이 숫자가 지금 누구 몫인지 기록해둔다
        /// (DamageNumberSpawner의 병합 대상 오판 방지, OwnerTarget 참고) — 병합 갱신 시에도 매번
        /// 넘겨받아 그대로 재기록한다(같아도 무해함).
        /// </summary>
        public void Show(GameObject owner, Vector3 worldPosition, float amount, bool isCritical, bool isPoison = false)
        {
            OwnerTarget = owner;
            _startPosition = worldPosition + Vector3.up * config.SpawnHeightOffset;
            transform.position = _startPosition;
            _elapsed = 0f;

            _textMesh.text = Mathf.RoundToInt(amount).ToString();
            _textMesh.fontSize = config.FontSize;
            _textMesh.color = isPoison ? config.PoisonColor : (isCritical ? config.CriticalColor : config.NormalColor);

            ApplyZoomCompensation();
        }

        void ITickable.Tick(float deltaTime)
        {
            ApplyZoomCompensation();

            _elapsed += deltaTime;

            float progress01 = config.Lifetime <= 0f ? 1f : Mathf.Clamp01(_elapsed / config.Lifetime);

            transform.position = _startPosition + Vector3.up * (config.RiseDistance * progress01);

            Color color = _textMesh.color;
            color.a = 1f - progress01;
            _textMesh.color = color;

            if (_elapsed >= config.Lifetime)
            {
                ReleaseSelf();
            }
        }

        private void ReleaseSelf()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                pool.Release(gameObject);
            }
        }

        /// <summary>
        /// 카메라 핀치 줌(UI.CameraPinchZoomUI)으로 Camera.orthographicSize가 바뀌어도 데미지
        /// 숫자의 화면상 크기가 항상 일정해 보이도록, 현재 orthographicSize와 config가 튜닝된
        /// 기준 크기의 비율만큼 자기 자신의 localScale을 보정한다 - 월드 스페이스 TextMesh라
        /// 아무 보정 없이는 확대(orthographicSize 작아짐)할수록 커 보이고 축소할수록 작아 보인다.
        /// </summary>
        private void ApplyZoomCompensation()
        {
            if (_camera == null || config.ReferenceOrthographicSize <= 0f)
            {
                return;
            }

            float scale = _camera.orthographicSize / config.ReferenceOrthographicSize;
            transform.localScale = Vector3.one * scale;
        }
    }
}
