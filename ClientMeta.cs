using System.Collections.Concurrent;

namespace IW4MAdmin
{
    class ClientMeta
    {
        private readonly ConcurrentDictionary<string, object> _metaMap;

        public ClientMeta()
        {
            _metaMap = new ConcurrentDictionary<string, object>();
        }

        public void Set<T>(string key, T value)
        {
            if (!_metaMap.ContainsKey(key))
            {
                _metaMap.TryAdd(key, value);
            }

            else
                _metaMap[key] = value;
        }

        public T Get<T>(string key)
        {
            return (T)_metaMap[key];
        }
    }
}