using System.Collections.Concurrent;
using System.Windows.Media.Imaging;

namespace TabPaint
{
    // 简单的 LRU 缓存实现
    public class ThumbnailCache
    {
        // 缓存容量：比如最多存 200 张缩略图 (100px宽的图大概几KB到几十KB，200张没压力)
        private readonly int _capacity;
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

            // 如果已经有了，更新一下（简单起见，这里直接覆盖）
            if (_cache.ContainsKey(key))
            {
                _cache[key] = bitmap;
                return;
            }

            // 检查容量
            if (_cache.Count >= _capacity)
            {
                string oldKey;
                // 尝试移除最老的
                if (_lruQueue.TryDequeue(out oldKey))
                {
                    _cache.TryRemove(oldKey, out _);
                }
            }

            // 加入新数据
            _cache[key] = bitmap;
            _lruQueue.Enqueue(key);
        }

        public BitmapSource Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (_cache.TryGetValue(key, out var bitmap))
            {
                return bitmap;
            }
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
