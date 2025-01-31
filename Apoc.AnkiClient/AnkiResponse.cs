using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Apoc.AnkiClient
{
    public class AnkiResponse<T>
    {
        [JsonProperty("result")]
        public T Result { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        public bool Succeeded => string.IsNullOrWhiteSpace(Error);

        public AnkiResponse()
        {
        }
    }
}
