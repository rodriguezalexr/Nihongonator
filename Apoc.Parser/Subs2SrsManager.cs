using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Apoc.Utils
{
    public class Subs2SrsManager
    {
        private static Parser _parser;

        public Subs2SrsManager(Parser parser)
        {
            _parser = parser;
        }

        public void ProcessTsv(string inputFileName, string outputFileName)
        {
            var tokenizedLines = new List<WriteObject>();
            var lines = ReadFromSubs2SrsFile(inputFileName);
            var writeObjects = new List<WriteObject>();
            int i = 0;
            foreach (var line in lines.Take(50))
            {
                var tokens = _parser.Tokenize(line.Japanese).ToList();
                var tokenRootTexts = tokens.Select(t => t.RootText).ToList();
                var writeObject = new WriteObject(line, tokenRootTexts);
                writeObjects.Add(writeObject);

                i++;
                Console.WriteLine($"Processed line {i} of {lines.Count}");
            }

            var config = new CsvConfiguration(CultureInfo.InvariantCulture);
            config.HeaderValidated = null;
            config.Delimiter = "\t";
            config.BadDataFound = null;

            using (var writer = new StreamWriter(outputFileName))
            using (var csv = new CsvWriter(writer, config))
            {
                csv.WriteRecords(writeObjects);
            }
        }

        private List<ReadObject> ReadFromSubs2SrsFile(string filename)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture);
            config.HeaderValidated = null;
            config.Delimiter = "\t";
            config.BadDataFound = null;
            config.HasHeaderRecord = false;
            using (var reader = new StreamReader(filename))
            {
                using (var csv = new CsvReader(reader, config))
                {
                    var cards = csv.GetRecords<ReadObject>().ToList();
                    return cards;
                }
            }
        }

        private class ReadObject
        {
            public string Episode { get; set; }
            public string Timing { get; set; }
            public string Audio { get; set; }
            public string Image { get; set; }
            public string Japanese { get; set; }
        }

        private class WriteObject
        {
            public WriteObject() { }

            public WriteObject(ReadObject readObject, List<string> tokens)
            {
                Episode = readObject.Episode;
                Timing = readObject.Timing;
                Japanese = readObject.Japanese;
                Tokens = tokens;
            }

            public string Episode { get; set; }
            public string Timing { get; set; }
            public string Japanese { get; set; }
            public List<string> Tokens { get; set; }
        }
    }
}
