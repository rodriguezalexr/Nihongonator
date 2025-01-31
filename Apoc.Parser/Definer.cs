using System;
using System.Collections.Generic;
using System.Linq;
using Wacton.Desu.Japanese;

namespace Apoc.Utils
{
    public class Definer
    {
        private List<IJapaneseEntry> _entries;

        public Definer()
        {
            var japaneseDictionary = new JapaneseDictionary();
            _entries = japaneseDictionary.GetEntries().ToList();
        }

        public bool HasDefinition(string searchString)
        {
            return _entries.Any(entry => entry.Kanjis.Any(kanji => kanji.Text == searchString));
        }

        //TODO:  Don't leak the Watcon def?
        public List<IJapaneseEntry> GetDefinitions(string searchString)
        {
            return _entries.Where(entry => entry.Kanjis.Any(kanji => kanji.Text == searchString)).Concat(_entries.Where(entry => entry.Readings.Any(reading => reading.Text == searchString))).ToList();
        }

        public IJapaneseEntry GetMostApplicableDefinition(string searchString)
        {
            var definitions = GetDefinitions(searchString);
            //Take our only option
            if (definitions.Count() == 1)
            {
                return definitions.FirstOrDefault();
            }
            //Catch the case where we have an exact match we should be using instead of the first match, ex: "娘" matching "嬢" first
            else if (definitions.Any(def => def.Kanjis.Any() && def.Kanjis.First().Text == searchString))
            {
                return definitions.First(def => def.Kanjis.Any() && def.Kanjis.First().Text == searchString);
            }
            else if (definitions.Any(def => def.Readings.Any() && def.Readings.Any(reading => reading.Text == searchString)))
            {
                return definitions.FirstOrDefault(def => def.Readings.Any() && def.Readings.Any(reading => reading.Text == searchString));
            }
            //Failover to the default
            else
            {
                return definitions.FirstOrDefault();
            }
        }

        public IKanji GetMostApplicableKanji(string searchString, IJapaneseEntry definition = null)
        {
            if (definition == null)
                definition = GetMostApplicableDefinition(searchString);

            return definition?.Kanjis.FirstOrDefault(kanji => kanji.Text == searchString);
        }

        public IReading GetMostApplicableReading(string searchString, IJapaneseEntry definition = null)
        {
            if (definition == null)
                definition = GetMostApplicableDefinition(searchString);

            return definition?.Readings.FirstOrDefault(reading => reading.Text == searchString);
        }
    }
}
