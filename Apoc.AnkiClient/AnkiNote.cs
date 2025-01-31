using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Apoc.AnkiClient
{
    public class AnkiNote
    {
        [JsonProperty("noteId")]
        public long NoteId { get; set; }

        [JsonProperty("modelname")]
        public string ModelName { get; set; }

        [JsonProperty("tags")]
        public string[] Tags { get; set; }
    }

    public class AnkiNote<TFields> : AnkiNote
    {

        [JsonProperty("fields")]
        public TFields Fields { get; set; }
    }
}
