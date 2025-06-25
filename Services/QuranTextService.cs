using server.Models;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Text;

namespace server.Services
{
    public class QuranTextService
    {
        private readonly ILogger<QuranTextService> _logger;
        private readonly List<QuranSurah> _quranData;
        private readonly Dictionary<string, QuranAyah> _ayahIndex;

        public QuranTextService(ILogger<QuranTextService> logger)
        {
            _logger = logger;
            _quranData = new List<QuranSurah>();
            _ayahIndex = new Dictionary<string, QuranAyah>();
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("📖 Loading Quran text data...");
                
                // Try loading the enhanced Tarteel/QUL format first
                var enhancedDataPath = Path.Combine("Data", "quran_complete.json");
                var basicDataPath = Path.Combine("Data", "quran.json");
                
                List<QuranSurah>? quranData = null;
                
                if (File.Exists(enhancedDataPath))
                {
                    _logger.LogInformation("📚 Loading enhanced Quran dataset from {Path}", enhancedDataPath);
                    quranData = await LoadEnhancedQuranDataAsync(enhancedDataPath);
                }
                else if (File.Exists(basicDataPath))
                {
                    _logger.LogInformation("📖 Loading basic Quran dataset from {Path}", basicDataPath);
                    var jsonContent = await File.ReadAllTextAsync(basicDataPath);
                    quranData = JsonSerializer.Deserialize<List<QuranSurah>>(jsonContent);
                }
                else
                {
                    _logger.LogWarning("⚠️ No Quran data files found. Creating sample data...");
                    await CreateSampleQuranData(basicDataPath);
                    var jsonContent = await File.ReadAllTextAsync(basicDataPath);
                    quranData = JsonSerializer.Deserialize<List<QuranSurah>>(jsonContent);
                }

                if (quranData != null)
                {
                    _quranData.AddRange(quranData);
                    BuildAyahIndex();
                    _logger.LogInformation("✅ Loaded {SurahCount} surahs with {AyahCount} ayahs", 
                        _quranData.Count, _ayahIndex.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize Quran text service");
                throw;
            }
        }

        /// <summary>
        /// Load enhanced Quran data from Tarteel/QUL format
        /// Supports multiple data formats for maximum compatibility
        /// </summary>
        private async Task<List<QuranSurah>?> LoadEnhancedQuranDataAsync(string filePath)
        {
            try
            {
                var jsonContent = await File.ReadAllTextAsync(filePath);
                
                // Try different data formats
                
                // 1. Try your nested format first 
                try
                {
                    var nestedFormat = JsonSerializer.Deserialize<List<NestedQuranSurah>>(jsonContent);
                    if (nestedFormat?.Any() == true && nestedFormat.First().Verses?.Any() == true)
                    {
                        _logger.LogInformation("✅ Converting from nested format ({Count} surahs)", nestedFormat.Count);
                        return ConvertFromNestedFormat(nestedFormat);
                    }
                }
                catch { /* Try next format */ }
                
                // 2. Try standard format (our current format)
                try
                {
                    var standardFormat = JsonSerializer.Deserialize<List<QuranSurah>>(jsonContent);
                    if (standardFormat?.Any() == true)
                    {
                        _logger.LogInformation("✅ Loaded standard format Quran data");
                        return standardFormat;
                    }
                }
                catch { /* Try next format */ }
                
                // 3. Try Tarteel flat format: [{"surah": 1, "ayah": 1, "text": "..."}]
                try
                {
                    var tarteelFormat = JsonSerializer.Deserialize<List<TarteelAyahEntry>>(jsonContent);
                    if (tarteelFormat?.Any() == true)
                    {
                        _logger.LogInformation("✅ Converting from Tarteel flat format ({Count} ayahs)", tarteelFormat.Count);
                        return ConvertFromTarteelFormat(tarteelFormat);
                    }
                }
                catch { /* Try next format */ }
                
                // 4. Try QUL format with different structure
                try
                {
                    var qulFormat = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
                    if (qulFormat?.ContainsKey("surahs") == true)
                    {
                        _logger.LogInformation("✅ Loading QUL format Quran data");
                        return ConvertFromQULFormat(qulFormat);
                    }
                }
                catch { /* Continue */ }
                
                _logger.LogWarning("⚠️ Could not parse enhanced Quran data format, falling back to basic format");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to load enhanced Quran data from {Path}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Convert from Tarteel flat format to our structured format
        /// </summary>
        private List<QuranSurah> ConvertFromTarteelFormat(List<TarteelAyahEntry> tarteelData)
        {
            var surahs = new Dictionary<int, QuranSurah>();
            
            foreach (var entry in tarteelData)
            {
                if (!surahs.ContainsKey(entry.Surah))
                {
                    surahs[entry.Surah] = new QuranSurah
                    {
                        Number = entry.Surah,
                        Name = GetSurahName(entry.Surah),
                        ArabicName = GetSurahArabicName(entry.Surah),
                        Translation = GetSurahTranslation(entry.Surah),
                        TotalAyahs = GetSurahTotalAyahs(entry.Surah),
                        Ayahs = new List<QuranAyah>()
                    };
                }
                
                var ayah = new QuranAyah
                {
                    SurahNumber = entry.Surah,
                    AyahNumber = entry.Ayah,
                    ArabicText = entry.Text,
                    ArabicTextNoDiacritics = NormalizeArabicText(entry.Text),
                    Translation = entry.Translation ?? "",
                    Words = entry.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    WordsNoDiacritics = NormalizeArabicText(entry.Text)
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
                };
                
                surahs[entry.Surah].Ayahs.Add(ayah);
            }
            
            // Sort ayahs within each surah
            foreach (var surah in surahs.Values)
            {
                surah.Ayahs = surah.Ayahs.OrderBy(a => a.AyahNumber).ToList();
            }
            
            return surahs.Values.OrderBy(s => s.Number).ToList();
        }

        /// <summary>
        /// Convert from your nested format to our structured format
        /// </summary>
        private List<QuranSurah> ConvertFromNestedFormat(List<NestedQuranSurah> nestedData)
        {
            var surahs = new List<QuranSurah>();
            
            foreach (var nested in nestedData)
            {
                var surah = new QuranSurah
                {
                    Number = nested.Id,
                    Name = nested.Transliteration,
                    ArabicName = nested.Name,
                    Translation = nested.Transliteration,
                    TotalAyahs = nested.TotalVerses,
                    Ayahs = new List<QuranAyah>()
                };
                
                foreach (var verse in nested.Verses)
                {
                    var ayah = new QuranAyah
                    {
                        SurahNumber = nested.Id,
                        AyahNumber = verse.Id,
                        ArabicText = verse.Text,
                        ArabicTextNoDiacritics = NormalizeArabicText(verse.Text),
                        Translation = "",
                        Words = verse.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
                        WordsNoDiacritics = NormalizeArabicText(verse.Text)
                            .Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
                    };
                    
                    surah.Ayahs.Add(ayah);
                }
                
                surahs.Add(surah);
            }
            
            return surahs.OrderBy(s => s.Number).ToList();
        }

        /// <summary>
        /// Convert from QUL format (if needed)
        /// </summary>
        private List<QuranSurah> ConvertFromQULFormat(Dictionary<string, object> qulData)
        {
            // Implementation depends on exact QUL format structure
            // This is a placeholder - adjust based on actual QUL JSON structure
            throw new NotImplementedException("QUL format conversion not implemented yet");
        }

        // Helper methods for Surah metadata (you can expand these with complete data)
        private string GetSurahName(int surahNumber) => surahNumber switch
        {
            1 => "Al-Fatiha",
            2 => "Al-Baqarah", 
            3 => "Ali 'Imran",
            // Add all 114 surahs...
            _ => $"Surah {surahNumber}"
        };

        private string GetSurahArabicName(int surahNumber) => surahNumber switch
        {
            1 => "الفاتحة",
            2 => "البقرة",
            3 => "آل عمران", 
            // Add all 114 surahs...
            _ => $"سورة {surahNumber}"
        };

        private string GetSurahTranslation(int surahNumber) => surahNumber switch
        {
            1 => "The Opening",
            2 => "The Cow",
            3 => "Family of Imran",
            // Add all 114 surahs...
            _ => $"Chapter {surahNumber}"
        };

        private int GetSurahTotalAyahs(int surahNumber) => surahNumber switch
        {
            1 => 7, 2 => 286, 3 => 200, 4 => 176, 5 => 120,
            // Add all 114 surahs...
            _ => 1 // Default fallback
        };

        // Data models for different formats
        private class TarteelAyahEntry
        {
            [JsonPropertyName("surah")]
            public int Surah { get; set; }
            
            [JsonPropertyName("ayah")]  
            public int Ayah { get; set; }
            
            [JsonPropertyName("text")]
            public string Text { get; set; } = "";
            
            [JsonPropertyName("translation")]
            public string? Translation { get; set; }
        }

        // Model for your nested format
        private class NestedQuranSurah
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }
            
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("transliteration")]
            public string Transliteration { get; set; } = "";
            
            [JsonPropertyName("type")]
            public string Type { get; set; } = "";
            
            [JsonPropertyName("total_verses")]
            public int TotalVerses { get; set; }
            
            [JsonPropertyName("verses")]
            public List<NestedVerse> Verses { get; set; } = new();
        }

        private class NestedVerse
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }
            
            [JsonPropertyName("text")]
            public string Text { get; set; } = "";
        }

        private void BuildAyahIndex()
        {
            _ayahIndex.Clear();
            
            foreach (var surah in _quranData)
            {
                foreach (var ayah in surah.Ayahs)
                {
                    var key = $"{ayah.SurahNumber}:{ayah.AyahNumber}";
                    _ayahIndex[key] = ayah;
                }
            }
        }

        public async Task<QuranAyah?> GetAyahAsync(int surahNumber, int ayahNumber)
        {
            var key = $"{surahNumber}:{ayahNumber}";
            return _ayahIndex.TryGetValue(key, out var ayah) ? ayah : null;
        }

        public async Task<QuranSurah?> GetSurahAsync(int surahNumber)
        {
            return _quranData.FirstOrDefault(s => s.Number == surahNumber);
        }

        public async Task<QuranAyah?> FindSimilarAyahAsync(string text, double threshold = 0.6)
        {
            var normalizedInput = NormalizeArabicText(text);
            var words = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            QuranAyah? bestMatch = null;
            double highestScore = 0;

            foreach (var ayah in _ayahIndex.Values)
            {
                var similarity = CalculateTextSimilarity(words, ayah.WordsNoDiacritics.ToArray());
                
                if (similarity > threshold && similarity > highestScore)
                {
                    highestScore = similarity;
                    bestMatch = ayah;
                }
            }

            if (bestMatch != null)
            {
                _logger.LogDebug("🔍 Found similar ayah {Surah}:{Ayah} with similarity {Score:F2}", 
                    bestMatch.SurahNumber, bestMatch.AyahNumber, highestScore);
            }

            return bestMatch;
        }

        /// <summary>
        /// Enhanced fuzzy matching specifically optimized for Whisper ASR transcripts
        /// Uses multiple similarity algorithms for better accuracy
        /// </summary>
        public async Task<(QuranAyah? ayah, double confidence, List<string> matchedWords)> FindBestWhisperMatchAsync(
            string whisperTranscript, 
            int? expectedSurah = null, 
            int? expectedAyah = null, 
            double threshold = 0.65)
        {
            var normalizedTranscript = NormalizeArabicText(whisperTranscript);
            var transcriptWords = normalizedTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (transcriptWords.Length == 0)
                return (null, 0.0, new List<string>());

            var candidates = GetSearchCandidates(expectedSurah, expectedAyah);
            
            QuranAyah? bestMatch = null;
            double highestScore = 0;
            List<string> bestMatchedWords = new();

            foreach (var ayah in candidates)
            {
                var (similarity, matchedWords) = CalculateWhisperSimilarity(transcriptWords, ayah.WordsNoDiacritics.ToArray());
                
                if (similarity > threshold && similarity > highestScore)
                {
                    highestScore = similarity;
                    bestMatch = ayah;
                    bestMatchedWords = matchedWords;
                }
            }

            if (bestMatch != null)
            {
                _logger.LogDebug("🎯 Whisper match: {Transcript} → {Surah}:{Ayah} (confidence: {Score:F2})", 
                    whisperTranscript, bestMatch.SurahNumber, bestMatch.AyahNumber, highestScore);
            }

            return (bestMatch, highestScore, bestMatchedWords);
        }

        /// <summary>
        /// Get search candidates - if expected surah/ayah provided, search nearby first
        /// </summary>
        private IEnumerable<QuranAyah> GetSearchCandidates(int? expectedSurah, int? expectedAyah)
        {
            if (expectedSurah.HasValue)
            {
                // Search in expected surah first, then expand
                var surahAyahs = _ayahIndex.Values
                    .Where(a => a.SurahNumber == expectedSurah.Value)
                    .OrderBy(a => expectedAyah.HasValue ? Math.Abs(a.AyahNumber - expectedAyah.Value) : a.AyahNumber);
                
                // Then search in nearby surahs
                var nearbySurahAyahs = _ayahIndex.Values
                    .Where(a => a.SurahNumber != expectedSurah.Value && 
                               Math.Abs(a.SurahNumber - expectedSurah.Value) <= 2);
                
                return surahAyahs.Concat(nearbySurahAyahs);
            }
            
            return _ayahIndex.Values;
        }

        /// <summary>
        /// Advanced similarity calculation optimized for Whisper ASR results
        /// Combines multiple similarity metrics for better accuracy
        /// </summary>
        private (double similarity, List<string> matchedWords) CalculateWhisperSimilarity(string[] transcriptWords, string[] ayahWords)
        {
            if (transcriptWords.Length == 0 || ayahWords.Length == 0) 
                return (0, new List<string>());

            var matchedWords = new List<string>();
            
            // 1. Exact word matches (weighted heavily)
            var exactMatches = CountExactWordMatches(transcriptWords, ayahWords, matchedWords);
            
            // 2. Fuzzy word matches using Levenshtein (for ASR errors)
            var fuzzyMatches = CountFuzzyWordMatches(transcriptWords, ayahWords, matchedWords);
            
            // 3. Sequential similarity (order matters in Quran)
            var sequentialSimilarity = CalculateSequentialSimilarity(transcriptWords, ayahWords);
            
            // 4. Length similarity (prevents matching very different length ayahs)
            var lengthSimilarity = 1.0 - Math.Abs(transcriptWords.Length - ayahWords.Length) / (double)Math.Max(transcriptWords.Length, ayahWords.Length);
            
            // Combined weighted score
            var totalWords = Math.Max(transcriptWords.Length, ayahWords.Length);
            var exactWeight = 0.4;
            var fuzzyWeight = 0.3;
            var sequentialWeight = 0.2;
            var lengthWeight = 0.1;
            
            var similarity = (exactMatches * exactWeight + fuzzyMatches * fuzzyWeight + 
                             sequentialSimilarity * sequentialWeight + lengthSimilarity * lengthWeight);
            
            return (similarity, matchedWords);
        }

        private double CountExactWordMatches(string[] transcript, string[] ayah, List<string> matchedWords)
        {
            var matches = 0;
            var used = new bool[ayah.Length];
            
            foreach (var word in transcript)
            {
                for (int i = 0; i < ayah.Length; i++)
                {
                    if (!used[i] && word.Equals(ayah[i], StringComparison.OrdinalIgnoreCase))
                    {
                        matches++;
                        used[i] = true;
                        matchedWords.Add(word);
                        break;
                    }
                }
            }
            
            return (double)matches / Math.Max(transcript.Length, ayah.Length);
        }

        private double CountFuzzyWordMatches(string[] transcript, string[] ayah, List<string> matchedWords)
        {
            var fuzzyMatches = 0;
            var used = new bool[ayah.Length];
            
            foreach (var word in transcript)
            {
                var bestMatch = -1;
                var bestSimilarity = 0.0;
                
                for (int i = 0; i < ayah.Length; i++)
                {
                    if (!used[i])
                    {
                        var similarity = CalculateWordSimilarity(word, ayah[i]);
                        if (similarity > 0.75 && similarity > bestSimilarity) // High threshold for fuzzy matches
                        {
                            bestSimilarity = similarity;
                            bestMatch = i;
                        }
                    }
                }
                
                if (bestMatch >= 0)
                {
                    fuzzyMatches++;
                    used[bestMatch] = true;
                    if (!matchedWords.Contains(word))
                        matchedWords.Add(word);
                }
            }
            
            return (double)fuzzyMatches / Math.Max(transcript.Length, ayah.Length);
        }

        private double CalculateSequentialSimilarity(string[] transcript, string[] ayah)
        {
            if (transcript.Length == 0 || ayah.Length == 0) return 0;
            
            // Use longest common subsequence algorithm
            var dp = new int[transcript.Length + 1, ayah.Length + 1];
            
            for (int i = 1; i <= transcript.Length; i++)
            {
                for (int j = 1; j <= ayah.Length; j++)
                {
                    if (CalculateWordSimilarity(transcript[i-1], ayah[j-1]) > 0.8)
                    {
                        dp[i, j] = dp[i-1, j-1] + 1;
                    }
                    else
                    {
                        dp[i, j] = Math.Max(dp[i-1, j], dp[i, j-1]);
                    }
                }
            }
            
            return (double)dp[transcript.Length, ayah.Length] / Math.Max(transcript.Length, ayah.Length);
        }

        /// <summary>
        /// Find the exact position of words within an ayah for precise error detection
        /// </summary>
        public async Task<List<(int wordIndex, string expectedWord, string heardWord, double confidence)>> 
            AlignWordsWithAyahAsync(string[] whisperWords, QuranAyah ayah)
        {
            var alignments = new List<(int, string, string, double)>();
            var ayahWords = ayah.WordsNoDiacritics.ToArray();
            
            var usedAyahIndices = new bool[ayahWords.Length];
            
            for (int i = 0; i < whisperWords.Length; i++)
            {
                var whisperWord = NormalizeArabicText(whisperWords[i]);
                var bestMatchIndex = -1;
                var bestSimilarity = 0.0;
                
                // Find best matching unused word in ayah
                for (int j = 0; j < ayahWords.Length; j++)
                {
                    if (!usedAyahIndices[j])
                    {
                        var similarity = CalculateWordSimilarity(whisperWord, ayahWords[j]);
                        if (similarity > bestSimilarity)
                        {
                            bestSimilarity = similarity;
                            bestMatchIndex = j;
                        }
                    }
                }
                
                if (bestMatchIndex >= 0 && bestSimilarity > 0.6)
                {
                    usedAyahIndices[bestMatchIndex] = true;
                    alignments.Add((bestMatchIndex, ayahWords[bestMatchIndex], whisperWord, bestSimilarity));
                }
                else
                {
                    // Insertion error - extra word not in ayah
                    alignments.Add((-1, "", whisperWord, 0.0));
                }
            }
            
            // Check for omitted words (in ayah but not spoken)
            for (int j = 0; j < ayahWords.Length; j++)
            {
                if (!usedAyahIndices[j])
                {
                    alignments.Add((j, ayahWords[j], "", 0.0)); // Omission
                }
            }
            
            return alignments.OrderBy(a => a.Item1).ToList();
        }

        public async Task<List<QuranAyah>> SearchAyahsAsync(string query, int maxResults = 10)
        {
            var normalizedQuery = NormalizeArabicText(query);
            var results = new List<(QuranAyah ayah, double score)>();

            foreach (var ayah in _ayahIndex.Values)
            {
                if (ayah.ArabicTextNoDiacritics.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    var score = CalculateTextSimilarity(
                        normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries),
                        ayah.WordsNoDiacritics.ToArray());
                    
                    results.Add((ayah, score));
                }
            }

            return results
                .OrderByDescending(r => r.score)
                .Take(maxResults)
                .Select(r => r.ayah)
                .ToList();
        }

        private string NormalizeArabicText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // First, normalize to NFD form to separate base characters from diacritics
            text = text.Normalize(NormalizationForm.FormD);

            // Remove ALL diacritics and modifier marks (comprehensive range)
            var normalized = Regex.Replace(text, @"[\u064B-\u065F\u0670\u06D6-\u06ED\u08E3-\u08FE\u0640\u06E5\u06E6]", "");
            
            // Normalize different forms of letters
            normalized = normalized
                .Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا")  // Alif variants
                .Replace("ة", "ه")  // Taa marbuta to haa
                .Replace("ى", "ي")  // Alif maksura to yaa
                .Replace("ٱ", "ا")  // Alif wasla to regular alif
                .Replace("ء", "")   // Remove hamza
                .Replace("ئ", "ي")  // Yaa with hamza above to yaa
                .Replace("ؤ", "و")  // Waw with hamza above to waw
                .Replace("لله", "الله"); // Special case for Allah

            // Remove ALL remaining diacritics, marks, and special characters
            normalized = Regex.Replace(normalized, @"[^\u0621-\u063A\u0641-\u064A\s]", "");
            
            // Clean up whitespace
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            return normalized.ToLowerInvariant();
        }

        private double CalculateTextSimilarity(string[] words1, string[] words2)
        {
            if (words1.Length == 0 || words2.Length == 0) return 0;

            var matches = 0;
            var totalWords = Math.Max(words1.Length, words2.Length);

            // Simple word-by-word comparison
            for (int i = 0; i < Math.Min(words1.Length, words2.Length); i++)
            {
                if (CalculateWordSimilarity(words1[i], words2[i]) > 0.7)
                {
                    matches++;
                }
            }

            return (double)matches / totalWords;
        }

        private double CalculateWordSimilarity(string word1, string word2)
        {
            if (string.IsNullOrEmpty(word1) || string.IsNullOrEmpty(word2))
                return 0;

            var cleaned1 = word1.ToLowerInvariant().Trim();
            var cleaned2 = word2.ToLowerInvariant().Trim();

            if (cleaned1 == cleaned2) return 1.0;
            
            // Levenshtein distance for better similarity calculation
            return 1.0 - (double)LevenshteinDistance(cleaned1, cleaned2) / Math.Max(cleaned1.Length, cleaned2.Length);
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
            if (string.IsNullOrEmpty(s2)) return s1.Length;

            var distance = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                distance[i, 0] = i;

            for (int j = 0; j <= s2.Length; j++)
                distance[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[s1.Length, s2.Length];
        }

        private async Task CreateSampleQuranData(string dataPath)
        {
            // Create sample data for Al-Fatiha (first surah)
            var sampleData = new List<QuranSurah>
            {
                new QuranSurah
                {
                    Number = 1,
                    Name = "Al-Fatiha",
                    ArabicName = "الفاتحة",
                    Translation = "The Opening",
                    TotalAyahs = 7,
                    Ayahs = new List<QuranAyah>
                    {
                        new QuranAyah
                        {
                            SurahNumber = 1,
                            AyahNumber = 1,
                            ArabicText = "بِسْمِ اللَّهِ الرَّحْمَنِ الرَّحِيمِ",
                            ArabicTextNoDiacritics = "بسم الله الرحمن الرحيم",
                            Translation = "In the name of Allah, the Entirely Merciful, the Especially Merciful.",
                            Words = new List<string> { "بِسْمِ", "اللَّهِ", "الرَّحْمَنِ", "الرَّحِيمِ" },
                            WordsNoDiacritics = new List<string> { "بسم", "الله", "الرحمن", "الرحيم" }
                        },
                        new QuranAyah
                        {
                            SurahNumber = 1,
                            AyahNumber = 2,
                            ArabicText = "الْحَمْدُ لِلَّهِ رَبِّ الْعَالَمِينَ",
                            ArabicTextNoDiacritics = "الحمد لله رب العالمين",
                            Translation = "[All] praise is [due] to Allah, Lord of the worlds -",
                            Words = new List<string> { "الْحَمْدُ", "لِلَّهِ", "رَبِّ", "الْعَالَمِينَ" },
                            WordsNoDiacritics = new List<string> { "الحمد", "لله", "رب", "العالمين" }
                        }
                        // Add more ayahs as needed...
                    }
                }
            };

            Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);
            var jsonContent = JsonSerializer.Serialize(sampleData, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            
            await File.WriteAllTextAsync(dataPath, jsonContent);
            _logger.LogInformation("📝 Created sample Quran data at {Path}", dataPath);
        }

        public IEnumerable<QuranSurah> GetAllSurahs()
        {
            return _quranData.AsReadOnly();
        }

        public async Task<List<QuranAyah>> GetAyahsInRangeAsync(int surahNumber, int fromAyah, int toAyah)
        {
            var surah = await GetSurahAsync(surahNumber);
            if (surah == null) return new List<QuranAyah>();

            return surah.Ayahs
                .Where(a => a.AyahNumber >= fromAyah && a.AyahNumber <= toAyah)
                .ToList();
        }
    }
} 