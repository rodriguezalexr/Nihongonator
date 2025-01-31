using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Apoc.Utils
{
    public class FrequencyManager
    {
        private Dictionary<string, int> _frequencyList;

        public void Init()
        {
            string fileEntries = File.ReadAllText(@"C:\Nihongonator\AnimeDramaFrequencyList.json");
            var fullArray = JsonConvert.DeserializeObject<object[][]>(fileEntries);
            _frequencyList = fullArray.ToDictionary(subArray => subArray[0].ToString(), subArray => Convert.ToInt32(subArray[2]));
        }

        public int? GetFrequency(string token)
        {
            if (_frequencyList.ContainsKey(token))
                return _frequencyList[token];
            return null;
        }

        public Dictionary<string, int> GetFrequenciesExcept(List<string> except)
        {
            return _frequencyList.Where(kvp => !except.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
