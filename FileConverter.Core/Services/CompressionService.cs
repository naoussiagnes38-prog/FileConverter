using System;
using System.IO;
using System.IO.Compression;
using FileConverter.Core.Interfaces;

namespace FileConverter.Core.Services
{
    public sealed class CompressionService : IFileCompressionService
    {
        public void CompressFile(string input, string output, CompressionLevel level)
        {
            CompressFile(input, output, level, null);
        }

        public void CompressFile(string input, string output, CompressionLevel level, Action<double> progress)
        {
            using (var source = File.OpenRead(input))
            using (var archive = ZipFile.Open(output, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(Path.GetFileName(input), level);
                DateTime lastWrite = File.GetLastWriteTime(input);
                if (lastWrite.Year >= 1980 && lastWrite.Year <= 2107)
                    entry.LastWriteTime = lastWrite;

                using (var destination = entry.Open())
                {
                    var buffer = new byte[81920];
                    long length = Math.Max(1, source.Length);
                    int count;
                    while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        destination.Write(buffer, 0, count);
                        progress?.Invoke((double)source.Position / length);
                    }
                }
            }
        }
    }
}
