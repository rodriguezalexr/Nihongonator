using MeCab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Apoc.Utils
{
    public class ApocToken
    {
        /// <summary>
        /// The text raw from the input string
        /// </summary>
        public string RawText { get; set; }

        /// <summary>
        /// The base of the text, ex: さ -> する
        /// </summary>
        public string RootText { get; set; }

        public string FullRawText
        {
            get
            {
                return $"{RawText}{string.Join("", SubTokens.Select(subToken => subToken.RawText))}";
            }
        }

        public PartOfSpeech PartOfSpeech { get; set; }

        public string Subtype { get; set; }

        public string SourceSentence { get; set; }

        public string Features { get; set; }

        public ApocToken Previous { get; set; }

        public ApocToken Next { get; set; }

        public List<ApocToken> SubTokens { get; set; }

        public MeCabNode MeCabNode { get; set; }

        public ApocToken ()
        {
            SubTokens = new List<ApocToken>();
        }

        public override string ToString()
        {
            if (RawText != RootText || SubTokens.Any())
                return $"{RootText} - {FullRawText} - {PartOfSpeech}";
            return $"{FullRawText} - {PartOfSpeech}";
        }
    }
}
