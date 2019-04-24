
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SharpNao
{
    public class SharpNao
    {
        /// <summary>
        /// An enumeration of values mapped to the explicitness rating of an image.
        /// </summary>
        public enum SourceRating
        {
            /// <summary>The image explicitness rating could not be determained.</summary>
            Unknown = 0,
            /// <summary>The image explicitness rating was determained to be safe, and contains no nudity.</summary>
            Safe = 1,
            /// <summary>The image explicitness rating was determained to be questionable, and could contain nudity.</summary>
            Questionable = 2,
            /// <summary>The image explicitness rating was determained to be NSFW, and contains nudity.</summary>
            Nsfw = 3
        }

        /// <summary>
        /// An enumeration of values mapped to the type of response we want the API to give us.
        /// </summary>
        public enum OutputType
        {
            // NOT SUPPORTED
            Normal = 0,
            // NOT SUPPORTED
            XML = 1,
            // Only one that works at the moment
            Json = 2
        }

        [Flags]
        public enum DbMaskIndex : long
        {
            AllDatabases = 0,
            HMagazines = 1,
            HGameCG = 2,
            DoujinshiDb = 4,
            Pixiv = 8,
            NicoNicoSeiga = 16,
            Danbooru = 32,
            Drawr = 64,
            Nijie = 128,
            Yandere = 256,
            Shutterstock = 512,
            FAKKU = 1024,
            HMisc = 2048,
            TwoDMarket = 4096,
            MediBang = 8192,
            Anime = 16384,
            HAnime = 32768,
            Movies = 65536,
            Shows = 131072,
            Gelbooru = 262144,
            Konachan = 524288,
            AnimePictureNet = 1048576,
            e621Net = 2097152,
            IdolComplex = 4194304,
            BcyNetIllust = 8388608,
            BcyNetCosplay = 16777216,
            PortalGraphicsNet = 33554432,
            DeviantArt = 67108864,
            PawooNet = 134217728,
            Madokami = 536870912,
            MangaDex = 1073741824
        }

        public class RateLimiter
        {
            /// <summary>
            /// The amount of searchs that can be made per limit cycle
            /// </summary>
            public ushort UsesPerLimitCycle { get; private set; }

            /// <summary>
            /// How long does each cycle last
            /// </summary>
            public TimeSpan CycleLength { get; private set; }

            /// <summary>
            /// When was the last cycle started?
            /// </summary>
            public DateTime LastCycleTime { get; private set; }

            /// <summary>
            /// How many uses have been made this cycle
            /// </summary>
            public ushort CurrentUses { get; private set; }

            /// <summary>
            /// Check if we are currently being limited
            /// </summary>
            public bool IsLimited()
            {
                CurrentUses++; // Increment the amount of times we've used it this cycle

                // Have we gone into a new cycle?
                if (LastCycleTime.Add(CycleLength) < DateTime.Now)
                {
                    CurrentUses = 1; // A new cycle dawns, reset.
                    LastCycleTime = DateTime.Now;
                    return false; // Not limited anymore
                }

                // If we max out our uses this cycle
                if (CurrentUses >= UsesPerLimitCycle)
                    return true; // Limited

                return false; // If we get here, we're not limited
            }

            public RateLimiter(ushort usesPerCycle, TimeSpan cycleLength)
            {
                CurrentUses = 0;
                LastCycleTime = DateTime.Today;
                CycleLength = cycleLength;
                UsesPerLimitCycle = usesPerCycle;
            }
        }

        public class SauceResult
        {
            /// <summary>
            /// The url where the source is from
            /// </summary>
            public string Url { get; }

            /// <summary>
            /// The name of the site that the original source is from
            /// </summary>
            public string Site { get; }

            /// <summary>
            /// 
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// How similar is the image to the one provided (Percentage)?
            /// </summary>
            public float Similarity { get; }

            /// <summary>
            /// A link to the thumbnail of the image
            /// </summary>
            public string Thumbnail { get; }

            /// <summary>
            /// How explicit is the image?
            /// </summary>
            public SourceRating Rating { get; }
        }

        /// <summary>
        /// The default Api Url. Can be changed in case it ever moves.
        /// </summary>
        public string ApiUrl => "https://saucenao.com/search.php";

        /// <summary>
        /// The key used the connect to your account on SauceNao. 
        /// </summary>
        private string ApiKey { get; }

        /// <summary>
        /// The amount of results that will be fetched by default. This can be overridden per search. Default 6.
        /// </summary>
        public int DefaultResultCount { get; set; }

        /// <summary>
        /// The default response type. Can be overridden per search. Default JSON.
        /// </summary>
        public OutputType DefaultResponseType { get; set; }

        /// <summary>
        /// A rate limiter that prevents too many searchs being made too quickly. SauceNao uses a short term and long term limiter.
        /// The default values are setup to match a free account duration. The limiters can be overridden for premium accounts.
        /// Short Term Default: 12 searches every 30 seconds.
        /// </summary>
        public RateLimiter ShortTermRateLimiter { get; set; } = new RateLimiter(12, new TimeSpan(0, 0, 30));

        /// <summary>
        /// A rate limiter that prevents too many searchs being made too quickly. SauceNao uses a short term and long term limiter.
        /// The default values are setup to match a free account duration. The limiters can be overridden for premium accounts.
        /// Long Term Default: 300 searches every 24 hours.
        /// </summary>
        public RateLimiter LongTermRateLimiter { get; set; } = new RateLimiter(300, new TimeSpan(24, 0, 0));

        /// <summary>
        /// A bit mask for ENABLING specific indexes
        /// </summary>
        public DbMaskIndex DatabaseMask { get; set; }

        /// <summary>
        /// A bit mask for DISABLING specific indexes
        /// </summary>
        public DbMaskIndex DatabaseMaskInverted { get; set; }

        /// <summary>
        /// When true, only a single result will ever be returned.
        /// </summary>
        public bool TestMode { get; set; }

        /// <summary>
        /// When true, an explicitness rating will be returned, gauging how lewd the image is.
        /// </summary>
        public bool ReturnRatings { get; set; }

        /// <summary>
        /// When true, questionable and nsfw results will be ignored from searchs.
        /// </summary>
        public bool PreventExplicitResults { get; set; }

        /// <summary>
        /// When true, results with a rating of unknown will be treated as questionable when filtering explicit results.
        /// </summary>
        public bool TreatUnknownAsQuestionable { get; set; }

        /// <summary>
        /// When true, queries can be made regardless of ratelimits. This could cause unintended behaviour if implemented incorrectly.
        /// It's suggested to leave this disabled unless you know what you're doing.
        /// </summary>
        public bool IgnoreRatelimits { get; set; }

        /// <summary>
        /// An array of the allowed file types. We check against these before sending the request.
        /// </summary>
        private string[] AllowedFileTypes { get; } = new[] {".jpg", ".jpeg", ".gif", ".bmp", ".png"};

        /// <summary></summary>
        /// <param name="apiKey">Your SauceNao Api Key</param>
        public SharpNao(string apiKey)
        {
            ApiKey = apiKey;
            DefaultResultCount = 6;
            DefaultResponseType = OutputType.Json;
            TestMode = false;

            ReturnRatings = true;
            PreventExplicitResults = false;
            TreatUnknownAsQuestionable = true;
            IgnoreRatelimits = false;
        }

        public async Task<KeyValuePair<string, SauceResult>> GetResultAsync(string sauceUrl, int results = 0)
        {
            if(!(Uri.TryCreate(sauceUrl, UriKind.Absolute, out Uri uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
                return new KeyValuePair<string, SauceResult>("Url supplied was not valid.", null);

            if (results == 0)
                results = DefaultResultCount;

            Dictionary<string, string> postData = new Dictionary<string, string>()
            {
                { "api_key", this.ApiKey },
                { "output_type", this.DefaultResponseType.ToString() },
                { "numres", results.ToString() },
                { "testmode", this.TestMode ? "1" : "0" },
                { "url", sauceUrl }
            };

            HttpClient client = new HttpClient();
            FormUrlEncodedContent data = new FormUrlEncodedContent(postData);
            HttpResponseMessage res = await client.PostAsync(this.ApiUrl, data);
            Stream json = await res.Content.ReadAsStreamAsync();

            using (StreamReader reader = new StreamReader(json))
            {
                Console.WriteLine(reader.ReadToEnd());
            }

            return new KeyValuePair<string, SauceResult>();
        }
    }
}
