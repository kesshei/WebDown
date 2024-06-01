using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace down
{
    internal class Program
    {
        static HttpClient client = new HttpClient();
        static object lockObj = new object();
        static async Task Main(string[] args)
        {
            var url = "http://localhost:5034/home/down";
            Stopwatch stopwatch = Stopwatch.StartNew();
            await DownUrl(url);
            stopwatch.Stop();
            Console.WriteLine($"单线程 直接下载耗时:{stopwatch.Elapsed.TotalSeconds}");
            stopwatch.Restart();
            (long? length, string filename) = await GetFileLengthandName(url);
            if (length.HasValue)
            {
                var number = 10;
                //获取分片大小，默认1M 缓存区，太小又太慢 设置成5M。
                var list = BytesRange.GetRanges(length.Value, 5 * 1024 * 1024);
                Console.WriteLine($"分片数:{list.Count} 每片大小:5MB 并发数:{number}");
                var path = Path.Combine(AppContext.BaseDirectory, filename);
                using (var write = File.OpenWrite(path))
                {
                    write.SetLength(length.Value);
                    await write.FlushAsync();
                    // 并行下载，每秒默认10并发
                    Parallel.ForEach(list, new ParallelOptions() { MaxDegreeOfParallelism = number }, range =>
                    {
                        //Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} {range}");
                        var bytes = GetBytesAsync(url, range).Result;
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
            stopwatch.Stop();
            Console.WriteLine($"并发下载 耗时:{stopwatch.Elapsed.TotalSeconds}秒");


            Console.ReadLine();
        }
        public static async Task<(long? length, string filename)> GetFileLengthandName(string url)
        {
            var result = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseHeadersRead);
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
        public static async Task DownUrl(string url)
        {
            var result = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseHeadersRead);
            if (result.IsSuccessStatusCode)
            {
                var filename = "temp.zip";
                using (var contentStream = await result.Content.ReadAsStreamAsync())
                {
                    using (var file = File.OpenWrite(filename))
                    {
                        await contentStream.CopyToAsync(file);
                    }
                }
            }
        }
    }
    public struct BytesRange
    {
        public long Start { get; set; }
        public long End { get; set; }
        public long Length { get { return End - Start + 1; } }
        public override string ToString()
        {
            return $"{Start} {End} : {Length}";
        }
        public static List<BytesRange> GetRanges(long length, long BufferSize = 1 * 1024 * 1024)
        {
            List<BytesRange> list = new List<BytesRange>();
            long count = length / BufferSize;
            long Lost = length - BufferSize * count;

            if (Lost > 0)
            {
                list.Add(new BytesRange() { Start = count * BufferSize, End = count * BufferSize + Lost - 1 });
            }

            if (count > 0)
            {
                for (long i = 0; i < count; i++)
                {
                    list.Add(new BytesRange() { Start = i * BufferSize, End = (i + 1) * BufferSize - 1 });
                }
            }
            else
            {
                list.Add(new BytesRange() { Start = 0, End = Lost - 1 });
            }
            list.OrderByDescending(t => t.Start);
            return list;
        }
    }
}
