using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace AssetStudio
{
    public static class Logger
    {
        private static readonly AsyncLocal<ILogger> CurrentLogger = new AsyncLocal<ILogger>();
        private static readonly ILogger FallbackLogger = new DummyLogger();

        public static ILogger Default
        {
            get => CurrentLogger.Value ?? FallbackLogger;
            set => CurrentLogger.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static void Verbose(string message) => Default.Log(LoggerEvent.Verbose, message);
        public static void Debug(string message) => Default.Log(LoggerEvent.Debug, message);
        public static void Info(string message) => Default.Log(LoggerEvent.Info, message);
        public static void Warning(string message) => Default.Log(LoggerEvent.Warning, message);
        public static void Error(string message) => Default.Log(LoggerEvent.Error, message);

        public static void Error(string message, Exception e)
        {
            var sb = new StringBuilder();
            sb.AppendLine(message);
            sb.AppendLine();
            sb.AppendLine(e.ToString());
            Default.Log(LoggerEvent.Error, sb.ToString());
        }
    }
}
