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
    public class RecitationController : ControllerBase
    {
        private readonly ILogger<RecitationController> _logger;
        private readonly WhisperRecognitionService _whisperService;
        private readonly QuranTextService _quranService;
        private readonly MongoDbService _mongoDbService;
        private static readonly Dictionary<string, RecitationSession> _activeSessions = new();

        public RecitationController(
            ILogger<RecitationController> logger,
            WhisperRecognitionService whisperService,
            QuranTextService quranService,
            MongoDbService mongoDbService)
        {
            _logger = logger;
            _whisperService = whisperService;
            _quranService = quranService;
            _mongoDbService = mongoDbService;
        }

        // MARK: - Session Management

        [HttpPost("sessions/start")]
        public async Task<ActionResult<RecitationSession>> StartSession([FromBody] StartSessionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User not authenticated");
                }

                _logger.LogInformation("🎯 Starting recitation session for user {UserId}", userId);

                var session = new RecitationSession
                {
                    UserId = userId,
                    StartingSurah = request.SurahNumber,
                    StartingAyah = request.AyahNumber,
                    RecitationMode = request.Mode ?? "guided",
                    ErrorThreshold = request.ErrorThreshold ?? 0.7,
                    CurrentProgress = new RecitationProgress
                    {
                        CurrentSurah = request.SurahNumber,
                        CurrentAyah = request.AyahNumber,
                        CurrentWordIndex = 0
                    }
                };

                // Store active session
                _activeSessions[session.Id] = session;

                // Save to database
                await _mongoDbService.CreateRecitationSessionAsync(session);

                _logger.LogInformation("✅ Session {SessionId} started for user {UserId}", session.Id, userId);

                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error starting recitation session");
                return StatusCode(500, "Failed to start session");
            }
        }

        [HttpPost("sessions/{sessionId}/stop")]
        public async Task<ActionResult> StopSession(string sessionId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (_activeSessions.TryGetValue(sessionId, out var session))
                {
                    if (session.UserId != userId)
                    {
                        return Forbid("Not authorized to stop this session");
                    }

                    session.IsActive = false;
                    session.EndTime = DateTime.UtcNow;
                    
                    // Calculate final statistics
                    var totalErrors = session.SessionErrors.Count;
                    var totalChunks = session.TotalChunksProcessed;
                    session.AverageAccuracy = totalChunks > 0 ? 
                        Math.Max(0, (totalChunks - totalErrors) / (double)totalChunks) : 0;

                    // Update in database
                    await _mongoDbService.UpdateRecitationSessionAsync(session);
                    
                    // Remove from active sessions
                    _activeSessions.Remove(sessionId);

                    _logger.LogInformation("⏹️ Session {SessionId} stopped. Accuracy: {Accuracy:F2}%", 
                        sessionId, session.AverageAccuracy * 100);

                    return Ok(new { message = "Session stopped successfully", accuracy = session.AverageAccuracy });
                }

                return NotFound("Session not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error stopping session {SessionId}", sessionId);
                return StatusCode(500, "Failed to stop session");
            }
        }

        [HttpGet("sessions/{sessionId}")]
        public async Task<ActionResult<RecitationSession>> GetSession(string sessionId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                if (session.UserId != userId)
                {
                    return Forbid("Not authorized to access this session");
                }
                return Ok(session);
            }

            // Try to load from database
            var dbSession = await _mongoDbService.GetRecitationSessionAsync(sessionId);
            if (dbSession != null && dbSession.UserId == userId)
            {
                return Ok(dbSession);
            }

            return NotFound("Session not found");
        }

        // MARK: - Audio Processing

        [HttpPost("recognize")]
        public async Task<ActionResult<RecitationFeedback>> RecognizeAudio()
        {
            try
            {
                var sessionId = Request.Headers["X-Session-Id"].FirstOrDefault();
                if (string.IsNullOrEmpty(sessionId))
                {
                    return BadRequest("Session ID required in X-Session-Id header");
                }

                if (!_activeSessions.TryGetValue(sessionId, out var session))
                {
                    return NotFound("Session not found or inactive");
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (session.UserId != userId)
                {
                    return Forbid("Not authorized to use this session");
                }

                // Check if audio file is provided
                var audioFile = Request.Form.Files.FirstOrDefault();
                if (audioFile == null || audioFile.Length == 0)
                {
                    return BadRequest("Audio file required");
                }

                _logger.LogDebug("🎙️ Processing audio chunk for session {SessionId}, size: {Size} bytes", 
                    sessionId, audioFile.Length);

                // Convert to RecitationChunk
                byte[] audioData;
                using (var stream = new MemoryStream())
                {
                    await audioFile.CopyToAsync(stream);
                    audioData = stream.ToArray();
                }

                var chunk = new RecitationChunk
                {
                    UserId = userId,
                    AudioData = audioData,
                    AudioFormat = Path.GetExtension(audioFile.FileName).TrimStart('.'),
                    DurationSeconds = 3.0, // Estimated - could be calculated from audio
                    ExpectedSurah = session.CurrentProgress.CurrentSurah,
                    ExpectedAyah = session.CurrentProgress.CurrentAyah,
                    ExpectedWordIndex = session.CurrentProgress.CurrentWordIndex
                };

                // Process with Whisper
                var feedback = await _whisperService.RecognizeChunkAsync(chunk, session);

                // Update session progress
                if (feedback.IsSuccess && feedback.Progress != null)
                {
                    session.CurrentProgress = feedback.Progress;
                    session.TotalChunksProcessed++;
                    session.TotalRecitationTimeSeconds += chunk.DurationSeconds;

                    // Add errors to session
                    if (feedback.Errors.Any())
                    {
                        session.SessionErrors.AddRange(feedback.Errors);
                        
                        // Update error type statistics
                        foreach (var error in feedback.Errors)
                        {
                            if (session.ErrorTypes.ContainsKey(error.Type))
                                session.ErrorTypes[error.Type]++;
                            else
                                session.ErrorTypes[error.Type] = 1;
                        }
                    }

                    // Update session in memory and database periodically
                    if (session.TotalChunksProcessed % 5 == 0) // Save every 5 chunks
                    {
                        await _mongoDbService.UpdateRecitationSessionAsync(session);
                    }
                }

                _logger.LogDebug("✅ Audio processed. Success: {Success}, Errors: {ErrorCount}", 
                    feedback.IsSuccess, feedback.Errors.Count);

                return Ok(feedback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing audio recognition");
                return StatusCode(500, "Failed to process audio");
            }
        }

        [HttpPost("sessions/{sessionId}/chunks")]
        public async Task<ActionResult<RecitationFeedback>> ProcessAudioChunk(
            string sessionId,
            [FromForm] IFormFile audioFile,
            [FromForm] double? duration,
            [FromForm] string? format)
        {
            try
            {
                if (!_activeSessions.TryGetValue(sessionId, out var session))
                {
                    return NotFound("Session not found or inactive");
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (session.UserId != userId)
                {
                    return Forbid("Not authorized to use this session");
                }

                if (audioFile == null || audioFile.Length == 0)
                {
                    return BadRequest("Audio file required");
                }

                // Convert audio file to RecitationChunk
                using var stream = new MemoryStream();
                await audioFile.CopyToAsync(stream);

                var chunk = new RecitationChunk
                {
                    UserId = userId,
                    AudioData = stream.ToArray(),
                    AudioFormat = format ?? "wav",
                    DurationSeconds = duration ?? 3.0,
                    ExpectedSurah = session.CurrentProgress.CurrentSurah,
                    ExpectedAyah = session.CurrentProgress.CurrentAyah,
                    ExpectedWordIndex = session.CurrentProgress.CurrentWordIndex
                };

                var feedback = await _whisperService.RecognizeChunkAsync(chunk, session);
                
                // Update session as before...
                if (feedback.IsSuccess && feedback.Progress != null)
                {
                    session.CurrentProgress = feedback.Progress;
                    session.TotalChunksProcessed++;
                    
                    if (feedback.Errors.Any())
                    {
                        session.SessionErrors.AddRange(feedback.Errors);
                    }
                }

                return Ok(feedback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing audio chunk for session {SessionId}", sessionId);
                return StatusCode(500, "Failed to process audio chunk");
            }
        }

        // MARK: - Quran Text API

        [HttpGet("quran/surahs")]
        public async Task<ActionResult<IEnumerable<QuranSurah>>> GetSurahs()
        {
            try
            {
                var surahs = _quranService.GetAllSurahs();
                return Ok(surahs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching surahs");
                return StatusCode(500, "Failed to fetch surahs");
            }
        }

        [HttpGet("quran/surahs/{surahNumber}/ayahs/{ayahNumber}")]
        public async Task<ActionResult<QuranAyah>> GetAyah(int surahNumber, int ayahNumber)
        {
            try
            {
                var ayah = await _quranService.GetAyahAsync(surahNumber, ayahNumber);
                if (ayah == null)
                {
                    return NotFound($"Ayah {surahNumber}:{ayahNumber} not found");
                }
                return Ok(ayah);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching ayah {Surah}:{Ayah}", surahNumber, ayahNumber);
                return StatusCode(500, "Failed to fetch ayah");
            }
        }

        [HttpGet("quran/search")]
        public async Task<ActionResult<List<QuranAyah>>> SearchAyahs([FromQuery] string query, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Search query is required");
                }

                var results = await _quranService.SearchAyahsAsync(query, limit);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error searching ayahs with query: {Query}", query);
                return StatusCode(500, "Failed to search ayahs");
            }
        }

        // MARK: - Statistics & Analytics

        [HttpGet("sessions/{sessionId}/statistics")]
        public async Task<ActionResult<RecitationStatistics>> GetSessionStatistics(string sessionId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                RecitationSession? session = null;
                
                if (_activeSessions.TryGetValue(sessionId, out var activeSession))
                {
                    session = activeSession;
                }
                else
                {
                    session = await _mongoDbService.GetRecitationSessionAsync(sessionId);
                }

                if (session == null || session.UserId != userId)
                {
                    return NotFound("Session not found");
                }

                var stats = new RecitationStatistics
                {
                    SessionId = sessionId,
                    TotalDurationSeconds = session.TotalRecitationTimeSeconds,
                    TotalChunksProcessed = session.TotalChunksProcessed,
                    TotalErrors = session.SessionErrors.Count,
                    AverageAccuracy = session.AverageAccuracy,
                    ErrorBreakdown = session.ErrorTypes,
                    CurrentProgress = session.CurrentProgress,
                    MostDifficultAyahs = GetMostDifficultAyahs(session),
                    WordsPerMinute = CalculateWordsPerMinute(session)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting session statistics for {SessionId}", sessionId);
                return StatusCode(500, "Failed to get statistics");
            }
        }

        private List<AyahDifficulty> GetMostDifficultAyahs(RecitationSession session)
        {
            return session.SessionErrors
                .GroupBy(e => new { e.SurahNumber, e.AyahNumber })
                .Select(g => new AyahDifficulty
                {
                    SurahNumber = g.Key.SurahNumber,
                    AyahNumber = g.Key.AyahNumber,
                    ErrorCount = g.Count(),
                    ErrorTypes = g.GroupBy(e => e.Type).ToDictionary(t => t.Key, t => t.Count())
                })
                .OrderByDescending(a => a.ErrorCount)
                .Take(5)
                .ToList();
        }

        private double CalculateWordsPerMinute(RecitationSession session)
        {
            if (session.TotalRecitationTimeSeconds <= 0) return 0;
            
            var totalWords = session.TotalChunksProcessed * 10; // Estimated words per chunk
            var minutes = session.TotalRecitationTimeSeconds / 60.0;
            
            return totalWords / minutes;
        }
    }

    // MARK: - Request/Response Models

    public class StartSessionRequest
    {
        public int SurahNumber { get; set; } = 1;
        public int AyahNumber { get; set; } = 1;
        public string? Mode { get; set; } = "guided";
        public double? ErrorThreshold { get; set; } = 0.7;
    }

    public class RecitationStatistics
    {
        public string SessionId { get; set; } = "";
        public double TotalDurationSeconds { get; set; }
        public int TotalChunksProcessed { get; set; }
        public int TotalErrors { get; set; }
        public double AverageAccuracy { get; set; }
        public Dictionary<string, int> ErrorBreakdown { get; set; } = new();
        public RecitationProgress CurrentProgress { get; set; } = new();
        public List<AyahDifficulty> MostDifficultAyahs { get; set; } = new();
        public double WordsPerMinute { get; set; }
    }

    public class AyahDifficulty
    {
        public int SurahNumber { get; set; }
        public int AyahNumber { get; set; }
        public int ErrorCount { get; set; }
        public Dictionary<string, int> ErrorTypes { get; set; } = new();
    }
} 