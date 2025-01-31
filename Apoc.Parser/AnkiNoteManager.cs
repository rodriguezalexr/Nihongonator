using Apoc.AnkiClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wacton.Desu.Japanese;

namespace Apoc.Utils
{
    public class AnkiNoteManager
    {
        private static List<string> _notableFrequencies = new List<string>()
        {
            "Ichimango1",
            "Ichimango2",
            "Newspaper1",
            "Newspaper2",
            "SpecialCommon1",
            "SpecialCommon2"
        };

        private const string _freq_top_1k = "Freq:Top_1k";
        private const string _freq_top_2k = "Freq:Top_2k";
        private const string _freq_top_3k = "Freq:Top_3k";
        private const string _freq_top_5k = "Freq:Top_5k";
        private const string _freq_top_10k = "Freq:Top_10k";
        private const string _freq_top_20k = "Freq:Top_20k";
        private const string _freq_top_30k = "Freq:Top_30k";
        private const string _freq_top_50k = "Freq:Top_50k";
        private const string _freq_over_50k = "Freq:Over_50k";
        private const string _freq_not_found = "Freq:Not_Found";
        private static List<string> _orderedFrequencies = new List<string>()
        {
            _freq_top_1k,
            _freq_top_2k,
            _freq_top_3k,
            _freq_top_5k,
            _freq_top_10k,
            _freq_top_20k,
            _freq_top_30k,
            _freq_top_50k,
            _freq_over_50k,
            _freq_not_found
        };

        private JlptLevelManager _jlptLevelManager;
        private Definer _definer;
        private AnkiClient.AnkiClient _ankiClient;
        private FrequencyManager _frequencyManager;
        private Parser _parser;
        private JapanesePodAudioFetcher _audioFetcher;

        public AnkiNoteManager(JlptLevelManager jlptLevelManager, AnkiClient.AnkiClient ankiClient, Definer definer, FrequencyManager frequencyManager, Parser parser, JapanesePodAudioFetcher audioFetcher)
        {
            _jlptLevelManager = jlptLevelManager;
            _ankiClient = ankiClient;
            _definer = definer;
            _frequencyManager = frequencyManager;
            _parser = parser;
            _audioFetcher = audioFetcher;
        }

        public ApocGenkiNoteWriteModel BuildNote(ApocToken token, Definer definer)
        {
            var definition = definer.GetMostApplicableDefinition(token.RootText);

            if (definition == null)
                return null;

            //JapaneseFull
            var tokenKanji = definer.GetMostApplicableKanji(token.RootText, definition);
            var tokenReading = definer.GetMostApplicableReading(token.RootText, definition);

            string tokenText = tokenKanji?.Text ?? tokenReading?.Text;
            var tokenPriorities = tokenKanji?.Priorities ?? tokenReading?.Priorities;

            //JapaneseKana
            //TODO: Verbs need to be conjugated
            //Everything else just append all the readings
            string japaneseKana = string.Join(", ", definition.Readings.Select(reading => reading.Text));

            //English
            //TODO: Do we care about infos, misc, etc?
            string english = null;
            var englishSenses = definition.Senses.Where(sense => sense.Glosses.Any(gloss => gloss.Language.Value == 15)).Select(sense => new { Sense = sense, PartOfSpeech = string.Join(", ", sense.PartsOfSpeech.Select(pos => pos.Description)) }).ToList();
            if (englishSenses.Count() == 1)
            {
                english = GetStringForSense(englishSenses.First().Sense);
            }
            else
            {
                //bool singlePartOfSpeech = englishSenses.Select(sense => sense.PartOfSpeech).Distinct().Count() == 1;
                //string previousPartOfSpeech = string.Empty;
                var sb = new StringBuilder();
                int i = 1;
                foreach (var sense in englishSenses)
                {
                    //if (!singlePartOfSpeech && previousPartOfSpeech != sense.PartOfSpeech)
                    //    sb.AppendLine(sense.PartOfSpeech);

                    sb.AppendLine($"{i++}) {GetStringForSense(sense.Sense)}");

                    //previousPartOfSpeech = sense.PartOfSpeech;
                }

                english = sb.ToString();
            }

            //Part of speech
            //TODO: Verbs should have godan/ichidan
            //TODO: する verb stuff?
            //string partOfSpeech = token.PartOfSpeech.ToString();
            string partOfSpeech = string.Join(", ", englishSenses.Where(sense => !string.IsNullOrWhiteSpace(sense.PartOfSpeech)).Select(sense => sense.PartOfSpeech).Distinct());

            var extraStrings = new List<string>();

            int? jlptLevel = _jlptLevelManager.GetJlptLevel(tokenText);
            if (jlptLevel != null)
            {
                extraStrings.Add($"JLPT N{jlptLevel}");
            }

            if (tokenPriorities.Any(priority => _notableFrequencies.Contains(priority.DisplayName)))
            {
                extraStrings.AddRange(tokenPriorities.Where(priority => _notableFrequencies.Contains(priority.DisplayName)).Select(priority => priority.DisplayName));
            }

            return new ApocGenkiNoteWriteModel()
            {
                Japanese = tokenText,
                Reading = japaneseKana,
                PartOfSpeech = partOfSpeech,
                English = english,
                Sentence = token.SourceSentence,
                Extra = string.Join(", ", extraStrings)
                //Chapter = string.Empty
            };
        }

        private static string GetStringForSense(Wacton.Desu.Japanese.ISense sense)
        {
            var sb = new StringBuilder();

            sb.Append(string.Join("; ", sense.Glosses.Select(gloss => gloss.Term)));

            if (sense.Miscellanea.Any())
            {
                sb.Append($" ({string.Join(", ", sense.Miscellanea.Select(misc => misc.DisplayName))})");
            }

            if (sense.Informations.Any())
            {
                sb.Append($" ({string.Join(", ", sense.Informations.Select(misc => misc))})");
            }

            return sb.ToString();
        }

        public Task EnrichNotesForDeck(string deck, bool overridePartOfSpeech)
        {
            return EnrichNotes($"deck:{deck}", overridePartOfSpeech);
        }

        public async Task EnrichNotes(string query, bool overridePartOfSpeech, bool skipAlreadyEnriched = true, string tag = null)
        {
            string enrichVersionTag = "Enrich01";
            var notes = await _ankiClient.GetNotesForQuery<ApocGenkiNoteReadModel>(query);
            Console.WriteLine($"Notes: {notes.Result.Count}");

            var notesToUpdate = notes.Result;
            if (!string.IsNullOrWhiteSpace(tag))
            {
                notesToUpdate = notesToUpdate.Where(note => note.Tags.Contains(tag)).ToList();
            }

            Console.WriteLine($"Filtered Notes: {notesToUpdate.Count}");

            if (skipAlreadyEnriched)
            {
                notesToUpdate = notesToUpdate.Where(note => !note.Tags.Contains(enrichVersionTag)).ToList();

                Console.WriteLine($"Unenriched Filtered Notes: {notesToUpdate.Count}");
            }

            var sw = new Stopwatch();
            sw.Start();
            int i = 0;
            List<string> noDefsCards = new List<string>();

            foreach (var noteToUpdate in notesToUpdate)
            {
                i++;

                var searchString = noteToUpdate.Fields.Japanese.Value.Replace("(", string.Empty).Replace(")", string.Empty).Replace("（", string.Empty).Replace("）", string.Empty).Replace("～", string.Empty);
                if (searchString != noteToUpdate.Fields.Japanese.Value)
                    Console.WriteLine($"Searching for {searchString} for {noteToUpdate.Fields.Japanese.Value}");

                bool addTags = true;

                var def = _definer.GetMostApplicableDefinition(searchString);
                var frequency = _frequencyManager.GetFrequency(noteToUpdate.Fields.Japanese.Value);
                if (def == null)
                {
                    noDefsCards.Add(noteToUpdate.Fields.Japanese.Value);
                }
                else
                {
                    var englishSenses = def.Senses.Where(sense => sense.Glosses.Any(gloss => gloss.Language.Value == 15)).Select(sense => new { Sense = sense, PartOfSpeech = string.Join(", ", sense.PartsOfSpeech.Select(pos => pos.Description)) }).ToList();
                    string newPartOfSpeech = string.Join(", ", englishSenses.Where(sense => !string.IsNullOrWhiteSpace(sense.PartOfSpeech)).Select(sense => sense.PartOfSpeech).Distinct());


                    bool updatePartOfSpeech = overridePartOfSpeech || string.IsNullOrWhiteSpace(noteToUpdate.Fields.PartOfSpeech.Value);

                    var sparseUpdate = new SparseUpdateModel()
                    {
                        PartOfSpeech = updatePartOfSpeech ? newPartOfSpeech : noteToUpdate.Fields.PartOfSpeech.Value,
                        Frequency = frequency == null ? string.Empty : frequency.ToString()
                    };

                    var updateResult = await _ankiClient.UpdateNoteAsync(noteToUpdate.NoteId, sparseUpdate);
                    addTags = updateResult.Succeeded;
                }


                int numTagsAdded = 0;
                if (addTags)
                {
                    var frequencyTags = await UpdateTagsForFrequency(noteToUpdate, frequency, false);
                    var extraTags = await UpdateTagsForExtra(noteToUpdate, def, searchString, false);
                    var allTagsToAdd = frequencyTags.Concat(extraTags).ToList();
                    numTagsAdded = allTagsToAdd.Count;
                    allTagsToAdd.Add(enrichVersionTag);
                    var tagsString = string.Join(" ", allTagsToAdd);
                    await _ankiClient.AddTagAsync(noteToUpdate.NoteId, tagsString);
                }

                Console.WriteLine($"Updated {noteToUpdate.Fields.Japanese.Value} : Added {numTagsAdded} tags.  {i} of {notesToUpdate.Count} in {sw.Elapsed.TotalSeconds} at rate {sw.Elapsed.TotalSeconds / i}.");
            }

            Console.WriteLine($"Skipping Defs: {string.Join(Environment.NewLine, noDefsCards)}");
        }

        public async Task<List<string>> UpdateTagsForFrequency(AnkiNote note, int? frequency, bool addTags)
        {
            var tagsToAdd = new List<string>();

            string tag = null;
            if (!frequency.HasValue)
                tag = _freq_not_found;
            else if (frequency <= 1000)
                tag = _freq_top_1k;
            else if (frequency <= 2000)
                tag = _freq_top_2k;
            else if (frequency <= 3000)
                tag = _freq_top_3k;
            else if (frequency <= 5000)
                tag = _freq_top_5k;
            else if (frequency <= 10000)
                tag = _freq_top_10k;
            else if (frequency <= 20000)
                tag = _freq_top_20k;
            else if (frequency <= 30000)
                tag = _freq_top_30k;
            else if (frequency <= 50000)
                tag = _freq_top_50k;
            else
                tag = _freq_over_50k;

            var freqTags = note.Tags.Where(t => t.Contains("Freq:")).ToList();
            var correctExistingTag = freqTags.FirstOrDefault(t => t.Equals(tag, StringComparison.InvariantCultureIgnoreCase));
            var incorrectExistingTags = freqTags.Except(new List<string>() { correctExistingTag }).ToList();

            foreach (var incorrectTag in incorrectExistingTags)
            {
                await _ankiClient.RemoveTagAsync(note.NoteId, incorrectTag);
            }

            if (correctExistingTag == null)
            {
                if (addTags)
                    await _ankiClient.AddTagAsync(note.NoteId, tag);
                tagsToAdd.Add(tag);
            }

            return tagsToAdd;
        }

        private async Task<List<string>> UpdateTagsForExtra(AnkiNote<ApocGenkiNoteReadModel> note, IJapaneseEntry definition, string searchString, bool addNotes)
        {
            if (definition == null)
                return new List<string>();

            //JapaneseFull
            var tokenKanji = _definer.GetMostApplicableKanji(searchString, definition);
            var tokenReading = _definer.GetMostApplicableReading(searchString, definition);

            string tokenText = tokenKanji?.Text ?? tokenReading?.Text;
            var tokenPriorities = tokenKanji?.Priorities ?? tokenReading?.Priorities;

            var extraStrings = new List<string>();

            int? jlptLevel = _jlptLevelManager.GetJlptLevel(tokenText);
            if (jlptLevel != null)
            {
                extraStrings.Add($"JLPT_N{jlptLevel}");
            }

            if (tokenPriorities.Any(priority => _notableFrequencies.Contains(priority.DisplayName)))
            {
                extraStrings.AddRange(tokenPriorities.Where(priority => _notableFrequencies.Contains(priority.DisplayName)).Select(priority => priority.DisplayName));
            }

            var missingTags = extraStrings.Except(note.Tags).ToList();

            if (missingTags.Any() && addNotes)
            {
                var stringTagsToAdd = string.Join(" ", missingTags);
                await _ankiClient.AddTagAsync(note.NoteId, stringTagsToAdd);
            }

            return missingTags;
        }

        private class SparseUpdateModel
        {
            [JsonProperty("Part of Speech")]
            public string PartOfSpeech { get; set; }

            [JsonProperty("Frequency")]
            public string Frequency { get; set; }
        }


        private string _noAudioAvailableTag = "NoAudio";
        private string _badReadingTag = "BadReading";
        public async Task AddAudioToCards(string query)
        {
            var notes = await _ankiClient.GetNotesForQuery<ApocGenkiNoteReadModel>(query);
            Console.WriteLine($"Notes: {notes.Result.Count}");

            var notesToUpdate = notes.Result.Where(note => string.IsNullOrWhiteSpace(note.Fields.Audio.Value) && !note.Tags.Contains(_noAudioAvailableTag) && !note.Tags.Contains(_badReadingTag)).ToList();

            Console.WriteLine($"Notes without Audio: {notesToUpdate.Count}");

            var sw = new Stopwatch();
            sw.Start();
            int i = 0;
            string regexMatchString = "<.*?>";

            foreach (var note in notesToUpdate)
            {
                i++;

                var kanji = note.Fields.Japanese.Value.Trim();
                if (Regex.IsMatch(kanji, regexMatchString))
                {
                    kanji = Regex.Replace(kanji, regexMatchString, string.Empty).Trim();
                }
                
                var kana = note.Fields.Reading.Value;
                if (kana.Contains("・") || kana.Contains(",") || kana.Contains("、"))
                {
                    var parts = kana.Split('・', ',','、');
                    kana = parts.First();
                }
                kana = kana.Trim();

                if (!_parser.IsAllJapaneseCharacters(kana))
                {
                    await _ankiClient.AddTagAsync(note.NoteId, _badReadingTag);
                    LogProgress(note.Fields.Japanese.Value, false, i, notesToUpdate.Count, sw.Elapsed.TotalSeconds);
                    continue;
                }

                var audioResult = await _audioFetcher.FetchUniqueAudioForTerm(kana, kanji);
                if (audioResult.AudioExists)
                {
                    await _ankiClient.UpdateNoteWithAudioFromFile(note.NoteId, audioResult.AudioFileName, audioResult.PathToAudio);
                    LogProgress(note.Fields.Japanese.Value, true, i, notesToUpdate.Count, sw.Elapsed.TotalSeconds);
                }
                else
                {
                    await _ankiClient.AddTagAsync(note.NoteId, _noAudioAvailableTag);
                    LogProgress(note.Fields.Japanese.Value, false, i, notesToUpdate.Count, sw.Elapsed.TotalSeconds);
                }
            }
        }

        private void LogProgress(string name, bool succeded, int progress, int total, double secondsElapsed)
        {
            Console.WriteLine($"Updated {name} : {succeded}. {progress} of {total} in {secondsElapsed} at rate {secondsElapsed / progress}");
        }

        public async Task<List<string>> GetAllWords(string deck)
        {
            var notes = await _ankiClient.GetNotesForDeck<ApocGenkiNoteReadModel>(deck);
            if (notes.Succeeded)
                return notes.Result.Select(n => n.Fields.Japanese.Value).ToList();
            return new List<string>();
        }

        public async Task SortCards(string deck)
        {
            Console.WriteLine($"Sorting cards for {deck}");
            var query = string.Format("deck:{0} is:new", deck);
            //var query = string.Format("deck:{0}", deck);
            var cards = await _ankiClient.GetCardsForQuery<ApocGenkiNoteReadModel>(query);
            if (cards.Succeeded)
            {
                Console.WriteLine($"Retrieved {cards.Result.Count()} cards for sorting.");
                if (cards.Result.Any(c => c.Lapses != 0 || c.Reps != 0 || c.Type != 0))
                    throw(new Exception("Suspicious cards for sorting found"));

                int i = 1;
                var sw = new Stopwatch();
                sw.Start();
                cards.Result.Where(c => c.Fields.Frequency is null).ToList().ForEach(c => c.Fields.Frequency = new AnkiNoteField() { Value = "1000000" });
                var orderedCards = cards.Result.OrderBy(c => int.TryParse(c.Fields.Frequency.Value, out _) ? int.Parse(c.Fields.Frequency.Value) : int.MaxValue).ToList();

                //Prio Handling
                var prioIds = await GetIdsForTags("ApocPrio", true);
                if (prioIds.Any())
                {
                    var prioCards = orderedCards.Where(c => prioIds.Contains(c.NoteId)).ToList();
                    orderedCards = orderedCards.Where(c => !prioIds.Contains(c.NoteId)).ToList();
                    orderedCards.InsertRange(0, prioCards);
                }

                foreach (AnkiCard<ApocGenkiNoteReadModel> card in orderedCards)
                {
                    Console.WriteLine($"Sorting card {card.Fields.Japanese.Value} with frequency ({card.Fields.Frequency.Value}) to due index {i}. {i} of {orderedCards.Count} in {sw.Elapsed.TotalSeconds} at rate {sw.Elapsed.TotalSeconds / i}.");
                    var result = await _ankiClient.SetCardDue(card.CardId, i);
                    if (!result.Succeeded || result.Result.Any(r => r == false))
                        Console.WriteLine($"Failed to sort card {card.CardId}");
                    i++;
                }

            }
            else
            {
                Console.WriteLine("Failed to retrieve cards for sorting");
            }
        }

        public async Task ChangeDeck(string originalDeck, string newDeck)
        {
            var cards = await _ankiClient.GetCardIdsForDeck(originalDeck);
            var result = await _ankiClient.ChangeDeck(cards.Result.ToArray(), newDeck);
            if (result.Succeeded)
                Console.WriteLine($"Moved {cards.Result.Count} to deck {newDeck}");
            else
                Console.WriteLine("Error moving cards");
        }

        public async Task PrintSummary()
        {
            Console.WriteLine();
            Console.WriteLine("Learned/Learning Card Frequencies:");
            int runningTotal = 0;
            List<long> allIds = new List<long>();
            foreach (var freqTag in _orderedFrequencies)
            {
                var ids = await GetIdsForTags(freqTag, false);
                allIds.AddRange(ids);
                var currentCount = ids.Count;
                runningTotal += currentCount;
                Console.WriteLine($"{freqTag}: {currentCount}, {runningTotal} total");
            }

            var allIdsInDeck = await _ankiClient.GetNoteIdsForQuery("deck:Japanese::Vocab -is:new");
            //var allIdsInDeck = await _ankiClient.GetNoteIdsForQuery("deck:Japanese::Vocab (is:learn OR is:review)");
            var missingIds = allIdsInDeck.Result.Where(id => !allIdsInDeck.Result.Contains(id)).ToList();

            int newCardRunningTotal = 0;
            Console.WriteLine();
            Console.WriteLine("New Card Totals:");
            foreach (var freqTag in _orderedFrequencies)
            {
                var ids = await GetIdsForTags(freqTag, true);
                var currentCount = ids.Count;
                newCardRunningTotal += currentCount;
                Console.WriteLine($"{freqTag}: {currentCount}, {newCardRunningTotal} total");
            }
            Console.WriteLine($"Learned Count: {allIdsInDeck.Result.Count}, New Count: {newCardRunningTotal}, Total Count: {allIdsInDeck.Result.Count + newCardRunningTotal} Missing Count: {missingIds.Count}");

            var firstDay = new DateTime(2019, 1, 2);
            var numDays = (DateTime.Today - firstDay).TotalDays + 1; //Add a day to match anki's calculations
            var averageCardsPerDay = allIdsInDeck.Result.Count / numDays;
            Console.WriteLine($"Deck active for {numDays} days, new cards studied at {averageCardsPerDay.ToString("#.00")} cards per day");
        }

        public async Task PrintTagCount(string tag, int? expectedMax = null)
        {
            var query = $"deck:Japanese::Vocab -is:new tag:{tag}";
            var noteIds = await _ankiClient.GetNoteIdsForQuery(query);
            if (expectedMax.HasValue)
                Console.WriteLine($"{tag}: {noteIds.Result.Count} out of {expectedMax}");
            else
                Console.WriteLine($"{tag}: {noteIds.Result.Count}");
        }

        public async Task<List<long>> GetIdsForTags(string tag, bool evalNew)
        {
            var query = evalNew ? $"deck:Japanese::Vocab is:new tag:{tag}" : $"deck:Japanese::Vocab -is:new tag:{tag}";
            var noteIds = await _ankiClient.GetNoteIdsForQuery(query);
            return noteIds.Result;
        }
    }
}
