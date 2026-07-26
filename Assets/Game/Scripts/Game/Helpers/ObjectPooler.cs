using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game
{
    public class ObjectPooler<T> where T : MonoBehaviour, IPooled<T>
    {
        private readonly Transform _parent;
        private readonly T[] _instances;
        private readonly Stack<int> _freeIdx;

        public  ObjectPooler (int count, T prefab)
        {
            _parent = new GameObject(prefab.name).transform;
            _instances = new T[count];
            _freeIdx = new Stack<int>(count);

            for (var i = 0; i < count; ++i)
            {
                _instances[i] = Object.Instantiate(prefab);
                _instances[i].gameObject.SetActive(false);
                _instances[i].PoolID = i;
                _instances[i].Pool = this;

                _freeIdx.Push(i);
                
                _instances[i].transform.SetParent(_parent);
            }
        }

        public void ClearAll()
        {
            if (_instances?.Length > 0)
            {
                foreach (var instance in _instances)
                {
                    Object.Destroy(instance);
                }
            }

            if (!_parent)
            {
                return;
            }
            
            Object.Destroy(_parent.gameObject);
        }

        public T GetNew()
        {
            var idx = _freeIdx.Pop();
            _instances[idx].transform.SetParent(null);
            _instances[idx].gameObject.SetActive(true);

            return _instances[idx];
        }

        public void Free(T obj)
        {
            _freeIdx.Push(obj.PoolID);
            _instances[obj.PoolID].gameObject.SetActive(false);
            _instances[obj.PoolID].transform.SetParent(_parent);
        }
    }

    public interface IPooled<T> where T : MonoBehaviour, IPooled<T>
    {
        int PoolID { get; set; }
        ObjectPooler<T> Pool { get; set; }
    } 
}