using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace TabPaint
{
    public static class SingleInstance
    {
        private const string UniqueId = "TabPaint_App_Mutex_UUID_91823091";
        private static Mutex _mutex;
        private static CancellationTokenSource _pipeCts; // 用于停止管道监听

        // 检查是否是第一个实例
        public static bool IsFirstInstance()
        {
            _mutex = new Mutex(true, UniqueId, out bool createdNew);
            return createdNew;
        }

        public static void Release()
        {
            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch { /* 忽略并非拥有者的异常 */ }

                _mutex.Close();
                _mutex = null;
            }
            _pipeCts?.Cancel();
        }
        public static void SendArgsToFirstInstance(string[] args)
        {
            if (args == null || args.Length == 0) return;
            try    // 连接超时设短一点，如果连不上说明旧实例可能正在关闭中
            {
                using (var client = new NamedPipeClientStream(".", UniqueId, PipeDirection.Out))
                {
                    client.Connect(300); 
                    using (var writer = new StreamWriter(client))
                    {
                        writer.WriteLine(args[0]);
                        writer.Flush();
                    }
                }
            }
            catch (Exception) { }
        }
        public static void ListenForArgs(Action<string> onFileReceived)
        {
            _pipeCts = new CancellationTokenSource();
            var token = _pipeCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(UniqueId, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                        {
                            // 使用带Token的等待
                            await server.WaitForConnectionAsync(token);

                            using (var reader = new StreamReader(server))
                            {
                                string filePath = await reader.ReadLineAsync();
                                if (!string.IsNullOrEmpty(filePath))  onFileReceived?.Invoke(filePath);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break; // 正常退出监听
                    }
                    catch
                    {
                        await Task.Delay(1000, token);
                    }
                }
            });
        }
    }
}
