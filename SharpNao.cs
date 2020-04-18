using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Json;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace Laz.Api
{
    public class SharpNao
    {
        /// <summary>Convert a word that is formatted in pascal case to have splits (by space) at each upper case letter.</summary>
        private static string SplitPascalCase(string convert)
        {
            return Regex.Replace(Regex.Replace(convert, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"), @"(\p{Ll})(\P{Ll})", "$1 $2");
        }

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

        public enum SiteIndex
        {
            DoujinshiMangaLexicon = 3,
            Pixiv = 5,
            PixivArchive = 6,
            NicoNicoSeiga = 8,
            Danbooru = 9,
            Drawr = 10,
            Nijie = 11,
            Yandere = 12,
            OpeningsMoe = 13,
            FAKKU = 16,
            nHentai = 18,
            TwoDMarket = 19,
            MediBang = 20,
            AniDb = 21,
            IMDB = 23,
            Gelbooru = 25,
            Konachan = 26,
            SankakuChannel = 27,
            AnimePictures = 28,
            e621 = 29,
            IdolComplex = 30,
            BcyNetIllust = 31,
            BcyNetCosplay = 32,
            PortalGraphics = 33,
            DeviantArt = 34,
            Pawoo = 35,
            MangaUpdates = 36,
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

        [DataContract]
        public class SourceResult
        {
            /// <summary>
            /// The url(s) where the source is from. Multiple will be returned if the exact same image is found in multiple places
            /// </summary>
            [DataMember(Name = "ext_urls")]
            public string[] Url { get; internal set; }           

            /// <summary>
            /// The search index of the image
            /// </summary>
            [DataMember(Name = "index_id")]
            public SiteIndex Index { get; internal set; }

            /// <summary>
            /// How similar is the image to the one provided (Percentage)?
            /// </summary>
            [DataMember(Name = "similarity")]
            public float Similarity { get; internal set; }

            /// <summary>
            /// A link to the thumbnail of the image
            /// </summary>
            [DataMember(Name = "thumbnail")]
            public string Thumbnail { get; internal set; }

            /// <summary>
            /// The name of the website it came from
            /// </summary>
            [IgnoreDataMember]
            public string WebsiteName { get; internal set; }

            /// <summary>
            /// How explicit is the image?
            /// </summary>
            [IgnoreDataMember]
            public SourceRating Rating { get; internal set; }
        }

        [DataContract]
        internal class SourceResultList
        {
            [DataMember(Name = "results")]
            internal SourceResult[] Results { get; set; }
        }

        /// <summary>
        /// The default Api Url. Can be changed in case it ever moves.
        /// </summary>
        public string ApiUrl { get; set; } = "https://saucenao.com/search.php";

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
        /// When true, only a single result will ever be returned.
        /// </summary>
        public bool TestMode { get; set; }

        /// <summary>
        /// When true, an explicitness rating will be returned. If false, everything will be set to unknown.
        /// </summary>
        public bool ReturnRatings { get; set; }

        /// <summary>
        /// When true, questionable and nsfw results will be ignored from searchs. The same amount of results will still be returned
        /// when the search is conducted, but explicit results will be given a value of null.
        /// If you requested six results and all were explicit with this setting active, you would get an array of six null values.
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
        private string[] AllowedFileTypes { get; } = new[] {".jpg", ".jpeg", ".gif", ".bmp", ".png", ".webp"};

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

        /// <summary>
        /// Get the source of an image.
        /// </summary>
        /// <param name="sauceUrl">The url of the image.</param>
        /// <param name="results">An optional override for the amount of results to fetch. If 0, it will use the default value.</param>
        /// <returns></returns>
        public async Task<KeyValuePair<string, SourceResult[]>> GetResultAsync(string sauceUrl, int results = 0)
        {
            if(!(Uri.TryCreate(sauceUrl, UriKind.Absolute, out Uri uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
                return new KeyValuePair<string, SourceResult[]>("Url supplied was not valid.", new SourceResult[0]);

            {
                string extension = Path.GetExtension(sauceUrl);

                int index = extension.IndexOf('?');
                if(index > 0)
                    extension = extension.Substring(0, index);

                if (AllowedFileTypes.All(x => x != extension))
                    return new KeyValuePair<string, SourceResult[]>("File provided was not of a valid format.", new SourceResult[0]);
            }

            if (results == 0)
                results = DefaultResultCount;

            if (!IgnoreRatelimits)
            {
                if (ShortTermRateLimiter.IsLimited())
                    return new KeyValuePair<string, SourceResult[]>
                        ($"You are being sort term rate limited. Check again after {ShortTermRateLimiter.CycleLength.Seconds} seconds.", new SourceResult[0]);

                if (LongTermRateLimiter.IsLimited())
                    return new KeyValuePair<string, SourceResult[]>
                        ($"You are being long term rate limited. Check again after {LongTermRateLimiter.CycleLength.Hours} hours.", new SourceResult[0]);
            }

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.PostAsync(ApiUrl, new MultipartFormDataContent
                {
                    {new StringContent(this.ApiKey), "api_key"},
                    {new StringContent(((int) this.DefaultResponseType).ToString()), "output_type"},
                    {new StringContent(results.ToString()), "numres"},
                    {new StringContent(this.TestMode ? "1" : "0"), "testmode"},
                    {new StringContent(sauceUrl), "url"},
                    {new StringContent("999"), "db"},
                });

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    return new KeyValuePair<string, SourceResult[]>("Response was not 200", new SourceResult[0]);
                // TODO: Actually do proper error handling

                JsonValue jsonString = JsonValue.Parse(await response.Content.ReadAsStringAsync());
                if (jsonString is JsonObject jsonObject)
                {
                    JsonValue jsonArray = jsonObject["results"];
                    for (int i = 0; i < jsonArray.Count; i++)
                    {
                        JsonValue header = jsonArray[i]["header"];
                        JsonValue data = jsonArray[i]["data"];
                        string obj = header.ToString();
                        obj = obj.Remove(obj.Length - 1);
                        obj += data.ToString().Remove(0, 1).Insert(0, ",");
                        jsonArray[i] = JsonValue.Parse(obj);
                    }

                    string json = jsonArray.ToString();
                    json = json.Insert(json.Length - 1, "}").Insert(0, "{\"results\":");
                    using (var stream = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(SourceResultList));
                        SourceResultList result = serializer.ReadObject(stream) as SourceResultList;
                        stream.Dispose();
                        if (result is null)
                            return new KeyValuePair<string, SourceResult[]>("Error parsing results.", new SourceResult[0]);

                        for (int i = 0; i < result.Results.Length; i++)
                        {
                            result.Results[i].WebsiteName = SplitPascalCase(result.Results[i].Index.ToString());
                            if (ReturnRatings)
                                result.Results[i] = await GetRating(client, result.Results[i], this.TreatUnknownAsQuestionable, this.PreventExplicitResults);
                            else
                                result.Results[i].Rating = SourceRating.Unknown;
                        }

                        return new KeyValuePair<string, SourceResult[]>("Success.", result.Results);
                    }
                }

                else
                    return new KeyValuePair<string, SourceResult[]>("Error parsing results.", new SourceResult[0]);
            }
        }

        private async Task<SourceResult> GetRating(HttpClient client, SourceResult result, bool unknownIsQuestionable, bool ignoreExplicit)
        {
            async Task<Match> WebRequest(string url, string pattern)
            {
                Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
                HttpResponseMessage res = await client.GetAsync(url);
                Match webMatch = regex.Match((await res.Content.ReadAsStringAsync()));
                return webMatch;
            }

            // TODO: Test how effective the regex is without the backup urls
            Match match = null;
            switch (result.Index)
            {
                case SiteIndex.DoujinshiMangaLexicon:
                    match = await WebRequest(result.Url[0], @"<td>.*?<b>Adult:<\/b><\/td><td>(.*)<\/td>");
                    if (match.Success)
                        result.Rating = match.Groups[1].Value == "Yes" ? SourceRating.Nsfw : SourceRating.Safe;
                    else result.Rating = SourceRating.Unknown;
                    break;

                case SiteIndex.Pixiv:
                case SiteIndex.PixivArchive:
                    match = await WebRequest(result.Url[0], @"<div class=""introduction-modal""><p class=""title"">(.*?)<\/p>");
                    if (!match.Success) result.Rating = SourceRating.Safe;
                    else result.Rating = match.Groups[1].Value.ToLowerInvariant().Contains("r-18") ? SourceRating.Nsfw : SourceRating.Safe;
                    break;

                case SiteIndex.Gelbooru:
                case SiteIndex.Danbooru:
                case SiteIndex.SankakuChannel:
                case SiteIndex.IdolComplex:
                    match = await WebRequest(result.Url[0], @"<li>Rating: (.*?)<\/li>");
                    if (!match.Success) result.Rating = SourceRating.Unknown;
                    else result.Rating = (SourceRating)Array.IndexOf(new[] { null, "Safe", "Questionable", "Explicit" }, match.Groups[1].Value);
                    break;

                case SiteIndex.Yandere:
                case SiteIndex.Konachan:
                    match = await WebRequest(result.Url[0], @"<li>Rating: (.*?) <span class="".*?""><\/span><\/li>");
                    if (!match.Success) result.Rating = SourceRating.Unknown;
                    else result.Rating = (SourceRating)Array.IndexOf(new[] { null, "Safe", "Questionable", "Explicit" }, match.Groups[1].Value);
                    break;

                case SiteIndex.e621:
                    match = await WebRequest(result.Url[0], @"<li>Rating: <span class="".*?"">(.*)<\/span><\/li>");
                    if (!match.Success) result.Rating = SourceRating.Unknown;
                    else result.Rating = (SourceRating)Array.IndexOf(new[] { null, "Safe", "Questionable", "Explicit" }, match.Groups[1].Value);
                    break;

                case SiteIndex.FAKKU:
                case SiteIndex.TwoDMarket:
                case SiteIndex.nHentai:
                    result.Rating = SourceRating.Nsfw;
                    break;

                case SiteIndex.DeviantArt:
                    match = await WebRequest(result.Url[0], @"<h1>Mature Content<\/h1>");
                    result.Rating = match.Success ? SourceRating.Nsfw : SourceRating.Safe;
                    break;

                default:
                    result.Rating = SourceRating.Unknown;
                    break;
            }

            if (unknownIsQuestionable && result.Rating is SourceRating.Unknown)
                result.Rating = SourceRating.Questionable;

            if (ignoreExplicit && (result.Rating is SourceRating.Questionable || result.Rating is SourceRating.Nsfw))
                return null;

            return result;
        }
    }
}
