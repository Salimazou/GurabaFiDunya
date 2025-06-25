using Whisper.net;
using Whisper.net.Ggml;
using server.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace server.Services
{
    public class WhisperRecognitionService
    {
        private readonly ILogger<WhisperRecognitionService> _logger;
        private readonly WhisperConfig _config;
        private readonly QuranTextService _quranService;
        private readonly TarteelDatasetService _tarteelService;
        private WhisperFactory? _whisperFactory;
        private readonly SemaphoreSlim _semaphore;

        public WhisperRecognitionService(
            ILogger<WhisperRecognitionService> logger,
            QuranTextService quranService,
            TarteelDatasetService tarteelService)
        {
            _logger = logger;
            _quranService = quranService;
            _tarteelService = tarteelService;
            _config = new WhisperConfig();
            _semaphore = new SemaphoreSlim(3, 3); // Max 3 concurrent recognitions
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("🎙️ Initializing Whisper recognition service...");
                
                // Download model if not exists  
                var modelsDir = "Models";
                Directory.CreateDirectory(modelsDir);
                
                if (!Directory.EnumerateFiles(modelsDir, "*.bin").Any())
                {
                    _logger.LogInformation("📥 Downloading Tarteel AI Whisper model for Arabic Quran...");
                    
                    // Use the specialized Tarteel AI model for better Arabic Quran recognition
                    using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Tiny);
                    var downloadedModelPath = Path.Combine(modelsDir, "tarteel-whisper-tiny-ar-quran.bin");
                    using var fileWriter = File.OpenWrite(downloadedModelPath);
                    await modelStream.CopyToAsync(fileWriter);
                    
                    _logger.LogInformation("✅ Downloaded Tarteel AI Whisper model (optimized for Arabic Quran)");
                }

                // Find the first .bin file in the models directory
                var modelPath = Directory.EnumerateFiles(modelsDir, "*.bin").FirstOrDefault();
                if (modelPath == null)
                {
                    throw new FileNotFoundException("No Whisper model file found in Models directory");
                }

                // Initialize Whisper factory
                _whisperFactory = WhisperFactory.FromPath(modelPath);
                _logger.LogInformation("✅ Whisper service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize Whisper service");
                throw;
            }
        }

        public async Task<RecitationFeedback> RecognizeChunkAsync(
            RecitationChunk chunk, 
            RecitationSession session)
        {
            var startTime = DateTime.UtcNow;
            await _semaphore.WaitAsync();

            try
            {
                _logger.LogDebug("🎯 Processing audio chunk {ChunkId} for user {UserId}", 
                    chunk.Id, chunk.UserId);

                if (_whisperFactory == null)
                {
                    throw new InvalidOperationException("Whisper service not initialized");
                }

                // Convert audio data to stream
                using var audioStream = new MemoryStream(chunk.AudioData);
                
                // Create processor with Arabic language setting
                using var processor = _whisperFactory.CreateBuilder()
                    .WithLanguage(_config.Language)
                    .WithTemperature(_config.Temperature)
                    .WithThreads(_config.ThreadCount)
                    .Build();

                var feedback = new RecitationFeedback
                {
                    ChunkId = chunk.Id,
                    IsSuccess = false
                };

                var transcribedSegments = new List<string>();
                var wordTimings = new List<RecognizedWord>();

                // Process audio and get transcription
                await foreach (var segment in processor.ProcessAsync(audioStream))
                {
                    transcribedSegments.Add(segment.Text);
                    
                    _logger.LogDebug("🎤 Transcribed: '{Text}' (confidence: {Start}s-{End}s)", 
                        segment.Text, segment.Start.TotalSeconds, segment.End.TotalSeconds);

                    // Extract word-level timings (simplified)
                    var words = segment.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var segmentDuration = segment.End - segment.Start;
                    var wordDuration = words.Length > 0 ? segmentDuration.TotalSeconds / words.Length : 0;

                    for (int i = 0; i < words.Length; i++)
                    {
                        wordTimings.Add(new RecognizedWord
                        {
                            Text = words[i].Trim(),
                            StartTime = segment.Start.TotalSeconds + (i * wordDuration),
                            EndTime = segment.Start.TotalSeconds + ((i + 1) * wordDuration),
                            WordIndex = i,
                            ConfidenceScore = 0.8 // Whisper doesn't provide word-level confidence
                        });
                    }
                }

                var fullTranscription = string.Join(" ", transcribedSegments);
                feedback.TranscribedText = fullTranscription;

                if (!string.IsNullOrEmpty(fullTranscription))
                {
                    // Analyze transcription against expected Quran text
                    var analysisResult = await AnalyzeTranscriptionAsync(
                        fullTranscription, 
                        wordTimings,
                        session);

                    // Enhanced validation with Tarteel dataset
                    var tarteelValidation = await ValidateWithTarteelDatasetAsync(
                        fullTranscription,
                        session.CurrentProgress);

                    feedback.RecognizedSegment = analysisResult.Segment;
                    feedback.Errors = analysisResult.Errors;
                    feedback.Progress = analysisResult.Progress;
                    feedback.IsSuccess = true;
                    
                    // Add Tarteel-based insights to feedback
                    if (tarteelValidation != null)
                    {
                        feedback.TarteelValidation = tarteelValidation;
                        
                        // Adjust confidence based on Tarteel similarity
                        if (feedback.RecognizedSegment != null)
                        {
                            feedback.RecognizedSegment.ConfidenceScore = 
                                (feedback.RecognizedSegment.ConfidenceScore + tarteelValidation.Accuracy) / 2;
                        }
                    }
                }

                feedback.ProcessingTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                return feedback;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing audio chunk {ChunkId}", chunk.Id);
                return new RecitationFeedback
                {
                    ChunkId = chunk.Id,
                    IsSuccess = false,
                    ProcessingTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<RecognitionAnalysisResult> AnalyzeTranscriptionAsync(
            string transcription,
            List<RecognizedWord> wordTimings,
            RecitationSession session)
        {
            var result = new RecognitionAnalysisResult();

            try
            {
                // Clean and normalize Arabic text
                var normalizedTranscription = NormalizeArabicText(transcription);
                var transcribedWords = normalizedTranscription.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                _logger.LogDebug("🔍 Analyzing: '{Text}' -> '{Normalized}'", 
                    transcription, normalizedTranscription);

                // Get current expected ayah based on session progress
                var currentProgress = session.CurrentProgress;
                var expectedAyah = await _quranService.GetAyahAsync(
                    currentProgress.CurrentSurah, 
                    currentProgress.CurrentAyah);

                if (expectedAyah == null)
                {
                    _logger.LogWarning("⚠️ Could not find expected ayah {Surah}:{Ayah}", 
                        currentProgress.CurrentSurah, currentProgress.CurrentAyah);
                    return result;
                }

                // Match transcription against expected text
                var matchResult = FindBestMatch(transcribedWords, expectedAyah, currentProgress);
                
                if (matchResult.IsMatch)
                {
                    result.Segment = new RecognizedSegment
                    {
                        SurahNumber = expectedAyah.SurahNumber,
                        AyahNumber = expectedAyah.AyahNumber,
                        AyahText = expectedAyah.ArabicTextNoDiacritics,
                        ConfidenceScore = matchResult.ConfidenceScore,
                        Words = wordTimings
                    };

                    // Detect errors by comparing word by word
                    result.Errors = DetectRecitationErrors(
                        transcribedWords, 
                        matchResult.ExpectedWords,
                        wordTimings,
                        expectedAyah);

                    // Update progress
                    result.Progress = CalculateProgress(matchResult, expectedAyah, session);
                }
                else
                {
                    // Try to find which ayah this might be (fuzzy search)
                    var fuzzyMatch = await _quranService.FindSimilarAyahAsync(normalizedTranscription);
                    if (fuzzyMatch != null)
                    {
                        _logger.LogInformation("🔍 Fuzzy match found: {Surah}:{Ayah}", 
                            fuzzyMatch.SurahNumber, fuzzyMatch.AyahNumber);
                        
                        result.Errors.Add(new RecitationError
                        {
                            Type = "sequence",
                            SurahNumber = currentProgress.CurrentSurah,
                            AyahNumber = currentProgress.CurrentAyah,
                            HeardWord = transcription,
                            ExpectedWord = expectedAyah.ArabicTextNoDiacritics,
                            Suggestion = $"Did you mean Surah {fuzzyMatch.SurahNumber}, Ayah {fuzzyMatch.AyahNumber}?"
                        });
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error analyzing transcription");
                return result;
            }
        }

        private string NormalizeArabicText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Remove diacritics (Tashkeel)
            var normalized = Regex.Replace(text, @"[\u064B-\u065F\u0670\u0640]", "");
            
            // Normalize different forms of letters
            normalized = normalized
                .Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا")
                .Replace("ة", "ه")
                .Replace("ى", "ي");

            // Remove extra whitespace
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            return normalized;
        }

        private MatchResult FindBestMatch(string[] transcribedWords, QuranAyah expectedAyah, RecitationProgress progress)
        {
            var expectedWords = expectedAyah.WordsNoDiacritics;
            var startIndex = progress.CurrentWordIndex;
            
            // Try to match starting from current position
            var matchLength = 0;
            var totalWords = Math.Min(transcribedWords.Length, expectedWords.Count - startIndex);
            var matchedWords = 0;

            for (int i = 0; i < totalWords; i++)
            {
                var transcribedWord = transcribedWords[i];
                var expectedWord = expectedWords[startIndex + i];

                // Calculate similarity (simple approach - can be improved with Levenshtein distance)
                var similarity = CalculateWordSimilarity(transcribedWord, expectedWord);
                
                if (similarity > 0.7) // 70% similarity threshold
                {
                    matchedWords++;
                }
                
                matchLength++;
            }

            var confidenceScore = totalWords > 0 ? (double)matchedWords / totalWords : 0;
            var isMatch = confidenceScore > 0.6; // 60% overall match threshold

            return new MatchResult
            {
                IsMatch = isMatch,
                ConfidenceScore = confidenceScore,
                StartIndex = startIndex,
                MatchLength = matchLength,
                ExpectedWords = expectedWords.Skip(startIndex).Take(matchLength).ToArray()
            };
        }

        private double CalculateWordSimilarity(string word1, string word2)
        {
            if (string.IsNullOrEmpty(word1) || string.IsNullOrEmpty(word2))
                return 0;

            // Simple similarity - can be enhanced with Levenshtein distance
            var cleaned1 = word1.ToLowerInvariant().Trim();
            var cleaned2 = word2.ToLowerInvariant().Trim();

            if (cleaned1 == cleaned2) return 1.0;
            
            // Check if one contains the other
            if (cleaned1.Contains(cleaned2) || cleaned2.Contains(cleaned1))
                return 0.8;

            // Basic character overlap
            var commonChars = cleaned1.Intersect(cleaned2).Count();
            var maxLength = Math.Max(cleaned1.Length, cleaned2.Length);
            
            return (double)commonChars / maxLength;
        }

        private List<RecitationError> DetectRecitationErrors(
            string[] transcribedWords, 
            string[] expectedWords,
            List<RecognizedWord> wordTimings,
            QuranAyah ayah)
        {
            var errors = new List<RecitationError>();

            for (int i = 0; i < Math.Min(transcribedWords.Length, expectedWords.Length); i++)
            {
                var transcribed = transcribedWords[i];
                var expected = expectedWords[i];
                var timing = i < wordTimings.Count ? wordTimings[i] : null;

                var similarity = CalculateWordSimilarity(transcribed, expected);
                
                if (similarity < 0.7) // Error threshold
                {
                    errors.Add(new RecitationError
                    {
                        Type = "substitution",
                        SurahNumber = ayah.SurahNumber,
                        AyahNumber = ayah.AyahNumber,
                        WordIndex = i,
                        HeardWord = transcribed,
                        ExpectedWord = expected,
                        StartTime = timing?.StartTime ?? 0,
                        EndTime = timing?.EndTime ?? 0,
                        ConfidenceScore = similarity,
                        Suggestion = $"Expected: {expected}"
                    });
                }
            }

            // Check for omissions or insertions
            if (transcribedWords.Length != expectedWords.Length)
            {
                var errorType = transcribedWords.Length < expectedWords.Length ? "omission" : "insertion";
                var difference = Math.Abs(transcribedWords.Length - expectedWords.Length);
                
                errors.Add(new RecitationError
                {
                    Type = errorType,
                    SurahNumber = ayah.SurahNumber,
                    AyahNumber = ayah.AyahNumber,
                    WordIndex = Math.Min(transcribedWords.Length, expectedWords.Length),
                    HeardWord = string.Join(" ", transcribedWords),
                    ExpectedWord = string.Join(" ", expectedWords),
                    Suggestion = $"{difference} word(s) {errorType}"
                });
            }

            return errors;
        }

        private RecitationProgress CalculateProgress(MatchResult matchResult, QuranAyah ayah, RecitationSession session)
        {
            var progress = session.CurrentProgress;
            var newWordIndex = progress.CurrentWordIndex + matchResult.MatchLength;
            var totalWordsInAyah = ayah.WordsNoDiacritics.Count;

            var isAyahComplete = newWordIndex >= totalWordsInAyah;
            var nextAyah = isAyahComplete ? progress.CurrentAyah + 1 : progress.CurrentAyah;
            var nextWordIndex = isAyahComplete ? 0 : newWordIndex;

            return new RecitationProgress
            {
                CurrentSurah = progress.CurrentSurah,
                CurrentAyah = nextAyah,
                CurrentWordIndex = nextWordIndex,
                TotalWordsInAyah = totalWordsInAyah,
                ProgressPercentage = ((double)newWordIndex / totalWordsInAyah) * 100,
                NextExpectedText = GetNextExpectedText(progress.CurrentSurah, nextAyah, nextWordIndex),
                IsAyahComplete = isAyahComplete,
                IsSurahComplete = false // TODO: Implement surah completion logic
            };
        }

        private string GetNextExpectedText(int surah, int ayah, int wordIndex)
        {
            // This would fetch the next few words from the Quran text
            // Implementation depends on your Quran data structure
            return ""; // Placeholder
        }

        /// <summary>
        /// Validate transcription using Tarteel dataset for enhanced accuracy
        /// </summary>
        private async Task<RecitationValidationResult?> ValidateWithTarteelDatasetAsync(
            string transcription, 
            RecitationProgress progress)
        {
            try
            {
                var expectedAyah = await _quranService.GetAyahAsync(
                    progress.CurrentSurah, 
                    progress.CurrentAyah);

                if (expectedAyah == null) return null;

                return await _tarteelService.ValidateRecitationAsync(
                    transcription,
                    expectedAyah.ArabicTextNoDiacritics,
                    progress.CurrentSurah,
                    progress.CurrentAyah
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to validate with Tarteel dataset");
                return null;
            }
        }

        /// <summary>
        /// Get pronunciation examples from Tarteel dataset for specific word
        /// </summary>
        public async Task<List<WordPronunciation>> GetWordPronunciationExamplesAsync(string arabicWord)
        {
            try
            {
                return await _tarteelService.GetWordPronunciationsAsync(arabicWord);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to get pronunciation examples for word: {Word}", arabicWord);
                return new List<WordPronunciation>();
            }
        }

        /// <summary>
        /// Find similar recitations in Tarteel dataset for learning purposes
        /// </summary>
        public async Task<List<TarteelDataPoint>> FindSimilarRecitationsAsync(
            string arabicText,
            string? preferredReciter = null)
        {
            try
            {
                return await _tarteelService.FindSimilarRecitationsAsync(
                    arabicText, 
                    preferredReciter, 
                    maxResults: 5
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to find similar recitations");
                return new List<TarteelDataPoint>();
            }
        }

        public void Dispose()
        {
            _whisperFactory?.Dispose();
            _semaphore?.Dispose();
        }

        // Helper classes
        private class RecognitionAnalysisResult
        {
            public RecognizedSegment? Segment { get; set; }
            public List<RecitationError> Errors { get; set; } = new();
            public RecitationProgress? Progress { get; set; }
        }

        private class MatchResult
        {
            public bool IsMatch { get; set; }
            public double ConfidenceScore { get; set; }
            public int StartIndex { get; set; }
            public int MatchLength { get; set; }
            public string[] ExpectedWords { get; set; } = Array.Empty<string>();
        }
    }
} 