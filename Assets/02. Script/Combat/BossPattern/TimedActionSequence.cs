using System;
using System.Collections.Generic;

namespace Combat.BossPattern
{
    /// <summary>
    /// (경과 시간, 실행할 액션)의 정렬된 플랫 목록을 순서대로 재생하는 범용 타이밍 실행기.
    /// MonoBehaviour도 ITickable도 아닌 순수 C# 클래스 - 소유자(Rank.Boss.PromotionBossController 등)가
    /// 자기 Tick() 안에서 직접 Tick(deltaTime)을 호출해 구동한다. 같은 시각에 여러 스텝을 등록하면
    /// (예: 두 텔레그래프를 동시에 띄우기) 자연히 "동시 실행"이 되므로, 여러 판정이 겹치는 타이밍도
    /// 별도의 병렬/분기 개념 없이 이 하나의 정렬된 목록만으로 표현된다. 이 프로젝트 전체에
    /// 코루틴(StartCoroutine/IEnumerator) 전례가 없어, 기존 GameTicker+ITickable 컨벤션을
    /// 그대로 따르기 위해 코루틴 대신 이 방식을 쓴다.
    /// </summary>
    public sealed class TimedActionSequence
    {
        private readonly struct Step
        {
            public readonly float DelaySinceStart;
            public readonly Action Execute;

            public Step(float delaySinceStart, Action execute)
            {
                DelaySinceStart = delaySinceStart;
                Execute = execute;
            }
        }

        private List<Step> _steps;
        private float _elapsed;
        private int _nextStepIndex;

        public bool IsRunning { get; private set; }

        /// <summary>
        /// 현재 재생 위치(Play() 이후 누적 경과 시간, 초). 소유자가 "지금 표시 중인 텔레그래프가
        /// 얼마나 진행됐는지" 계산할 때(예: 예고 표시 경과율에 따라 점점 진하게) 기준 시각으로 쓴다.
        /// </summary>
        public float Elapsed => _elapsed;

        /// <summary>
        /// steps는 delaySinceStart 오름차순으로 정렬돼 있어야 한다(호출자가 순서대로 추가하면
        /// 자연히 정렬된다).
        /// </summary>
        public void Play(List<(float delaySinceStart, Action execute)> steps)
        {
            _steps = new List<Step>(steps.Count);

            foreach ((float delaySinceStart, Action execute) in steps)
            {
                _steps.Add(new Step(delaySinceStart, execute));
            }

            _elapsed = 0f;
            _nextStepIndex = 0;
            IsRunning = _steps.Count > 0;
        }

        public void Cancel()
        {
            IsRunning = false;
            _steps = null;
            _nextStepIndex = 0;
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            _elapsed += deltaTime;

            // 스텝 실행(Execute)이 데미지를 주다가 대상을 죽여 그 죽음이 동기적으로 이 시퀀스의
            // 소유자 자신을 정리(Cancel 호출)하는 재진입 사슬을 만들 수 있다(War.Boss.
            // WarBossPatternRunner.ResolvePattern이 문서화한 것과 동일한 함정 - 예: 보스 패턴이
            // 플레이어를 죽이면 RankPromotionBattleController.HandleFailure()가 이 보스 자신을
            // 풀에 반납하며 PromotionBossController.OnDespawned()가 이 시퀀스를 Cancel()한다).
            // 그래서 while 조건 맨 앞에 IsRunning을 매번 다시 확인해, Cancel() 이후엔 _steps가
            // null이어도 그 뒤 조건을 평가하지 않고 즉시 루프를 빠져나가게 한다.
            while (IsRunning && _nextStepIndex < _steps.Count && _steps[_nextStepIndex].DelaySinceStart <= _elapsed)
            {
                Step step = _steps[_nextStepIndex];
                _nextStepIndex++;
                step.Execute?.Invoke();
            }

            if (IsRunning && _nextStepIndex >= _steps.Count)
            {
                IsRunning = false;
            }
        }
    }
}
