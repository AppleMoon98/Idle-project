using System;
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
                // 개별 Tick()이 예외를 던져도(예: 파괴된 오브젝트에 접근) 이 루프 자체가 중단되면
                // 안 된다 - 중단되면 아래 _isTicking = false / ApplyPendingChanges()가 영원히
                // 실행되지 않아, 그 시점부터 새로 등록되는 ITickable은 전부 시작조차 못 하고
                // 대기 중인 해제도 반영되지 않는 채로 게임 전체의 틱이 사실상 멈춰버린다(실제
                // 발생했던 연쇄 장애). 예외를 던진 tickable은 로그를 남기고 즉시 해제 대상으로
                // 등록해, 같은 오브젝트가 다음 프레임에도 똑같이 예외를 반복하지 않게 한다.
                try
                {
                    _tickables[i].Tick(deltaTime);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    _pendingRemove.Add(_tickables[i]);
                }
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
