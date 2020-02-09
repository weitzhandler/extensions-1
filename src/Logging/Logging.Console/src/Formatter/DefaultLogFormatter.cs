using System;
using System.Text;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Logging.Console
{
    public class DefaultLogFormatter : ILogFormatter
    {
        private static readonly string _loglevelPadding = ": ";
        private static readonly string _messagePadding;
        private static readonly string _newLineWithMessagePadding;

        private readonly IOptionsMonitor<ConsoleLoggerOptions> _options;
        // ConsoleColor does not have a value to specify the 'Default' color
        private readonly ConsoleColor? DefaultConsoleColor = null;

        [ThreadStatic]
        private static StringBuilder _logBuilder;

        static DefaultLogFormatter()
        {
            var logLevelString = GetLogLevelString(LogLevel.Information);
            _messagePadding = new string(' ', logLevelString.Length + _loglevelPadding.Length);
            _newLineWithMessagePadding = Environment.NewLine + _messagePadding;
        }

        public DefaultLogFormatter(IOptionsMonitor<ConsoleLoggerOptions> options)
        {
            _options = options;
        }

        public string Name => "Default";

        internal ConsoleLoggerOptions Options { get; set; }

        public Action<IConsole, IConsole> Format(IExternalScopeProvider scopeProvider, LogLevel logLevel, string logName, int eventId, string message, Exception exception)
        {
            return (console, errorConsole) =>
            {
                Options = _options.CurrentValue;

                var targetConsole = logLevel >= Options.LogToStandardErrorThreshold ? errorConsole : console;

                // Example:
                // INFO: ConsoleApp.Program[10]
                //       Request received

                string timestamp = null;
                var timestampFormat = Options.TimestampFormat;
                if (timestampFormat != null)
                {
                    var dateTime = GetCurrentDateTime();
                    timestamp = dateTime.ToString(timestampFormat);

                    targetConsole.Write(timestamp, DefaultConsoleColor, DefaultConsoleColor);
                }

                var logLevelColors = GetLogLevelConsoleColors(logLevel);
                var logLevelString = GetLogLevelString(logLevel);

                targetConsole.Write(logLevelString, logLevelColors.Background, logLevelColors.Foreground);

                var logBuilder = _logBuilder;
                _logBuilder = null;

                if (logBuilder == null)
                {
                    logBuilder = new StringBuilder();
                }

                // category and event id
                logBuilder.Append(_loglevelPadding);
                logBuilder.Append(logName);
                logBuilder.Append("[");
                logBuilder.Append(eventId);
                logBuilder.AppendLine("]");

                // scope information
                GetScopeInformation(logBuilder, scopeProvider);

                if (!string.IsNullOrEmpty(message))
                {
                    // message
                    logBuilder.Append(_messagePadding);

                    var len = logBuilder.Length;
                    logBuilder.AppendLine(message);
                    logBuilder.Replace(Environment.NewLine, _newLineWithMessagePadding, len, message.Length);
                }

                // Example:
                // System.InvalidOperationException
                //    at Namespace.Class.Function() in File:line X
                if (exception != null)
                {
                    // exception message
                    logBuilder.AppendLine(exception.ToString());
                }

                targetConsole.Write(logBuilder.ToString(), DefaultConsoleColor, DefaultConsoleColor);

                logBuilder.Clear();
                if (logBuilder.Capacity > 1024)
                {
                    logBuilder.Capacity = 1024;
                }
                _logBuilder = logBuilder;

                console.Flush();
            };
        }

        private DateTime GetCurrentDateTime()
        {
            return Options.UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now;
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    return "trce";
                case LogLevel.Debug:
                    return "dbug";
                case LogLevel.Information:
                    return "info";
                case LogLevel.Warning:
                    return "warn";
                case LogLevel.Error:
                    return "fail";
                case LogLevel.Critical:
                    return "crit";
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel));
            }
        }

        private ConsoleColors GetLogLevelConsoleColors(LogLevel logLevel)
        {
            if (Options.DisableColors)
            {
                return new ConsoleColors(null, null);
            }

            // We must explicitly set the background color if we are setting the foreground color,
            // since just setting one can look bad on the users console.
            switch (logLevel)
            {
                case LogLevel.Critical:
                    return new ConsoleColors(ConsoleColor.White, ConsoleColor.Red);
                case LogLevel.Error:
                    return new ConsoleColors(ConsoleColor.Black, ConsoleColor.Red);
                case LogLevel.Warning:
                    return new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black);
                case LogLevel.Information:
                    return new ConsoleColors(ConsoleColor.DarkGreen, ConsoleColor.Black);
                case LogLevel.Debug:
                    return new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black);
                case LogLevel.Trace:
                    return new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black);
                default:
                    return new ConsoleColors(DefaultConsoleColor, DefaultConsoleColor);
            }
        }

        private void GetScopeInformation(StringBuilder stringBuilder, IExternalScopeProvider scopeProvider)
        {
            if (Options.IncludeScopes && scopeProvider != null)
            {
                var initialLength = stringBuilder.Length;

                scopeProvider.ForEachScope((scope, state) =>
                {
                    var (builder, paddAt) = state;
                    var padd = paddAt == builder.Length;
                    if (padd)
                    {
                        builder.Append(_messagePadding);
                        builder.Append("=> ");
                    }
                    else
                    {
                        builder.Append(" => ");
                    }
                    builder.Append(scope);
                }, (stringBuilder, initialLength));

                if (stringBuilder.Length > initialLength)
                {
                    stringBuilder.AppendLine();
                }
            }
        }

        private readonly struct ConsoleColors
        {
            public ConsoleColors(ConsoleColor? foreground, ConsoleColor? background)
            {
                Foreground = foreground;
                Background = background;
            }

            public ConsoleColor? Foreground { get; }

            public ConsoleColor? Background { get; }
        }
    }
}
