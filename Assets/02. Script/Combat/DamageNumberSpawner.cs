using System.Collections.Generic;
using Character.Events;
using Core;
using Managers;
using Stage.Events;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// DamageAppliedEvent를 구독해 데미지가 적용된 위치에 DamageNumber를 스폰한다.
    /// 데미지/치명타 판정에는 관여하지 않고 이미 계산된 값을 그대로 전달만 한다.
    ///
    /// **과밀 표시 방지(다수 유닛 전투에서 숫자가 겹쳐 쌓여 화면을 못 읽는 문제):** 같은 대상이
    /// DamageNumberConfigSO.AggregationWindowSeconds 이내에 다시 맞으면 새 숫자를 스폰하지 않고
    /// 기존 숫자에 값을 더해 다시 띄운다(대상별 사실상 1개로 병합). 화면 전체 동시 표시 개수도
    /// MaxActiveOnScreen으로 상한을 둔다(그 이상은 데미지 계산은 그대로 진행하되 숫자 표시만
    /// 조용히 건너뜀). 새로 스폰되는 숫자는 PositionJitterRadius만큼 위치를 살짝 흩어 같은
    /// 지점에 몰린 여러 대상의 숫자가 완전히 겹치지 않게 한다.
    /// </summary>
    public sealed class DamageNumberSpawner
    {
        private readonly EventBus _events;
        private readonly PoolManager _pool;
        private readonly GameObject _damageNumberPrefab;
        private readonly DamageNumberConfigSO _config;

        /// <summary>
        /// 대상 GameObject -> 그 대상 몫으로 현재 표시 중인 데미지 숫자의 누적 상태. 대상이
        /// 죽거나 병합 윈도우가 지나면 갱신 없이 그대로 남아있을 수 있지만, OwnerTarget 비교와
        /// 병합 윈도우 만료 체크가 그 경우를 안전하게 걸러낸다(값이 틀리게 재사용되지 않는다).
        /// </summary>
        private readonly Dictionary<GameObject, AggregatedNumber> _activeByTarget = new();

        private sealed class AggregatedNumber
        {
            public DamageNumber Component;
            public float AggregatedAmount;
            public bool IsCritical;
            public float LastUpdateTime;
        }

        public DamageNumberSpawner(EventBus events, PoolManager pool, GameObject damageNumberPrefab, int poolCapacity = 16, int poolMaxSize = 64)
        {
            _events = events;
            _pool = pool;
            _damageNumberPrefab = damageNumberPrefab;

            if (_damageNumberPrefab != null)
            {
                _pool.EnsurePool(_damageNumberPrefab, poolCapacity, poolMaxSize);
                _config = _damageNumberPrefab.GetComponent<DamageNumber>()?.Config;
            }

            _events.Subscribe<DamageAppliedEvent>(OnDamageApplied);
            _events.Subscribe<CombatFieldResetEvent>(OnCombatFieldReset);
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. 게임 종료 시 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);
            _events.Unsubscribe<CombatFieldResetEvent>(OnCombatFieldReset);
        }

        /// <summary>
        /// 스테이지 전환/던전 등 오버레이 진입·복귀마다 발행되는 신호(Skill.Effects의 지속 효과들이
        /// 같은 이유로 구독하는 것과 동일) — 이전 전투에서 추적하던 대상 GameObject 참조를 전부
        /// 비운다. 몬스터는 풀링되어 다음 웨이브에서 같은 인스턴스가 완전히 다른 개체로 재사용될
        /// 수 있으므로, 여기서 비워두지 않으면 새 대상의 첫 피격이 이전 전투의 낡은 누적값 위에
        /// 잘못 병합될 수 있다.
        /// </summary>
        private void OnCombatFieldReset(CombatFieldResetEvent evt)
        {
            _activeByTarget.Clear();
        }

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (_damageNumberPrefab == null || evt.Target == null)
            {
                return;
            }

            if (TryMergeIntoActive(evt))
            {
                return;
            }

            if (_config != null && _activeByTarget.Count >= _config.MaxActiveOnScreen)
            {
                // 상한에 걸렸을 때만(매 이벤트마다 무조건이 아니라) 죽은 항목을 정리한다 -
                // 대부분의 대상은 한두 번 맞고 죽어 "다시 맞아서 병합 시도되며 정리되는" 경로
                // 자체가 없으므로, 정리를 전적으로 TryMergeIntoActive의 실패 경로에만 맡기면
                // 실제로는 화면에 하나도 안 떠 있는데도 Count가 영원히 상한에 고정된다.
                PruneStaleEntries();

                if (_activeByTarget.Count >= _config.MaxActiveOnScreen)
                {
                    return;
                }
            }

            Vector3 spawnPosition = evt.Target.transform.position + RandomJitter();
            GameObject instance = _pool.Get(_damageNumberPrefab, spawnPosition, Quaternion.identity);
            DamageNumber component = instance.GetComponent<DamageNumber>();
            component.Show(evt.Target, spawnPosition, evt.Amount, evt.IsCritical, evt.IsPoison);

            _activeByTarget[evt.Target] = new AggregatedNumber
            {
                Component = component,
                AggregatedAmount = evt.Amount,
                IsCritical = evt.IsCritical,
                LastUpdateTime = Time.time
            };
        }

        /// <summary>
        /// 같은 대상이 병합 윈도우 이내에 또 맞았고, 추적 중인 숫자가 아직 이 대상 몫 그대로
        /// 살아있으면(풀 재사용으로 다른 대상 몫이 되지 않았으면) 그 숫자에 값을 더해 다시
        /// 띄운다. 병합했으면 true.
        /// </summary>
        private bool TryMergeIntoActive(DamageAppliedEvent evt)
        {
            if (_config == null || !_activeByTarget.TryGetValue(evt.Target, out AggregatedNumber active))
            {
                return false;
            }

            if (active.Component == null
                || !active.Component.gameObject.activeInHierarchy
                || active.Component.OwnerTarget != evt.Target
                || Time.time - active.LastUpdateTime > _config.AggregationWindowSeconds)
            {
                return false;
            }

            active.AggregatedAmount += evt.Amount;
            active.IsCritical |= evt.IsCritical;
            active.LastUpdateTime = Time.time;
            active.Component.Show(evt.Target, evt.Target.transform.position, active.AggregatedAmount, active.IsCritical, evt.IsPoison);

            return true;
        }

        /// <summary>
        /// 컴포넌트가 이미 비활성화됐거나(Lifetime 만료로 스스로 풀에 반납됨) 다른 대상 몫으로
        /// 재사용된(OwnerTarget 불일치) 죽은 항목을 _activeByTarget에서 제거한다 - 실제로는 화면에
        /// 보이지 않는데도 계속 "활성"으로 집계돼 MaxActiveOnScreen 상한을 영구히 채우던 실사용
        /// 버그를 고친다. Dictionary를 순회하며 바로 제거하면 InvalidOperationException이 나므로
        /// 대상 키를 먼저 모아 두 번째 패스에서 제거한다.
        /// </summary>
        private void PruneStaleEntries()
        {
            List<GameObject> staleKeys = null;

            foreach (KeyValuePair<GameObject, AggregatedNumber> pair in _activeByTarget)
            {
                AggregatedNumber entry = pair.Value;
                bool isStale = entry.Component == null
                    || !entry.Component.gameObject.activeInHierarchy
                    || entry.Component.OwnerTarget != pair.Key;

                if (isStale)
                {
                    staleKeys ??= new List<GameObject>();
                    staleKeys.Add(pair.Key);
                }
            }

            if (staleKeys == null)
            {
                return;
            }

            foreach (GameObject key in staleKeys)
            {
                _activeByTarget.Remove(key);
            }
        }

        private Vector3 RandomJitter()
        {
            if (_config == null || _config.PositionJitterRadius <= 0f)
            {
                return Vector3.zero;
            }

            Vector2 offset = Random.insideUnitCircle * _config.PositionJitterRadius;
            return new Vector3(offset.x, offset.y, 0f);
        }
    }
}
