using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TwitterService;

namespace WebAdmin.Data
{
    public class ApplicationDbContext : IdentityDbContext
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
