using FileConverter.Core.Interfaces;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace FileConverter.Core.Services
{
    public sealed class ConversionService : IFileConverter
    {
        public void ConvertImage(string input, string output, string format)
        {
            ImageFormat encoding;
            switch (format)
            {
                case "PNG": encoding = ImageFormat.Png; break;
                case "JPG": encoding = ImageFormat.Jpeg; break;
                case "BMP": encoding = ImageFormat.Bmp; break;
                default: throw new ArgumentException("Unsupported image output format.");
            }
            using (var image = Image.FromFile(input))
            {
                if (image.GetFrameCount(new FrameDimension(image.FrameDimensionsList[0])) > 1)
                    throw new InvalidDataException("Multi-frame images are not supported.");
                image.Save(output, encoding);
            }
        }
    }
}
