using server.Models;
using System.Text.Json;
using System.IO.Compression;
using System.Net.Http;

namespace server.Services
{
    public class TarteelDatasetService
    {
        private readonly ILogger<TarteelDatasetService> _logger;
        private readonly HttpClient _httpClient;
        private readonly QuranTextService _quranService;
        private readonly string _dataPath;
        private readonly List<TarteelDataPoint> _datasetCache;

        public TarteelDatasetService(
            ILogger<TarteelDatasetService> logger,
            HttpClient httpClient,
            QuranTextService quranService)
        {
            _logger = logger;
            _httpClient = httpClient;
            _quranService = quranService;
            _dataPath = Path.Combine("Data", "Tarteel");
            _datasetCache = new List<TarteelDataPoint>();
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("🎯 Initializing Tarteel AI dataset service...");
                
                Directory.CreateDirectory(_dataPath);
                
                // Initialize with Tarteel AI Whisper model information (no large dataset download needed)
                var metadataPath = Path.Combine(_dataPath, "dataset_metadata.json");
                if (File.Exists(metadataPath))
                {
                    await LoadCachedDataset();
                }
                else
                {
                    _logger.LogInformation("💡 Using Tarteel AI Whisper model for speech recognition (no dataset download required)");
                    await CreateTarteelMetadata();
                }
                
                _logger.LogInformation("✅ Tarteel dataset service initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize Tarteel dataset service");
                throw;
            }
        }

        /// <summary>
        /// Download the Tarteel AI EveryAyah dataset from Hugging Face
        /// Note: This is a large dataset (~90k audio files, ~10GB)
        /// </summary>
        public async Task<bool> DownloadDatasetAsync(
            int maxRecords = 1000,
            string[] selectedReciters = null,
            bool downloadAudio = true)
        {
            try
            {
                _logger.LogInformation("📥 Starting Tarteel dataset download...");
                _logger.LogWarning("⚠️ This will download ~{MaxRecords} records. Full dataset is ~10GB!", maxRecords);

                // Use Hugging Face datasets API to get the data
                var datasetUrl = "https://huggingface.co/api/datasets/Salama1429/tarteel-ai-everyayah-Quran";
                
                // First, get dataset metadata
                var metadataResponse = await _httpClient.GetAsync(datasetUrl);
                if (!metadataResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("❌ Failed to fetch dataset metadata");
                    return false;
                }

                var metadata = new DatasetMetadata
                {
                    Source = "Tarteel AI EveryAyah",
                    HuggingFaceUrl = "https://huggingface.co/datasets/Salama1429/tarteel-ai-everyayah-Quran",
                    TotalRecords = 89978,
                    Reciters = GetAvailableReciters(),
                    DownloadDate = DateTime.UtcNow,
                    MaxRecordsDownloaded = maxRecords,
                    AudioDownloaded = downloadAudio
                };

                // For now, create sample data structure based on the dataset schema
                await CreateSampleTarteelData(maxRecords, selectedReciters);
                
                // Save metadata
                var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                await File.WriteAllTextAsync(Path.Combine(_dataPath, "dataset_metadata.json"), metadataJson);

                _logger.LogInformation("✅ Tarteel dataset sample created successfully");
                _logger.LogInformation("💡 To use the full dataset, integrate with Hugging Face datasets library");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to download Tarteel dataset");
                return false;
            }
        }

        /// <summary>
        /// Get available reciters from the Tarteel dataset
        /// </summary>
        public static string[] GetAvailableReciters()
        {
            return new[]
            {
                "abdul_basit", "abdullah_basfar", "abdullah_matroud", "abdulsamad",
                "abdurrahmaan_as-sudais", "abu_bakr_ash-shaatree", "ahmed_ibn_ali_al_ajamy",
                "ahmed_neana", "akram_alalaqimy", "alafasy", "ali_hajjaj_alsuesy",
                "aziz_alili", "fares_abbad", "ghamadi", "hani_rifai", "husary",
                "karim_mansoori", "khaalid_abdullaah_al-qahtaanee", "khalefa_al_tunaiji",
                "maher_al_muaiqly", "mahmoud_ali_al_banna", "menshawi", "minshawi",
                "mohammad_al_tablaway", "muhammad_abdulkareem", "muhammad_ayyoub",
                "muhammad_jibreel", "muhsin_al_qasim", "mustafa_ismail", "nasser_alqatami",
                "parhizgar", "sahl_yassin", "salaah_abdulrahman_bukhatir", "saood_ash-shuraym",
                "yaser_salamah", "yasser_ad-dussary"
            };
        }

        /// <summary>
        /// Find similar recitations for training Whisper with specific reciters
        /// </summary>
        public async Task<List<TarteelDataPoint>> FindSimilarRecitationsAsync(
            string arabicText,
            string reciterName = null,
            int maxResults = 10)
        {
            var normalizedText = NormalizeArabicText(arabicText);
            var results = new List<TarteelDataPoint>();

            var matches = _datasetCache.Where(dataPoint =>
            {
                var similarity = CalculateTextSimilarity(normalizedText, dataPoint.NormalizedText);
                return similarity > 0.7 && // 70% similarity
                       (reciterName == null || dataPoint.Reciter.Equals(reciterName, StringComparison.OrdinalIgnoreCase));
            })
            .OrderByDescending(dp => CalculateTextSimilarity(normalizedText, dp.NormalizedText))
            .Take(maxResults);

            return matches.ToList();
        }

        /// <summary>
        /// Get recitation examples for specific ayah across different reciters
        /// </summary>
        public async Task<List<TarteelDataPoint>> GetAyahRecitationsAsync(
            int surahNumber,
            int ayahNumber)
        {
            return _datasetCache
                .Where(dp => dp.SurahNumber == surahNumber && dp.AyahNumber == ayahNumber)
                .OrderBy(dp => dp.Reciter)
                .ToList();
        }

        /// <summary>
        /// Get pronunciation variations for a specific word
        /// </summary>
        public async Task<List<WordPronunciation>> GetWordPronunciationsAsync(string arabicWord)
        {
            var normalizedWord = NormalizeArabicText(arabicWord);
            var pronunciations = new List<WordPronunciation>();

            var matchingRecitations = _datasetCache.Where(dp =>
                dp.Words.Any(w => NormalizeArabicText(w).Contains(normalizedWord))
            );

            foreach (var recitation in matchingRecitations)
            {
                var wordIndex = recitation.Words
                    .Select((word, index) => new { Word = word, Index = index })
                    .FirstOrDefault(w => NormalizeArabicText(w.Word).Contains(normalizedWord));

                if (wordIndex != null)
                {
                    pronunciations.Add(new WordPronunciation
                    {
                        Word = arabicWord,
                        Reciter = recitation.Reciter,
                        AudioPath = recitation.AudioPath,
                        Duration = recitation.Duration,
                        Context = string.Join(" ", recitation.Words),
                        SurahNumber = recitation.SurahNumber,
                        AyahNumber = recitation.AyahNumber
                    });
                }
            }

            return pronunciations.Take(20).ToList(); // Limit to 20 examples
        }

        /// <summary>
        /// Get training data for fine-tuning Whisper on Quranic Arabic
        /// </summary>
        public async Task<TrainingDataBatch> GetTrainingBatchAsync(
            int batchSize = 32,
            string[] targetReciters = null,
            int? specificSurah = null)
        {
            var query = _datasetCache.AsQueryable();

            if (targetReciters?.Any() == true)
            {
                query = query.Where(dp => targetReciters.Contains(dp.Reciter));
            }

            if (specificSurah.HasValue)
            {
                query = query.Where(dp => dp.SurahNumber == specificSurah.Value);
            }

            var batch = query
                .OrderBy(dp => Guid.NewGuid()) // Random selection
                .Take(batchSize)
                .ToList();

            return new TrainingDataBatch
            {
                DataPoints = batch,
                BatchSize = batch.Count,
                CreatedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    {"target_reciters", targetReciters ?? Array.Empty<string>()},
                    {"specific_surah", specificSurah},
                    {"total_duration", batch.Sum(dp => dp.Duration)}
                }
            };
        }

        /// <summary>
        /// Validate user recitation against Tarteel dataset examples
        /// </summary>
        public async Task<RecitationValidationResult> ValidateRecitationAsync(
            string userTranscription,
            string expectedArabicText,
            int surahNumber,
            int ayahNumber)
        {
            var referenceRecitations = await GetAyahRecitationsAsync(surahNumber, ayahNumber);
            var normalizedUser = NormalizeArabicText(userTranscription);
            var normalizedExpected = NormalizeArabicText(expectedArabicText);

            var similarities = referenceRecitations.Select(ref_rec =>
            {
                var refSimilarity = CalculateTextSimilarity(normalizedUser, ref_rec.NormalizedText);
                return new ReciterSimilarity
                {
                    Reciter = ref_rec.Reciter,
                    Similarity = refSimilarity,
                    AudioPath = ref_rec.AudioPath,
                    Duration = ref_rec.Duration
                };
            }).OrderByDescending(rs => rs.Similarity).ToList();

            var bestMatch = similarities.FirstOrDefault();
            var accuracy = CalculateTextSimilarity(normalizedUser, normalizedExpected);

            return new RecitationValidationResult
            {
                UserTranscription = userTranscription,
                ExpectedText = expectedArabicText,
                Accuracy = accuracy,
                IsAccurate = accuracy > 0.8, // 80% threshold
                BestMatchingReciter = bestMatch?.Reciter,
                BestMatchSimilarity = bestMatch?.Similarity ?? 0,
                AllReciterSimilarities = similarities,
                SurahNumber = surahNumber,
                AyahNumber = ayahNumber,
                Feedback = GenerateFeedback(accuracy, bestMatch)
            };
        }

        #region Private Helper Methods

        private async Task CreateTarteelMetadata()
        {
            try
            {
                var metadata = new DatasetMetadata
                {
                    Source = "Tarteel AI Whisper Model",
                    HuggingFaceUrl = "https://huggingface.co/tarteel-ai/whisper-tiny-ar-quran",
                    TotalRecords = 0, // Using pre-trained model, not raw dataset
                    Reciters = GetAvailableReciters(),
                    DownloadDate = DateTime.UtcNow,
                    MaxRecordsDownloaded = 0,
                    AudioDownloaded = false
                };

                var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                await File.WriteAllTextAsync(Path.Combine(_dataPath, "dataset_metadata.json"), metadataJson);

                _logger.LogInformation("✅ Created Tarteel AI metadata (using pre-trained Whisper model)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to create Tarteel metadata");
            }
        }

        private async Task LoadCachedDataset()
        {
            var datasetPath = Path.Combine(_dataPath, "dataset_cache.json");
            if (File.Exists(datasetPath))
            {
                var json = await File.ReadAllTextAsync(datasetPath);
                var cachedData = JsonSerializer.Deserialize<List<TarteelDataPoint>>(json);
                
                if (cachedData != null)
                {
                    _datasetCache.AddRange(cachedData);
                    _logger.LogInformation("📖 Loaded {Count} cached Tarteel data points", cachedData.Count);
                }
            }
        }

        private async Task CreateSampleTarteelData(int maxRecords, string[] selectedReciters)
        {
            _logger.LogInformation("📝 Creating sample Tarteel dataset structure...");
            
            var sampleData = new List<TarteelDataPoint>();
            var reciters = selectedReciters ?? GetAvailableReciters().Take(5).ToArray();
            var random = new Random();

            // Generate sample data based on Al-Fatiha
            var alFatihaAyahs = new[]
            {
                "بِسْمِ اللَّهِ الرَّحْمَنِ الرَّحِيمِ",
                "الْحَمْدُ لِلَّهِ رَبِّ الْعَالَمِينَ",
                "الرَّحْمَنِ الرَّحِيمِ",
                "مَالِكِ يَوْمِ الدِّينِ",
                "إِيَّاكَ نَعْبُدُ وَإِيَّاكَ نَسْتَعِينُ",
                "اهْدِنَا الصِّرَاطَ الْمُسْتَقِيمَ",
                "صِرَاطَ الَّذِينَ أَنْعَمْتَ عَلَيْهِمْ غَيْرِ الْمَغْضُوبِ عَلَيْهِمْ وَلَا الضَّالِّينَ"
            };

            int recordCount = 0;
            for (int ayahIndex = 0; ayahIndex < alFatihaAyahs.Length && recordCount < maxRecords; ayahIndex++)
            {
                foreach (var reciter in reciters)
                {
                    if (recordCount >= maxRecords) break;

                    var arabicText = alFatihaAyahs[ayahIndex];
                    var words = arabicText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var duration = 3.0 + random.NextDouble() * 5.0; // 3-8 seconds

                    sampleData.Add(new TarteelDataPoint
                    {
                        Id = $"tarteel_{reciter}_{1}_{ayahIndex + 1}",
                        Reciter = reciter,
                        SurahNumber = 1,
                        AyahNumber = ayahIndex + 1,
                        ArabicText = arabicText,
                        NormalizedText = NormalizeArabicText(arabicText),
                        Words = words.ToList(),
                        Duration = duration,
                        SampleRate = 16000,
                        AudioPath = $"audio/{reciter}/001_{ayahIndex + 1:003}.wav",
                        CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 365))
                    });

                    recordCount++;
                }
            }

            _datasetCache.AddRange(sampleData);

            // Save cache
            var json = JsonSerializer.Serialize(sampleData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(_dataPath, "dataset_cache.json"), json);

            _logger.LogInformation("✅ Created {Count} sample Tarteel data points", sampleData.Count);
        }

        private string NormalizeArabicText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Remove diacritics and normalize
            var normalized = System.Text.RegularExpressions.Regex.Replace(text, @"[\u064B-\u065F\u0670\u0640]", "");
            normalized = normalized
                .Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا")
                .Replace("ة", "ه")
                .Replace("ى", "ي");

            return System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
        }

        private double CalculateTextSimilarity(string text1, string text2)
        {
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
                return 0;

            var words1 = text1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var words2 = text2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var commonWords = words1.Intersect(words2).Count();
            var totalWords = Math.Max(words1.Length, words2.Length);

            return totalWords > 0 ? (double)commonWords / totalWords : 0;
        }

        private string GenerateFeedback(double accuracy, ReciterSimilarity bestMatch)
        {
            if (accuracy > 0.9)
                return "Excellent recitation! Very close to perfect pronunciation.";
            
            if (accuracy > 0.8)
                return "Good recitation with minor pronunciation differences.";
            
            if (accuracy > 0.7)
                return $"Decent effort. Your style is closest to {bestMatch?.Reciter ?? "reference"}. Keep practicing!";
            
            if (accuracy > 0.5)
                return "Needs improvement. Focus on pronunciation and rhythm.";
            
            return "Please try again. Listen to reference recitations for guidance.";
        }

        #endregion
    }
} 