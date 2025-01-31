using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Apoc.AnkiClient
{
    public class AnkiNoteField
    {
        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("order")]
        public int Order { get; set; }
    }
}
