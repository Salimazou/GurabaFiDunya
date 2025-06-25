using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using server.Models;
using server.Services;
using System.Security.Claims;

namespace server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TarteelController : ControllerBase
    {
        private readonly ILogger<TarteelController> _logger;
        private readonly TarteelDatasetService _tarteelService;
        private readonly WhisperRecognitionService _whisperService;

        public TarteelController(
            ILogger<TarteelController> logger,
            TarteelDatasetService tarteelService,
            WhisperRecognitionService whisperService)
        {
            _logger = logger;
            _tarteelService = tarteelService;
            _whisperService = whisperService;
        }

        // MARK: - Dataset Management

        [HttpGet("dataset/info")]
        public ActionResult<object> GetDatasetInfo()
        {
            try
            {
                var reciters = TarteelDatasetService.GetAvailableReciters();
                return Ok(new
                {
                    source = "Tarteel AI EveryAyah Quran Dataset",
                    huggingFaceUrl = "https://huggingface.co/datasets/Salama1429/tarteel-ai-everyayah-Quran",
                    totalRecords = 89978,
                    reciters = reciters,
                    reciterCount = reciters.Length,
                    description = "High-quality Quranic recitations with transcriptions from 36 different reciters",
                    audioFormat = "16kHz WAV",
                    languages = new[] { "Arabic" },
                    license = "MIT"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting dataset info");
                return StatusCode(500, "Failed to get dataset information");
            }
        }

        [HttpPost("dataset/download")]
        public async Task<ActionResult<object>> DownloadDataset([FromBody] DownloadDatasetRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found");
                }

                _logger.LogInformation("🔽 User {UserId} requesting dataset download", userId);

                var success = await _tarteelService.DownloadDatasetAsync(
                    request.MaxRecords,
                    request.SelectedReciters,
                    request.DownloadAudio
                );

                if (success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Dataset download initiated successfully",
                        maxRecords = request.MaxRecords,
                        selectedReciters = request.SelectedReciters ?? new string[0],
                        audioDownloaded = request.DownloadAudio
                    });
                }
                else
                {
                    return BadRequest("Failed to download dataset");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error downloading dataset");
                return StatusCode(500, "Failed to download dataset");
            }
        }

        // MARK: - Recitation Validation & Analysis

        [HttpPost("validate")]
        public async Task<ActionResult<RecitationValidationResult>> ValidateRecitation(
            [FromBody] ValidationRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found");
                }

                var result = await _tarteelService.ValidateRecitationAsync(
                    request.UserTranscription,
                    request.ExpectedText,
                    request.SurahNumber,
                    request.AyahNumber
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error validating recitation");
                return StatusCode(500, "Failed to validate recitation");
            }
        }

        [HttpGet("ayahs/{surahNumber}/{ayahNumber}/recitations")]
        public async Task<ActionResult<List<TarteelDataPoint>>> GetAyahRecitations(
            int surahNumber,
            int ayahNumber)
        {
            try
            {
                var recitations = await _tarteelService.GetAyahRecitationsAsync(surahNumber, ayahNumber);
                return Ok(recitations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting ayah recitations for {Surah}:{Ayah}", 
                    surahNumber, ayahNumber);
                return StatusCode(500, "Failed to get ayah recitations");
            }
        }

        [HttpGet("words/{word}/pronunciations")]
        public async Task<ActionResult<List<WordPronunciation>>> GetWordPronunciations(
            string word,
            [FromQuery] int maxResults = 20)
        {
            try
            {
                var pronunciations = await _tarteelService.GetWordPronunciationsAsync(word);
                return Ok(pronunciations.Take(maxResults));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting pronunciations for word: {Word}", word);
                return StatusCode(500, "Failed to get word pronunciations");
            }
        }

        // MARK: - Training Data & Analytics

        [HttpPost("training/batch")]
        public async Task<ActionResult<TrainingDataBatch>> GetTrainingBatch(
            [FromBody] TrainingBatchRequest request)
        {
            try
            {
                var batch = await _tarteelService.GetTrainingBatchAsync(
                    request.BatchSize,
                    request.TargetReciters,
                    request.SpecificSurah
                );

                return Ok(batch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating training batch");
                return StatusCode(500, "Failed to create training batch");
            }
        }

        [HttpPost("search/similar")]
        public async Task<ActionResult<List<TarteelDataPoint>>> FindSimilarRecitations(
            [FromBody] SimilaritySearchRequest request)
        {
            try
            {
                var results = await _tarteelService.FindSimilarRecitationsAsync(
                    request.ArabicText,
                    request.ReciterName,
                    request.MaxResults
                );

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error finding similar recitations");
                return StatusCode(500, "Failed to find similar recitations");
            }
        }

        // MARK: - Analytics & Statistics

        [HttpGet("reciters")]
        public ActionResult<object> GetReciterStatistics()
        {
            try
            {
                var reciters = TarteelDatasetService.GetAvailableReciters();
                var reciterInfo = reciters.Select(reciter => new
                {
                    name = reciter,
                    displayName = FormatReciterName(reciter),
                    origin = GetReciterOrigin(reciter),
                    style = GetRecitationStyle(reciter)
                });

                return Ok(new
                {
                    totalReciters = reciters.Length,
                    reciters = reciterInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting reciter statistics");
                return StatusCode(500, "Failed to get reciter statistics");
            }
        }

        [HttpGet("reciters/{reciterName}/stats")]
        public async Task<ActionResult<object>> GetReciterDetailedStats(string reciterName)
        {
            try
            {
                // This would be implemented with actual dataset statistics
                return Ok(new
                {
                    reciter = reciterName,
                    displayName = FormatReciterName(reciterName),
                    totalRecitations = 6236, // Sample data
                    averageDuration = 8.5,
                    surahsCovered = 114,
                    quality = "High",
                    sampleRate = 16000,
                    description = GetReciterDescription(reciterName)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting detailed stats for reciter {Reciter}", reciterName);
                return StatusCode(500, "Failed to get reciter statistics");
            }
        }

        // MARK: - Helper Methods

        private string FormatReciterName(string reciterKey)
        {
            return reciterKey switch
            {
                "abdulsamad" => "Abdul Basit Abdul Samad",
                "alafasy" => "Mishary bin Rashid Alafasy",
                "husary" => "Mahmoud Khalil Al-Husary",
                "minshawi" => "Mohamed Siddiq Al-Minshawi",
                "ghamadi" => "Saad Al-Ghamdi",
                "abdul_basit" => "Abdul Basit Abdul Samad",
                "maher_al_muaiqly" => "Maher Al Muaiqly",
                "abdurrahmaan_as-sudais" => "Abdul Rahman As-Sudais",
                "saood_ash-shuraym" => "Saud Ash-Shuraim",
                _ => reciterKey.Replace("_", " ").Replace("-", " ")
                    .Split(' ')
                    .Select(word => char.ToUpper(word[0]) + word.Substring(1))
                    .Aggregate((a, b) => a + " " + b)
            };
        }

        private string GetReciterOrigin(string reciterKey)
        {
            return reciterKey switch
            {
                "abdulsamad" or "abdul_basit" => "Egypt",
                "alafasy" => "Kuwait",
                "husary" => "Egypt",
                "minshawi" => "Egypt",
                "ghamadi" => "Saudi Arabia",
                "abdurrahmaan_as-sudais" or "saood_ash-shuraym" => "Saudi Arabia (Mecca)",
                "maher_al_muaiqly" => "Saudi Arabia (Medina)",
                _ => "Various"
            };
        }

        private string GetRecitationStyle(string reciterKey)
        {
            return reciterKey switch
            {
                "abdulsamad" or "abdul_basit" => "Melodic, Emotional",
                "alafasy" => "Clear, Modern",
                "husary" => "Traditional, Tajweed Master",
                "minshawi" => "Melodic, Classical",
                "ghamadi" => "Steady, Clear",
                "abdurrahmaan_as-sudais" => "Majestic, Emotional",
                "maher_al_muaiqly" => "Calm, Precise",
                _ => "Traditional"
            };
        }

        private string GetReciterDescription(string reciterKey)
        {
            return reciterKey switch
            {
                "abdulsamad" => "Renowned Egyptian Qari known for his emotional and melodic recitation style",
                "alafasy" => "Popular Kuwaiti reciter with clear pronunciation and modern approach",
                "husary" => "Egyptian master of Tajweed, considered one of the greatest Qaris of all time",
                "minshawi" => "Egyptian reciter famous for his beautiful melodic style",
                "ghamadi" => "Saudi reciter known for his steady and clear recitation",
                _ => "Accomplished Quranic reciter with unique style and clear pronunciation"
            };
        }
    }

} 