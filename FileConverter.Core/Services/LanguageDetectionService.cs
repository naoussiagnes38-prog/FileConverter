using System;
using System.Collections.Generic;
using System.IO;
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

        public async Task<string> TranslateAsync(string text, string source, string target)
        {
            var output = new StringBuilder();
            foreach (string line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                int offset = 0;
                while (offset < line.Length)
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
                    string chunk = line.Substring(offset, count);
                    if (!string.IsNullOrWhiteSpace(chunk))
                    {
                        var result = serializer.Deserialize<Translation>(await PostAsync("translate",
                            new Dictionary<string, string> { { "q", chunk }, { "source", source }, { "target", target } }));
                        if (string.IsNullOrWhiteSpace(result.translatedText))
                            throw new InvalidDataException("The translation engine returned empty text.");
                        output.Append(result.translatedText);
                        if (offset + count < line.Length)
                            output.Append(' ');
                    }
                    offset += count;
                }
                output.AppendLine();
            }
            return output.ToString();
        }

        private async Task<string> PostAsync(string route, Dictionary<string, string> fields)
        {
            using (var content = new FormUrlEncodedContent(fields))
            using (var response = await client.PostAsync(route, content))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }

        public void Dispose()
        {
            client.Dispose();
        }

        public sealed class Translation
        {
            public string translatedText { get; set; }
        }
    }
}
