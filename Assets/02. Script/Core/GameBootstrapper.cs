using Managers;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 씬 진입점. ServiceLocator/EventBus/GameTicker/PoolManager를 초기화하고
    /// 이후 모든 시스템이 조회할 수 있는 단일 접근점(composition root)을 제공한다.
    /// </summary>
    [RequireComponent(typeof(GameTicker))]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        /// <summary>
        /// 부트스트랩 완료 후 전역에서 조회 가능한 ServiceLocator.
        /// </summary>
        public static ServiceLocator Services { get; private set; }

        /// <summary>
        /// 부트스트랩 완료 후 전역에서 조회 가능한 EventBus.
        /// </summary>
        public static EventBus Events { get; private set; }

        private void Awake()
        {
            Services = new ServiceLocator();
            Events = new EventBus();

            Services.Register(Events);
            Services.Register(GetComponent<GameTicker>());

            var poolManager = new PoolManager();
            poolManager.Initialize();
            Services.Register(poolManager);
        }

        private void OnDestroy()
        {
            if (Services != null && Services.TryGet(out PoolManager poolManager))
            {
                poolManager.Shutdown();
            }

            Services?.Clear();
            Events?.Clear();

            Services = null;
            Events = null;
        }
    }
}
