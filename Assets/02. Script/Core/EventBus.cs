using System;
using System.Collections.Generic;

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
        /// 이벤트 타입 T의 인스턴스를 구독자 전체에게 발행한다.
        /// </summary>
        public void Publish<T>(T evt)
        {
            if (_channels.TryGetValue(typeof(T), out Delegate existing))
            {
                ((Action<T>)existing).Invoke(evt);
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
