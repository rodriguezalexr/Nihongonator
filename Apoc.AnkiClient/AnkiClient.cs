using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Apoc.AnkiClient
{
    public class AnkiClient
    {
        private int _version = 6;
        private string _url = "http://localhost:8765";
        private JsonSerializerSettings _jsonSerializerSettings;

        public AnkiClient()
        {
            _jsonSerializerSettings = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        private AnkiRequest BuildRequest(string action)
        {
            return new AnkiRequest()
            {
                Action = action,
                Version = _version
            };
        }

        private AnkiRequestWithParameters BuildRequest(string action, object parameters)
        {
            return new AnkiRequestWithParameters()
            {
                Action = action,
                Version = _version,
                Parameters = parameters
            };
        }

        private async Task<T> PostAsync<T>(object arg = null)
        {
            //TODO: Version check?
            using (var httpClient = new HttpClient())
            {
                var serializedArg = JsonConvert.SerializeObject(arg);
                var result = await httpClient.PostAsync(_url, new StringContent(serializedArg));
                var strResult = await result.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(strResult, _jsonSerializerSettings);
            }
        }

        public Task<AnkiResponse<List<string>>> GetDecksAsync()
        {
            return PostAsync<AnkiResponse<List<string>>>(BuildRequest("deckNames"));
        }

        public Task<AnkiResponse<List<string>>> GetModelNamesAsync()
        {
            return PostAsync<AnkiResponse<List<string>>>(BuildRequest("modelNames"));
        }

        public Task<AnkiResponse<List<string>>> GetModelFieldNamesAsync(string modelName)
        {
            var parameters = new { modelName = modelName };
            return PostAsync<AnkiResponse<List<string>>>(BuildRequest("modelcurrentFieldNames", parameters));
        }

        public Task<AnkiResponse<List<long>>> GetNoteIdsForDeck(string deckName)
        {
            var parameters = new { query = $"deck:{deckName}" };
            return PostAsync<AnkiResponse<List<long>>>(BuildRequest("findNotes", parameters));
        }

        public async Task<AnkiResponse<List<AnkiNote<TFields>>>> GetNotesForDeck<TFields>(string deckName)
        {
            var noteIds = await GetNoteIdsForDeck(deckName);
            var parameters = new { notes = noteIds.Result.ToArray() };
            return await PostAsync<AnkiResponse<List<AnkiNote<TFields>>>>(BuildRequest("notesInfo", parameters));
        }

        public Task<AnkiResponse<List<long>>> GetNoteIdsForQuery(string query)
        {
            var parameters = new { query = query };
            return PostAsync<AnkiResponse<List<long>>>(BuildRequest("findNotes", parameters));
        }

        public async Task<AnkiResponse<List<AnkiNote<TFields>>>> GetNotesForQuery<TFields>(string query)
        {
            var noteIds = await GetNoteIdsForQuery(query);
            var parameters = new { notes = noteIds.Result.ToArray() };
            return await PostAsync<AnkiResponse<List<AnkiNote<TFields>>>>(BuildRequest("notesInfo", parameters));
        }

        public Task<AnkiResponse<double>> AddNoteAsync(string deckName, string modelName, object fields, List<string> tags = null)
        {
            var parameters = new
            {
                note = new
                {
                    deckName = deckName,
                    modelName = modelName,
                    fields = fields,
                    tags = tags ?? new List<string>()
                }
            };
            return PostAsync<AnkiResponse<double>>(BuildRequest("addNote", parameters));
        }

        public Task<AnkiResponse<string>> UpdateNoteAsync(double id, object fields)
        {
            var parameters = new
            {
                note = new UpdateNote()
                {
                    //id = id.ToString("#0"),
                    id = (long)id,
                    fields = fields
                }
            };
            return PostAsync<AnkiResponse<string>>(BuildRequest("updateNoteFields", parameters));
        }

        private class UpdateNote
        {
            public long id  { get; set; }

            public object fields { get; set; }
        }

        public Task<AnkiResponse<string>> UpdateNoteWithAudioFromFile(long id, string fileName, string filePath)
        {
            var parameters = new
            {
                //note = new UpdateNoteWithAudio()
                note = new 
                {
                    id = id,
                    fields = new object(),
                    audio = new object[]
                    {
                        new 
                        {
                            filename = fileName,
                            path = filePath,
                            fields = new string[]
                            {
                                "Audio"
                            }
                        }
                    }
                }
            };
            return PostAsync<AnkiResponse<string>>(BuildRequest("updateNoteFields", parameters));
        }

        //private class UpdateNoteWithAudio
        //{
        //    public long id { get; set; }

        //    public string filename { get; set; }
        //}

        public Task<AnkiResponse<string>> AddTagAsync(long id, string tag)
        {
            return AddTagAsync(new List<long>() { id }, tag);
        }

        public Task<AnkiResponse<string>> AddTagAsync(List<long> ids, string tag)
        {
            var parameters = new
            {
                notes = ids,
                tags = tag
            };
            return PostAsync<AnkiResponse<string>>(BuildRequest("addTags", parameters));
        }

        public Task<AnkiResponse<string>> RemoveTagAsync(long id, string tag)
        {
            return RemoveTagAsync(new List<long>() { id }, tag);
        }

        public Task<AnkiResponse<string>> RemoveTagAsync(List<long> ids, string tag)
        {
            var parameters = new
            {
                notes = ids,
                tags = tag
            };
            return PostAsync<AnkiResponse<string>>(BuildRequest("removeTags", parameters));
        }

        public Task<AnkiResponse<List<long>>> GetCardIdsForDeck(string deckName)
        {
            var parameters = new { query = $"deck:{deckName}" };
            return PostAsync<AnkiResponse<List<long>>>(BuildRequest("findCards", parameters));
        }

        public async Task<AnkiResponse<List<AnkiCard>>> GetCardsForDeck<TFields>(string deckName)
        {
            var cardIds = await GetCardIdsForDeck(deckName);
            var parameters = new { cards = cardIds.Result.ToArray() };
            return await PostAsync<AnkiResponse<List<AnkiCard>>>(BuildRequest("cardsInfo", parameters));
        }

        public Task<AnkiResponse<List<long>>> GetCardIdsForQuery (string query)
        {
            var parameters = new { query };
            return PostAsync<AnkiResponse<List<long>>>(BuildRequest("findCards", parameters));
        }

        public async Task<AnkiResponse<List<AnkiCard<TFields>>>> GetCardsForQuery<TFields>(string query)
        {
            var cardIds = await GetCardIdsForQuery(query);
            var parameters = new { cards = cardIds.Result.ToArray() };
            return await PostAsync<AnkiResponse<List<AnkiCard<TFields>>>>(BuildRequest("cardsInfo", parameters));
        }

        public Task<AnkiResponse<bool[]>> SetCardDue(long cardId, int due)
        {
            var parameters = new
            {
                card = cardId,
                keys = new[] { "due" },
                newValues = new[] { due }
            };
            return PostAsync<AnkiResponse<bool[]>>(BuildRequest("setSpecificValueOfCard", parameters));
        }

        public Task<AnkiResponse<bool>> ChangeDeck(long[] cards, string newDeck)
        {
            var parameters = new
            {
                cards = cards,
                deck = newDeck
            };
            return PostAsync<AnkiResponse<bool>>(BuildRequest("changeDeck", parameters));
        }
    }
}
