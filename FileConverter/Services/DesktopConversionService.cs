using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FileConverter.Core.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace FileConverter.Services
{
    internal enum ConversionMethod
    {
        WordToPdf, ImageToPdf, ExcelToPdf, PdfToWord, PdfToImages, MergePdf, Jpg, Png, Bmp
    }

    internal static class DesktopConversionService
    {
        public static void Convert(string input, string output, ConversionMethod method, int quality, string renderer)
        {
            switch (method)
            {
                case ConversionMethod.WordToPdf: ConvertOffice(input, output, false, false); break;
                case ConversionMethod.ExcelToPdf: ConvertOffice(input, output, true, false); break;
                case ConversionMethod.PdfToWord: ConvertOffice(input, output, false, true); break;
                case ConversionMethod.ImageToPdf: ImageToPdf(input, output, quality); break;
                case ConversionMethod.PdfToImages: RenderPdf(input, output, quality, renderer); break;
                case ConversionMethod.Jpg:
                    using (var source = Image.FromFile(input))
                    {
                        EnsureSingleFrame(source);
                        using (var stream = File.Create(output))
                            SaveJpeg(source, stream, quality);
                    }
                    break;
                case ConversionMethod.Png: new ConversionService().ConvertImage(input, output, "PNG"); break;
                case ConversionMethod.Bmp: new ConversionService().ConvertImage(input, output, "BMP"); break;
                default: throw new ArgumentException("Unsupported conversion.");
            }
        }

        public static ConversionMethod? PdfInputMethod(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".doc":
                case ".docx": return ConversionMethod.WordToPdf;
                case ".xls":
                case ".xlsx": return ConversionMethod.ExcelToPdf;
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".gif":
                case ".tif":
                case ".tiff": return ConversionMethod.ImageToPdf;
                default: return null;
            }
        }

        public static void MergePdf(IEnumerable<string> inputs, string output)
        {
            var builder = new PdfDocumentBuilder();
            foreach (string path in inputs)
            {
                using (var pdf = PdfDocument.Open(path))
                {
                    for (int page = 1; page <= pdf.NumberOfPages; page++)
                        builder.AddPage(pdf, page);
                }
            }
            File.WriteAllBytes(output, builder.Build());
        }

        private static void ImageToPdf(string input, string output, int quality)
        {
            using (var image = Image.FromFile(input))
            {
                EnsureSingleFrame(image);
                double scale = Math.Min(595.0 / image.Width, 842.0 / image.Height);
                var builder = new PdfDocumentBuilder();
                var page = builder.AddPage(image.Width * scale, image.Height * scale);
                using (var jpeg = new MemoryStream())
                {
                    SaveJpeg(image, jpeg, quality);
                    page.AddJpeg(jpeg.ToArray(), new PdfRectangle(0, 0, image.Width * scale, image.Height * scale));
                }
                File.WriteAllBytes(output, builder.Build());
            }
        }

        private static void EnsureSingleFrame(Image image)
        {
            if (image.FrameDimensionsList.Any(id => image.GetFrameCount(new FrameDimension(id)) > 1))
                throw new InvalidDataException("Select a single-frame image.");
        }

        private static void SaveJpeg(Image image, Stream output, int quality)
        {
            using (var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var options = new EncoderParameters(1))
            {
                graphics.Clear(Color.White);
                graphics.DrawImage(image, 0, 0, image.Width, image.Height);
                options.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
                var codec = ImageCodecInfo.GetImageEncoders().First(item => item.FormatID == ImageFormat.Jpeg.Guid);
                bitmap.Save(output, codec, options);
            }
        }

        private static void RenderPdf(string input, string output, int quality, string renderer)
        {
            string executable = renderer;
            if (string.IsNullOrWhiteSpace(executable))
            {
                string bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdftoppm.exe");
                executable = File.Exists(bundled) ? bundled : "pdftoppm.exe";
            }

            var start = new ProcessStartInfo(executable,
                "-r 150 -jpeg -jpegopt quality=" + quality.ToString(CultureInfo.InvariantCulture) +
                " \"" + input + "\" \"" + Path.Combine(output, "page") + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            Process process;
            try { process = Process.Start(start); }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException("Install Poppler or set PdfToPpmPath to pdftoppm.exe.", ex);
            }

            using (process)
            {
                var errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(300000))
                {
                    process.Kill();
                    process.WaitForExit();
                    throw new IOException("PDF rendering timed out.");
                }

                if (process.ExitCode != 0 || !Directory.EnumerateFiles(output, "*.jpg").Any())
                    throw new IOException("PDF rendering failed. " + errors.GetAwaiter().GetResult().Trim());
            }
        }

        private static void ConvertOffice(string input, string output, bool excel, bool toWord)
        {
            string progId = excel ? "Excel.Application" : "Word.Application";
            Type type = Type.GetTypeFromProgID(progId);
            if (type == null)
                throw new InvalidOperationException("Install desktop Microsoft " + (excel ? "Excel." : "Word (2013 or later)."));

            dynamic application = Activator.CreateInstance(type);
            dynamic documents = null;
            dynamic document = null;
            try
            {
                application.AutomationSecurity = 3; // Disable macros.
                if (excel)
                {
                    application.DisplayAlerts = false;
                    application.EnableEvents = false;
                    documents = application.Workbooks;
                    document = documents.Open(input, UpdateLinks: 0, ReadOnly: true, Password: "", AddToMru: false);
                    document.ExportAsFixedFormat(0, output);
                }
                else
                {
                    application.DisplayAlerts = 0;
                    application.Options.UpdateLinksAtOpen = false;
                    if (toWord && Version.Parse((string)application.Version).Major < 15)
                        throw new InvalidOperationException("PDF to Word requires Word 2013 or later.");
                    documents = application.Documents;
                    document = documents.Open(input, ConfirmConversions: false, ReadOnly: true,
                        AddToRecentFiles: false, PasswordDocument: "", Visible: false, NoEncodingDialog: true);
                    if (toWord)
                        document.SaveAs2(output, 16, AddToRecentFiles: false);
                    else
                        document.ExportAsFixedFormat(output, 17);
                }
            }
            finally
            {
                try
                {
                    if (document != null)
                        document.Close(false);
                }
                finally
                {
                    try { application.Quit(); }
                    finally
                    {
                        if (document != null)
                            Marshal.FinalReleaseComObject(document);
                        if (documents != null)
                            Marshal.FinalReleaseComObject(documents);
                        Marshal.FinalReleaseComObject(application);
                    }
                }
            }
        }
    }
}
