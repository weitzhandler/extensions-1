using System;
using System.Text;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Logging.Console
{
    public class SystemdLogFormatter : ILogFormatter
    {
        private static readonly string _loglevelPadding = ": ";
        private static readonly string _messagePadding;
        private readonly IOptionsMonitor<ConsoleLoggerOptions> _options;

        [ThreadStatic]
        private static StringBuilder _logBuilder;

        static SystemdLogFormatter()
        {
            var logLevelString = GetSyslogSeverityString(LogLevel.Information);
            _messagePadding = new string(' ', logLevelString.Length + _loglevelPadding.Length);
        }

        public SystemdLogFormatter(IOptionsMonitor<ConsoleLoggerOptions> options)
        {
            _options = options;
        }

        public string Name => "Systemd";

        internal ConsoleLoggerOptions Options { get; set; }

        public Action<IConsole, IConsole> Format(IExternalScopeProvider scopeProvider, LogLevel logLevel, string logName, int eventId, string message, Exception exception)
        {
            return (console, errorConsole) =>
            {
                Options = _options.CurrentValue;

                var targetConsole = logLevel >= Options.LogToStandardErrorThreshold ? errorConsole : console;
                var logBuilder = _logBuilder;
                _logBuilder = null;

                if (logBuilder == null)
                {
                    logBuilder = new StringBuilder();
                }

                // systemd reads messages from standard out line-by-line in a '<pri>message' format.
                // newline characters are treated as message delimiters, so we must replace them.
                // Messages longer than the journal LineMax setting (default: 48KB) are cropped.
                // Example:
                // <6>ConsoleApp.Program[10] Request received

                // loglevel
                var logLevelString = GetSyslogSeverityString(logLevel);
                logBuilder.Append(logLevelString);

                // timestamp
                var timestampFormat = Options.TimestampFormat;
                if (timestampFormat != null)
                {
                    var dateTime = GetCurrentDateTime();
                    logBuilder.Append(dateTime.ToString(timestampFormat));
                }

                // category and event id
                logBuilder.Append(logName);
                logBuilder.Append("[");
                logBuilder.Append(eventId);
                logBuilder.Append("]");

                // scope information
                GetScopeInformation(logBuilder, scopeProvider);

                // message
                if (!string.IsNullOrEmpty(message))
                {
                    logBuilder.Append(' ');
                    // message
                    AppendAndReplaceNewLine(logBuilder, message);
                }

                // exception
                // System.InvalidOperationException at Namespace.Class.Function() in File:line X
                if (exception != null)
                {
                    logBuilder.Append(' ');
                    AppendAndReplaceNewLine(logBuilder, exception.ToString());
                }

                // newline delimiter
                logBuilder.Append(Environment.NewLine);

                targetConsole.Write(logBuilder.ToString(), null, null);

                logBuilder.Clear();
                if (logBuilder.Capacity > 1024)
                {
                    logBuilder.Capacity = 1024;
                }
                _logBuilder = logBuilder;
            };

            static void AppendAndReplaceNewLine(StringBuilder sb, string message)
            {
                var len = sb.Length;
                sb.Append(message);
                sb.Replace(Environment.NewLine, " ", len, message.Length);
            }
        }

        private DateTime GetCurrentDateTime()
        {
            return Options.UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now;
        }

        private static string GetSyslogSeverityString(LogLevel logLevel)
        {
            // 'Syslog Message Severities' from https://tools.ietf.org/html/rfc5424.
            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    return "<7>"; // debug-level messages
                case LogLevel.Information:
                    return "<6>"; // informational messages
                case LogLevel.Warning:
                    return "<4>"; // warning conditions
                case LogLevel.Error:
                    return "<3>"; // error conditions
                case LogLevel.Critical:
                    return "<2>"; // critical conditions
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel));
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
                }, (stringBuilder, -1));
            }
        }
    }
}
