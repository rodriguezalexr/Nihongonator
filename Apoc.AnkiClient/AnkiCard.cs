using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Apoc.AnkiClient
{
    public class AnkiCard
    {
        [JsonProperty("cardId")]
        public long CardId { get; set; }

        [JsonProperty("note")]
        public long NoteId { get; set; }

        [JsonProperty("interval")]
        public int Interval { get; set; }

        [JsonProperty("ord")]
        public int Ord { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("queue")]
        public int Queue { get; set; }

        [JsonProperty("due")]
        public int Due { get; set; }

        [JsonProperty("reps")]
        public int Reps { get; set; }

        [JsonProperty("lapses")]
        public int Lapses { get; set; }

        [JsonProperty("left")]
        public int Left { get; set; }
    }

    public class AnkiCard<TFields> : AnkiCard
    {
        [JsonProperty("fields")]
        public TFields Fields { get; set; }
    }
}
