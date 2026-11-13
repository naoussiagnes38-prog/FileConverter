using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace FileConverter.Core.Services
{
    public sealed class LanguageDetectionService : IDisposable
    {
        private readonly HttpClient client;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public LanguageDetectionService(Uri endpoint)
        {
            if (endpoint == null || !endpoint.IsLoopback ||
                (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("Configure a local HTTP(S) LibreTranslate endpoint.");

            client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
            client.BaseAddress = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(3);
        }

        public async Task<bool> IsAvailableAsync()
        {
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                HttpResponseMessage response;
                try { response = await client.GetAsync("languages", timeout.Token); }
                catch (HttpRequestException) { return false; }
                catch (OperationCanceledException) { return false; }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("LibreTranslate /languages returned HTTP " + (int)response.StatusCode + ".");

                    var languages = serializer.Deserialize<TranslationLanguage[]>(await response.Content.ReadAsStringAsync());
                    if (languages == null ||
                        !languages.Any(language => language.code == "en" && language.targets != null && language.targets.Contains("fr")) ||
                        !languages.Any(language => language.code == "fr" && language.targets != null && language.targets.Contains("en")))
                        throw new InvalidOperationException("LibreTranslate needs both English and French translation models.");
                    return true;
                }
            }
        }

        public async Task<string> TranslateAsync(string text, string source, string target, Action<double> reportProgress = null)
        {
            const int maxBatchItems = 12;
            const int maxBatchCharacters = 6000;
            var output = new StringBuilder();
            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            var batch = new List<TextFragment>();
            int batchCharacters = 0;

            foreach (var fragment in SplitText(normalized))
            {
                if (batch.Count == maxBatchItems || batchCharacters + fragment.Text.Length > maxBatchCharacters)
                {
                    await AppendBatchAsync(batch, source, target, output);
                    reportProgress?.Invoke((double)batch[batch.Count - 1].EndOffset / Math.Max(1, normalized.Length));
                    batch.Clear();
                    batchCharacters = 0;
                }

                batch.Add(fragment);
                batchCharacters += fragment.Text.Length;
            }

            await AppendBatchAsync(batch, source, target, output);
            reportProgress?.Invoke(1);
            return output.ToString();
        }

        private static IEnumerable<TextFragment> SplitText(string text)
        {
            int lineStart = 0;
            foreach (string line in text.Split('\n'))
            {
                int offset = 0;
                bool hasNewline = lineStart + line.Length < text.Length;
                do
                {
                    int count = Math.Min(2000, line.Length - offset);
                    if (offset + count < line.Length)
                    {
                        int space = line.LastIndexOf(' ', offset + count - 1, count);
                        if (space > offset)
                            count = space - offset + 1;
                        else if (char.IsHighSurrogate(line[offset + count - 1]))
                            count--;
                    }

                    string fragment = line.Substring(offset, count);
                    offset += count;
                    bool endsLine = offset == line.Length;
                    bool needsTranslation = fragment.Any(char.IsLetter);
                    string leading = needsTranslation ? fragment.Substring(0, fragment.Length - fragment.TrimStart().Length) : "";
                    string trailing = needsTranslation ? fragment.Substring(fragment.TrimEnd().Length) : "";
                    yield return new TextFragment
                    {
                        Text = needsTranslation ? fragment.Trim() : fragment,
                        NeedsTranslation = needsTranslation,
                        Prefix = leading,
                        Suffix = trailing + (endsLine && hasNewline ? Environment.NewLine : ""),
                        EndOffset = lineStart + offset
                    };
                } while (offset < line.Length);

                lineStart += line.Length + 1;
            }
        }

        private async Task AppendBatchAsync(List<TextFragment> batch, string source, string target, StringBuilder output)
        {
            string[] inputs = batch.Where(fragment => fragment.NeedsTranslation).Select(fragment => fragment.Text)
                .Distinct(StringComparer.Ordinal).ToArray();
            var translations = new Dictionary<string, string>(StringComparer.Ordinal);
            if (inputs.Length > 0)
            {
                string payload = serializer.Serialize(new { q = inputs, source, target, format = "text" });
                using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
                using (var response = await client.PostAsync("translate", content))
                {
                    string body = await ReadResponseAsync(response);
                    var result = serializer.Deserialize<TranslationBatch>(body);
                    if (result == null || result.translatedText == null || result.translatedText.Length != inputs.Length ||
                        result.translatedText.Any(string.IsNullOrWhiteSpace))
                        throw new InvalidDataException("LibreTranslate returned an incomplete translation batch.");

                    for (int index = 0; index < inputs.Length; index++)
                        translations.Add(inputs[index], result.translatedText[index]);
                }
            }

            foreach (var fragment in batch)
            {
                output.Append(fragment.Prefix);
                output.Append(fragment.NeedsTranslation ? translations[fragment.Text] : fragment.Text);
                output.Append(fragment.Suffix);
            }
        }

        private async Task<string> ReadResponseAsync(HttpResponseMessage response)
        {
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                string detail = null;
                try { detail = serializer.Deserialize<TranslationError>(body)?.error; }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
                throw new InvalidOperationException("LibreTranslate returned HTTP " + (int)response.StatusCode +
                    (string.IsNullOrWhiteSpace(detail) ? "." : ": " + detail));
            }
            return body;
        }

        public void Dispose()
        {
            client.Dispose();
        }

        public sealed class TranslationLanguage
        {
            public string code { get; set; }
            public string[] targets { get; set; }
        }

        public sealed class TranslationError
        {
            public string error { get; set; }
        }

        public sealed class TranslationBatch
        {
            public string[] translatedText { get; set; }
        }

        private sealed class TextFragment
        {
            public string Text { get; set; }
            public string Prefix { get; set; }
            public string Suffix { get; set; }
            public bool NeedsTranslation { get; set; }
            public int EndOffset { get; set; }
        }
    }
}
