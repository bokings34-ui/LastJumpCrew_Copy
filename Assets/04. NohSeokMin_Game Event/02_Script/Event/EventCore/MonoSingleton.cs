using UnityEngine;

namespace SM
{
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;

        public static bool HasInstance
        {
            get { return _instance != null; }
        }

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<T>();

                    if (_instance == null)
                    {
                        GameObject obj = new GameObject(typeof(T).Name);
                        _instance = obj.AddComponent<T>();
                    }
                }
                return _instance;
            }
        }

        public static T Peek()
        {
            return _instance;
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this as T;
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}