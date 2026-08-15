using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 타입별 이벤트 채널을 통해 발행(Publish)/구독(Subscribe)하는 중앙 통신 허브.
    /// 도메인 시스템 간 직접 참조 없이 이벤트만으로 통신하도록 한다.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> _channels = new();

        /// <summary>
        /// 이벤트 타입 T에 대한 핸들러를 구독한다.
        /// </summary>
        public void Subscribe<T>(Action<T> handler)
        {
            Type key = typeof(T);

            if (_channels.TryGetValue(key, out Delegate existing))
            {
                _channels[key] = Delegate.Combine(existing, handler);
            }
            else
            {
                _channels[key] = handler;
            }
        }

        /// <summary>
        /// 이벤트 타입 T에 대한 핸들러 구독을 해제한다.
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            Type key = typeof(T);

            if (!_channels.TryGetValue(key, out Delegate existing))
            {
                return;
            }

            Delegate combined = Delegate.Remove(existing, handler);

            if (combined == null)
            {
                _channels.Remove(key);
            }
            else
            {
                _channels[key] = combined;
            }
        }

        /// <summary>
        /// 이벤트 타입 T의 인스턴스를 구독자 전체에게 발행한다. 구독자 하나가 예외를 던져도
        /// 나머지 구독자는 계속 호출되어야 한다 - 예를 들어 스테이지 클리어의 마지막 처치는 같은
        /// CharacterDiedEvent 처리 도중 재진입으로 StageClearedEvent/LoadStage까지 동기적으로
        /// 실행하는데, 이때 중간의 어떤 구독자가 예외를 던지면 뒤에 늦게 구독된
        /// Character.PoolReleaseOnDeath(몬스터를 실제로 풀에 반납하는 구독자)가 아예 호출되지
        /// 못해 IsDead만 true인 채 반납되지 않은 "좀비" 몬스터가 남는 문제가 있었다(실사용 중
        /// 발견). GameTicker.Update()가 개별 Tick() 호출을 try/catch로 감싸는 것과 동일한 이유로
        /// 동일하게 구독자를 하나씩 분리 호출한다.
        /// </summary>
        public void Publish<T>(T evt)
        {
            if (!_channels.TryGetValue(typeof(T), out Delegate existing))
            {
                return;
            }

            foreach (Delegate handler in existing.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler).Invoke(evt);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        /// <summary>
        /// 등록된 모든 구독을 제거한다.
        /// </summary>
        public void Clear()
        {
            _channels.Clear();
        }
    }
}
