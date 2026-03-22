using System;

namespace NetworkAnalyzer.Models
{
    public class HistoryItem
    {
        public string Url { get; set; }
        public DateTime Timestamp { get; set; }
        public string Scheme { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }

        public HistoryItem(string url, string scheme, string host, int port)
        {
            Url = url;
            Scheme = scheme;
            Host = host;
            Port = port;
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] {Url}";
        }
    }
}