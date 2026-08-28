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

        /// <summary>
        /// 한 Tick에서 몰아서 생성할 수 있는 슬롯 개수 상한. deltaTime이 크게 튀면(랙 스파이크,
        /// 백그라운드 복귀 등) while 루프가 한 프레임에 최대 300개까지 Instantiate를 몰아칠 수
        /// 있었다(GitHub 이슈 #10) - 초과분은 다음 Tick으로 자연스럽게 이월된다(_elapsed가
        /// 그대로 누적된 채 남아있으므로 별도 이월 로직이 필요 없음).
        /// </summary>
        private const int MaxSpawnsPerTick = 20;

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

            RestoreAlreadyRevealedSlots();
        }

        /// <summary>
        /// 패널이 다시 활성화될 때(탭 왕복 등), OnDisable에서 반납했던 슬롯 중 아직 리빌이
        /// 진행 중이던(인덱스가 _nextIndex 미만이고 아직 전체를 다 못 보여준) 항목들을 애니메이션
        /// 없이 즉시 다시 스폰한다(GitHub 이슈 #10 완료 조건 3번 중 "리빌 도중 이탈" 케이스) - 이게
        /// 없으면 리빌 도중 탭을 벗어났다 돌아왔을 때 이미 보여줬던 앞쪽 슬롯들은 다시는 나타나지
        /// 않고 남은 뒤쪽 슬롯만 보인다(예: 10개 중 3개를 보여준 뒤 벗어났다 돌아오면 나머지 7개만
        /// 나타남).
        ///
        /// 리빌이 이미 완전히 끝난 뒤(_nextIndex >= _pending.Count) 이탈했다 돌아온 경우는 더 이상
        /// 복원하지 않고 _pending을 비운다 - 원래는 이 경우도 무조건 다시 스폰했었지만(화면이
        /// 비면 안 된다는 이유), 그 결과 이미 다 본 뽑기 결과가 탭을 오갈 때마다("뽑기 기록이
        /// 초기화되지 않고 계속 남아있다") 계속 다시 나타나 pullButtonsContainer 위 슬롯 영역을
        /// 채우며 뽑기 버튼을 가리는 문제로 실사용 중 제보됐다. 다 본 결과는 한 번 탭을 벗어나는
        /// 순간 "소비됨"으로 간주해 비우는 쪽이, 매번 예전 뽑기 결과가 되살아나 화면을 계속
        /// 차지하는 것보다 낫다는 판단.
        /// </summary>
        private void RestoreAlreadyRevealedSlots()
        {
            if (_pool == null || _pending == null || _nextIndex <= 0)
            {
                return;
            }

            if (_nextIndex >= _pending.Count)
            {
                _pending = null;
                _nextIndex = 0;
                return;
            }

            for (int i = 0; i < _nextIndex; i++)
            {
                SpawnAt(i);
            }

            ScrollToBottom();
        }

        /// <summary>
        /// 패널이 비활성화될 때(카테고리 전환 등) 스폰해둔 슬롯 전체를 풀로 반납한다(GitHub 이슈
        /// #10) - 이전엔 Reveal()이 다시 호출될 때만 반납해서, 탭을 그냥 전환하기만 해도 이전
        /// 슬롯이 "체크아웃된" 상태로 영원히 남아 풀 스택이 채워지지 않았다. 같은 프리팹을 쓰는
        /// 모든 GachaResultRevealController가 풀 하나를 공유하므로, 여기서 반납해야 다른
        /// 컨트롤러가 새로 Instantiate하지 않고 재사용할 수 있다.
        /// </summary>
        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }

            ReleaseSpawned();
        }

        private void ReleaseSpawned()
        {
            if (_pool != null)
            {
                foreach (GameObject spawned in _spawned)
                {
                    _pool.Release(spawned);
                }
            }

            _spawned.Clear();
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

            ReleaseSpawned();

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
            int spawnedThisTick = 0;

            while (_elapsed >= _intervalPerSlot && _nextIndex < _pending.Count && spawnedThisTick < MaxSpawnsPerTick)
            {
                _elapsed -= _intervalPerSlot;
                SpawnNext();
                spawnedAny = true;
                spawnedThisTick++;
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
            SpawnAt(_nextIndex);
            _nextIndex++;
        }

        /// <summary>
        /// index번째 결과 슬롯을 스폰해 content에 붙인다. SpawnNext(애니메이션 진행 중 다음 한 칸)와
        /// RestoreAlreadyRevealedSlots(재활성화 시 이미 결정된 구간 전체를 즉시 복원) 둘 다 이
        /// 하나의 스폰 로직을 공유한다 - _nextIndex를 건드리는 책임은 호출자에게 남겨(RestoreAlready
        /// RevealedSlots는 이미 결정된 인덱스를 다시 그릴 뿐 진행 상태 자체를 바꾸면 안 되므로).
        /// </summary>
        private void SpawnAt(int index)
        {
            GameObject instance = _pool.Get(slotPrefab.gameObject, content.position, Quaternion.identity);
            instance.transform.SetParent(content, false);
            instance.GetComponent<GachaResultSlotUI>().Initialize(_pending[index]);
            _spawned.Add(instance);
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
