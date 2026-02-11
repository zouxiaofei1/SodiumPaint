
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
//
//TabPaint主程序
//

namespace TabPaint
{
	public class CompressedBuffer
	{
		private byte[] _raw;           // 未压缩（热数据）
		private byte[] _compressed;    // 压缩后（冷数据）
		private volatile bool _isCompressed;
		private readonly object _lock = new object();
		public int OriginalLength { get; }

		public CompressedBuffer(byte[] data)
		{
			_raw = data;
			_isCompressed = false;
			OriginalLength = data.Length;
		}
		public void Compress()
		{
			lock (_lock)
			{
				if (_isCompressed || _raw == null) return;
				using var ms = new MemoryStream();
				using (var brotli = new BrotliStream(ms, CompressionLevel.Fastest))
				{
					brotli.Write(_raw, 0, _raw.Length);
				}
				_compressed = ms.ToArray();
				_raw = null;  // 释放原始数据
				_isCompressed = true;
			}
		}
		public byte[] GetData()
		{
			lock (_lock)
			{
				if (!_isCompressed) return _raw;

				byte[] result = new byte[OriginalLength];
				using var ms = new MemoryStream(_compressed);
				using var brotli = new BrotliStream(ms, CompressionMode.Decompress);
				int totalRead = 0;
				while (totalRead < OriginalLength)
				{
					int read = brotli.Read(result, totalRead, OriginalLength - totalRead);
					if (read == 0) break;
					totalRead += read;
				}
				return result;
			}
		}
		public void Decompress()
		{
			if (!_isCompressed) return;
			_raw = GetData();
			_compressed = null;
			_isCompressed = false;
		}
		public long ActualMemorySize
		{
			get
			{
				lock (_lock)
				{
					return _isCompressed
						? (_compressed?.Length ?? 0)
						: (_raw?.Length ?? 0);
				}
			}
		}

		public double CompressionRatio
		{
			get
			{
				lock (_lock)
				{
					return _isCompressed && _compressed != null
						? (double)OriginalLength / _compressed.Length
						: 1.0;
				}
			}
		}
	}
	public static class ListStackExtensions
	{
		public static void Push<T>(this List<T> list, T item) { list.Add(item); }
		public static T Pop<T>(this List<T> list)
		{
			if (list.Count == 0) throw new InvalidOperationException("Stack is empty");
			T last = list[list.Count - 1];
			list.RemoveAt(list.Count - 1);
			return last;
		}
		public static T Peek<T>(this List<T> list)
		{
			if (list.Count == 0) throw new InvalidOperationException("Stack is empty");
			return list[list.Count - 1];
		}
	}
}