#nullable enable

using AssetStudio;
using System;
using System.Collections.Generic;

namespace AssetStudioCore.Runtime
{
    internal static class AssetStudioProcessState
    {
        public static IDisposable EnterCore(ILogger logger, IAssetStudioProgressSink? progressSink)
        {
            return new Scope(logger, CreateCoreProgress(progressSink), configureLogger: true);
        }

        public static IDisposable EnterCli(ILogger logger)
        {
            return new Scope(logger, CreateConsoleProgress(), configureLogger: true);
        }

        public static IDisposable EnterCoreProgress(IAssetStudioProgressSink? progressSink)
        {
            return new Scope(logger: null, CreateCoreProgress(progressSink), configureLogger: false);
        }

        public static IDisposable EnterConsoleProgress()
        {
            return new Scope(logger: null, CreateConsoleProgress(), configureLogger: false);
        }

        public static void ConfigureCore(ILogger logger, IAssetStudioProgressSink? progressSink)
        {
            Logger.Default = logger ?? throw new ArgumentNullException(nameof(logger));
            ConfigureCoreProgress(progressSink);
        }

        public static void ConfigureCli(ILogger logger)
        {
            Logger.Default = logger ?? throw new ArgumentNullException(nameof(logger));
            ConfigureConsoleProgress();
        }

        public static void ConfigureCoreProgress(IAssetStudioProgressSink? sink)
        {
            SetProgress(CreateCoreProgress(sink));
        }

        public static void ConfigureConsoleProgress()
        {
            SetProgress(CreateConsoleProgress());
        }

        public static void ConfigureImageTimingSink(Action<string, long>? timingSink)
        {
            ImageSharpNativeAotGuard.TimingSink = timingSink;
        }

        public static void Reset()
        {
            AssetStudioRuntimeOptions.Reset();
            Logger.Default = new DummyLogger();
            ConfigureCoreProgress(null);
            ConfigureImageTimingSink(null);
            Progress.Reset();
            Progress.Reset(index: 1);
        }

        private static IProgress<int>[] CreateCoreProgress(IAssetStudioProgressSink? sink)
        {
            return new IProgress<int>[]
            {
                new AssetStudioProgressAdapter(sink, index: 0),
                new AssetStudioProgressAdapter(sink, index: 1),
            };
        }

        private static IProgress<int>[] CreateConsoleProgress()
        {
            return new IProgress<int>[]
            {
                new AssetStudioConsoleProgressAdapter(),
                new AssetStudioConsoleProgressAdapter(),
            };
        }

        private static void SetProgress(IReadOnlyList<IProgress<int>> progress)
        {
            Progress.Default = progress[0];
            Progress.SetInstance(1, progress[1]);
        }

        private sealed class Scope : IDisposable
        {
            private readonly ILogger previousLogger;
            private readonly IProgress<int>[] previousProgress;
            private readonly bool configureLogger;
            private bool disposed;

            public Scope(ILogger? logger, IProgress<int>[] progress, bool configureLogger)
            {
                this.configureLogger = configureLogger;
                previousLogger = Logger.Default;
                previousProgress = new[]
                {
                    Progress.GetInstance(0),
                    Progress.GetInstance(1),
                };
                if (configureLogger)
                {
                    Logger.Default = logger ?? throw new ArgumentNullException(nameof(logger));
                }
                SetProgress(progress);
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                if (configureLogger)
                {
                    Logger.Default = previousLogger;
                }
                SetProgress(previousProgress);
            }
        }
    }
}
