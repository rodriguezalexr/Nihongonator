using Apoc.AnkiClient;
using Apoc.Utils;
using MeCab;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Nihongonator.Cli
{
    public class Program
    {
        private static int _minCount = 5;
        private static bool _printCards = true;
        private static bool _exportCards = false;
        private static bool _limitByFrequency = true;

        private static JlptLevelManager _jlptLevelManager;
        private static Definer _definer;
        private static Parser _parser;
        private static AnkiClient _ankiClient;
        private static AnkiNoteManager _ankiNoteManager;
        private static List<string> _rootTextsFromAnkiNotes;
        private static List<string> _allRootTextsAndReadingsFromAnkiNotes;
        private static List<string> _allIgnoredTokens;
        private static SentenceManager _sentenceManager;
        private static FrequencyManager _frequencyManager;
        private static JapanesePodAudioFetcher _audioFetcher;
        private static Subs2SrsManager _subs2SrsManager;

        static void Main(string[] args)
        {
            Console.WriteLine("Nihongonator CLI");

            _frequencyManager = new FrequencyManager();
            _frequencyManager.Init();

            Console.WriteLine("Loading JLPT Data");
            _jlptLevelManager = new JlptLevelManager();
            _jlptLevelManager.Init();

            Console.WriteLine("Initializing Utils");
            _definer = new Definer();
            _parser = new Parser(_definer, _jlptLevelManager);
            _audioFetcher = new JapanesePodAudioFetcher();
            _subs2SrsManager = new Subs2SrsManager(_parser);

            Console.WriteLine("Loading Existing Anki Data");
            _ankiClient = new AnkiClient();

            _ankiNoteManager = new AnkiNoteManager(_jlptLevelManager, _ankiClient, _definer, _frequencyManager, _parser, _audioFetcher);

            //Normally Run these
            Nito.AsyncEx.AsyncContext.Run(() => _ankiNoteManager.EnrichNotesForDeck("Japanese::Mining", true));
            Nito.AsyncEx.AsyncContext.Run(() => _ankiNoteManager.ChangeDeck("Japanese::Mining", "Japanese::Vocab"));
            Nito.AsyncEx.AsyncContext.Run(() => _ankiNoteManager.SortCards("Japanese::Vocab"));
            Nito.AsyncEx.AsyncContext.Run(() => _ankiNoteManager.PrintSummary());

            //Nito.AsyncEx.AsyncContext.Run(() => _ankiNoteManager.AddAudioToCards("deck:Japanese::Vocab"));
            //Nito.AsyncEx.AsyncContext.Run(() => _ankiNoteManager.AddAudioToCards("deck:Japanese::Mining"));
            //Nito.AsyncEx.AsyncContext.Run(() => _ankiNoteManager.AddAudioToCards("deck:Japanese::Vocab prop:due=1"));

            //_subs2SrsManager.ProcessTsv(@"C:\Nihongonator\Toradora.tsv", @"C:\Nihongonator\ToradoraProcessed.tsv");

            Console.WriteLine();
            Console.WriteLine("Done");
            Console.ReadLine();
        }

        private static void PrintCountUnderX(Dictionary<string, int> frequencies, int x)
        {
            Console.WriteLine($"Count under {x}: {frequencies.Count(freq => freq.Value < x)}");
        }


        public static void BuildCardsFromVocabListFromFile(string filename)
        {
            BuildCardsFromVocabList(ParseFile(filename));
        }

        public static void BuildCardsForJlptLevel(int level)
        {
            BuildCardsFromVocabList(_jlptLevelManager.GetVocabForLevel(level));
        }

        public static void BuildCardsFromVocabList(List<string> rawVocab)
        {
            Console.WriteLine("Reading Vocab List");
            //TODO: Filter out tokens we know?  Maybe move that logic
            var vocabItems = rawVocab
                .SelectMany(line => _parser.Tokenize(line, false, false))
                .Where(token => !(token.RootText.Count() == 1 && _parser.IsSingleKana(token.RootText)))
                .ToList();

            var wordsWithoutDefs = new List<string>();

            int numNewCards = 0;
            int numNoDefs = 0;
            int numExistingCards = 0;

            var newCardsCheckStringBuilder = new StringBuilder();

            foreach (var vocabItem in vocabItems)
            {
                if (_printCards || _exportCards)
                {
                    if (_rootTextsFromAnkiNotes.Contains(vocabItem.RootText))
                    {
                        numExistingCards++;
                        Console.WriteLine("Card already exists");
                    }
                    else
                    {
                        var exampleSentence = _sentenceManager.PickBestExampleSentence(vocabItem, _allRootTextsAndReadingsFromAnkiNotes);
                        Console.WriteLine($"{vocabItem.RootText} - {vocabItem.PartOfSpeech} - {exampleSentence}");

                        var note = _ankiNoteManager.BuildNote(vocabItem, _definer);
                        if (note != null)
                        {
                            numNewCards++;
                            newCardsCheckStringBuilder.AppendLine($"{note.Japanese} - {note.Reading}");
                            note.Sentence = exampleSentence ?? string.Empty;
                            if (_printCards)
                            {
                                PrintNote(note);

                            }
                            if (_exportCards)
                            {
                                var resp = Nito.AsyncEx.AsyncContext.Run(() => _ankiClient.AddNoteAsync("Japanese::Vocab", "Genki (Apoc)", note));
                                if (!resp.Succeeded)
                                    Console.WriteLine($"Error: {resp.Error}");
                            }

                            //Add new tokens to the readings list
                            _allRootTextsAndReadingsFromAnkiNotes.Add(note.Japanese);
                            _allRootTextsAndReadingsFromAnkiNotes.AddRange(note.Reading.Split(",").Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)));
                        }
                        else
                        {
                            numNoDefs++;
                            wordsWithoutDefs.Add(vocabItem.RootText);
                            Console.WriteLine("No Note");
                        }

                    }

                    Console.WriteLine();
                }
            }

            Console.WriteLine();
            Console.WriteLine("Words without defs:");
            foreach (var skippedword in wordsWithoutDefs)
            {
                Console.WriteLine(skippedword);
            }


            Console.WriteLine();
            Console.WriteLine("New Words:");
            Console.WriteLine(newCardsCheckStringBuilder.ToString());

            Console.WriteLine($"Num New: {numNewCards}");
            Console.WriteLine($"Num Existing: {numExistingCards}");
            Console.WriteLine($"Num No Defs: {numNoDefs}");
        }


        public static void BuildCardsFromText()
        {
            Console.WriteLine("Reading test file");
            var inputLines = ParseFile(@"c:\Nihongonator\TestInput.txt");

            var groupedTokens = inputLines
                .SelectMany(line => _parser.Tokenize(line, _limitByFrequency, true, _allIgnoredTokens))
                .GroupBy(token => token.RootText)
                .Where(group => group.Count() >= _minCount)
                .OrderByDescending(group => group.Count())
                .ToList();

            int potentialNotes = 0;

            //Normal Behavior
            foreach (var group in groupedTokens)
            {
                var token = group.First();
                var note = _ankiNoteManager.BuildNote(group.First(), _definer);

                if (note != null)
                {
                    Console.WriteLine($"{group.Key} - {group.Count()} - {token.PartOfSpeech} - {note.Reading} - {(string.IsNullOrWhiteSpace(note.Extra) ? "???" : note.Extra)} - {note.English.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}");
                }
                else
                {
                    Console.WriteLine($"{group.Key} - {group.Count()} - {token.PartOfSpeech} - No Note");
                }

                if (_printCards || _exportCards)
                {
                    if (_printCards)
                    {
                        if (note != null)
                        {
                            PrintNote(note);
                        }
                    }
                    if (_exportCards)
                    {
                        var resp = Nito.AsyncEx.AsyncContext.Run(() => _ankiClient.AddNoteAsync("Japanese::Vocab", "Genki (Apoc)", note));
                        if (!resp.Succeeded)
                            Console.WriteLine($"Error: {resp.Error}");
                    }
                }

                potentialNotes++;
            }

            Console.WriteLine($"Potential Additions: {potentialNotes}");
        }

        private static void PrintNote(ApocGenkiNoteWriteModel note)
        {
            Console.WriteLine($"    Japanese(Full): {note.Japanese}");
            Console.WriteLine($"    Japanese(Kana): {note.Reading}");
            Console.WriteLine($"    Part of Speech: {note.PartOfSpeech}");
            Console.WriteLine($"    English: {note.English}");
            Console.WriteLine($"    Extra: {note.Extra}");
            Console.WriteLine($"    Sentence: {note.Sentence}");
        }

        public static List<string> ParseFile(string fileName, string separator = null, Func<string, string> modifier = null)
        {
            if (separator == null)
                separator = Environment.NewLine;

            string inputText = File.ReadAllText(fileName);

            var lines = inputText
                .Split(separator, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim());

            if (modifier != null)
                lines = lines.Select(modifier);

            return lines.ToList();
        }
    }
}
