using System;
using System.Collections.Generic;
using System.IO;

namespace Mayflower
{
    interface ILogger
    {
        OutputFormat Format { get; }
        Verbosity MaximumVerbosity { get; }
        /// <summary>
        /// The verbosity of the enclosing block. Any messages logged below this level will still be outputted, but they will be freestanding (rather than
        /// enclosed in the block of this logger).
        /// </summary>
        Verbosity BlockVerbosity { get; }
        void Log(Verbosity logLevel, string message);
        NestedLogger CreateNestedLogger(string name, bool buffered, Verbosity? verbosity = null);
    }

    class Logger : ILogger
    {
        internal static readonly object GlobalLock = new object();

        readonly TextWriter _output;

        public Verbosity MaximumVerbosity { get; }
        public Verbosity BlockVerbosity => Verbosity.Minimal;
        public OutputFormat Format { get; }

        internal Logger(TextWriter output, Verbosity verbosity, OutputFormat format)
        {
            _output = output;
            MaximumVerbosity = verbosity;
            Format = format;
        }

        public void Log(Verbosity logLevel, string message)
        {
            if (logLevel < MaximumVerbosity)
                return;

            lock (GlobalLock)
            {
                _output.WriteLine(message);
            }
        }

        public NestedLogger CreateNestedLogger(string name, bool buffered, Verbosity? verbosity = null)
        {
            return new NestedLogger(name, this, buffered, verbosity ?? BlockVerbosity);
        }
    }

    struct BufferedMessage
    {
        public Verbosity Verbosity { get; }
        public string Message { get; }

        public BufferedMessage(Verbosity verbosity, string message)
        {
            Verbosity = verbosity;
            Message = message;
        }
    }

    class NestedLogger : ILogger, IDisposable
    {
        readonly ILogger _parent;
        readonly List<BufferedMessage> _buffer;

        public string Name { get; }
        public OutputFormat Format => _parent.Format;
        public Verbosity MaximumVerbosity => _parent.MaximumVerbosity;
        public Verbosity BlockVerbosity { get; }

        internal NestedLogger(string name, ILogger parent, bool buffered, Verbosity blockVerbosity)
        {
            _parent = parent;

            if (buffered)
                _buffer = new List<BufferedMessage>();

            Name = name;
            BlockVerbosity = blockVerbosity;

            WriteOpenBlock();
        }

        public void Log(Verbosity logLevel, string message)
        {
            if (_buffer == null)
            {
                _parent.Log(logLevel, message);
            }
            else
            {
                _buffer.Add(new BufferedMessage(logLevel, message));
            }
        }

        public NestedLogger CreateNestedLogger(string name, bool buffered, Verbosity? verbosity = null)
        {
            return new NestedLogger(name, this, buffered, verbosity ?? BlockVerbosity);
        }

        public void Dispose()
        {
            WriteCloseBlock();

            if (_buffer != null && _buffer.Count > 0)
            {
                lock (Logger.GlobalLock)
                {
                    foreach (var msg in _buffer)
                    {
                        _parent.Log(msg.Verbosity, msg.Message);
                    }
                }
            }
        }

        void WriteOpenBlock()
        {
            if (Format == OutputFormat.TeamCity)
            {
                var name = EscapeTeamCityString(Name);
                Log(BlockVerbosity, $"##teamcity[blockOpened name='{name}']");
            }
        }

        void WriteCloseBlock()
        {
            if (Format == OutputFormat.TeamCity)
            {
                var name = EscapeTeamCityString(Name);
                Log(BlockVerbosity, $"##teamcity[blockClosed name='{name}']");
            }
        }

        string EscapeTeamCityString(string name)
        {
            // Technically there are other characters which are supposed to be escaped, but they're really not likely to be encountered.
            // https://confluence.jetbrains.com/display/TCD10/Build+Script+Interaction+with+TeamCity
            return name.Replace("|", "||").Replace("'", "|'");
        }
    }
}