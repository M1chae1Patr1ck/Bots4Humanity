using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Streaming.Parameters;
using TwitterService.Models;

namespace TwitterService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IHostApplicationLifetime applicationLifetime;
        private readonly IConfiguration _configuration;

        public Worker(ILogger<Worker> logger, IHostApplicationLifetime applicationLifetime, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            this.applicationLifetime = applicationLifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var logEntry = new LogEntry();
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false
                };
                options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

                //var configurationBuilder = new ConfigurationBuilder().AddUserSecrets();
               // var configuration = configurationBuilder.Build();
                var hashTags = _configuration.GetValue<string>("HashTags").Split(",").ToList();
                var users = _configuration.GetValue<string>("Users").Split(",").ToList();

                var userCredentials = new TwitterCredentials(
                    _configuration.GetValue<string>("Twitter:ApiKey"),
                    _configuration.GetValue<string>("Twitter:ApiSecret"),
                    _configuration.GetValue<string>("Twitter:AccessToken"),
                    _configuration.GetValue<string>("Twitter:AccessSecret")
                    );

                var textAnalyticsClient = new TextAnalyticsClient(
                    new Uri(_configuration.GetValue<string>("Azure:TextAnalyticsURI")),
                    new AzureKeyCredential(_configuration.GetValue<string>("Azure:TextAnalyticsKey"))
                    );
                var userClient = new TwitterClient(userCredentials);
                var stream = userClient.Streams.CreateFilteredStream();
                stream.AddLanguageFilter(LanguageFilter.English);
                stream.FilterLevel = StreamFilterLevel.None;
                foreach (var hashTag in hashTags) stream.AddTrack(hashTag);
                foreach (var user in users)
                {
                    var twitterUser = await userClient.Users.GetUserAsync(user);
                    stream.AddFollow(twitterUser);
                }

                stream.MatchingTweetReceived += (sender, eventReceived) =>
                {

                    ITweet tweet = eventReceived.Tweet;
                    if (eventReceived.Tweet.IsRetweet) return;
                    if (eventReceived.Tweet.CreatedBy.CreatedAt > DateTime.Now.AddMonths(-1)) return;
                    if (eventReceived.Tweet.CreatedBy.FollowersCount < 100) return;
                    if (eventReceived.MatchingFollowers.Length > 0 && eventReceived.MatchingFollowers.Contains(tweet.CreatedBy.Id) == false) return;

                    string textToAnalyze = tweet.FullText ?? tweet.Text;
                    //_logger.LogInformation("Matching tweet: {time}, {text}", DateTimeOffset.Now, textToAnalyze.Replace(Environment.NewLine,""));
                    var connStr = _configuration.GetConnectionString("DefaultConnection");
                    var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                    optionsBuilder.UseSqlServer(connStr);
                    hashTags = _configuration.GetValue<string>("HashTags").Split(",").ToList();
                    foreach (var hashTag in hashTags)
                    {
                        textToAnalyze = textToAnalyze.Replace(hashTag, "");                        
                    }
                    
                    DocumentSentiment documentSentiment = null;
                    TweetSentiment tweetSentiment = new TweetSentiment();
                    List<SentimentDetail> sentimentDetails = new List<SentimentDetail>();
                    List<TweetEntity> listEntities = new List<TweetEntity>();
                    List<TweetKeyPhrase> listKeyPhrases = new List<TweetKeyPhrase>();


                    //_logger.LogInformation("Analyzing sentiment: {time}", DateTimeOffset.Now);
                    documentSentiment = textAnalyticsClient.AnalyzeSentiment(textToAnalyze);
                    logEntry.Sentiment = JsonSerializer.Serialize(documentSentiment, options);
                    //_logger.LogInformation("Sentiment: {time}", documentSentiment.Sentiment);
                    tweetSentiment = new TweetSentiment
                    {
                        IsPositive = (eventReceived.MatchingFollowers.Contains(tweet.CreatedBy.Id) && documentSentiment.Sentiment != TextSentiment.Negative) ||(documentSentiment.Sentiment == TextSentiment.Positive) && (!documentSentiment.Sentences.Where(s => s.Sentiment == TextSentiment.Mixed || s.Sentiment == TextSentiment.Negative).Any()),
                        TweetContent = textToAnalyze,
                        TweetedBy = 0,
                        TweetedOn = DateTime.Now,
                        TweetID = tweet.Id
                    };

                    foreach (var sentence in documentSentiment.Sentences)
                    {
                        var sentimentDetail = new SentimentDetail()
                        {
                            Sentence = sentence.Text,
                            Positive = sentence.ConfidenceScores.Positive,
                            Negative = sentence.ConfidenceScores.Negative,
                            Neutral = sentence.ConfidenceScores.Neutral,
                            TweetID = tweet.Id,
                            Sentiment = sentence.Sentiment.ToString()
                        };
                        sentimentDetails.Add(sentimentDetail);
                    }
                    logEntry.Details = JsonSerializer.Serialize(sentimentDetails, options);

                    var responseEntities = textAnalyticsClient.RecognizeEntities(textToAnalyze);
                    foreach (var entity in responseEntities.Value)
                    {
                        var tweetEntity = new TweetEntity
                        {
                            EntityText = entity.Text,
                            Category = entity.Category.ToString(),
                            SubCategory = entity.SubCategory,
                            Confidence = entity.ConfidenceScore,
                            TweetID = tweet.Id
                        };
                        listEntities.Add(tweetEntity);
                    }
                    logEntry.Entities = JsonSerializer.Serialize(listEntities);

                    var responseKeyPhrases = textAnalyticsClient.ExtractKeyPhrases(textToAnalyze);
                    foreach (string keyphrase in responseKeyPhrases.Value)
                    {
                        var tweetKeyPhrase = new TweetKeyPhrase
                        {
                            TweetID = tweet.Id,
                            KeyPhrase = keyphrase
                        };
                        listKeyPhrases.Add(tweetKeyPhrase);
                    }
                    logEntry.Phrases = JsonSerializer.Serialize(listKeyPhrases, options);


                    using (ApplicationDbContext db = new ApplicationDbContext(optionsBuilder.Options))
                    {
                        //_logger.LogWarning("Saving tweet: {time}", DateTimeOffset.Now);
                        db.TweetSentiments.Add(tweetSentiment);
                        db.SentimentDetails.AddRange(sentimentDetails);
                        db.TweetEntities.AddRange(listEntities);
                        db.TweetKeyPhrases.AddRange(listKeyPhrases);
                        db.SaveChanges();
                    }

                    if (tweetSentiment.IsPositive)
                    {

                        eventReceived.Tweet.FavoriteAsync();
                        eventReceived.Tweet.PublishRetweetAsync();
                    }

                    _logger.LogInformation(@$"{logEntry.Sentiment} {logEntry.Details} {logEntry.Entities} {logEntry.Phrases}");

                };

                stream.StreamStopped += (sender, eventReceived) =>
                {
                    stream.StartMatchingAnyConditionAsync();
                };
                _ = stream.StartMatchingAnyConditionAsync();
                while (!stoppingToken.IsCancellationRequested)
                {


                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Worker process was cancelled");

                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker process caught exception");

                Environment.ExitCode = 0;
            }
            finally
            {
                // No matter what happens (success or exception), we need to indicate that it's time to stop the application.
                applicationLifetime.StopApplication();
            }

        }
    }
}
