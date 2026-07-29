using System;
using System.Collections.Generic;

namespace Core
{
    /// <summary>
    /// 타입 기반으로 서비스/매니저 인스턴스를 등록하고 조회하는 경량 레지스트리.
    /// 전역 정적 싱글턴을 대체해, 의존성을 명시적으로 등록/조회할 수 있게 한다.
    /// </summary>
    public sealed class ServiceLocator
    {
        private readonly Dictionary<Type, object> _registry = new();

        /// <summary>
        /// 인스턴스를 타입 T로 등록한다. 이미 등록된 타입이면 예외를 던진다.
        /// </summary>
        public void Register<T>(T instance) where T : class
        {
            Type key = typeof(T);

            if (_registry.ContainsKey(key))
            {
                throw new InvalidOperationException($"Service of type {key.Name} is already registered.");
            }

            _registry.Add(key, instance);
        }

        /// <summary>
        /// 타입 T로 등록된 인스턴스를 반환한다. 등록되어 있지 않으면 예외를 던진다.
        /// </summary>
        public T Get<T>() where T : class
        {
            Type key = typeof(T);

            if (!_registry.TryGetValue(key, out object instance))
            {
                throw new InvalidOperationException($"Service of type {key.Name} is not registered.");
            }

            return (T)instance;
        }

        /// <summary>
        /// 타입 T로 등록된 인스턴스가 있으면 instance에 담고 true를 반환한다.
        /// </summary>
        public bool TryGet<T>(out T instance) where T : class
        {
            if (_registry.TryGetValue(typeof(T), out object raw))
            {
                instance = (T)raw;
                return true;
            }

            instance = null;
            return false;
        }

        /// <summary>
        /// 타입 T의 등록을 해제한다.
        /// </summary>
        public void Unregister<T>() where T : class
        {
            _registry.Remove(typeof(T));
        }

        /// <summary>
        /// 등록된 모든 인스턴스를 제거한다.
        /// </summary>
        public void Clear()
        {
            _registry.Clear();
        }
    }
}
