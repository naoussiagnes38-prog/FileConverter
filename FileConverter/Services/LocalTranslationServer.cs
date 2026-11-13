using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileConverter.Core.Services;

namespace FileConverter.Services
{
    internal static class LocalTranslationServer
    {
        private static readonly SemaphoreSlim StartupLock = new SemaphoreSlim(1);
        private static Process startedServer;

        public static async Task EnsureReadyAsync(LanguageDetectionService service, Uri endpoint, Action onStarting)
        {
            await StartupLock.WaitAsync();
            try
            {
                if (await service.IsAvailableAsync())
                    return;

                if (endpoint.Scheme != Uri.UriSchemeHttp || endpoint.AbsolutePath != "/" ||
                    (endpoint.Host != "127.0.0.1" && endpoint.Host != "localhost"))
                    throw new InvalidOperationException(string.Format(LocalizationManager.Text(
                        "Start LibreTranslate at {0}, then retry."), endpoint));

                if (startedServer == null || startedServer.HasExited)
                {
                    startedServer?.Dispose();
                    startedServer = null;
                    try
                    {
                        startedServer = Process.Start(CreateStartInfo(endpoint.Port));
                    }
                    catch (Win32Exception ex)
                    {
                        throw new InvalidOperationException(string.Format(LocalizationManager.Text(
                            "LibreTranslate could not start. Install it with python -m pip install libretranslate, then add libretranslate.exe to PATH or set LibreTranslatePath. {0}"), ex.Message), ex);
                    }
                }

                onStarting();
                var elapsed = Stopwatch.StartNew();
                while (elapsed.Elapsed < TimeSpan.FromSeconds(90))
                {
                    if (await service.IsAvailableAsync())
                        return;
                    if (startedServer.HasExited)
                        throw new InvalidOperationException(string.Format(LocalizationManager.Text(
                            "LibreTranslate stopped during startup (exit code {0}). Start it in a terminal to see its error."), startedServer.ExitCode));
                    await Task.Delay(1000);
                }

                throw new TimeoutException(LocalizationManager.Text(
                    "LibreTranslate is still starting. Its first launch can download language models. Retry in a moment."));
            }
            finally { StartupLock.Release(); }
        }

        internal static void Stop()
        {
            if (startedServer == null)
                return;

            try
            {
                if (!startedServer.HasExited)
                    startedServer.Kill();
            }
            catch (InvalidOperationException) { }
            catch (Win32Exception ex) { Trace.TraceWarning(ex.Message); }
            finally
            {
                startedServer.Dispose();
                startedServer = null;
            }
        }

        private static ProcessStartInfo CreateStartInfo(int port)
        {
            string arguments = "--host 127.0.0.1 --port " + port.ToString(CultureInfo.InvariantCulture) +
                " --load-only en,fr";
            string configuredPath = ConfigurationManager.AppSettings["LibreTranslatePath"];
            string runtime = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TranslationRuntime");
            string bundledPython = Path.Combine(runtime, "python.exe");
            string bundledLauncher = Path.Combine(runtime, "run_libretranslate.py");

            string executable;
            if (!string.IsNullOrWhiteSpace(configuredPath))
                executable = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
            else if (File.Exists(bundledPython) && File.Exists(bundledLauncher))
            {
                executable = bundledPython;
                arguments = "-B \"" + bundledLauncher + "\" " + arguments + " --disable-web-ui --disable-files-translation";
            }
            else
                executable = FindInstalledExecutable();

            return new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        private static string FindInstalledExecutable()
        {
            string besideApp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libretranslate.exe");
            if (File.Exists(besideApp))
                return besideApp;

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string installedPath = Path.Combine(profile, "libretranslate-env", "Scripts", "libretranslate.exe");
            return File.Exists(installedPath) ? installedPath : "libretranslate.exe";
        }
    }
}
