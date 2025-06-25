using Microsoft.AspNetCore.Mvc;
using server.Services;
using System.IO;

namespace server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuranController : ControllerBase
    {
        private readonly ILogger<QuranController> _logger;
        private readonly QuranTextService _quranTextService;
        private readonly QuranDatasetDownloader _datasetDownloader;

        public QuranController(
            ILogger<QuranController> logger,
            QuranTextService quranTextService,
            QuranDatasetDownloader datasetDownloader)
        {
            _logger = logger;
            _quranTextService = quranTextService;
            _datasetDownloader = datasetDownloader;
        }

        /// <summary>
        /// Get all available Surahs
        /// </summary>
        [HttpGet("surahs")]
        public async Task<ActionResult<IEnumerable<object>>> GetSurahs()
        {
            try
            {
                var surahs = _quranTextService.GetAllSurahs();
                var result = surahs.Select(s => new 
                {
                    Number = s.Number,
                    Name = s.Name,
                    ArabicName = s.ArabicName,
                    Translation = s.Translation,
                    TotalAyahs = s.TotalAyahs
                });
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to get Surahs");
                return StatusCode(500, "Failed to retrieve Surahs");
            }
        }

        /// <summary>
        /// Get specific Ayah by Surah and Ayah number
        /// </summary>
        [HttpGet("ayah/{surahNumber}/{ayahNumber}")]
        public async Task<ActionResult<object>> GetAyah(int surahNumber, int ayahNumber)
        {
            try
            {
                var ayah = await _quranTextService.GetAyahAsync(surahNumber, ayahNumber);
                
                if (ayah == null)
                {
                    return NotFound($"Ayah {surahNumber}:{ayahNumber} not found");
                }
                
                return Ok(new 
                {
                    SurahNumber = ayah.SurahNumber,
                    AyahNumber = ayah.AyahNumber,
                    ArabicText = ayah.ArabicText,
                    ArabicTextNoDiacritics = ayah.ArabicTextNoDiacritics,
                    Translation = ayah.Translation,
                    Words = ayah.Words,
                    WordsNoDiacritics = ayah.WordsNoDiacritics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to get Ayah {Surah}:{Ayah}", surahNumber, ayahNumber);
                return StatusCode(500, "Failed to retrieve Ayah");
            }
        }

        /// <summary>
        /// Search Ayahs by Arabic text (fuzzy matching)
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<object>> SearchAyahs([FromQuery] string query, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Query parameter is required");
                }

                var results = await _quranTextService.SearchAyahsAsync(query, limit);
                
                return Ok(new 
                {
                    Query = query,
                    ResultCount = results.Count,
                    Results = results.Select(ayah => new 
                    {
                        SurahNumber = ayah.SurahNumber,
                        AyahNumber = ayah.AyahNumber,
                        ArabicText = ayah.ArabicText,
                        ArabicTextNoDiacritics = ayah.ArabicTextNoDiacritics,
                        Translation = ayah.Translation
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to search Ayahs with query: {Query}", query);
                return StatusCode(500, "Failed to search Ayahs");
            }
        }

        /// <summary>
        /// Enhanced Whisper-optimized search for ASR transcript matching
        /// </summary>
        [HttpPost("whisper-match")]
        public async Task<ActionResult<object>> FindWhisperMatch([FromBody] WhisperMatchRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Transcript))
                {
                    return BadRequest("Transcript is required");
                }

                var (ayah, confidence, matchedWords) = await _quranTextService.FindBestWhisperMatchAsync(
                    request.Transcript,
                    request.ExpectedSurah,
                    request.ExpectedAyah,
                    request.Threshold ?? 0.65);
                
                if (ayah == null)
                {
                    return Ok(new 
                    {
                        Found = false,
                        Confidence = confidence,
                        Message = "No matching Ayah found with sufficient confidence"
                    });
                }

                return Ok(new 
                {
                    Found = true,
                    Confidence = confidence,
                    MatchedWords = matchedWords,
                    Ayah = new 
                    {
                        SurahNumber = ayah.SurahNumber,
                        AyahNumber = ayah.AyahNumber,
                        ArabicText = ayah.ArabicText,
                        ArabicTextNoDiacritics = ayah.ArabicTextNoDiacritics,
                        Translation = ayah.Translation
                    },
                    InputTranscript = request.Transcript
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to find Whisper match for transcript: {Transcript}", request.Transcript);
                return StatusCode(500, "Failed to process Whisper transcript");
            }
        }

        /// <summary>
        /// Word-level alignment for detailed error analysis
        /// </summary>
        [HttpPost("align-words")]
        public async Task<ActionResult<object>> AlignWords([FromBody] WordAlignmentRequest request)
        {
            try
            {
                var ayah = await _quranTextService.GetAyahAsync(request.SurahNumber, request.AyahNumber);
                if (ayah == null)
                {
                    return NotFound($"Ayah {request.SurahNumber}:{request.AyahNumber} not found");
                }

                var alignments = await _quranTextService.AlignWordsWithAyahAsync(request.WhisperWords, ayah);
                
                return Ok(new 
                {
                    SurahNumber = request.SurahNumber,
                    AyahNumber = request.AyahNumber,
                    ExpectedText = ayah.ArabicTextNoDiacritics,
                    WhisperWords = request.WhisperWords,
                    Alignments = alignments.Select(a => new 
                    {
                        WordIndex = a.wordIndex,
                        ExpectedWord = a.expectedWord,
                        HeardWord = a.heardWord,
                        Confidence = a.confidence,
                        Status = a.wordIndex == -1 ? "insertion" : 
                                string.IsNullOrEmpty(a.heardWord) ? "omission" : 
                                a.confidence > 0.8 ? "correct" : "substitution"
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to align words for Ayah {Surah}:{Ayah}", 
                    request.SurahNumber, request.AyahNumber);
                return StatusCode(500, "Failed to align words");
            }
        }

        // MARK: - Dataset Management Endpoints

        /// <summary>
        /// Download latest Quran dataset from Tarteel
        /// </summary>
        [HttpPost("dataset/download")]
        public async Task<ActionResult<object>> DownloadDataset([FromQuery] string source = "tarteel")
        {
            try
            {
                _logger.LogInformation("📥 Starting dataset download from {Source}", source);
                
                bool success = source.ToLower() switch
                {
                    "tarteel" => await _datasetDownloader.DownloadTarteelQuranJsonAsync(),
                    "qul" => await _datasetDownloader.DownloadQULDatasetAsync(),
                    _ => await _datasetDownloader.DownloadTarteelQuranJsonAsync()
                };

                if (success)
                {
                    return Ok(new 
                    {
                        Success = true,
                        Message = $"Successfully downloaded {source} dataset",
                        Source = source,
                        Timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    return StatusCode(500, new 
                    {
                        Success = false,
                        Message = $"Failed to download {source} dataset"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to download dataset from {Source}", source);
                return StatusCode(500, "Dataset download failed");
            }
        }

        /// <summary>
        /// Setup complete optimized dataset for production
        /// </summary>
        [HttpPost("dataset/setup")]
        public async Task<ActionResult<object>> SetupDataset()
        {
            try
            {
                _logger.LogInformation("🚀 Starting complete dataset setup...");
                
                var success = await _datasetDownloader.SetupCompleteDatasetAsync();
                
                if (success)
                {
                    // Reinitialize QuranTextService with new data
                    await _quranTextService.InitializeAsync();
                    
                    return Ok(new 
                    {
                        Success = true,
                        Message = "Complete dataset setup finished successfully",
                        Timestamp = DateTime.UtcNow,
                        RecommendedNextSteps = new[]
                        {
                            "Test ASR matching with /api/quran/whisper-match",
                            "Verify data quality with /api/quran/surahs",
                            "Run word alignment tests with /api/quran/align-words"
                        }
                    });
                }
                else
                {
                    return StatusCode(500, new 
                    {
                        Success = false,
                        Message = "Dataset setup encountered some issues"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to setup dataset");
                return StatusCode(500, "Dataset setup failed");
            }
        }

        /// <summary>
        /// Get dataset statistics and health check
        /// </summary>
        [HttpGet("dataset/status")]
        public async Task<ActionResult<object>> GetDatasetStatus()
        {
            try
            {
                var surahs = _quranTextService.GetAllSurahs().ToList();
                var totalAyahs = surahs.Sum(s => s.TotalAyahs);
                var loadedAyahs = surahs.Sum(s => s.Ayahs.Count);
                
                var dataFiles = new[]
                {
                    ("quran.json", Path.Combine("Data", "quran.json")),
                    ("quran_complete.json", Path.Combine("Data", "quran_complete.json")),
                    ("quran_asr_optimized.json", Path.Combine("Data", "quran_asr_optimized.json"))
                };

                return Ok(new 
                {
                    Status = "healthy",
                    LoadedSurahs = surahs.Count,
                    ExpectedSurahs = 114,
                    LoadedAyahs = loadedAyahs,
                    ExpectedAyahs = 6236, // Total ayahs in Quran
                    CompletionPercentage = (double)loadedAyahs / 6236 * 100,
                    DataFiles = dataFiles.Select(f => new 
                    {
                        Name = f.Item1,
                        Exists = System.IO.File.Exists(f.Item2),
                        Size = System.IO.File.Exists(f.Item2) ? new FileInfo(f.Item2).Length : 0,
                        LastModified = System.IO.File.Exists(f.Item2) ? System.IO.File.GetLastWriteTime(f.Item2) : (DateTime?)null
                    }),
                    LastCheck = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to get dataset status");
                return StatusCode(500, "Failed to get dataset status");
            }
        }

        // Request models
        public class WhisperMatchRequest
        {
            public string Transcript { get; set; } = "";
            public int? ExpectedSurah { get; set; }
            public int? ExpectedAyah { get; set; }
            public double? Threshold { get; set; }
        }

        public class WordAlignmentRequest
        {
            public int SurahNumber { get; set; }
            public int AyahNumber { get; set; }
            public string[] WhisperWords { get; set; } = Array.Empty<string>();
        }
    }
} 