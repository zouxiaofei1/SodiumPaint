using System.Collections.Concurrent;
using System.Windows.Media.Imaging;

namespace TabPaint
{
    // 简单的 LRU 缓存实现
    public class ThumbnailCache
    {
        private readonly int _capacity;// 缓存容量
        private readonly ConcurrentDictionary<string, BitmapSource> _cache;
        private readonly ConcurrentQueue<string> _lruQueue;

        public ThumbnailCache(int capacity = 200)
        {
            _capacity = capacity;
            _cache = new ConcurrentDictionary<string, BitmapSource>();
            _lruQueue = new ConcurrentQueue<string>();
        }

        public void Add(string key, BitmapSource bitmap)
        {
            if (string.IsNullOrEmpty(key) || bitmap == null) return;
            if (_cache.ContainsKey(key))
            {
                _cache[key] = bitmap;
                return;
            }
            if (_cache.Count >= _capacity)
            {
                string oldKey; // 尝试移除最老的
                if (_lruQueue.TryDequeue(out oldKey))  _cache.TryRemove(oldKey, out _);
            }
            _cache[key] = bitmap;
            _lruQueue.Enqueue(key);
        }

        public BitmapSource Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_cache.TryGetValue(key, out var bitmap))return bitmap;
            return null;
        }

        public void Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _cache.TryRemove(key, out _);
        }

        public void Clear()
        {
            _cache.Clear();
            while (_lruQueue.TryDequeue(out _)) { }
        }
    }
}
