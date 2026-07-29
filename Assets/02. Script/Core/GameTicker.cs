using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 씬에 단 하나만 존재하며, 등록된 ITickable 대상들을 매 프레임 일괄 갱신한다.
    /// 개별 시스템이 각자 MonoBehaviour.Update()를 갖는 대신 이곳에 등록해 사용한다.
    /// </summary>
    public sealed class GameTicker : MonoBehaviour
    {
        private readonly List<ITickable> _tickables = new();
        private readonly List<ITickable> _pendingAdd = new();
        private readonly List<ITickable> _pendingRemove = new();
        private bool _isTicking;

        /// <summary>
        /// 틱 대상으로 등록한다. 순회(Tick) 도중 호출해도 안전하다.
        /// </summary>
        public void Register(ITickable tickable)
        {
            if (_isTicking)
            {
                _pendingAdd.Add(tickable);
            }
            else
            {
                _tickables.Add(tickable);
            }
        }

        /// <summary>
        /// 틱 대상에서 제거한다. 순회(Tick) 도중 호출해도 안전하다.
        /// </summary>
        public void Unregister(ITickable tickable)
        {
            if (_isTicking)
            {
                _pendingRemove.Add(tickable);
            }
            else
            {
                _tickables.Remove(tickable);
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            _isTicking = true;
            for (int i = 0; i < _tickables.Count; i++)
            {
                _tickables[i].Tick(deltaTime);
            }
            _isTicking = false;

            ApplyPendingChanges();
        }

        private void ApplyPendingChanges()
        {
            if (_pendingAdd.Count > 0)
            {
                _tickables.AddRange(_pendingAdd);
                _pendingAdd.Clear();
            }

            if (_pendingRemove.Count > 0)
            {
                for (int i = 0; i < _pendingRemove.Count; i++)
                {
                    _tickables.Remove(_pendingRemove[i]);
                }

                _pendingRemove.Clear();
            }
        }
    }
}
