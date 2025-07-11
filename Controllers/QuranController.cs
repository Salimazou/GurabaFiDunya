using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GurabaFiDunya.DTOs;
using System.Text.Json;

namespace GurabaFiDunya.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuranController : ControllerBase
{
    private readonly HttpClient _httpClient;
    
    public QuranController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    [HttpGet("metadata")]
    public async Task<IActionResult> GetQuranMetadata()
    {
        try
        {
            // Static data for reciters and surahs
            var reciters = new List<ReciterInfo>
            {
                new ReciterInfo { Id = "1", Name = "Abdul Rahman Al-Sudais", NameArabic = "عبد الرحمن السديس", Subdirectory = "abdulrahman_as_sudais", Style = "Tarteel" },
                new ReciterInfo { Id = "2", Name = "Mishary Rashid Alafasy", NameArabic = "مشاري راشد العفاسي", Subdirectory = "mishary_rashid_alafasy", Style = "Tarteel" },
                new ReciterInfo { Id = "3", Name = "Saad Al-Ghamdi", NameArabic = "سعد الغامدي", Subdirectory = "saad_al_ghamdi", Style = "Tarteel" },
                new ReciterInfo { Id = "4", Name = "Maher Al-Muaiqly", NameArabic = "ماهر المعيقلي", Subdirectory = "maher_al_muaiqly", Style = "Tarteel" },
                new ReciterInfo { Id = "5", Name = "Ahmed Al-Ajmi", NameArabic = "أحمد العجمي", Subdirectory = "ahmed_al_ajmi", Style = "Tarteel" },
                new ReciterInfo { Id = "6", Name = "Yasser Al-Dosari", NameArabic = "ياسر الدوسري", Subdirectory = "yasser_al_dosari", Style = "Tarteel" },
                new ReciterInfo { Id = "7", Name = "Abdur-Rahman as-Sudais", NameArabic = "عبد الرحمن السديس", Subdirectory = "abdurrahman_as_sudais", Style = "Tarteel" },
                new ReciterInfo { Id = "8", Name = "Nasser Al-Qatami", NameArabic = "ناصر القطامي", Subdirectory = "nasser_al_qatami", Style = "Tarteel" }
            };
            
            var surahs = new List<SurahInfo>
            {
                new SurahInfo { Number = 1, Name = "Al-Fatihah", NameArabic = "الفاتحة", NameTranslation = "The Opening", Verses = 7, RevelationType = "Meccan" },
                new SurahInfo { Number = 2, Name = "Al-Baqarah", NameArabic = "البقرة", NameTranslation = "The Cow", Verses = 286, RevelationType = "Medinan" },
                new SurahInfo { Number = 3, Name = "Ali 'Imran", NameArabic = "آل عمران", NameTranslation = "Family of Imran", Verses = 200, RevelationType = "Medinan" },
                new SurahInfo { Number = 4, Name = "An-Nisa", NameArabic = "النساء", NameTranslation = "The Women", Verses = 176, RevelationType = "Medinan" },
                new SurahInfo { Number = 5, Name = "Al-Ma'idah", NameArabic = "المائدة", NameTranslation = "The Table Spread", Verses = 120, RevelationType = "Medinan" },
                new SurahInfo { Number = 6, Name = "Al-An'am", NameArabic = "الأنعام", NameTranslation = "The Cattle", Verses = 165, RevelationType = "Meccan" },
                new SurahInfo { Number = 7, Name = "Al-A'raf", NameArabic = "الأعراف", NameTranslation = "The Heights", Verses = 206, RevelationType = "Meccan" },
                new SurahInfo { Number = 8, Name = "Al-Anfal", NameArabic = "الأنفال", NameTranslation = "The Spoils of War", Verses = 75, RevelationType = "Medinan" },
                new SurahInfo { Number = 9, Name = "At-Tawbah", NameArabic = "التوبة", NameTranslation = "The Repentance", Verses = 129, RevelationType = "Medinan" },
                new SurahInfo { Number = 10, Name = "Yunus", NameArabic = "يونس", NameTranslation = "Jonah", Verses = 109, RevelationType = "Meccan" },
                new SurahInfo { Number = 11, Name = "Hud", NameArabic = "هود", NameTranslation = "Hud", Verses = 123, RevelationType = "Meccan" },
                new SurahInfo { Number = 12, Name = "Yusuf", NameArabic = "يوسف", NameTranslation = "Joseph", Verses = 111, RevelationType = "Meccan" },
                new SurahInfo { Number = 13, Name = "Ar-Ra'd", NameArabic = "الرعد", NameTranslation = "The Thunder", Verses = 43, RevelationType = "Medinan" },
                new SurahInfo { Number = 14, Name = "Ibrahim", NameArabic = "إبراهيم", NameTranslation = "Abraham", Verses = 52, RevelationType = "Meccan" },
                new SurahInfo { Number = 15, Name = "Al-Hijr", NameArabic = "الحجر", NameTranslation = "The Rocky Tract", Verses = 99, RevelationType = "Meccan" },
                new SurahInfo { Number = 16, Name = "An-Nahl", NameArabic = "النحل", NameTranslation = "The Bee", Verses = 128, RevelationType = "Meccan" },
                new SurahInfo { Number = 17, Name = "Al-Isra", NameArabic = "الإسراء", NameTranslation = "The Night Journey", Verses = 111, RevelationType = "Meccan" },
                new SurahInfo { Number = 18, Name = "Al-Kahf", NameArabic = "الكهف", NameTranslation = "The Cave", Verses = 110, RevelationType = "Meccan" },
                new SurahInfo { Number = 19, Name = "Maryam", NameArabic = "مريم", NameTranslation = "Mary", Verses = 98, RevelationType = "Meccan" },
                new SurahInfo { Number = 20, Name = "Taha", NameArabic = "طه", NameTranslation = "Ta-Ha", Verses = 135, RevelationType = "Meccan" },
                new SurahInfo { Number = 21, Name = "Al-Anbya", NameArabic = "الأنبياء", NameTranslation = "The Prophets", Verses = 112, RevelationType = "Meccan" },
                new SurahInfo { Number = 22, Name = "Al-Hajj", NameArabic = "الحج", NameTranslation = "The Pilgrimage", Verses = 78, RevelationType = "Medinan" },
                new SurahInfo { Number = 23, Name = "Al-Mu'minun", NameArabic = "المؤمنون", NameTranslation = "The Believers", Verses = 118, RevelationType = "Meccan" },
                new SurahInfo { Number = 24, Name = "An-Nur", NameArabic = "النور", NameTranslation = "The Light", Verses = 64, RevelationType = "Medinan" },
                new SurahInfo { Number = 25, Name = "Al-Furqan", NameArabic = "الفرقان", NameTranslation = "The Criterion", Verses = 77, RevelationType = "Meccan" },
                new SurahInfo { Number = 26, Name = "Ash-Shu'ara", NameArabic = "الشعراء", NameTranslation = "The Poets", Verses = 227, RevelationType = "Meccan" },
                new SurahInfo { Number = 27, Name = "An-Naml", NameArabic = "النمل", NameTranslation = "The Ant", Verses = 93, RevelationType = "Meccan" },
                new SurahInfo { Number = 28, Name = "Al-Qasas", NameArabic = "القصص", NameTranslation = "The Stories", Verses = 88, RevelationType = "Meccan" },
                new SurahInfo { Number = 29, Name = "Al-'Ankabut", NameArabic = "العنكبوت", NameTranslation = "The Spider", Verses = 69, RevelationType = "Meccan" },
                new SurahInfo { Number = 30, Name = "Ar-Rum", NameArabic = "الروم", NameTranslation = "The Romans", Verses = 60, RevelationType = "Meccan" },
                new SurahInfo { Number = 31, Name = "Luqman", NameArabic = "لقمان", NameTranslation = "Luqman", Verses = 34, RevelationType = "Meccan" },
                new SurahInfo { Number = 32, Name = "As-Sajdah", NameArabic = "السجدة", NameTranslation = "The Prostration", Verses = 30, RevelationType = "Meccan" },
                new SurahInfo { Number = 33, Name = "Al-Ahzab", NameArabic = "الأحزاب", NameTranslation = "The Clans", Verses = 73, RevelationType = "Medinan" },
                new SurahInfo { Number = 34, Name = "Saba", NameArabic = "سبأ", NameTranslation = "Sheba", Verses = 54, RevelationType = "Meccan" },
                new SurahInfo { Number = 35, Name = "Fatir", NameArabic = "فاطر", NameTranslation = "Originator", Verses = 45, RevelationType = "Meccan" },
                new SurahInfo { Number = 36, Name = "Ya-Sin", NameArabic = "يس", NameTranslation = "Ya Sin", Verses = 83, RevelationType = "Meccan" },
                new SurahInfo { Number = 37, Name = "As-Saffat", NameArabic = "الصافات", NameTranslation = "Those who set the Ranks", Verses = 182, RevelationType = "Meccan" },
                new SurahInfo { Number = 38, Name = "Sad", NameArabic = "ص", NameTranslation = "The Letter Sad", Verses = 88, RevelationType = "Meccan" },
                new SurahInfo { Number = 39, Name = "Az-Zumar", NameArabic = "الزمر", NameTranslation = "The Troops", Verses = 75, RevelationType = "Meccan" },
                new SurahInfo { Number = 40, Name = "Ghafir", NameArabic = "غافر", NameTranslation = "The Forgiver", Verses = 85, RevelationType = "Meccan" },
                new SurahInfo { Number = 41, Name = "Fussilat", NameArabic = "فصلت", NameTranslation = "Explained in Detail", Verses = 54, RevelationType = "Meccan" },
                new SurahInfo { Number = 42, Name = "Ash-Shuraa", NameArabic = "الشورى", NameTranslation = "The Consultation", Verses = 53, RevelationType = "Meccan" },
                new SurahInfo { Number = 43, Name = "Az-Zukhruf", NameArabic = "الزخرف", NameTranslation = "The Ornaments of Gold", Verses = 89, RevelationType = "Meccan" },
                new SurahInfo { Number = 44, Name = "Ad-Dukhan", NameArabic = "الدخان", NameTranslation = "The Smoke", Verses = 59, RevelationType = "Meccan" },
                new SurahInfo { Number = 45, Name = "Al-Jathiyah", NameArabic = "الجاثية", NameTranslation = "The Crouching", Verses = 37, RevelationType = "Meccan" },
                new SurahInfo { Number = 46, Name = "Al-Ahqaf", NameArabic = "الأحقاف", NameTranslation = "The Wind-Curved Sandhills", Verses = 35, RevelationType = "Meccan" },
                new SurahInfo { Number = 47, Name = "Muhammad", NameArabic = "محمد", NameTranslation = "Muhammad", Verses = 38, RevelationType = "Medinan" },
                new SurahInfo { Number = 48, Name = "Al-Fath", NameArabic = "الفتح", NameTranslation = "The Victory", Verses = 29, RevelationType = "Medinan" },
                new SurahInfo { Number = 49, Name = "Al-Hujurat", NameArabic = "الحجرات", NameTranslation = "The Rooms", Verses = 18, RevelationType = "Medinan" },
                new SurahInfo { Number = 50, Name = "Qaf", NameArabic = "ق", NameTranslation = "The Letter Qaf", Verses = 45, RevelationType = "Meccan" },
                new SurahInfo { Number = 51, Name = "Adh-Dhariyat", NameArabic = "الذاريات", NameTranslation = "The Winnowing Winds", Verses = 60, RevelationType = "Meccan" },
                new SurahInfo { Number = 52, Name = "At-Tur", NameArabic = "الطور", NameTranslation = "The Mount", Verses = 49, RevelationType = "Meccan" },
                new SurahInfo { Number = 53, Name = "An-Najm", NameArabic = "النجم", NameTranslation = "The Star", Verses = 62, RevelationType = "Meccan" },
                new SurahInfo { Number = 54, Name = "Al-Qamar", NameArabic = "القمر", NameTranslation = "The Moon", Verses = 55, RevelationType = "Meccan" },
                new SurahInfo { Number = 55, Name = "Ar-Rahman", NameArabic = "الرحمن", NameTranslation = "The Beneficent", Verses = 78, RevelationType = "Medinan" },
                new SurahInfo { Number = 56, Name = "Al-Waqi'ah", NameArabic = "الواقعة", NameTranslation = "The Inevitable", Verses = 96, RevelationType = "Meccan" },
                new SurahInfo { Number = 57, Name = "Al-Hadid", NameArabic = "الحديد", NameTranslation = "The Iron", Verses = 29, RevelationType = "Medinan" },
                new SurahInfo { Number = 58, Name = "Al-Mujadilah", NameArabic = "المجادلة", NameTranslation = "The Pleading Woman", Verses = 22, RevelationType = "Medinan" },
                new SurahInfo { Number = 59, Name = "Al-Hashr", NameArabic = "الحشر", NameTranslation = "The Exile", Verses = 24, RevelationType = "Medinan" },
                new SurahInfo { Number = 60, Name = "Al-Mumtahanah", NameArabic = "الممتحنة", NameTranslation = "She that is to be examined", Verses = 13, RevelationType = "Medinan" },
                new SurahInfo { Number = 61, Name = "As-Saff", NameArabic = "الصف", NameTranslation = "The Ranks", Verses = 14, RevelationType = "Medinan" },
                new SurahInfo { Number = 62, Name = "Al-Jumu'ah", NameArabic = "الجمعة", NameTranslation = "The Friday", Verses = 11, RevelationType = "Medinan" },
                new SurahInfo { Number = 63, Name = "Al-Munafiqun", NameArabic = "المنافقون", NameTranslation = "The Hypocrites", Verses = 11, RevelationType = "Medinan" },
                new SurahInfo { Number = 64, Name = "At-Taghabun", NameArabic = "التغابن", NameTranslation = "The Mutual Disillusion", Verses = 18, RevelationType = "Medinan" },
                new SurahInfo { Number = 65, Name = "At-Talaq", NameArabic = "الطلاق", NameTranslation = "The Divorce", Verses = 12, RevelationType = "Medinan" },
                new SurahInfo { Number = 66, Name = "At-Tahrim", NameArabic = "التحريم", NameTranslation = "The Prohibition", Verses = 12, RevelationType = "Medinan" },
                new SurahInfo { Number = 67, Name = "Al-Mulk", NameArabic = "الملك", NameTranslation = "The Sovereignty", Verses = 30, RevelationType = "Meccan" },
                new SurahInfo { Number = 68, Name = "Al-Qalam", NameArabic = "القلم", NameTranslation = "The Pen", Verses = 52, RevelationType = "Meccan" },
                new SurahInfo { Number = 69, Name = "Al-Haqqah", NameArabic = "الحاقة", NameTranslation = "The Reality", Verses = 52, RevelationType = "Meccan" },
                new SurahInfo { Number = 70, Name = "Al-Ma'arij", NameArabic = "المعارج", NameTranslation = "The Ascending Stairways", Verses = 44, RevelationType = "Meccan" },
                new SurahInfo { Number = 71, Name = "Nuh", NameArabic = "نوح", NameTranslation = "Noah", Verses = 28, RevelationType = "Meccan" },
                new SurahInfo { Number = 72, Name = "Al-Jinn", NameArabic = "الجن", NameTranslation = "The Jinn", Verses = 28, RevelationType = "Meccan" },
                new SurahInfo { Number = 73, Name = "Al-Muzzammil", NameArabic = "المزمل", NameTranslation = "The Enshrouded One", Verses = 20, RevelationType = "Meccan" },
                new SurahInfo { Number = 74, Name = "Al-Muddaththir", NameArabic = "المدثر", NameTranslation = "The Cloaked One", Verses = 56, RevelationType = "Meccan" },
                new SurahInfo { Number = 75, Name = "Al-Qiyamah", NameArabic = "القيامة", NameTranslation = "The Resurrection", Verses = 40, RevelationType = "Meccan" },
                new SurahInfo { Number = 76, Name = "Al-Insan", NameArabic = "الإنسان", NameTranslation = "The Man", Verses = 31, RevelationType = "Medinan" },
                new SurahInfo { Number = 77, Name = "Al-Mursalat", NameArabic = "المرسلات", NameTranslation = "The Emissaries", Verses = 50, RevelationType = "Meccan" },
                new SurahInfo { Number = 78, Name = "An-Naba", NameArabic = "النبأ", NameTranslation = "The Announcement", Verses = 40, RevelationType = "Meccan" },
                new SurahInfo { Number = 79, Name = "An-Nazi'at", NameArabic = "النازعات", NameTranslation = "Those who drag forth", Verses = 46, RevelationType = "Meccan" },
                new SurahInfo { Number = 80, Name = "Abasa", NameArabic = "عبس", NameTranslation = "He frowned", Verses = 42, RevelationType = "Meccan" },
                new SurahInfo { Number = 81, Name = "At-Takwir", NameArabic = "التكوير", NameTranslation = "The Overthrowing", Verses = 29, RevelationType = "Meccan" },
                new SurahInfo { Number = 82, Name = "Al-Infitar", NameArabic = "الإنفطار", NameTranslation = "The Cleaving", Verses = 19, RevelationType = "Meccan" },
                new SurahInfo { Number = 83, Name = "Al-Mutaffifin", NameArabic = "المطففين", NameTranslation = "The Defrauding", Verses = 36, RevelationType = "Meccan" },
                new SurahInfo { Number = 84, Name = "Al-Inshiqaq", NameArabic = "الإنشقاق", NameTranslation = "The Splitting Open", Verses = 25, RevelationType = "Meccan" },
                new SurahInfo { Number = 85, Name = "Al-Buruj", NameArabic = "البروج", NameTranslation = "The Mansions of the Stars", Verses = 22, RevelationType = "Meccan" },
                new SurahInfo { Number = 86, Name = "At-Tariq", NameArabic = "الطارق", NameTranslation = "The Morning Star", Verses = 17, RevelationType = "Meccan" },
                new SurahInfo { Number = 87, Name = "Al-A'la", NameArabic = "الأعلى", NameTranslation = "The Most High", Verses = 19, RevelationType = "Meccan" },
                new SurahInfo { Number = 88, Name = "Al-Ghashiyah", NameArabic = "الغاشية", NameTranslation = "The Overwhelming", Verses = 26, RevelationType = "Meccan" },
                new SurahInfo { Number = 89, Name = "Al-Fajr", NameArabic = "الفجر", NameTranslation = "The Dawn", Verses = 30, RevelationType = "Meccan" },
                new SurahInfo { Number = 90, Name = "Al-Balad", NameArabic = "البلد", NameTranslation = "The City", Verses = 20, RevelationType = "Meccan" },
                new SurahInfo { Number = 91, Name = "Ash-Shams", NameArabic = "الشمس", NameTranslation = "The Sun", Verses = 15, RevelationType = "Meccan" },
                new SurahInfo { Number = 92, Name = "Al-Layl", NameArabic = "الليل", NameTranslation = "The Night", Verses = 21, RevelationType = "Meccan" },
                new SurahInfo { Number = 93, Name = "Ad-Duhaa", NameArabic = "الضحى", NameTranslation = "The Morning Hours", Verses = 11, RevelationType = "Meccan" },
                new SurahInfo { Number = 94, Name = "Ash-Sharh", NameArabic = "الشرح", NameTranslation = "The Relief", Verses = 8, RevelationType = "Meccan" },
                new SurahInfo { Number = 95, Name = "At-Tin", NameArabic = "التين", NameTranslation = "The Fig", Verses = 8, RevelationType = "Meccan" },
                new SurahInfo { Number = 96, Name = "Al-Alaq", NameArabic = "العلق", NameTranslation = "The Clot", Verses = 19, RevelationType = "Meccan" },
                new SurahInfo { Number = 97, Name = "Al-Qadr", NameArabic = "القدر", NameTranslation = "The Power", Verses = 5, RevelationType = "Meccan" },
                new SurahInfo { Number = 98, Name = "Al-Bayyinah", NameArabic = "البينة", NameTranslation = "The Evidence", Verses = 8, RevelationType = "Medinan" },
                new SurahInfo { Number = 99, Name = "Az-Zalzalah", NameArabic = "الزلزلة", NameTranslation = "The Earthquake", Verses = 8, RevelationType = "Medinan" },
                new SurahInfo { Number = 100, Name = "Al-Adiyat", NameArabic = "العاديات", NameTranslation = "The Courser", Verses = 11, RevelationType = "Meccan" },
                new SurahInfo { Number = 101, Name = "Al-Qari'ah", NameArabic = "القارعة", NameTranslation = "The Calamity", Verses = 11, RevelationType = "Meccan" },
                new SurahInfo { Number = 102, Name = "At-Takathur", NameArabic = "التكاثر", NameTranslation = "The Rivalry in world increase", Verses = 8, RevelationType = "Meccan" },
                new SurahInfo { Number = 103, Name = "Al-Asr", NameArabic = "العصر", NameTranslation = "The Declining Day", Verses = 3, RevelationType = "Meccan" },
                new SurahInfo { Number = 104, Name = "Al-Humazah", NameArabic = "الهمزة", NameTranslation = "The Traducer", Verses = 9, RevelationType = "Meccan" },
                new SurahInfo { Number = 105, Name = "Al-Fil", NameArabic = "الفيل", NameTranslation = "The Elephant", Verses = 5, RevelationType = "Meccan" },
                new SurahInfo { Number = 106, Name = "Quraysh", NameArabic = "قريش", NameTranslation = "Quraysh", Verses = 4, RevelationType = "Meccan" },
                new SurahInfo { Number = 107, Name = "Al-Ma'un", NameArabic = "الماعون", NameTranslation = "The Small kindnesses", Verses = 7, RevelationType = "Meccan" },
                new SurahInfo { Number = 108, Name = "Al-Kawthar", NameArabic = "الكوثر", NameTranslation = "The Abundance", Verses = 3, RevelationType = "Meccan" },
                new SurahInfo { Number = 109, Name = "Al-Kafirun", NameArabic = "الكافرون", NameTranslation = "The Disbelievers", Verses = 6, RevelationType = "Meccan" },
                new SurahInfo { Number = 110, Name = "An-Nasr", NameArabic = "النصر", NameTranslation = "The Divine Support", Verses = 3, RevelationType = "Medinan" },
                new SurahInfo { Number = 111, Name = "Al-Masad", NameArabic = "المسد", NameTranslation = "The Palm Fibre", Verses = 5, RevelationType = "Meccan" },
                new SurahInfo { Number = 112, Name = "Al-Ikhlas", NameArabic = "الإخلاص", NameTranslation = "The Sincerity", Verses = 4, RevelationType = "Meccan" },
                new SurahInfo { Number = 113, Name = "Al-Falaq", NameArabic = "الفلق", NameTranslation = "The Daybreak", Verses = 5, RevelationType = "Meccan" },
                new SurahInfo { Number = 114, Name = "An-Nas", NameArabic = "الناس", NameTranslation = "Mankind", Verses = 6, RevelationType = "Meccan" }
            };
            
            var response = new QuranMetadataResponse
            {
                Reciters = reciters,
                Surahs = surahs
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpGet("audio/{reciterId}/{surahNumber}")]
    public IActionResult GetAudioUrl(string reciterId, int surahNumber)
    {
        try
        {
            if (surahNumber < 1 || surahNumber > 114)
            {
                return BadRequest("Invalid surah number. Must be between 1 and 114.");
            }
            
            // Get reciter info
            var reciters = new Dictionary<string, ReciterInfo>
            {
                { "1", new ReciterInfo { Id = "1", Name = "Abdul Rahman Al-Sudais", Subdirectory = "abdulrahman_as_sudais" } },
                { "2", new ReciterInfo { Id = "2", Name = "Mishary Rashid Alafasy", Subdirectory = "mishary_rashid_alafasy" } },
                { "3", new ReciterInfo { Id = "3", Name = "Saad Al-Ghamdi", Subdirectory = "saad_al_ghamdi" } },
                { "4", new ReciterInfo { Id = "4", Name = "Maher Al-Muaiqly", Subdirectory = "maher_al_muaiqly" } },
                { "5", new ReciterInfo { Id = "5", Name = "Ahmed Al-Ajmi", Subdirectory = "ahmed_al_ajmi" } },
                { "6", new ReciterInfo { Id = "6", Name = "Yasser Al-Dosari", Subdirectory = "yasser_al_dosari" } },
                { "7", new ReciterInfo { Id = "7", Name = "Abdur-Rahman as-Sudais", Subdirectory = "abdurrahman_as_sudais" } },
                { "8", new ReciterInfo { Id = "8", Name = "Nasser Al-Qatami", Subdirectory = "nasser_al_qatami" } }
            };
            
            if (!reciters.ContainsKey(reciterId))
            {
                return NotFound("Reciter not found");
            }
            
            var reciter = reciters[reciterId];
            
            // Format surah number with leading zeros
            var formattedSurahNumber = surahNumber.ToString("D3");
            
            // Construct audio URL (using QuranicAudio style)
            var audioUrl = $"https://download.quranicaudio.com/quran/{reciter.Subdirectory}/{formattedSurahNumber}.mp3";
            
            var response = new AudioUrlResponse
            {
                AudioUrl = audioUrl,
                ReciterName = reciter.Name,
                SurahNumber = surahNumber
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpGet("reciters")]
    public IActionResult GetReciters()
    {
        try
        {
            var reciters = new List<ReciterInfo>
            {
                new ReciterInfo { Id = "1", Name = "Abdul Rahman Al-Sudais", NameArabic = "عبد الرحمن السديس", Subdirectory = "abdulrahman_as_sudais", Style = "Tarteel" },
                new ReciterInfo { Id = "2", Name = "Mishary Rashid Alafasy", NameArabic = "مشاري راشد العفاسي", Subdirectory = "mishary_rashid_alafasy", Style = "Tarteel" },
                new ReciterInfo { Id = "3", Name = "Saad Al-Ghamdi", NameArabic = "سعد الغامدي", Subdirectory = "saad_al_ghamdi", Style = "Tarteel" },
                new ReciterInfo { Id = "4", Name = "Maher Al-Muaiqly", NameArabic = "ماهر المعيقلي", Subdirectory = "maher_al_muaiqly", Style = "Tarteel" },
                new ReciterInfo { Id = "5", Name = "Ahmed Al-Ajmi", NameArabic = "أحمد العجمي", Subdirectory = "ahmed_al_ajmi", Style = "Tarteel" },
                new ReciterInfo { Id = "6", Name = "Yasser Al-Dosari", NameArabic = "ياسر الدوسري", Subdirectory = "yasser_al_dosari", Style = "Tarteel" },
                new ReciterInfo { Id = "7", Name = "Abdur-Rahman as-Sudais", NameArabic = "عبد الرحمن السديس", Subdirectory = "abdurrahman_as_sudais", Style = "Tarteel" },
                new ReciterInfo { Id = "8", Name = "Nasser Al-Qatami", NameArabic = "ناصر القطامي", Subdirectory = "nasser_al_qatami", Style = "Tarteel" }
            };
            
            return Ok(reciters);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    [HttpGet("surahs")]
    public IActionResult GetSurahs()
    {
        try
        {
            var surahs = new List<SurahInfo>();
            
            // Add some example surahs (you can expand this list)
            for (int i = 1; i <= 114; i++)
            {
                surahs.Add(new SurahInfo 
                { 
                    Number = i, 
                    Name = $"Surah {i}", 
                    NameArabic = $"سورة {i}", 
                    NameTranslation = $"Surah {i}",
                    Verses = i == 1 ? 7 : i == 2 ? 286 : 10, // Simplified
                    RevelationType = i <= 90 ? "Meccan" : "Medinan"
                });
            }
            
            return Ok(surahs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
} 