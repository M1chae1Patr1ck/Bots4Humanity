using System;
using System.Collections.Generic;
using System.Text;

namespace TwitterService.Models
{
    public class LogEntry
    {
        public string Sentiment { get; set; }
        public string Details { get; set; }
        public string Phrases { get; set; }
        public string Entities { get; set; }
    }
}

