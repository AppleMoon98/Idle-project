using Core;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 게임 실행 시 화면 전체를 덮는 타이틀 화면. Canvas의 마지막 sibling(다른 모든 팝업보다 위,
    /// 이 프로젝트의 "나중 sibling이 위에 그려진다" 관례)으로 배치돼 별도 InputBlocker 없이도
    /// 전체화면 배경 자체가 밑의 게임/UI 입력을 가린다.
    ///
    /// 이 화면이 떠 있는 동안은 게임이 실제로 진행되면 안 된다(다른 게임들의 흔한 "탭하여 시작"
    /// 타이틀 화면과 동일) — Awake()에서 Time.timeScale을 0으로 낮춰 몬스터 이동/스폰,
    /// 플레이어·병사 전투, 물리, 애니메이션 등 이 프로젝트의 거의 모든 것이 의존하는
    /// Core.GameTicker(매 프레임 Time.deltaTime을 넘겨 틱)를 사실상 통째로 멈춘다 — 개별
    /// 시스템을 하나씩 찾아 멈추는 대신, 유니티가 이미 제공하는 전역 일시정지 스위치를 그대로
    /// 쓴 것. GameBootstrapper.Start()의 세이브 복원/오프라인 보상 계산은 실시간 시각(DateTimeOffset.
    /// UtcNow)만 쓰고 Time.deltaTime에 의존하지 않아 이 일시정지와 무관하게 정상 진행된다.
    /// 터치로 오버레이가 사라지는 순간(OnDisable) Time.timeScale을 1로 되돌려 게임이 시작된다.
    ///
    /// 로딩 판정은 두 조건을 모두 만족해야 한다: (1) minimumDisplaySeconds 이상 경과 (2)
    /// GameBootstrapper.IsReady(Start()의 모든 초기화 완료). 지금은 GameBootstrapper의 초기화가
    /// 전부 동기적으로 한 프레임 안에 끝나 (2)는 사실상 항상 즉시 참이지만, 다른 도메인
    /// 서비스에는 전혀 의존하지 않고 이 정적 플래그만 폴링하도록 분리해뒀다 — 나중에 진짜
    /// 비동기 초기화(Addressables 사전 다운로드 등)가 추가되면 GameBootstrapper.IsReady가
    /// true가 되는 시점만 그쪽으로 옮기면 되고, 이 컨트롤러는 손댈 필요가 없다.
    ///
    /// 진행바는 실제 로딩 진행률을 알 방법이 없어(동기 초기화라 진짜 %가 없음) 경과 시간을
    /// minimumDisplaySeconds로 나눈 값을 그대로 쓴다 — TemporaryMessageUI/WarClimaxWarningUI가
    /// 이미 쓰는 "GameTicker 기반 ITickable" 패턴을 그대로 재사용하되, GameTicker가 넘겨주는
    /// deltaTime은 이 컨트롤러 자신이 멈춰둔 Time.timeScale=0 때문에 항상 0이라(GameTicker.Update가
    /// Time.deltaTime을 그대로 전달) 대신 Time.unscaledDeltaTime을 직접 읽는다 — 그래야 게임은
    /// 멈춰 있어도 로딩 진행바 자신은 정상적으로 흘러간다.
    /// </summary>
    public sealed class TitleScreenController : MonoBehaviour, ITickable
    {
        private const string LoadingStatusText = "로딩 중...";
        private const string ReadyStatusText = "터치하여 시작";

        [SerializeField]
        private float minimumDisplaySeconds = 1.5f;

        [SerializeField]
        private Image progressBarFill;

        [SerializeField]
        private Text statusText;

        [SerializeField]
        private Button touchButton;

        private float _elapsed;
        private bool _isReadyToEnter;

        private void Awake()
        {
            // 이 화면이 존재하는 동안은 게임이 진행되면 안 된다 - 클래스 doc 참고. 씬의 어떤
            // Update/Tick보다도 먼저 멈춰야 하므로 Awake() 맨 앞에서 즉시 적용한다.
            Time.timeScale = 0f;

            touchButton.onClick.AddListener(OnTouchClicked);
            touchButton.interactable = false;
            statusText.text = LoadingStatusText;
            progressBarFill.fillAmount = 0f;
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);

            // 이 화면이 사라지는 모든 경로(현재는 터치 시 Destroy 하나뿐)에서 예외 없이 게임을
            // 재개시킨다 - 재개를 OnTouchClicked에만 두지 않고 생명주기(OnDisable)에 묶어, 이
            // 컴포넌트가 없어지는 한 반드시 timeScale이 복구되도록 보장한다.
            Time.timeScale = 1f;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_isReadyToEnter)
            {
                return;
            }

            // GameTicker가 넘겨주는 deltaTime은 Time.timeScale=0 때문에 항상 0이다 - 클래스 doc 참고.
            _elapsed += Time.unscaledDeltaTime;
            float progress = minimumDisplaySeconds > 0f ? Mathf.Clamp01(_elapsed / minimumDisplaySeconds) : 1f;
            progressBarFill.fillAmount = progress;

            if (progress >= 1f && GameBootstrapper.IsReady)
            {
                _isReadyToEnter = true;
                statusText.text = ReadyStatusText;
                touchButton.interactable = true;
            }
        }

        // 터치하면 오버레이 전체(자기 자신의 GameObject, 배경/텍스트/진행바/버튼을 전부 포함하는
        // 루트)를 파괴한다 - 다시 볼 일이 없는 1회성 화면이라 SetActive(false) 대신 Destroy를 쓴다.
        private void OnTouchClicked()
        {
            if (!_isReadyToEnter)
            {
                return;
            }

            Destroy(gameObject);
        }
    }
}
