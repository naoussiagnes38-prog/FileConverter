using System.IO.Compression;

namespace FileConverter.Core.Interfaces
{
    public interface IFileCompressionService
    {
        void CompressFile(string input, string output, CompressionLevel level);
    }
}
