using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Apoc.Utils
{
    public class JlptLevelManager
    {
        private Dictionary<string, int> _jlptLevels = new Dictionary<string, int>();

        public void Init()
        {
            var jlptData = ParseFile(@"c:\Nihongonator\JLPT.txt");
            foreach (var jlpt in jlptData)
            {
                var parts = jlpt.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Count() == 2)
                {
                    string strLevel = parts[0];
                    int level = -1;
                    switch (strLevel)
                    {
                        case "1":
                            level = 1;
                            break;
                        case "2":
                            level = 2;
                            break;
                        case "3":
                            level = 3;
                            break;
                        case "4":
                            level = 4;
                            break;
                        case "5":
                            level = 5;
                            break;
                    }

                    string word = parts[1];
                    if (level != -1 && !_jlptLevels.ContainsKey(word))
                        _jlptLevels.Add(word, level);
                }
            }
        }

        private List<string> ParseFile(string fileName)
        {
            string inputText = File.ReadAllText(fileName);

            var lines = inputText
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .ToList();

            return lines;
        }

        public int? GetJlptLevel (string input)
        {
            if (_jlptLevels.ContainsKey(input))
                return _jlptLevels[input];
            return null;
        }

        public List<string> GetVocabForLevel(int level)
        {
            return _jlptLevels.Where(kvp => kvp.Value == level).Select(kvp => kvp.Key).ToList();
        }
    }
}
