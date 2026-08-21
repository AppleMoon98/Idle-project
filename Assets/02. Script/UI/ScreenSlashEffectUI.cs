using Core;
using UI.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 화면이 대각선/세로 등으로 베이는 듯한 슬래시 연출. 슬래시 라인이 짧게 스윕되며 나타났다
    /// 사라지고, 그 순간에 화이트 플래시가 함께 겹쳐진다. TemporaryMessageUI가 쓰는 "GameTicker
    /// 기반 ITickable + TickerRegistration + *RequestedEvent 구독" 패턴을 그대로 재사용한다.
    /// Canvas 전역에 하나만 두고, 어떤 도메인이든 ScreenSlashRequestedEvent만 발행하면 재생된다.
    ///
    /// 슬래시 라인은 slashLines 개수만큼의 슬롯을 라운드로빈으로 돌려쓴다 - 승급전 보스의 세로줄
    /// 볼리처럼 같은 순간에 여러 줄이 동시에 그어져야 하는 경우, 하나의 라인만으로는 나중 호출이
    /// 이전 호출의 진행 중이던 애니메이션을 덮어써버려 실제로는 한 줄만 보인다. 화이트 플래시는
    /// 화면 전체에 한 번만 겹치면 충분하므로 공유 상태 하나만 둔다.
    /// </summary>
    public sealed class ScreenSlashEffectUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private RectTransform[] slashLines;

        [SerializeField]
        private Image[] slashImages;

        [SerializeField]
        private Image flashOverlay;

        [SerializeField]
        private float sweepInSeconds = 0.08f;

        [SerializeField]
        private float holdSeconds = 0.08f;

        [SerializeField]
        private float sweepOutSeconds = 0.22f;

        [SerializeField]
        private float flashSeconds = 0.3f;

        // 1.0(완전 불투명)까지 올라가면 그 순간 화면이 통째로 하얗게 사라져 슬래시 라인도 게임
        // 화면도 아예 안 보이는 문제가 있었다(실사용 중 발견) - 최대 알파를 낮춰 항상 게임 화면이
        // 살짝 비쳐 보이도록 한다.
        [SerializeField]
        private float maxFlashAlpha = 0.5f;

        private float[] _elapsed;
        private bool[] _isPlaying;
        private int _nextSlotIndex;

        private float _flashElapsed;
        private bool _isFlashing;

        private RectTransform _canvasRect;

        private float TotalLineDuration => sweepInSeconds + holdSeconds + sweepOutSeconds;

        private void Awake()
        {
            _elapsed = new float[slashLines.Length];
            _isPlaying = new bool[slashLines.Length];

            Canvas canvas = GetComponentInParent<Canvas>();
            _canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        }

        private void OnEnable()
        {
            // 이 GameObject는 항상 활성 상태를 유지한다 - Awake에서 SetActive(false)로 스스로를
            // 끄면 같은 활성화 패스 안에서 OnEnable 자체가 아예 호출되지 않아(Unity의 잘 알려진
            // 함정) TickerRegistration.Register/이벤트 구독이 영원히 실행되지 않는다. 숨김 상태는
            // 대신 Image의 alpha=0으로 표현한다(TemporaryMessageUI가 CanvasGroup.alpha로 하는
            // 것과 같은 방향).
            TickerRegistration.Register(this);
            GameBootstrapper.Events?.Subscribe<ScreenSlashRequestedEvent>(OnScreenSlashRequested);

            for (int i = 0; i < slashImages.Length; i++)
            {
                SetAlpha(slashImages[i], 0f);
                _isPlaying[i] = false;
            }

            SetAlpha(flashOverlay, 0f);
            _isFlashing = false;
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
            GameBootstrapper.Events?.Unsubscribe<ScreenSlashRequestedEvent>(OnScreenSlashRequested);
        }

        private void OnScreenSlashRequested(ScreenSlashRequestedEvent evt)
        {
            if (evt.AngleDegrees.HasValue && evt.WorldPosition.HasValue)
            {
                Play(evt.AngleDegrees.Value, evt.WorldPosition.Value);
            }
            else if (evt.AngleDegrees.HasValue)
            {
                Play(evt.AngleDegrees.Value);
            }
            else
            {
                Play();
            }
        }

        /// <summary>
        /// 슬래시 연출을 화면 중앙, 씬에 마지막으로 설정된 각도 그대로 즉시 재생한다.
        /// </summary>
        public void Play()
        {
            PlayAt(null, null);
        }

        /// <summary>
        /// 슬래시 라인의 회전각(도)을 지정해 화면 중앙에서 재생한다. 0 = 수평, 90 = 수직.
        /// </summary>
        public void Play(float angleDegrees)
        {
            PlayAt(angleDegrees, null);
        }

        /// <summary>
        /// 슬래시 라인의 회전각과 특정 월드 좌표(예: 보스 패턴이 실제로 그은 세로줄의 위치)를
        /// 지정해 재생한다. 월드 좌표를 현재 카메라 기준 캔버스 로컬 좌표로 변환해 그 지점에
        /// 라인을 배치한다.
        /// </summary>
        public void Play(float angleDegrees, Vector3 worldPosition)
        {
            PlayAt(angleDegrees, worldPosition);
        }

        private void PlayAt(float? angleDegrees, Vector3? worldPosition)
        {
            if (slashLines == null || slashLines.Length == 0)
            {
                return;
            }

            int slot = _nextSlotIndex;
            _nextSlotIndex = (_nextSlotIndex + 1) % slashLines.Length;

            RectTransform rect = slashLines[slot];

            if (angleDegrees.HasValue)
            {
                rect.localEulerAngles = new Vector3(0f, 0f, angleDegrees.Value);
            }

            rect.anchoredPosition = worldPosition.HasValue && TryWorldToCanvasLocalPoint(worldPosition.Value, out Vector2 localPoint)
                ? localPoint
                : Vector2.zero;

            _elapsed[slot] = 0f;
            rect.localScale = new Vector3(0f, 1f, 1f);
            SetAlpha(slashImages[slot], 0f);
            _isPlaying[slot] = true;

            _flashElapsed = 0f;
            _isFlashing = true;
            SetAlpha(flashOverlay, 0f);
        }

        private bool TryWorldToCanvasLocalPoint(Vector3 worldPosition, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;

            if (Camera.main == null || _canvasRect == null)
            {
                return false;
            }

            Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldPosition);

            // Screen Space - Overlay 캔버스는 camera 인자에 반드시 null을 넘겨야 한다(카메라를
            // 넘기면 Screen Space - Camera 모드 기준 변환식이 적용돼 좌표가 어긋난다).
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, null, out localPoint);
        }

        void ITickable.Tick(float deltaTime)
        {
            for (int i = 0; i < slashLines.Length; i++)
            {
                if (_isPlaying[i])
                {
                    _elapsed[i] += deltaTime;
                    TickLine(i);
                }
            }

            if (_isFlashing)
            {
                _flashElapsed += deltaTime;
                TickFlash();
            }
        }

        private void TickLine(int slot)
        {
            RectTransform rect = slashLines[slot];
            Image image = slashImages[slot];
            float elapsed = _elapsed[slot];

            if (elapsed <= sweepInSeconds)
            {
                float t = sweepInSeconds > 0f ? elapsed / sweepInSeconds : 1f;
                rect.localScale = new Vector3(t, 1f, 1f);
                SetAlpha(image, t);
                return;
            }

            if (elapsed <= sweepInSeconds + holdSeconds)
            {
                rect.localScale = Vector3.one;
                SetAlpha(image, 1f);
                return;
            }

            if (elapsed <= TotalLineDuration)
            {
                float fadeElapsed = elapsed - sweepInSeconds - holdSeconds;
                float t = sweepOutSeconds > 0f ? 1f - fadeElapsed / sweepOutSeconds : 0f;
                rect.localScale = Vector3.one;
                SetAlpha(image, Mathf.Clamp01(t));
                return;
            }

            _isPlaying[slot] = false;
            SetAlpha(image, 0f);
        }

        private void TickFlash()
        {
            float t = flashSeconds > 0f ? Mathf.Clamp01(_flashElapsed / flashSeconds) : 1f;
            float alpha = maxFlashAlpha * (1f - t) * (1f - t);
            SetAlpha(flashOverlay, alpha);

            if (t >= 1f)
            {
                _isFlashing = false;
                SetAlpha(flashOverlay, 0f);
            }
        }

        private static void SetAlpha(Image image, float alpha)
        {
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}
