using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Apoc.AnkiClient
{
    public class AnkiRequest
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }
    }

    public class AnkiRequestWithParameters : AnkiRequest
    {

        [JsonProperty("params")]
        public object Parameters { get; set; }
    }
}
