using System;
using System.Net.Http;

namespace down
{
    internal class Program
    {
        static HttpClient client = new HttpClient();
        static object lockObj = new object();
        static async Task Main(string[] args)
        {
            var url = "http://localhost:5034/home/down";
            (long? length, string filename) = await GetFileLengthandName(url);
            if (length.HasValue)
            {
                //获取分片大小，默认1M 缓存区，太小又太慢。
                var list = BytesRange.GetRanges(length.Value);
                var path = Path.Combine(AppContext.BaseDirectory, filename);
                using (var write = File.OpenWrite(path))
                {
                    write.SetLength(length.Value);
                    await write.FlushAsync();
                    // 并行下载，每秒默认10并发
                    Parallel.ForEach(list, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, range =>
                    {
                        var bytes = GetBytesAsync(url, range).Result;
                        Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} {range}");
                        lock (lockObj)
                        {
                            write.Seek(range.Start, SeekOrigin.Begin);
                            write.Write(bytes);
                        }
                    });
                }
                Console.WriteLine("下载完毕，请验证!");
            }
            else
            {
                Console.WriteLine("没有获取到下载文件的信息!");
            }
            Console.ReadLine();
        }
        public static async Task<(long? length, string filename)> GetFileLengthandName(string url)
        {
            var result = await client.GetAsync(url);
            if (result.IsSuccessStatusCode)
            {
                return (result.Content.Headers.ContentLength, result.Content.Headers.ContentDisposition.FileName);
            }
            return (null, null);
        }
        public static async Task<byte[]> GetBytesAsync(string url, BytesRange range)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Range", $"bytes={range.Start}-{range.End}");
            using (HttpResponseMessage response = await client.SendAsync(request))
            {
                using (Stream stream = await response.Content.ReadAsStreamAsync())
                {
                    if (range.Length != stream.Length)
                    {
                        throw new Exception("数据不匹配!");
                    }
                    byte[] bytes = new byte[stream.Length];
                    stream.Read(bytes, 0, bytes.Length);
                    return bytes;
                }
            }
        }
    }
    public struct BytesRange
    {
        public int Start { get; set; }
        public int End { get; set; }
        public int Length { get { return End - Start + 1; } }
        public override string ToString()
        {
            return $"{Start} {End} : {Length}";
        }
        public static List<BytesRange> GetRanges(long length, int BufferSize = 1 * 1024 * 1024)
        {
            List<BytesRange> list = new List<BytesRange>();
            int count = (int)(length / BufferSize);
            int Lost = (int)(length - BufferSize * count);
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    list.Add(new BytesRange() { Start = i * BufferSize, End = (i + 1) * BufferSize - 1 });
                }
                if (Lost > 0)
                {
                    list.Add(new BytesRange() { Start = (int)count * BufferSize, End = (int)count * BufferSize + Lost - 1 });
                }
            }
            else
            {
                list.Add(new BytesRange() { Start = 0, End = Lost - 1 });
            }
            return list;
        }
    }
}
