using System.Collections.Generic;
using Core;
using Managers;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 뽑기 결과를 슬롯으로 하나씩 리빌한다. 개수와 무관하게 총 소요 시간(totalRevealDuration)이
    /// 항상 같도록, 슬롯 하나당 간격을 totalRevealDuration/개수로 매번 새로 계산한다 - 많이 뽑을수록
    /// 슬롯 하나하나는 더 빨리 나타나지만 전체가 끝나는 시점은 10개를 뽑든 300개를 뽑든 동일하다.
    /// 리빌 중에는 pullButtonsContainer를 숨겨 결과를 확인하는 동안 재요청을 막고, 다 끝나면
    /// 되돌린다. 리빌하는 동안 스크롤을 매번 맨 아래로 붙여, 방금 나온 슬롯이 항상 보이게 한다.
    /// 카테고리(장비/병사/스킬)를 전혀 모른다 - Reveal 호출자(각 *GachaTierPanelUI)가 이미
    /// GachaResultVisual로 변환해 넘긴다.
    ///
    /// 슬롯은 Instantiate/Destroy 대신 PoolManager로 재사용한다 - 뽑기 1회당 최대 300개까지
    /// 생성/파괴가 반복되는데, 플레이 세션 동안 대량 뽑기를 계속 반복하면(수십만 회 단위) 매번
    /// 새로 Instantiate/Destroy하는 비용과 GC 부담이 누적된다. 같은 프리팹을 쓰는 모든
    /// GachaResultRevealController 인스턴스(장비/병사/스킬 각 티어 패널)가 풀 하나를 공유한다.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class GachaResultRevealController : MonoBehaviour, ITickable
    {
        private const int PoolDefaultCapacity = 30;
        private const int PoolMaxSize = 300;

        [SerializeField]
        private Transform content;

        [SerializeField]
        private GachaResultSlotUI slotPrefab;

        [SerializeField]
        private GameObject pullButtonsContainer;

        [SerializeField]
        [Min(0.1f)]
        private float totalRevealDuration = 2f;

        private ScrollRect _scrollRect;
        private PoolManager _pool;
        private readonly List<GameObject> _spawned = new();
        private IReadOnlyList<GachaResultVisual> _pending;
        private int _nextIndex;
        private float _elapsed;
        private float _intervalPerSlot;

        private void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
        }

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            if (_pool == null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                _pool = pool;
                _pool.EnsurePool(slotPrefab.gameObject, PoolDefaultCapacity, PoolMaxSize);
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        /// <summary>
        /// 이전 결과를 지우고 results를 처음부터 다시 하나씩 리빌한다. results가 비어있으면
        /// (예: 재화 부족으로 한 번도 못 뽑음) 버튼을 곧바로 되돌린다.
        /// </summary>
        public void Reveal(IReadOnlyList<GachaResultVisual> results)
        {
            if (_pool == null)
            {
                return;
            }

            foreach (GameObject spawned in _spawned)
            {
                _pool.Release(spawned);
            }

            _spawned.Clear();

            _pending = results;
            _nextIndex = 0;

            bool hasResults = _pending != null && _pending.Count > 0;
            _intervalPerSlot = hasResults ? totalRevealDuration / _pending.Count : totalRevealDuration;
            _elapsed = _intervalPerSlot;

            pullButtonsContainer?.SetActive(!hasResults);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_pending == null || _nextIndex >= _pending.Count)
            {
                return;
            }

            _elapsed += deltaTime;
            bool spawnedAny = false;

            while (_elapsed >= _intervalPerSlot && _nextIndex < _pending.Count)
            {
                _elapsed -= _intervalPerSlot;
                SpawnNext();
                spawnedAny = true;
            }

            if (spawnedAny)
            {
                ScrollToBottom();
            }

            if (_nextIndex >= _pending.Count)
            {
                pullButtonsContainer?.SetActive(true);
            }
        }

        private void SpawnNext()
        {
            GameObject instance = _pool.Get(slotPrefab.gameObject, content.position, Quaternion.identity);
            instance.transform.SetParent(content, false);
            instance.GetComponent<GachaResultSlotUI>().Initialize(_pending[_nextIndex]);
            _spawned.Add(instance);
            _nextIndex++;
        }

        /// <summary>
        /// 방금 추가된 슬롯의 레이아웃이 아직 반영되지 않은 채로 스크롤 위치를 계산하면 한 프레임
        /// 밀린 위치로 스크롤되므로, Content의 레이아웃을 즉시 강제 반영한 뒤 맨 아래(0)로 붙인다.
        /// </summary>
        private void ScrollToBottom()
        {
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
