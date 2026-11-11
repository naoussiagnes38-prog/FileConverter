using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FileConverter.Core.Services
{
    public static class DocumentLanguageDetector
    {
        private const int ProfileSize = 300;
        private static readonly Lazy<Dictionary<string, Dictionary<string, int>>> Profiles =
            new Lazy<Dictionary<string, Dictionary<string, int>>>(LoadProfiles);

        public static string Detect(string text)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            int letterCount = 0;
            int lengthLimit = Math.Min(text.Length, 16000);
            if (lengthLimit > 0 && char.IsHighSurrogate(text[lengthLimit - 1]))
                lengthLimit--;

            string sample = text.Substring(0, lengthLimit).Normalize(NormalizationForm.FormC);
            var words = Regex.Matches(sample.ToLowerInvariant(), @"\p{L}+");
            if (words.Count < 2)
                return null;

            foreach (Match match in words)
            {
                letterCount += match.Length;
                string word = "_" + match.Value + "_";
                for (int start = 0; start < word.Length; start++)
                {
                    for (int length = 2; length <= 5 && start + length <= word.Length; length++)
                    {
                        string gram = word.Substring(start, length);
                        int count;
                        counts.TryGetValue(gram, out count);
                        counts[gram] = count + 1;
                    }
                }
            }

            if (letterCount < 6)
                return null;

            var sampleRanks = Rank(counts);
            var best = Profiles.Value.OrderBy(profile => sampleRanks.Sum(gram =>
            {
                int rank;
                return profile.Value.TryGetValue(gram.Key, out rank)
                    ? Math.Abs(rank - gram.Value) : ProfileSize;
            })).First();

            if (best.Key == "eng")
                return "en";
            if (best.Key == "fra")
                return "fr";
            return best.Key;
        }

        private static Dictionary<string, int> Rank(Dictionary<string, int> counts)
        {
            return counts.OrderByDescending(gram => gram.Value)
                .ThenBy(gram => gram.Key, StringComparer.Ordinal).Take(ProfileSize)
                .Select((gram, rank) => new { gram.Key, Rank = rank })
                .ToDictionary(gram => gram.Key, gram => gram.Rank, StringComparer.Ordinal);
        }

        private static Dictionary<string, Dictionary<string, int>> LoadProfiles()
        {
            using (var input = typeof(DocumentLanguageDetector).Assembly.GetManifestResourceStream(
                "FileConverter.Core.Core14.profile.xml"))
            {
                if (input == null)
                    throw new InvalidDataException("The document language profiles are missing.");

                return XDocument.Load(input).Root.Element("LanguageModels").Elements("LanguageModel")
                    .ToDictionary(model => (string)model.Element("Language").Attribute("ISO639-3"),
                        model => Rank(model.Element("ngrams").Elements("ngram")
                            .Where(gram => ((string)gram.Attribute("text")).Length >= 2)
                            .GroupBy(gram => ((string)gram.Attribute("text")).ToLowerInvariant())
                            .ToDictionary(group => group.Key, group => group.Sum(gram => (int)gram.Attribute("count")))));
            }
        }
    }
}
