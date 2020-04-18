# SharpNao
A C# API Wrapper for interfacing with the SauceNao API, based off of the design of the Node JS wrapper, [Sagiri](https://github.com/ClarityCafe/Sagiri).

## Download
Either:

1. View the [Releases](https://github.com/LazDisco/SharpNao/releases) tab and download the latest release and move it to your project
   directory. After that, add it as a reference to your desired project.

2. Download it via NuGet using the Package Mangager Console. Type:  ``` Install-Package SharpNao```

## Examples

Basic Startup:
```cs
using Laz.Api;

// Declare new wrapper instance
SharpNao wrapper = new SharpNao("Your_token_here");

// Configure some settings here if you want
wrapper.ReturnRatings = true;
wrapper.DefaultResponseCount = 10;
// etc

// Get result
SourceResult result = await wrapper.GetResultAsync("Image_Url_Here.com/thing.png");
```

Configurable Settings:
```cs

// If the website decides to suddenly move, you can change the url.
wrapper.ApiUrl = "https://NewUrl.net/search.php";

// This is the default result count that will be used every time you get an image source, unless you override it when using the function.
wrapper.DefaultResultCount = 10;
// wrapper.GetResultAsync("url", 5); // Override

// You can attempt to get a rating for each image allowing you to determain whether the image is explicit or not.
wrapper.ReturnRatings = true;

// If the image's explicitness couldn't be dertamined it will be considered unknown. This settings will cause all unknown values to be returned as Questionable.
wrapper.TreatUnknownAsQuestionable = true;

// If true, any image returned with questionable/Nsfw will be turned into null. This opens the possibility of an array of all null values being returned.
wrapper.PreventExplicitResults = true;

```

## Rate Limits
There are in built rate limits to prevent you from quering the API too much. If you have a permium account, you can override these with
```cs
wrapper.ShortTermRateLimiter = new RateLimiter(imagesPerCycle, new TimeSpan(hours, mins, seconds));
wrapper.LongTermRateLimiter = new RateLimiter(imagesPerCycle, new TimeSpan(hours, mins, seconds));
```


## Notes
- Only .png, .jpeg, .jpg, .bmp, .gif and .webp images are supported
- DefaultReponseType exists in case the SauceNao developer(s) ever implement the XML output type (highly doubtful).
  Setting it to normal will just return a normal html search. The wrapper is not currently setup to deal with this (potentially in the future).
  
