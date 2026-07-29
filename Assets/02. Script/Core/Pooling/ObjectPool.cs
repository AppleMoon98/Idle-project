using System;
using System.Collections.Generic;

namespace Core.Pooling
{
    /// <summary>
    /// Stack 기반 범용 오브젝트 풀. 생성/획득/반납/폐기 로직을 콜백으로 위임받아
    /// 어떤 타입의 인스턴스든 재사용 가능하게 관리한다.
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _pool;
        private readonly Func<T> _createFunc;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Action<T> _onDestroy;
        private readonly int _maxSize;

        /// <summary>
        /// 현재 풀에 대기 중인(비활성) 인스턴스 개수.
        /// </summary>
        public int CountInactive => _pool.Count;

        /// <summary>
        /// ObjectPool을 생성하고 defaultCapacity만큼 미리 채운다(prewarm).
        /// </summary>
        /// <param name="createFunc">새 인스턴스를 생성하는 함수. 생성 직후에는 비활성/반납 상태여야 한다.</param>
        /// <param name="onGet">Get()으로 꺼낼 때 호출할 콜백 (예: 활성화, 위치 지정)</param>
        /// <param name="onRelease">Release()로 반납할 때 호출할 콜백 (예: 비활성화)</param>
        /// <param name="onDestroy">최대 크기를 초과해 실제로 폐기할 때 호출할 콜백</param>
        /// <param name="defaultCapacity">미리 생성해 둘 인스턴스 개수</param>
        /// <param name="maxSize">풀이 보유할 수 있는 최대 인스턴스 개수</param>
        public ObjectPool(
            Func<T> createFunc,
            Action<T> onGet,
            Action<T> onRelease,
            Action<T> onDestroy,
            int defaultCapacity,
            int maxSize)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _onGet = onGet;
            _onRelease = onRelease;
            _onDestroy = onDestroy;
            _maxSize = maxSize;
            _pool = new Stack<T>(defaultCapacity);

            Prewarm(defaultCapacity);
        }

        /// <summary>
        /// 인스턴스를 count개 미리 생성해 풀을 채운다.
        /// </summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _pool.Push(_createFunc());
            }
        }

        /// <summary>
        /// 풀에서 인스턴스를 꺼낸다. 비어 있으면 새로 생성한다.
        /// </summary>
        public T Get()
        {
            T instance = _pool.Count > 0 ? _pool.Pop() : _createFunc();
            _onGet?.Invoke(instance);
            return instance;
        }

        /// <summary>
        /// 인스턴스를 풀로 반납한다. 최대 크기를 초과하면 반납하지 않고 폐기한다.
        /// </summary>
        public void Release(T instance)
        {
            _onRelease?.Invoke(instance);

            if (_pool.Count >= _maxSize)
            {
                _onDestroy?.Invoke(instance);
                return;
            }

            _pool.Push(instance);
        }

        /// <summary>
        /// 풀에 대기 중인 모든 인스턴스를 폐기하고 비운다.
        /// </summary>
        public void Clear()
        {
            while (_pool.Count > 0)
            {
                _onDestroy?.Invoke(_pool.Pop());
            }
        }
    }
}
