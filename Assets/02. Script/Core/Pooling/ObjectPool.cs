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
        private readonly HashSet<T> _checkedOut = new();
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
        /// 현재 Get()으로 대여되어 아직 반납되지 않은 인스턴스 개수. 대여/유휴가 겹치지 않는다는
        /// 불변조건(동일 인스턴스를 두 번 반납해도 유휴 스택에 두 번 들어가지 않음)을 유지하기
        /// 위해 _checkedOut 집합으로 직접 추적한다 - 대여 개수를 별도로 세지 않으면 이중 반납이
        /// 예외/로그 없이 그대로 통과해 같은 GameObject가 두 호출자에게 동시에 대여되는 문제가
        /// 있었다(실제로 겪음 - 사망 이벤트 처리와 세션 정리 경로가 같은 프레임에 동일 몬스터를
        /// 반납하는 경우 등).
        /// </summary>
        public int CountActive => _checkedOut.Count;

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
            _checkedOut.Add(instance);
            _onGet?.Invoke(instance);
            return instance;
        }

        /// <summary>
        /// 인스턴스를 풀로 반납한다. 최대 크기를 초과하면 반납하지 않고 폐기한다. instance가
        /// 지금 대여 중인 상태가 아니면(이미 반납됐거나, 애초에 이 풀에서 Get()된 적이 없으면)
        /// 아무 것도 하지 않고 false를 반환한다 - 이중 반납이 유휴 스택에 같은 참조를 두 번
        /// 쌓아, 이후 서로 다른 두 Get() 호출자가 동일한 활성 인스턴스를 받게 되는 것을 막는다.
        /// 반환값은 호출자(Managers.PoolManager)가 IPoolable.OnDespawned를 "상태 전환당 정확히
        /// 한 번만" 호출하도록 판단하는 데 쓰인다.
        /// </summary>
        public bool Release(T instance)
        {
            if (!_checkedOut.Remove(instance))
            {
                return false;
            }

            _onRelease?.Invoke(instance);

            if (_pool.Count >= _maxSize)
            {
                _onDestroy?.Invoke(instance);
                return true;
            }

            _pool.Push(instance);
            return true;
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
