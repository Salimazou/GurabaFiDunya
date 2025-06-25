using System.ComponentModel.DataAnnotations;

namespace server.Models
{
    // MARK: - Recitation Recognition Models
    
    public class RecitationChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = "";
        public byte[] AudioData { get; set; } = Array.Empty<byte>();
        public string AudioFormat { get; set; } = "wav"; // wav, pcm, etc
        public int SampleRate { get; set; } = 16000;
        public double DurationSeconds { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        // Context for sequential recognition
        public int? ExpectedSurah { get; set; }
        public int? ExpectedAyah { get; set; }
        public int? ExpectedWordIndex { get; set; }
    }

    public class RecitationFeedback
    {
        public string ChunkId { get; set; } = "";
        public bool IsSuccess { get; set; }
        public string TranscribedText { get; set; } = "";
        public RecognizedSegment? RecognizedSegment { get; set; }
        public List<RecitationError> Errors { get; set; } = new();
        public RecitationProgress? Progress { get; set; }
        public double ProcessingTimeMs { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        // Enhanced with Tarteel dataset validation
        public RecitationValidationResult? TarteelValidation { get; set; }
    }

    public class RecognizedSegment
    {
        public int SurahNumber { get; set; }
        public int AyahNumber { get; set; }
        public string AyahText { get; set; } = "";
        public double ConfidenceScore { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public List<RecognizedWord> Words { get; set; } = new();
    }

    public class RecognizedWord
    {
        public string Text { get; set; } = "";
        public string Expected { get; set; } = "";
        public bool IsCorrect { get; set; }
        public double ConfidenceScore { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public int WordIndex { get; set; }
    }

    public class RecitationError
    {
        public string Type { get; set; } = ""; // "substitution", "omission", "insertion", "pronunciation"
        public int SurahNumber { get; set; }
        public int AyahNumber { get; set; }
        public int WordIndex { get; set; }
        public string HeardWord { get; set; } = "";
        public string ExpectedWord { get; set; } = "";
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public double ConfidenceScore { get; set; }
        public string Suggestion { get; set; } = "";
    }

    public class RecitationProgress
    {
        public int CurrentSurah { get; set; }
        public int CurrentAyah { get; set; }
        public int CurrentWordIndex { get; set; }
        public int TotalWordsInAyah { get; set; }
        public double ProgressPercentage { get; set; }
        public string NextExpectedText { get; set; } = "";
        public bool IsAyahComplete { get; set; }
        public bool IsSurahComplete { get; set; }
    }

    // MARK: - Quran Reference Models
    
    public class QuranAyah
    {
        public int SurahNumber { get; set; }
        public int AyahNumber { get; set; }
        public string ArabicText { get; set; } = "";
        public string ArabicTextNoDiacritics { get; set; } = "";
        public string Translation { get; set; } = "";
        public List<string> Words { get; set; } = new();
        public List<string> WordsNoDiacritics { get; set; } = new();
    }

    public class QuranSurah
    {
        public int Number { get; set; }
        public string Name { get; set; } = "";
        public string ArabicName { get; set; } = "";
        public string Translation { get; set; } = "";
        public int TotalAyahs { get; set; }
        public List<QuranAyah> Ayahs { get; set; } = new();
    }

    // MARK: - Session Management
    
    public class RecitationSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = "";
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Session settings
        public int StartingSurah { get; set; } = 1;
        public int StartingAyah { get; set; } = 1;
        public string RecitationMode { get; set; } = "guided"; // "guided", "free", "memorization"
        public double ErrorThreshold { get; set; } = 0.7; // Confidence threshold for error detection
        
        // Session progress
        public RecitationProgress CurrentProgress { get; set; } = new();
        public List<RecitationError> SessionErrors { get; set; } = new();
        public int TotalChunksProcessed { get; set; }
        public double TotalRecitationTimeSeconds { get; set; }
        public double AverageAccuracy { get; set; }
        
        // Statistics
        public Dictionary<string, int> ErrorTypes { get; set; } = new();
        public List<int> DifficultAyahs { get; set; } = new(); // Ayahs with most errors
    }

    // MARK: - Real-time Communication
    
    public class RecitationStatusUpdate
    {
        public string SessionId { get; set; } = "";
        public string Status { get; set; } = ""; // "listening", "processing", "feedback", "paused", "completed"
        public RecitationProgress? Progress { get; set; }
        public RecitationFeedback? LatestFeedback { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // MARK: - Configuration Models
    
    public class WhisperConfig
    {
        public string ModelPath { get; set; } = "tarteel-whisper-tiny-ar-quran.bin";
        public string Language { get; set; } = "ar";
        public int MaxTokens { get; set; } = 224;
        public float Temperature { get; set; } = 0.0f; // Low temperature for consistent Arabic recognition
        public bool UseGPU { get; set; } = false;
        public int ThreadCount { get; set; } = Environment.ProcessorCount;
        
        // Tarteel AI specific optimizations
        public bool EnableQuranOptimization { get; set; } = true;
        public double MinConfidenceThreshold { get; set; } = 0.6; // Based on Tarteel's 7.05% WER
        public bool EnableDiacriticsNormalization { get; set; } = true;
    }

    public class RecitationConfig
    {
        public double ChunkDurationSeconds { get; set; } = 3.0;
        public int SampleRate { get; set; } = 16000;
        public double OverlapSeconds { get; set; } = 0.5;
        public double MinConfidenceScore { get; set; } = 0.6;
        public int MaxConcurrentSessions { get; set; } = 10;
        public bool EnableRealTimeProcessing { get; set; } = true;
        public string QuranDataPath { get; set; } = "Data/quran.json";
    }

    // MARK: - Tarteel Dataset Models
    
    public class RecitationValidationResult
    {
        public string UserTranscription { get; set; } = "";
        public string ExpectedText { get; set; } = "";
        public double Accuracy { get; set; }
        public bool IsAccurate { get; set; }
        public string BestMatchingReciter { get; set; } = "";
        public double BestMatchSimilarity { get; set; }
        public List<ReciterSimilarity> AllReciterSimilarities { get; set; } = new();
        public int SurahNumber { get; set; }
        public int AyahNumber { get; set; }
        public string Feedback { get; set; } = "";
    }

    public class ReciterSimilarity
    {
        public string Reciter { get; set; } = "";
        public double Similarity { get; set; }
        public string AudioPath { get; set; } = "";
        public double Duration { get; set; }
    }

    // MARK: - Request Models for Tarteel API
    
    public class DownloadDatasetRequest
    {
        public int MaxRecords { get; set; } = 1000;
        public string[]? SelectedReciters { get; set; }
        public bool DownloadAudio { get; set; } = true;
    }

    public class ValidationRequest
    {
        public string UserTranscription { get; set; } = "";
        public string ExpectedText { get; set; } = "";
        public int SurahNumber { get; set; }
        public int AyahNumber { get; set; }
    }

    public class TrainingBatchRequest
    {
        public int BatchSize { get; set; } = 32;
        public string[]? TargetReciters { get; set; }
        public int? SpecificSurah { get; set; }
    }

    public class SimilaritySearchRequest
    {
        public string ArabicText { get; set; } = "";
        public string? ReciterName { get; set; }
        public int MaxResults { get; set; } = 10;
    }

    // MARK: - Tarteel Dataset Data Models
    
    public class TarteelDataPoint
    {
        public string Id { get; set; } = "";
        public string Reciter { get; set; } = "";
        public int SurahNumber { get; set; }
        public int AyahNumber { get; set; }
        public string ArabicText { get; set; } = "";
        public string NormalizedText { get; set; } = "";
        public List<string> Words { get; set; } = new();
        public double Duration { get; set; }
        public int SampleRate { get; set; } = 16000;
        public string AudioPath { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class DatasetMetadata
    {
        public string Source { get; set; } = "";
        public string HuggingFaceUrl { get; set; } = "";
        public int TotalRecords { get; set; }
        public string[] Reciters { get; set; } = Array.Empty<string>();
        public DateTime DownloadDate { get; set; }
        public int MaxRecordsDownloaded { get; set; }
        public bool AudioDownloaded { get; set; }
    }

    public class WordPronunciation
    {
        public string Word { get; set; } = "";
        public string Reciter { get; set; } = "";
        public string AudioPath { get; set; } = "";
        public double Duration { get; set; }
        public string Context { get; set; } = "";
        public int SurahNumber { get; set; }
        public int AyahNumber { get; set; }
    }

    public class TrainingDataBatch
    {
        public List<TarteelDataPoint> DataPoints { get; set; } = new();
        public int BatchSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
} 