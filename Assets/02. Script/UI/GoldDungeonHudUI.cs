using Core;
using Dungeon.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 골드 던전 세션 중 남은 시간/몬스터 수를 보여준다. Dungeon 도메인 컴포넌트를 직접
    /// 참조하지 않고 이벤트로만 반응한다. 남은 시간은 WarClimaxWarningUI와 동일하게
    /// 시작 시점의 총 시간만 받아 직접 카운트다운한다.
    /// </summary>
    public sealed class GoldDungeonHudUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GameObject hudRoot;

        [SerializeField]
        private Text timeText;

        [SerializeField]
        private Text remainingText;

        private float _remainingTime;
        private bool _isActive;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<GoldDungeonSessionStartedEvent>(OnSessionStarted);
            GameBootstrapper.Events?.Subscribe<GoldDungeonProgressChangedEvent>(OnProgressChanged);
            GameBootstrapper.Events?.Subscribe<GoldDungeonSessionEndedEvent>(OnSessionEnded);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            hudRoot.SetActive(false);
            _isActive = false;
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<GoldDungeonSessionStartedEvent>(OnSessionStarted);
            GameBootstrapper.Events?.Unsubscribe<GoldDungeonProgressChangedEvent>(OnProgressChanged);
            GameBootstrapper.Events?.Unsubscribe<GoldDungeonSessionEndedEvent>(OnSessionEnded);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        private void OnSessionStarted(GoldDungeonSessionStartedEvent evt)
        {
            _remainingTime = evt.TimeLimitSeconds;
            _isActive = true;
            hudRoot.SetActive(true);
            SetRemainingText(evt.TotalMonsters);
            UpdateTimeText();
        }

        private void OnProgressChanged(GoldDungeonProgressChangedEvent evt)
        {
            SetRemainingText(evt.RemainingMonsters);
        }

        private void SetRemainingText(int remainingMonsters)
        {
            remainingText.text = $"남은 몬스터: {remainingMonsters}";
        }

        private void OnSessionEnded(GoldDungeonSessionEndedEvent evt)
        {
            _isActive = false;
            hudRoot.SetActive(false);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isActive)
            {
                return;
            }

            _remainingTime = Mathf.Max(0f, _remainingTime - deltaTime);
            UpdateTimeText();
        }

        private void UpdateTimeText()
        {
            timeText.text = $"제한시간: {Mathf.CeilToInt(_remainingTime)}초";
        }
    }
}
