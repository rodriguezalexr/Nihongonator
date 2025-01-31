using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Apoc.AnkiClient
{
    public class ApocGenkiNoteWriteModel
    {
        [JsonProperty("Japanese")]
        public string Japanese { get; set; }

        [JsonProperty("Reading")]
        public string Reading { get; set; }

        [JsonProperty("English")]
        public string English { get; set; }

        [JsonProperty("Part of Speech")]
        public string PartOfSpeech { get; set; }

        [JsonProperty("Extra")]
        public string Extra { get; set; }

        [JsonProperty("Sentence")]
        public string Sentence { get; set; }

        [JsonProperty("Audio")]
        public string Audio { get; set; }

        [JsonProperty("SentenceAudio")]
        public string SentenceAudio { get; set; }

        [JsonProperty("Image")]
        public string Image { get; set; }

        [JsonProperty("ExtraImage")]
        public string ExtraImage { get; set; }
    }

    public class ApocGenkiNoteReadModel
    {
        [JsonProperty("Japanese")]
        public AnkiNoteField Japanese { get; set; }

        [JsonProperty("Reading")]
        public AnkiNoteField Reading { get; set; }

        [JsonProperty("English")]
        public AnkiNoteField English { get; set; }

        [JsonProperty("Part of Speech")]
        public AnkiNoteField PartOfSpeech { get; set; }

        [JsonProperty("Extra")]
        public AnkiNoteField Extra { get; set; }

        [JsonProperty("Sentence")]
        public AnkiNoteField Sentence { get; set; }

        [JsonProperty("Audio")]
        public AnkiNoteField Audio { get; set; }

        [JsonProperty("SentenceAudio")]
        public AnkiNoteField SentenceAudio { get; set; }

        [JsonProperty("Image")]
        public AnkiNoteField Image { get; set; }

        [JsonProperty("Frequency")]
        public AnkiNoteField Frequency { get; set; }

        [JsonProperty("ExtraImage")]
        public AnkiNoteField ExtraImage { get; set; }
    }
}
