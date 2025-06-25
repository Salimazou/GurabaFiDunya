using System.Text.Json;
using System.Text.Json.Serialization;

namespace server.Services
{
    public class QuranDatasetDownloader
    {
        private readonly ILogger<QuranDatasetDownloader> _logger;
        private readonly HttpClient _httpClient;

        public QuranDatasetDownloader(ILogger<QuranDatasetDownloader> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Download complete Quran text from Tarteel's quran-json repository
        /// Perfect for ASR matching with no diacritics
        /// </summary>
        public async Task<bool> DownloadTarteelQuranJsonAsync(string outputPath = "Data/quran_complete.json")
        {
            try
            {
                _logger.LogInformation("📥 Downloading Tarteel Quran JSON dataset...");
                
                // Tarteel's quran-json repo (clean Arabic text without diacritics)
                var url = "https://raw.githubusercontent.com/tarteel-io/quran-json/master/quran.json";
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                await File.WriteAllTextAsync(outputPath, jsonContent);
                
                _logger.LogInformation("✅ Successfully downloaded Tarteel Quran dataset to {Path}", outputPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to download Tarteel Quran dataset");
                return false;
            }
        }

        /// <summary>
        /// Download from Quranic Universal Library (alternative source)
        /// </summary>
        public async Task<bool> DownloadQULDatasetAsync(string outputPath = "Data/quran_qul.json")
        {
            try
            {
                _logger.LogInformation("📥 Downloading QUL Quran dataset...");
                
                // QUL API endpoint (adjust based on their actual API)
                var url = "https://api.alquran.cloud/v1/quran/ar.alafasy"; // Example URL
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                var jsonContent = await response.Content.ReadAsStringAsync();
                
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                await File.WriteAllTextAsync(outputPath, jsonContent);
                
                _logger.LogInformation("✅ Successfully downloaded QUL Quran dataset to {Path}", outputPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to download QUL Quran dataset");
                return false;
            }
        }

        /// <summary>
        /// Create an optimized dataset specifically for Whisper ASR matching
        /// - No diacritics
        /// - Normalized Arabic text  
        /// - Word-level tokenization
        /// - Fast lookup structure
        /// </summary>
        public async Task<bool> CreateOptimizedASRDatasetAsync(
            string inputPath = "Data/quran_complete.json", 
            string outputPath = "Data/quran_asr_optimized.json")
        {
            try
            {
                _logger.LogInformation("🛠️ Creating optimized ASR dataset from {Input}...", inputPath);
                
                if (!File.Exists(inputPath))
                {
                    _logger.LogWarning("⚠️ Input file not found, downloading Tarteel dataset first...");
                    await DownloadTarteelQuranJsonAsync(inputPath);
                }
                
                var jsonContent = await File.ReadAllTextAsync(inputPath);
                var sourceData = JsonSerializer.Deserialize<List<TarteelEntry>>(jsonContent);
                
                if (sourceData == null)
                {
                    _logger.LogError("❌ Failed to parse source dataset");
                    return false;
                }
                
                var optimizedData = sourceData.Select(entry => new OptimizedASREntry
                {
                    SurahNumber = entry.Surah,
                    AyahNumber = entry.Ayah,
                    OriginalText = entry.Text,
                    CleanText = NormalizeForASR(entry.Text),
                    Words = NormalizeForASR(entry.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries),
                    WordCount = NormalizeForASR(entry.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                    SearchTerms = GenerateSearchTerms(entry.Text)
                }).ToList();
                
                var jsonOptions = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                var optimizedJson = JsonSerializer.Serialize(optimizedData, jsonOptions);
                await File.WriteAllTextAsync(outputPath, optimizedJson);
                
                _logger.LogInformation("✅ Created optimized ASR dataset with {Count} ayahs at {Path}", 
                    optimizedData.Count, outputPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to create optimized ASR dataset");
                return false;
            }
        }

        /// <summary>
        /// Download and setup complete dataset for production use
        /// </summary>
        public async Task<bool> SetupCompleteDatasetAsync()
        {
            try
            {
                _logger.LogInformation("🚀 Setting up complete Quran dataset...");
                
                var tasks = new List<Task<bool>>
                {
                    DownloadTarteelQuranJsonAsync("Data/quran_complete.json"),
                    // Add more sources if needed
                };
                
                var results = await Task.WhenAll(tasks);
                
                if (results.All(r => r))
                {
                    // Create optimized version for ASR
                    await CreateOptimizedASRDatasetAsync();
                    
                    _logger.LogInformation("✅ Complete dataset setup finished successfully");
                    return true;
                }
                else
                {
                    _logger.LogWarning("⚠️ Some dataset downloads failed, but continuing...");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to setup complete dataset");
                return false;
            }
        }

        private string NormalizeForASR(string arabicText)
        {
            if (string.IsNullOrEmpty(arabicText)) return "";

            // Remove all diacritics (Tashkeel)
            var normalized = System.Text.RegularExpressions.Regex.Replace(arabicText, @"[\u064B-\u065F\u0670\u0640]", "");
            
            // Normalize different forms of letters for better ASR matching
            normalized = normalized
                .Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا")  // Alif forms
                .Replace("ة", "ه")  // Ta marbuta
                .Replace("ى", "ي"); // Alif maksura
            
            // Remove punctuation and extra whitespace
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\u0600-\u06FF\s]", "");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
            
            return normalized;
        }

        private List<string> GenerateSearchTerms(string arabicText)
        {
            var normalized = NormalizeForASR(arabicText);
            var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            var searchTerms = new List<string> { normalized };
            
            // Add individual words for partial matching
            searchTerms.AddRange(words);
            
            // Add bigrams for phrase matching
            for (int i = 0; i < words.Length - 1; i++)
            {
                searchTerms.Add($"{words[i]} {words[i + 1]}");
            }
            
            return searchTerms.Distinct().ToList();
        }

        // Data models for different formats
        private class TarteelEntry
        {
            [JsonPropertyName("surah")]
            public int Surah { get; set; }
            
            [JsonPropertyName("ayah")]
            public int Ayah { get; set; }
            
            [JsonPropertyName("text")]
            public string Text { get; set; } = "";
        }

        private class OptimizedASREntry
        {
            public int SurahNumber { get; set; }
            public int AyahNumber { get; set; }
            public string OriginalText { get; set; } = "";
            public string CleanText { get; set; } = "";
            public string[] Words { get; set; } = Array.Empty<string>();
            public int WordCount { get; set; }
            public List<string> SearchTerms { get; set; } = new();
        }
    }
} 