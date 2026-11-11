using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using FileConverter.Core.Enums;

namespace FileConverter.Core.Models
{
    public class FileItem : INotifyPropertyChanged
    {
        public Guid Id { get; set; }

        public string FileName { get; set; }

        public string FilePath { get; set; }

        public string Extension { get; set; }

        public long SizeInBytes { get; set; }

        public string OutputExtension { get; set; }

        private ConversionStatus status;

        public ConversionStatus Status
        {
            get { return status; }
            set
            {
                if (status == value)
                    return;
                status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string FormattedSize
        {
            get
            {
                double size = SizeInBytes;

                string[] units =
                {
                    "B", "KB", "MB", "GB", "TB"
                };

                int unitIndex = 0;

                while (size >= 1024 &&
                       unitIndex < units.Length - 1)
                {
                    size /= 1024;
                    unitIndex++;
                }

                return string.Format(
                    "{0:0.##} {1}",
                    size,
                    units[unitIndex]
                );
            }
        }

        public FileItem()
        {
            Id = Guid.NewGuid();
            Status = ConversionStatus.Pending;
            OutputExtension = "";
        }

        public FileItem(string filePath)
        {
            FileInfo file = new FileInfo(filePath);

            Id = Guid.NewGuid();
            FileName = file.Name;
            FilePath = file.FullName;
            Extension = file.Extension;
            SizeInBytes = file.Length;

            OutputExtension = "";
            Status = ConversionStatus.Pending;
        }
    }
}
