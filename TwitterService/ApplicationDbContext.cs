using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace TwitterService
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<TweetSentiment> TweetSentiments { get; set; }
        public DbSet<SentimentDetail> SentimentDetails { get; set; }
        public DbSet<TweetEntity> TweetEntities { get; set; }
        public DbSet<TweetKeyPhrase> TweetKeyPhrases { get; set; }
    }
}

