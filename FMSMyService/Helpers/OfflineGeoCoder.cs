using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FMSISSTransactionService.Models;

namespace FMSISSTransactionService.Helpers
{
    public class OfflineGeoCoder
    {
        private readonly List<City> _majorCities;

        // Allowed character swaps for “almost identical” words (ONLY 1 position changed)
        private static readonly HashSet<(char, char)> AllowedSwapPairs = new HashSet<(char, char)>
        {
            ('a', 'e'), ('e', 'a'),
            ('i', 'e'), ('e', 'i'),
            ('o', 'u'), ('u', 'o')
        };

        // ---------------- Correction rules ----------------
        private static readonly Dictionary<(char, char), char> CorrectionRules = new Dictionary<(char, char), char>
        {
            { ('e', 'a'), 'a' }, { ('a', 'e'), 'a' },
            { ('o', 'u'), 'u' }, { ('u', 'o'), 'u' },
            { ('i', 'e'), 'e' }, { ('e', 'i'), 'e' },

        };

        private static readonly HashSet<string> MajorCities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "karachi", "lahore", "islamabad", "quetta", "peshawar", "faisalabad"
        };
        private static readonly HashSet<string> AllowedProvinces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Azad Jammu and Kashmir", "Gilgit-Baltistan", "Islamabad","Balochistan"
        };
        private static readonly HashSet<string> Disallowedareas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Islamic Republic Of Pakistan", "Jhang City", "Jhang Sadr","Lakki","Malir Cantonment",
            "Nh&Mp Beat-34 Nooriabad","Rahimyar Khan","Shekhupura","Attock City","Korangi","Malir","Eidghah"
        };
        public OfflineGeoCoder()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(basePath, "MapData", "PK.txt");

            // We only load the major cities once
            _majorCities = LoadMajorCitiesOnly(path);
        }

        public List<City> LoadMajorCitiesOnly(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<City>();

            var entries = File.ReadLines(filePath)
                .Select(l => l.Split('\t'))
                .Where(p => p.Length >= 15)
                .Select(p => new
                {
                    feature = p[7],
                    pop = int.TryParse(p[14], out var pp) ? pp : 0,
                    row = p
                })
                .Where(x => x.feature == "PPLC" || x.feature == "PPLA" || x.feature == "PPLA2" || (x.pop > 100000))
                .Select(x =>
                {
                    var p = x.row;
                    return new City
                    {
                        GeoNameId = int.Parse(p[0]),
                        RawName = p[1],
                        BaseName = NormalizeBaseName(p[1]),
                        Latitude = double.Parse(p[4], CultureInfo.InvariantCulture),
                        Longitude = double.Parse(p[5], CultureInfo.InvariantCulture),
                        Admin1Code = p[10],
                        FeatureCode = x.feature,
                        Population = x.pop
                    };
                })
                .ToList();

            var groups = entries.GroupBy(c => c.BaseName, StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var renameMap = BuildRenameMap(groups);

            // Final collapse + selection
            var finalresult = entries
                .Select(c =>
                {
                    var baseName = renameMap.TryGetValue(c.BaseName, out var m) ? m : c.BaseName;
                    c.Name = ToDisplayName(baseName);
                    return c;
                })
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.Population).First())
                .Where(c => !IsProvince(c) && !Isarea(c)) // remove provinces not in whitelist
                .ToList();

            return finalresult;
        }

        // ------------------ CLEAN HELPERS ------------------

        private bool IsProvince(City c)
        {
            // FeatureCode "ADM1" indicates a province
            return c.FeatureCode.Equals("ADM1", StringComparison.OrdinalIgnoreCase)
                   && !AllowedProvinces.Contains(c.RawName); // remove if not in whitelist
        }

        private bool Isarea(City c)
        {
            return Disallowedareas.Contains(c.Name);
        }

        private Dictionary<string, string> BuildRenameMap(Dictionary<string, List<City>> groups)
        {
            var names = groups.Keys.ToList();
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < names.Count; i++)
            {
                for (int j = i + 1; j < names.Count; j++)
                {
                    var a = names[i];
                    var b = names[j];
                    if (map.ContainsKey(a) || map.ContainsKey(b)) continue;

                    if (AreSimilarExceptOneChar(a, b, out int diffIndex))
                    {
                        char ca = a[diffIndex];
                        char cb = b[diffIndex];

                        char correct = GetCorrectCharacter(ca, cb);

                        string canonical = ReplaceChar(a, diffIndex, correct);

                        map[a] = canonical;
                        map[b] = canonical;
                    }

                }
            }

            // Collapse indirect mappings
            foreach (var k in map.Keys.ToList())
            {
                string c = map[k];
                while (map.TryGetValue(c, out var next) && next != c)
                    c = next;

                map[k] = c;
            }

            return map;
        }

        private char GetCorrectCharacter(char a, char b)
        {
            if (CorrectionRules.TryGetValue((char.ToLower(a), char.ToLower(b)), out var correct))
                return correct;

            // fallback if no rule defined
            return a;
        }

        private string ReplaceChar(string word, int index, char newChar)
        {
            var arr = word.ToCharArray();
            arr[index] = newChar;
            return new string(arr);
        }
        private bool AreSimilarExceptOneChar(string a, string b, out int diffIndex)
        {
            diffIndex = -1;

            if (a.Length != b.Length)
                return false;

            int diffCount = 0;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    diffCount++;
                    if (diffCount > 1)
                        return false;

                    diffIndex = i;

                    if (!AllowedSwapPairs.Contains((a[i], b[i])))
                        return false;
                }
            }

            return diffCount == 1;
        }

        private string NormalizeBaseName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            string cleaned = RemoveDiacritics(name);

            foreach (var s in new[] { " District", " Division", " Tehsil", " Town" })
            {
                if (cleaned.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned.Substring(0, cleaned.Length - s.Length);
                }
            }

            var lower = cleaned.Split(' ').ToList();
            if (MajorCities.Contains(lower[0]))
            {
                foreach (var s in new[] { " Central", " East", " West", " South", " North" })
                {
                    if (cleaned.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                    {
                        cleaned = cleaned.Substring(0, cleaned.Length - s.Length);
                    }
                }

            }

            return Regex.Replace(cleaned, @"\s+", " ").Trim().ToLowerInvariant();
        }

        private string RemoveDiacritics(string text)
        {
            return new string(
                text.Normalize(NormalizationForm.FormD)
                    .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    .ToArray()
                ).Normalize(NormalizationForm.FormC);
        }

        private string ToDisplayName(string name) =>
            CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);


        public string GetCityName(double lat, double lon)
        {


            // Rough bounding box for Pakistan
            const double minLat = 23.5;
            const double maxLat = 37.0;
            const double minLon = 60.8;
            const double maxLon = 77.0;

            if (lat < minLat || lat > maxLat || lon < minLon || lon > maxLon)
                return null; // outside Pakistan

            var nearestCity = _majorCities
                .OrderBy(c => HaversineDistance(lat, lon, c.Latitude, c.Longitude))
                .FirstOrDefault();

            if (nearestCity == null) return null;

            return nearestCity.Name;

        }

        private double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371; // km
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180.0) *
                    Math.Cos(lat2 * Math.PI / 180.0) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
            return R * c;
        }
    }
}
