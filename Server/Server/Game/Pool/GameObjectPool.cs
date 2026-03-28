using System.Collections.Generic;

namespace Server.Game
{
    public class GameObjectPool<T> where T : GameObject, IPoolable, new()
    {
        private readonly Stack<T> _pool = new();
        private readonly int _maxSize;

        public GameObjectPool(int maxSize)
        {
            _maxSize = maxSize;
        }

        // ObjectManager의 lock 안에서 호출
        public T Rent()
        {
            if (_pool.Count > 0)
                return _pool.Pop();

            return new T();
        }

        // ObjectManager의 lock 안에서 호출
        public bool TryReturn(T obj)
        {
            if (_pool.Count >= _maxSize)
                return false;
                
            _pool.Push(obj);

            return true;
        }
    }
}
