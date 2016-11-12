using System;
using Tweetinvi;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using Tweetinvi.Parameters;
using Tweetinvi.Models;

public static async void Run(TimerInfo myTimer, TraceWriter log)
{

    Tweetinvi.Auth.SetUserCredentials("Consumer Key", "Consumer Secret", "Access Token", "Access Token Secret");

    log.Info("Perform search...");
    var result = await Tweetinvi.SearchAsync.SearchTweets("#aivision OR #وصف");

    log.Info("filtering results...");

    DateTime requestedDateTime = DateTime.Now.Add(new TimeSpan(0, -3, 0));

    result = result.Where(t => !Seen(t.Id.ToString()) && !t.IsRetweet && t.CreatedAt >= requestedDateTime).ToList<ITweet>();

    log.Info("Check result count");
    if (result.Count() != 0)
    {
        log.Info($"found: {result.Count().ToString()} tweets.");

        foreach (var tweet in result)
        {

            log.Info($"Tweet {tweet.Id.ToString()} discoverd!");
            await new HttpClient().GetStringAsync("{AI Vision Core link}&tweet=" + tweet.Url);
            LogTweetRecord(tweet.Id.ToString());

        }
    }
    else
    {
        log.Info("results: 0.");
    }

    log.Info("Finished.");


}

public static string logPath = System.Environment.GetEnvironmentVariable("HOME") + "tweets.log";
//public static string logPath = System.Environment.GetEnvironmentVariable("HOME") + "\\site\\wwwroot\\{YourFunctionName}\\" + "tweets.log";

public static bool Seen(string Id)
{

    string log = File.ReadAllText(logPath);
    foreach (string tweet in log.Split('\n'))
    {
        if (tweet.Contains(Id))
        {
            return true;
        }

    }

    return false;
}

public static void LogTweetRecord(string Id)
{
    File.AppendAllText(logPath, "\nTweetId: " + Id);
}
