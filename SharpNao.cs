
using System;
using System.Collections.Generic;

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
            UNKNOWN = 0,
            /// <summary>The image explicitness rating was determained to be safe, and contains no nudity.</summary>
            SAFE = 1,
            /// <summary>The image explicitness rating was determained to be questionable, and could contain nudity.</summary>
            QUESTIONABLE = 2,
            /// <summary>The image explicitness rating was determained to be NSFW, and contains nudity.</summary>
            NSFW = 3
        }

        /// <summary>
        /// An enumeration of values mapped to the type of response we want the API to give us.
        /// </summary>
        public enum OutputType
        {
            Normal = 0,
            XML = 1,
            JSON = 2
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

        }

        /// <summary>
        /// The key used the connect to your account on SauceNao. 
        /// </summary>
        private string _apiKey { get; }

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
        public List<uint> DatabaseMasks { get; } = new List<uint>();

        /// <summary>
        /// A bit mask for DISABLING specific indexes
        /// </summary>
        public List<uint> DatabaseMasksInverted { get; } = new List<uint>();

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

        /// <summary></summary>
        /// <param name="apiKey">Your SauceNao Api Key</param>
        public SharpNao(string apiKey)
        {
            _apiKey = apiKey;
            DefaultResultCount = 6;
            DefaultResponseType = OutputType.JSON;
            TestMode = false;

            ReturnRatings = true;
            PreventExplicitResults = false;
            TreatUnknownAsQuestionable = true;
            IgnoreRatelimits = false;
        }
    }
}
