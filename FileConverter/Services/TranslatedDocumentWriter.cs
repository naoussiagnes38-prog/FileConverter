using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace FileConverter.Services
{
    internal static class TranslatedDocumentWriter
    {
        public static void Write(string path, string text, string format)
        {
            switch (format)
            {
                case "txt":
                    File.WriteAllText(path, text, Encoding.UTF8);
                    break;
                case "docx":
                    WriteWord(path, text);
                    break;
                case "pdf":
                    WritePdf(path, text, Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"));
                    break;
                default:
                    throw new ArgumentException("Unsupported translation output format.", nameof(format));
            }
        }

        private static string[] Lines(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        private static void WriteWord(string path, string text)
        {
            XNamespace types = "http://schemas.openxmlformats.org/package/2006/content-types";
            XNamespace relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
            XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            using (var package = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                WritePart(package, "[Content_Types].xml", new XElement(types + "Types",
                    new XElement(types + "Default", new XAttribute("Extension", "rels"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(types + "Override", new XAttribute("PartName", "/word/document.xml"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"))));

                WritePart(package, "_rels/.rels", new XElement(relationships + "Relationships",
                    new XElement(relationships + "Relationship", new XAttribute("Id", "rId1"),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "word/document.xml"))));

                WritePart(package, "word/document.xml", new XElement(word + "document",
                    new XElement(word + "body", Lines(text).Select(line =>
                        new XElement(word + "p", new XElement(word + "r",
                            new XElement(word + "t", new XAttribute(XNamespace.Xml + "space", "preserve"),
                                line.Replace("\t", "    "))))))));
            }
        }

        private static void WritePart(ZipArchive archive, string name, XElement xml)
        {
            using (var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open())
                xml.Save(stream);
        }

        internal static void WritePdf(string path, string text, string fontPath)
        {
            const double width = 595;
            const double height = 842;
            const double margin = 48;
            const double lineHeight = 15;
            const decimal fontSize = 11;

            if (!File.Exists(fontPath))
                throw new FileNotFoundException("PDF export requires the Arial TrueType font.", fontPath);

            var builder = new PdfDocumentBuilder();
            var font = builder.AddTrueTypeFont(File.ReadAllBytes(fontPath));
            var page = builder.AddPage(width, height);
            var widths = new Dictionary<string, double>(StringComparer.Ordinal);
            double baseline = height - margin - (double)fontSize;
            foreach (string line in Lines(text))
            {
                foreach (string segment in Wrap(line.Replace("\t", "    "), width - margin * 2, element =>
                {
                    double measured;
                    if (!widths.TryGetValue(element, out measured))
                    {
                        measured = page.MeasureText(element, fontSize, new PdfPoint(0, 0), font)
                            .Last().EndBaseLine.X;
                        widths.Add(element, measured);
                    }
                    return measured;
                }))
                {
                    if (baseline < margin)
                    {
                        page = builder.AddPage(width, height);
                        baseline = height - margin - (double)fontSize;
                    }

                    if (!string.IsNullOrWhiteSpace(segment))
                        page.AddText(segment, fontSize, new PdfPoint(margin, baseline), font);
                    baseline -= lineHeight;
                }
            }

            File.WriteAllBytes(path, builder.Build());
        }

        private static IEnumerable<string> Wrap(string line, double maxWidth, Func<string, double> measure)
        {
            int[] elements = StringInfo.ParseCombiningCharacters(line);
            int start = 0;
            do
            {
                int end = start;
                int lastSpace = -1;
                double width = 0;
                while (end < elements.Length)
                {
                    int next = end + 1 < elements.Length ? elements[end + 1] : line.Length;
                    string element = line.Substring(elements[end], next - elements[end]);
                    width += measure(element);
                    if (end > start && width > maxWidth)
                        break;
                    if (string.IsNullOrWhiteSpace(element))
                        lastSpace = end;
                    end++;
                }

                if (end < elements.Length && lastSpace > start)
                    end = lastSpace + 1;

                int first = start < elements.Length ? elements[start] : line.Length;
                int limit = end < elements.Length ? elements[end] : line.Length;
                yield return line.Substring(first, limit - first).TrimEnd();
                start = end;
            } while (start < elements.Length);
        }
    }
}
