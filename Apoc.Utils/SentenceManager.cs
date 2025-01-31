using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace Apoc.Utils
{
    public class SentenceManager
    {
        private static Parser _parser;

        private Dictionary<int, List<TokenizedSentence>> _prioritizedExampleSentences;

        public SentenceManager(Parser parser)
        {
            _parser = parser;

            _prioritizedExampleSentences = new Dictionary<int, List<TokenizedSentence>>();
        }

        public void BuildExampleSentenceCacheFile(List<string> inputSentences, string outputFilePath, List<string> otherIgnoredTokens = null)
        {
            var sb = new StringBuilder();

            foreach (var sentence in inputSentences)
            {
                var tokens = _parser
                    .Tokenize(sentence)
                    .Where(token =>
                            !_disallowedPartsOfSpeech.Contains(token.PartOfSpeech)
                            && token.RootText != "*"
                            && !_disallowedCharacters.Any(c => token.RootText.Contains(c))
                            && !otherIgnoredTokens.Contains(token.RootText)
                            && !(token.RootText.Length == 1 && _parser.IsSingleKana(token.RootText))
                    )
                    //.Select(token => token.RootText).ToList()
                    .ToList();

                //Skip any sentences that we don't get any valuable characters from
                if (!tokens.Any())
                    continue;

                sb.AppendLine(sentence);
                sb.AppendLine(string.Join(",", tokens.Select(token => token.RootText)));
                sb.AppendLine();

                //if (sentence == "「純夏」ぜっっったいやだ！食べて帰るっ！")
                //    Console.WriteLine("Test");

                Console.WriteLine($"Parsed {sentence}");
            }

            File.WriteAllText(outputFilePath, sb.ToString());
        }

        public void LoadExampleSentencesFromCacheFile(string filePath, int priority = 100)
        {
            string separator = Environment.NewLine;

            var fileEntries = File.ReadAllText(filePath).Split(new string[] { separator }, StringSplitOptions.RemoveEmptyEntries);

            List<TokenizedSentence> tokenizedSentences = null;
            if (_prioritizedExampleSentences.ContainsKey(priority))
            {
                tokenizedSentences = _prioritizedExampleSentences[priority];
            }
            else
            {
                tokenizedSentences = new List<TokenizedSentence>();
                _prioritizedExampleSentences.Add(priority, tokenizedSentences);
            }

            for (int i = 0; i < fileEntries.Length; i += 2)
            {
                string sentence = fileEntries[i];
                string tokens = fileEntries[i + 1];

                tokenizedSentences.Add(new TokenizedSentence()
                {
                    Sentence = sentence,
                    RootTexts = tokens.Split(',').ToList()
                });
            }
        }

        public string PickBestExampleSentence(ApocToken word, List<string> knownWords)
        {
            string bestSentence = string.Empty;
            int? fewestUnknownTokens = null;

            foreach (var exampleSentenceGroup in _prioritizedExampleSentences.OrderBy(kvp => kvp.Key).ToList())
            {
                foreach (var exampleSentence in exampleSentenceGroup.Value)
                {
                    if (!exampleSentence.RootTexts.Contains(word.RootText))
                        continue;

                    Console.WriteLine(exampleSentence.Sentence);
                    var unknownTokens = exampleSentence.RootTexts.Where(rootText => !knownWords.Contains(rootText) && rootText != word.RootText).ToList();
                    int numUnknownTokens = unknownTokens.Count();
                    Console.WriteLine($"{numUnknownTokens} unknown tokens: {string.Join(", ", unknownTokens)}");

                    if (numUnknownTokens == 0)
                        return exampleSentence.Sentence;

                    if (fewestUnknownTokens == null || numUnknownTokens < fewestUnknownTokens)
                    {
                        bestSentence = exampleSentence.Sentence;
                        fewestUnknownTokens = numUnknownTokens;
                    }
                }
            }

            return bestSentence;
        }

        public class TokenizedSentence
        {
            public string Sentence { get; set; }

            public List<string> RootTexts { get; set; }
        }

        private List<PartOfSpeech> _disallowedPartsOfSpeech = new List<PartOfSpeech>()
        {
            PartOfSpeech.Particle,
            PartOfSpeech.Symbol,
            PartOfSpeech.Prefix,
            PartOfSpeech.AuxilaryVerb,
            PartOfSpeech.Interjection,
            PartOfSpeech.Conjunction,
            PartOfSpeech.Filler,
            PartOfSpeech.Unknown
        };

        private List<char> _disallowedCharacters = new List<char>()
        {
            '０',
            '１',
            '２',
            '３',
            '４',
            '５',
            '６',
            '７',
            '８',
            '９',
            '０',
            '1',
            '2',
            '3',
            '4',
            '5',
            '6',
            '7',
            '8',
            '9',
            'a',
            'b',
            'c',
            'd',
            'e',
            'f',
            'g',
            'h',
            'i',
            'j',
            'k',
            'l',
            'm',
            'n',
            'o',
            'p',
            'q',
            'r',
            's',
            't',
            'u',
            'v',
            'w',
            'x',
            'y',
            'z',
            'A',
            'B',
            'C',
            'D',
            'E',
            'F',
            'G',
            'H',
            'I',
            'J',
            'K',
            'L',
            'M',
            'N',
            'O',
            'P',
            'Q',
            'R',
            'S',
            'T',
            'U',
            'V',
            'W',
            'X',
            'Y',
            'Z'
        };
    }
}
