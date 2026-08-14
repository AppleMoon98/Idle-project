using Character.Events;
using Core;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// DamageAppliedEvent를 구독해 데미지가 적용된 위치에 DamageNumber를 스폰한다.
    /// 데미지/치명타 판정에는 관여하지 않고 이미 계산된 값을 그대로 전달만 한다.
    /// </summary>
    public sealed class DamageNumberSpawner
    {
        private readonly EventBus _events;
        private readonly PoolManager _pool;
        private readonly GameObject _damageNumberPrefab;

        public DamageNumberSpawner(EventBus events, PoolManager pool, GameObject damageNumberPrefab, int poolCapacity = 16, int poolMaxSize = 64)
        {
            _events = events;
            _pool = pool;
            _damageNumberPrefab = damageNumberPrefab;

            if (_damageNumberPrefab != null)
            {
                _pool.EnsurePool(_damageNumberPrefab, poolCapacity, poolMaxSize);
            }

            _events.Subscribe<DamageAppliedEvent>(OnDamageApplied);
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. 게임 종료 시 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);
        }

        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (_damageNumberPrefab == null || evt.Target == null)
            {
                return;
            }

            GameObject instance = _pool.Get(_damageNumberPrefab, evt.Target.transform.position, Quaternion.identity);
            instance.GetComponent<DamageNumber>().Show(evt.Target.transform.position, evt.Amount, evt.IsCritical, evt.IsPoison);
        }
    }
}
