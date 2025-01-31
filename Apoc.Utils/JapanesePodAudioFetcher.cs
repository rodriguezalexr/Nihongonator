using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Apoc.Utils
{
    public class JapanesePodAudioFetcher
    {
        public async Task<FetchAudioResult> FetchUniqueAudioForTerm(string kana, string kanji)
        {
            var client = new HttpClient();
            var kanaEncoded = System.Text.Encoding.UTF8.GetBytes(kana);
            var kanjiEncoded = System.Text.Encoding.UTF8.GetBytes(kanji);
            var kanaHex = "%" + BitConverter.ToString(kanaEncoded).Replace("-", "%");
            var kanjiHex = "%" + BitConverter.ToString(kanjiEncoded).Replace("-", "%");
            var result = await client.GetAsync($"https://assets.languagepod101.com/dictionary/japanese/audiomp3.php?kanji={kanjiHex}&kana={kanaHex}");

            string fileName = $"Apoc{kanji}.mp3";
            string fullFileName = @"C:\Nihongonator\Audio\" + fileName;
            using (var fs = new FileStream(fullFileName, FileMode.Create))
            {
                await  result.Content.CopyToAsync(fs);
            }

            var hash = GetHash(fullFileName);
            if (hash.Equals("7E-2C-2F-95-4E-F6-05-13-73-BA-91-6F-00-01-68-DC"))
                return new FetchAudioResult() { AudioExists = false };

            return new FetchAudioResult()
            {
                AudioExists = true,
                AudioFileName = fileName,
                PathToAudio = fullFileName
            };
        }

        public string GetHash(string fileName)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(fileName))
                {
                    var hash =  md5.ComputeHash(stream);
                    var str = BitConverter.ToString(hash);
                    return str;
                }
            }
        }

        public class FetchAudioResult
        {
            public bool AudioExists { get; set; }

            public string PathToAudio { get; set; }

            public string AudioFileName { get; set; }
        }
    }
}
