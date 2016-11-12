#r "System.Web"
#r "System.Threading.Tasks"
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using Newtonsoft.Json;
using System.Net;
using Tweetinvi;
using Tweetinvi.Models;
using System.Threading.Tasks;
using Tweetinvi.Parameters;
public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{

    string tweetLink = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "tweet", true) == 0)
        .Value;

    dynamic data = await req.Content.ReadAsAsync<object>();

    tweetLink = tweetLink ?? data?.tweet;

    if (string.IsNullOrEmpty(tweetLink)) { log.Info("Bad Request"); return req.CreateResponse(HttpStatusCode.BadRequest); }


    tweetLink = tweetLink.Replace("http://", "").Replace("https://", "");
    if (tweetLink.Contains("twitter.com") && tweetLink.Contains("status"))
    {
        string tweetId = tweetLink.Split('/')[3];

        log.Info("Executing...");
        await Execute(tweetId, log);

    }

    return req.CreateResponse(HttpStatusCode.OK);
}



public static async Task Execute(string tweetId, TraceWriter log)
{

    Tweetinvi.Auth.SetUserCredentials("Consumer Key", "Consumer Secret", "Access Token", "Access Token Secret");

    log.Info("Getting tweet Object...");
    long intTweetId = Convert.ToInt64(tweetId);
    var tweet = await TweetAsync.GetTweet(intTweetId);

    log.Info("Building media url...");
    string mediaurl = "";
    string hashtagUsed = null;

    hashtagUsed = tweet.Text.Contains("#aivision") ? "#aivision" : "#وصف";

    if (hashtagUsed == null) { return; }



    if (tweet.Entities.Medias.Count == 0 && tweet.InReplyToStatusId != null)
    {

        long inreplyTweetId = (long)tweet.InReplyToStatusId;
        log.Info("Getting inReplyTo Tweet...");
        var InReplyToTweet = await TweetAsync.GetTweet(inreplyTweetId);
        
        //Instagram integration:
        if (InReplyToTweet.Entities.Medias.Count == 0 && InReplyToTweet.Entities.Urls.Count != 0)
        {
            if (InReplyToTweet.Entities.Urls.Where(u => u.ExpandedURL.StartsWith("http://instagram.com/p/")).FirstOrDefault() != null)
            {
                string instagramPhotoLink = InReplyToTweet.Entities.Urls.Where(u => u.ExpandedURL.StartsWith("http://instagram.com/p/")).FirstOrDefault().ExpandedURL;
                instagramPhotoLink += instagramPhotoLink.EndsWith("/") ? "media" : "/media";
                mediaurl = instagramPhotoLink;
            }
            else
            {
                mediaurl = InReplyToTweet.Entities.Medias.FirstOrDefault().MediaURL;
            }

        }
        else
        {
            mediaurl = InReplyToTweet.Entities.Medias.FirstOrDefault().MediaURL;
        }

      
    }
    else
    {
        mediaurl = tweet.Entities.Medias.FirstOrDefault().MediaURL;
    }


    string img = "{\"url\":\"" + mediaurl + "\"}";

    log.Info("Calling Cognitive Services...");
    string imageInfo = await AnalyzeImage(img);
    log.Info("Calling Cognitive is OK: " + imageInfo);

    MSCognitiveObject imageCaptions = JsonConvert.DeserializeObject<MSCognitiveObject>(imageInfo);
    string caption = imageCaptions.description.captions[0].text;
   
    // string mayInclude = "This image may include: ";
    // int tagIndex = 0;
    // while(mayInclude.Length  < 140) {
    //      try{ string.IsNullOrEmpty(imageCaptions.description.tags[tagIndex]); } catch(Exception x) {break;}
    //      string comma = tagIndex == 0 ? "" : ", ";
    //      mayInclude += comma + imageCaptions.description.tags[tagIndex];
    //      tagIndex ++;
    //     }
    // log.Info(mayInclude);

    string formattedReply = "";
    if (hashtagUsed == "#aivision")
    {
        formattedReply = $"Hi, It's {caption}, I'm " + Math.Round(imageCaptions.description.captions[0].confidence * 100) + "%" + " sure.";

    }
    else if (hashtagUsed == "#وصف")
    {
        string perc = Math.Round(imageCaptions.description.captions[0].confidence * 100) + "%";
        string localizedCaption = await caption.Translate();
        formattedReply = $"مرحباً، إنه {localizedCaption}، أنا متأكد بنسبة {perc}!";
    }
    else
    {
        return;
    }
    
    string mentions = "";
    foreach (var mention in tweet.Entities.UserMentions)
    {
        if (mention.ScreenName != tweet.CreatedBy.ScreenName)
        {
            mentions += " @" + mention.ScreenName;
        }
    }

    var textToPublish = string.Format("@{0} \n{1}", tweet.CreatedBy.ScreenName + mentions, formattedReply);
 
    var reply = await TweetAsync.PublishTweetInReplyTo(textToPublish, new TweetIdentifier(tweet.Id));
    log.Info("Tweet published!");

}

public static async Task<string> AnalyzeImage(string imageLink)
{
    var client = new HttpClient();

    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "{Microsoft Cognitive Services API Key}");

    var uri = "https://api.projectoxford.ai/vision/v1.0/describe?" + "maxCandidates=1";
    HttpResponseMessage response;

    using (var content = new StringContent(imageLink))
    {
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        response = await client.PostAsync(uri, content);

        string imageInfo = await response.Content.ReadAsStringAsync();

        return imageInfo;
    }
}
 
public static async Task<string> Translate(this string text)
{

    try
    {
        using (HttpClient MSTRanslatorHttpClient = new HttpClient())
        {
            MSTRanslatorHttpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            MSTRanslatorHttpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", "{Bing Translator Basic Auth}");

            string uri = Uri.EscapeUriString($"https://api.datamarket.azure.com/Bing/MicrosoftTranslator/v1/Translate?Text='{text}'&To='ar'&From='en'");
            string js = await MSTRanslatorHttpClient.GetStringAsync(uri);
            if (string.IsNullOrEmpty(js)) { return string.Empty; }

            MSTranslatorObject trnas = JsonConvert.DeserializeObject<MSTranslatorObject>(js);
            return trnas.d.results[0].Text;

        }
    }
    catch (Exception)
    {
        return string.Empty;
    }
}

 
// MODELS
// MS TRANSLATOR MODEL
public class MSTranslatorObject
{
    public D d { get; set; }
}

public class D
{
    public Result[] results { get; set; }
}

public class Result
{
    public __Metadata __metadata { get; set; }
    public string Text { get; set; }
}

public class __Metadata
{
    public string uri { get; set; }
    public string type { get; set; }
}

// MS COGNITIVE 


public class Caption
{
    public string text { get; set; }
    public double confidence { get; set; }
}

public class Description
{
    public List<string> tags { get; set; }
    public List<Caption> captions { get; set; }
}

public class Metadata
{
    public int width { get; set; }
    public int height { get; set; }
    public string format { get; set; }
}

public class MSCognitiveObject
{
    public Description description { get; set; }
    public string requestId { get; set; }
    public Metadata metadata { get; set; }
}