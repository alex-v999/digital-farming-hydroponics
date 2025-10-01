using System;
using System.Collections.Generic;
using System.IO;

namespace Digital_Farming.Functii
{
    public class LoggingService
    {
        private readonly string _logFilePath;
        private readonly List<string> _inMemoryLog = new();

        public LoggingService(string logFileName = "sensor_log.txt")
        {
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logFileName);
            if (!File.Exists(_logFilePath))
                File.WriteAllText(_logFilePath, "");
        }

        public void Log(string entry)
        {
            var timestamped = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {entry}";
            _inMemoryLog.Add(timestamped);
            File.AppendAllText(_logFilePath, timestamped + Environment.NewLine);
        }


        public IReadOnlyList<string> GetAll() => _inMemoryLog.AsReadOnly();
    }
}
