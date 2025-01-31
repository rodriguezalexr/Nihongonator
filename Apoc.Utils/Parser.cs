using MeCab;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Apoc.Utils
{
    public class Parser
    {
        private Definer _definer;
        private JlptLevelManager _jlptLevelManager;
        private MeCabTagger _tagger;

        private List<PartOfSpeech> _nonNotablePartsOfSpeech = new List<PartOfSpeech>()
        {
                PartOfSpeech.Particle,
                PartOfSpeech.Symbol,
                PartOfSpeech.Filler,
                PartOfSpeech.Interjection
        };

        public Parser(Definer definer, JlptLevelManager jlptLevelManager)
        {
            _definer = definer;
            _jlptLevelManager = jlptLevelManager;

            var parameter = new MeCabParam();
            _tagger = MeCabTagger.Create(parameter);
        }

        public IEnumerable<ApocToken> Tokenize(string input, bool limitByFrequency = false, bool limitNonNotablePartsOfSpeech = false, List<string> ignoredTokens = null)
        {
            var nodes = _tagger.ParseToNodes(input).Where(node => node.CharType > 0).ToList();

            var apocTokens = new List<ApocToken>();

            ApocToken prevToken = null;
            foreach (var node in nodes)
            {
                var token = new ApocToken()
                {
                    RawText = node.Surface,
                    MeCabNode = node
                };

                var features = node.Feature.Split(',');
                var partOfSpeechFeature = features[0];
                var subTypeFeature = features[1];
                var rootFeature = features[6];

                if (partOfSpeechFeature == _nounTag)
                {
                    token.PartOfSpeech = PartOfSpeech.Noun;
                }
                else if (partOfSpeechFeature == _verbTag)
                {
                    token.PartOfSpeech = PartOfSpeech.Verb;
                }
                else if (partOfSpeechFeature == _adjectiveTag)
                {
                    token.PartOfSpeech = PartOfSpeech.Adjective;
                }
                else if (partOfSpeechFeature == _adverbTag)
                {
                    token.PartOfSpeech = PartOfSpeech.Adverb;
                }
                else if (partOfSpeechFeature == _particleTag)
                {
                    token.PartOfSpeech = PartOfSpeech.Particle;
                }
                else if (partOfSpeechFeature == _auxilaryVerbTag)
                {
                    token.PartOfSpeech = PartOfSpeech.AuxilaryVerb;
                }
                else if (partOfSpeechFeature == _symbolTag)
                {
                    token.PartOfSpeech = PartOfSpeech.Symbol;
                }
                else if (partOfSpeechFeature == _prefixTag)
                {
                    token.PartOfSpeech = PartOfSpeech.Prefix;
                }
                else if (partOfSpeechFeature == _interjectionTag)
                {
                    token.PartOfSpeech = PartOfSpeech.Interjection;
                }
                else if (partOfSpeechFeature == _preNounAdjectivalTag)
                {
                    token.PartOfSpeech = PartOfSpeech.PreNounAdjectival;
                }
                else if (partOfSpeechFeature == _conjuctionTag)
                {
                    token.PartOfSpeech = PartOfSpeech.Conjunction;
                }
                else if (partOfSpeechFeature == _fillerTag)
                {
                    token.PartOfSpeech = PartOfSpeech.Filler;
                }
                else
                {
                    token.PartOfSpeech = PartOfSpeech.Unknown;
                }

                token.Subtype = subTypeFeature;
                token.RootText = rootFeature;
                token.SourceSentence = input;
                token.Features = GetFeaturesToString(node.Feature);

                if (prevToken != null)
                {
                    token.Previous = prevToken;
                    prevToken.Next = token;
                }

                apocTokens.Add(token);

                prevToken = token;
            }

            //Processing layer
            var currentToken = apocTokens.First();
            while (currentToken != null)
            {
                bool changedSomething = true;
                while (changedSomething != false)
                {
                    //Evaluate the next verb for collapsing
                    if (currentToken.Next != null)
                    {
                        var lastSubtoken = currentToken.SubTokens.LastOrDefault();
                        //Determine if we're a verb, or we're ending with something that's verb-ish
                        bool currentTokenIsAVerb =
                            (!currentToken.SubTokens.Any() && (currentToken.PartOfSpeech == PartOfSpeech.Verb || currentToken.PartOfSpeech == PartOfSpeech.AuxilaryVerb))
                            || (lastSubtoken != null);//TODO: more conditions here?  We might only be doing this for conjugation, so maybe this can be anything

                        //する Should attach to verbs before it
                        if (currentToken.PartOfSpeech == PartOfSpeech.Noun && currentToken.Next.RootText == "する")
                        {
                            MoveTokenToSub(currentToken, currentToken.Next, apocTokens);
                        }
                        //Conjunctive Particles (maybe just て) should join with a verb prior to it
                        else if (currentTokenIsAVerb && currentToken.Next.PartOfSpeech == PartOfSpeech.Particle && currentToken.Next.Subtype == _conjunctiveParticleSubtypeTag)
                        {
                            MoveTokenToSub(currentToken, currentToken.Next, apocTokens);
                        }
                        //Non-Indepentent Verbs/Adjectives should get merged with what's before
                        else if (currentTokenIsAVerb && (currentToken.Next.PartOfSpeech == PartOfSpeech.Verb || currentToken.Next.PartOfSpeech == PartOfSpeech.Adjective) && currentToken.Next.Subtype == _notIndependentSubtypeTag)
                        {
                            MoveTokenToSub(currentToken, currentToken.Next, apocTokens);
                        }
                        //Suffix Verbs should get merged with what's before
                        else if (currentTokenIsAVerb && currentToken.Next.PartOfSpeech == PartOfSpeech.Verb && currentToken.Next.Subtype == _suffixSubtypeTag)
                        {
                            MoveTokenToSub(currentToken, currentToken.Next, apocTokens);
                        }
                        //Auxilary Verbs should get m erged with what's before
                        else if (currentTokenIsAVerb && currentToken.Next.PartOfSpeech == PartOfSpeech.AuxilaryVerb)
                        {
                            MoveTokenToSub(currentToken, currentToken.Next, apocTokens);
                        }
                        else 
                        {
                            changedSomething = false;
                        }

                        //Outside the main block because they don't change anything
                        //Find compound Nouns
                        if (currentToken.PartOfSpeech == PartOfSpeech.Noun && currentToken.Next != null && currentToken.Next.PartOfSpeech == PartOfSpeech.Noun)
                        {
                            var nextNoun = currentToken.Next;
                            string compoundText = currentToken.RawText;
                            while (nextNoun != null)
                            {
                                compoundText += nextNoun.RawText;
                                if (_definer.HasDefinition(compoundText))
                                {
                                    //TODO:  Anything more here?
                                    apocTokens.Add(new ApocToken()
                                    {
                                        RawText = compoundText,
                                        RootText = compoundText,
                                        PartOfSpeech = PartOfSpeech.Noun,
                                        SourceSentence = input
                                    });
                                }

                                if (nextNoun.Next != null && nextNoun.Next.PartOfSpeech == PartOfSpeech.Noun)
                                {
                                    nextNoun = nextNoun.Next;
                                }
                                else
                                {
                                    nextNoun = null;
                                }
                            }

                            changedSomething = false;
                        }
                    }
                    else
                    {
                        changedSomething = false;
                    }
                }

                currentToken = currentToken.Next;
            }

            IEnumerable<ApocToken> filteredTokens = apocTokens;

            if (ignoredTokens != null)
                filteredTokens = filteredTokens.Where(token => !ignoredTokens.Contains(token.RootText));

            if (limitByFrequency)
                filteredTokens = filteredTokens.Where(PassesFrequencyCheck);

            if (limitNonNotablePartsOfSpeech)
                filteredTokens = filteredTokens.Where(token => !_nonNotablePartsOfSpeech.Contains(token.PartOfSpeech));

            return filteredTokens;
        }

        private bool PassesFrequencyCheck(ApocToken token)
        {
            var jlptLevel = _jlptLevelManager.GetJlptLevel(token.RootText);
            if (jlptLevel > 0)
                return true;

            var kanji = _definer.GetMostApplicableKanji(token.RootText);
            return kanji.Priorities.Any(priority => priority.DisplayName == "Ichimango1" || priority.DisplayName == "Newspaper1");
        }

        private static void MoveTokenToSub(ApocToken token, ApocToken newSubToken, List<ApocToken> parentCollection)
        {
            var nextNextToken = newSubToken.Next;

            token.Next = nextNextToken;

            //Should there be previous and next in the subtokens?  I don't think so
            newSubToken.Previous = null;
            newSubToken.Next = null;

            token.SubTokens.Add(newSubToken);

            if (nextNextToken != null)
            {
                nextNextToken.Previous = token;
            }

            parentCollection.Remove(newSubToken);
        }

        public string GetFeaturesToString(string meCabFeatureString)
        {
            var features = meCabFeatureString.Split(',');
            return string.Join(", ", features.Select(feature =>
            {
                if (!_meCabFeatureTranslations.ContainsKey(feature))
                    return feature;
                return _meCabFeatureTranslations[feature];
            }));
        }

        #region Character Class

        private Dictionary<CharacterClass, CharacterClassInfo> _classInfos = new Dictionary<CharacterClass, CharacterClassInfo>()
        {
            {CharacterClass.Romaji, new CharacterClassInfo(CharacterClass.Romaji, 0x0020, 0x007E)},
            {CharacterClass.Hiragana, new CharacterClassInfo(CharacterClass.Hiragana, 0x3040, 0x309F)},
            {CharacterClass.Katakana, new CharacterClassInfo(CharacterClass.Katakana, 0x30A0, 0x30FF)},
            {CharacterClass.Kanji, new CharacterClassInfo(CharacterClass.Kanji, 0x4E00, 0x9FBF)}
        };

        public CharacterClass GetCharacterClass(char c)
        {
            foreach (var info in _classInfos)
            {
                if (IsInCharacterClass(c, info.Value))
                    return info.Key;
            }

            return CharacterClass.Unknown;
        }

        public bool IsInCharacterClass(char c, CharacterClass characterClass)
        {
            var info = _classInfos[characterClass];
            return IsInCharacterClass(c, info);
        }

        private bool IsInCharacterClass(char c, CharacterClassInfo info)
        {
            return c >= info.MinIndex && c <= info.MaxIndex;
        }

        public bool IsSingleKana(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            char c = input.First();

            return IsInCharacterClass(c, CharacterClass.Hiragana) || IsInCharacterClass(c, CharacterClass.Katakana);
        }

        public bool IsJapaneseCharacter(char c)
        {
            return IsInCharacterClass(c, CharacterClass.Hiragana) || IsInCharacterClass(c, CharacterClass.Katakana) || IsInCharacterClass(c, CharacterClass.Kanji);
        }

        public bool IsAllJapaneseCharacters(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            return input.All(c => IsJapaneseCharacter(c));
        }

        private class CharacterClassInfo
        {
            public CharacterClass CharacterClass { get; private set; }

            public int MinIndex { get; private set; }

            public int MaxIndex { get; private set; }

            public CharacterClassInfo(CharacterClass charClass, int minIndex, int maxIndex)
            {
                CharacterClass = charClass;
                MinIndex = minIndex;
                MaxIndex = maxIndex;
            }
        }

        #endregion Character Class


        private const string _nounTag = "名詞";
        private const string _verbTag = "動詞";
        private const string _adjectiveTag = "形容詞";
        private const string _adverbTag = "副詞";
        private const string _particleTag = "助詞";
        private const string _auxilaryVerbTag = "助動詞";
        private const string _symbolTag = "記号";
        private const string _prefixTag = "接頭詞";
        private const string _interjectionTag = "感動詞";
        private const string _preNounAdjectivalTag = "連体詞";
        private const string _conjuctionTag = "接続詞";
        private const string _fillerTag = "フィラー";

        private const string _conjunctiveParticleSubtypeTag = "接続助詞";
        private const string _notIndependentSubtypeTag = "非自立";
        private const string _suffixSubtypeTag = "接尾";

        private Dictionary<string, string> _meCabFeatureTranslations = new Dictionary<string, string>()
        {
            { _nounTag, "Noun" },
            { _verbTag, "Verb" },
            { _adjectiveTag, "adjective" },
            { _adverbTag, "Adverb" },
            { _particleTag, "Particle" },
            { _auxilaryVerbTag, "Auxilary Verb" },
            { _symbolTag, "symbol" },
            { _prefixTag, "Prefix" },
            { _interjectionTag, "Interjection" },
            { _preNounAdjectivalTag, "pre-noun adjectival" },
            { _conjuctionTag, "conjunction" },
            { _fillerTag, "Filler" },
            { _conjunctiveParticleSubtypeTag, "Conjunctive particle" },
            { _notIndependentSubtypeTag, "Not independent" },
            { _suffixSubtypeTag, "suffix" },
            { "その他", "Other" },
            { "アルファベット", "Alphabet" },
            { "カ変・クル", "Variable-kuru" },
            { "カ変・来ル", "Variable-rairu" },
            { "ガル接続", "Garu connection" },
            { "サ変・スル", "Sahen Sur" },
            { "サ変・－スル", "irregular conjugation (inflection, declension) - suru" },
            { "サ変・－ズル", "irregular conjugation (inflection, declension) - zuru" },
            { "サ変接続", "irregular conjugation (inflection, declension) connection" },
            { "ナイ形容詞語幹", "Nai adjective stem" },
            { "ラ変", "irregular conjugation (inflection, declension) of a limited number of verbs ending in 'ru'" },
            { "一段", "Ichidan verb group" },
            { "一段・クレル", "Ichidan verb group - kureru" },
            { "一段・得ル", "Ichidan verb group - eru" },
            { "一般", "General" },
            { "上二・ダ行", "Above two da row ???" },
            { "上二・ハ行", "The upper two-Ha line ???" },
            { "下二・カ行", "Under two-Ka Line ???" },
            { "下二・ガ行", "Under two and gas line ???" },
            { "下二・タ行", "Under two-Ta Line ???" },
            { "下二・ダ行", "Under two da row ???" },
            { "下二・ハ行", "Two lower-Ha line ???" },
            { "下二・マ行", "Under two Ma line ???" },
            { "下二・得", "Two lower-yield ???" },
            { "不変化型", "Non-change ???" },
            { "並立助詞", "parallel marker" },
            { "五段・カ行イ音便", "Godan - verb ending in 'ku', euphonic change wherein some mora ('ki', 'gi', 'shi' and 'ri') are pronounced 'i'" },
            { "五段・カ行促音便", "Godan - verb ending in 'ku', nasal sound change" },
            { "五段・カ行促音便ユク", "Godan - verb ending in 'ku', nasal sound change Yuk" },
            { "五段・ガ行", "Godan - verb ending in 'gu'" },
            { "五段・サ行", "Godan - verb ending in 'su'" },
            { "五段・タ行", "Godan - verb ending in 'tsu'" },
            { "五段・ナ行", "Godan - verb ending in 'nu'" },
            { "五段・バ行", "Godan - verb ending in 'bu'" },
            { "五段・マ行", "Godan - verb ending in 'mu'" },
            { "五段・ラ行", "Godan - verb ending in 'ru'" },
            { "五段・ラ行アル", "Godan - verb ending in 'ru', aru" },
            { "五段・ラ行特殊", "Godan - verb ending in 'ru', Special" },
            { "五段・ワ行ウ音便", "Godan - ???, euphonic change wherein some mora ('ku', 'gu', 'hi', 'bi' and 'mi') are pronounced 'u'" },
            { "五段・ワ行促音便", "Godan - ???, nasal sound change" },
            { "人名", "Personal (person's) name" },
            { "代名詞", "Pronoun" },
            { "仮定形", "Hypothetical form" },
            { "仮定縮約１", "Assumed contraction 1" },
            { "仮定縮約２", "Assumed contraction 2" },
            { "体言接続", "Substantive connection" },
            { "体言接続特殊", "Substantive connection Special" },
            { "体言接続特殊２", "Substantive connection Special 2" },
            { "係助詞", "dependency marker" },
            { "副助詞", "adverbial particle" },
            { "副助詞／並立助詞／終助詞", "adverbial particle / parallel marker / sentence-ending particle" },
            { "副詞化", "'Adverbification'" },
            { "副詞可能", "Potential adverb" },
            { "助動詞語幹", "Auxiliary verb stem" },
            { "助数詞", "Counter suffix" },
            { "助詞類接続", "Particle type connection" },
            { "動詞接続", "Verb connection" },
            { "動詞非自立的", "Verb not independent" },
            { "句点", "Period" },
            { "名", "given name" },
            { "名詞接続", "Noun connection" },
            { "命令ｅ", "Order e" },
            { "命令ｉ", "Order i" },
            { "命令ｒｏ", "Order ro" },
            { "命令ｙｏ", "Order yo" },
            { "四段・サ行", "Yodan - verb ending in 'su'" },
            { "四段・タ行", "Yodan - verb ending in 'tsu'" },
            { "四段・ハ行", "Yodan - verb ending in 'hu or fu'" },
            { "四段・バ行", "Yodan - verb ending in 'bu'" },
            { "固有名詞", "Proper noun" },
            { "国", "country" },
            { "地域", "Area" },
            { "基本形", "Basic form" },
            { "基本形-促音便", "Basic form - nasal sound change" },
            { "姓", "Surname" },
            { "引用", "Quote" },
            { "引用文字列", "Quoted string" },
            { "形容動詞語幹", "Adjective verb stem" },
            { "形容詞・アウオ段", "Adjective - Auodan" },
            { "形容詞・イイ", "Adjective - i" },
            { "形容詞・イ段", "Adjective Idan" },
            { "形容詞接続", "Adjective connection" },
            { "括弧閉", "Close Parenthesis" },
            { "括弧開", "Open parenthesis" },
            { "接続詞的", "Conjunction-like" },
            { "数", "number" },
            { "数接続", "Number connection" },
            { "文語・キ", "Literary language - ki" },
            { "文語・ケリ", "Literary language - keri" },
            { "文語・ゴトシ", "Literary language - gotoshi" },
            { "文語・ナリ", "Literary language - nari" },
            { "文語・ベシ", "Literary language - beshi" },
            { "文語・マジ", "Literary language - maji" },
            { "文語・リ", "Literary language - ri" },
            { "文語・ル", "Literary language - ru" },
            { "文語基本形", "Literary language, basic form" },
            { "未然ウ接続", "'before it happens' 'u' connection" },
            { "未然ヌ接続", "'before it happens' 'nu' connection" },
            { "未然レル接続", "'before it happens' 'reru' connection" },
            { "未然形", "Imperfective form" },
            { "未然特殊", "'before it happens' Special" },
            { "格助詞", "Case particle" },
            { "特殊", "Special" },
            { "特殊・ジャ", "Special - Jiya" },
            { "特殊・タ", "Special - ta" },
            { "特殊・タイ", "Special - tai" },
            { "特殊・ダ", "Special - da" },
            { "特殊・デス", "Special - desu" },
            { "特殊・ナイ", "Special - nai" },
            { "特殊・ヌ", "Special - nu" },
            { "特殊・マス", "Special - masu" },
            { "特殊・ヤ", "Special - ya" },
            { "現代基本形", "Modern basic form" },
            { "空白", "Blank" },
            { "終助詞", "Sentence-ending particle" },
            { "組織", "Organization" },
            { "縮約", "Contraction" },
            { "自立", "Independent" },
            { "読点", "Comma" },
            { "連体化", "'pre-noun adjectival'-like" },
            { "連用ゴザイ接続", "Continuous use 'gosai' connection" },
            { "連用タ接続", "Continuous use 'ta' connection" },
            { "連用テ接続", "Continuous use 'te' connection" },
            { "連用デ接続", "Continuous use 'de' connection" },
            { "連用ニ接続", "Continuous use 'ni' connection" },
            { "連用形", "Conjunctive form" },
            { "連語", "Collocation" },
            { "間投", "interjection" },
            { "音便基本形", "Euphonic basic form" }
        };
    }
    public enum CharacterClass
    {
        Unknown,
        Romaji,
        Hiragana,
        Katakana,
        Kanji
    }
}
