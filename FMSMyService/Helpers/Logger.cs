using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace FMSMyService.Helpers
{
    public static class Logger
    {

        private static readonly IConfigurationRoot configuration =
            new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appSettings.json", optional: true, reloadOnChange: true)
            .Build();

        private static readonly string Default =
            configuration["LogSettings:Folder"] ?? "ISS";
        private static readonly object _lock = new object();
        private static string _logDirectory = configuration["LogSettings:LogDirectory"] ?? @"C:\FMSLogs";

        public static string LogDirectory
        {
            get => _logDirectory;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _logDirectory = value;
                }
            }
        }
        //public static void Log(string message, string level = "INFO", string filepath-"" )
        public static void Log(string message, string level = "INFO", string filepath = "")
        {
            try
            {
                CultureInfo USCulture = CultureInfo.GetCultureInfo("en-us");
                string curyear = DateTime.Now.ToString("yyyy");
                string curmonth = DateTime.Now.ToString("MMM");
                string curday = DateTime.Now.ToString("dd");

                string logFilePath = Path.Combine(
                 LogDirectory,
                 curyear,
                 curmonth,
                 curday,
                 $"{DefaultFolder}-log {DateTime.Now.ToString("dd-MM-yyyy", USCulture)}.txt"
             );
                string dir = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string timestamp = DateTime.Now.ToString("yy-MM-dd HH:mm:ss");
                string logEntry = $"{timestamp}  {message}";

                //lock (_lock)
                //{
                //    using (streamWriter writer = new streamWriter(logFilePath, true))
                //    {
                //        writer.Writeline(logEntry);
                //    }
                //}
                lock (_lock)
                {
                    using (StreamWriter writer = new StreamWriter(logFilePath, true))
                    {
                        writer.WriteLine(logEntry);
                    }
                }

                WriteToConsole(timestamp, level, message);
            }

            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to write log: {ex.Message}");
            }
        }
        public static void Simple(string message)
        {
            try
            {
                CultureInfo USCulture = CultureInfo.GetCultureInfo("en-us");
                string curyear = DateTime.Now.ToString("yyyy");
                string curmonth = DateTime.Now.ToString("MMM");
                string curday = DateTime.Now.ToString("dd");

                string logFilePath = Path.Combine(
                    LogDirectory,
                    curyear,
                    curmonth,
                    curday,
                    $"{DefaultFolder}-log {DateTime.Now.ToString("dd-MM-yyyy", USCulture)}.txt"
                    );
                string dir = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                lock (_lock)
                {
                    using(StreamWriter writer = new StreamWriter(logFilePath, true))
                    {
                        writer.WriteLine(message);
                    }
                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(message);
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to write simple log: {ex.Message}");
            }
        }

        public static void LogException(Exception ex, string context = "")
        {
            string message = string.IsNullOrEmpty(context)
                ? $"Exception: {ex.Message}\nStack Trace: {ex.StackTrace}"
                : $"Context: {context} | Exception: {ex.Message}\nStack Trace: {ex.StackTrace}";

            Log(message, "ERROR");
        }
        public static void Info(
            string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int line = 0,
            [CallerMemberName] string caller = "")
            => Log(message, "INFO", filePath);

        public static void Warning(
            string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int line = 0,
            [CallerMemberName] string caller = "")
            => Log(message, "WARN", filePath);

        public static void Error(
            string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int line = 0,
            [CallerMemberName] string caller = "")
            => Log(message, "ERROR", filePath);

        private static void WriteToConsole(string timestamp, string level, string message)
        {
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"[{timestamp}] ");

                switch (level.ToUpperInvariant())
                {
                    case "INFO":
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write("[INFO] ");
                        break;
                    case "WARN":
                    case "WARNING":
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("[WARN] ");
                        break;
                    case "ERROR":
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("[FAIL] ");
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"[{level}] ");
                        break;
                }

                Console.ResetColor();
                Console.WriteLine(message);
            }
        }

    }
}