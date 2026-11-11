using FileConverter.Core.Interfaces;
using System.IO.Compression;

namespace FileConverter.Core.Services
{
    public sealed class CompressionService : IFileCompressionService
    {
        public void CompressFile(string input, string output, CompressionLevel level)
        {
            using (var archive = ZipFile.Open(output, ZipArchiveMode.Create))
                archive.CreateEntryFromFile(input, System.IO.Path.GetFileName(input), level);
        }
    }
}
