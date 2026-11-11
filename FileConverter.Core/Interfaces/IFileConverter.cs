namespace FileConverter.Core.Interfaces
{
    public interface IFileConverter
    {
        void ConvertImage(string input, string output, string format);
    }
}
