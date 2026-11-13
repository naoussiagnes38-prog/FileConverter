using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using FileConverter.Views;

namespace FileConverter.Services
{
    internal static class BatchOperations
    {
        private static readonly object OutputNameLock = new object();

        public static async Task RunAsync(UIElement view, IEnumerable<string> files,
            Func<string, string> destination, Action<string, string, Action<double>> operation,
            Action<double> reportProgress)
        {
            view.IsEnabled = false;
            var errors = new List<string>();
            var renamed = new List<string>();
            int completed = 0;
            var queue = files.ToArray();
            int processed = 0;
            reportProgress(0);
            try
            {
                foreach (string file in queue)
                {
                    string output = null;
                    bool success = false;
                    try
                    {
                        output = destination(file);
                        string requested = output;
                        int current = processed;
                        int lastPercentage = -1;
                        output = await Task.Run(() => WriteOutput(requested, temp => operation(file, temp, fraction =>
                        {
                            int percentage = (int)(100 * (current + Math.Min(fraction, 0.99)) / queue.Length);
                            if (percentage <= lastPercentage)
                                return;
                            lastPercentage = percentage;
                            view.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (processed == current)
                                    reportProgress(percentage);
                            }));
                        })));
                        if (output != requested)
                            renamed.Add(Path.GetFileName(file) + " → " + Path.GetFileName(output));
                        completed++;
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        errors.Add(Path.GetFileName(file) + ": " + ex.Message);
                    }
                    HistoryView.Record(file, output, success);
                    processed++;
                    reportProgress(100.0 * processed / queue.Length);
                }
                MessageBox.Show(string.Format(LocalizationManager.Text("{0} completed, {1} failed."), completed, errors.Count) + "\n" +
                    string.Join("\n", renamed) + (renamed.Count > 0 && errors.Count > 0 ? "\n" : "") +
                    string.Join("\n", errors), "FileConverter");
            }
            finally
            {
                view.IsEnabled = true;
            }
        }

        public static string WriteOutput(string output, Action<string> write, bool isDirectory = false)
        {
            output = Path.GetFullPath(output);
            string directory = Path.GetDirectoryName(output);
            string name = isDirectory ? Path.GetFileName(output) : Path.GetFileNameWithoutExtension(output);
            string extension = isDirectory ? "" : Path.GetExtension(output);
            if (extension.Equals(".encrypted", StringComparison.OrdinalIgnoreCase))
            {
                // Keep the original extension intact when the encrypted suffix is removed.
                extension = Path.GetExtension(name) + extension;
                name = Path.GetFileNameWithoutExtension(name);
            }
            string temp = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp" + extension);
            try
            {
                if (isDirectory)
                    Directory.CreateDirectory(temp);
                write(temp);
                lock (OutputNameLock)
                {
                    for (int copy = 1; ; copy++)
                    {
                        string candidate = copy == 1 ? output : Path.Combine(directory,
                            name + " (" + copy.ToString(CultureInfo.InvariantCulture) + ")" + extension);
                        if (File.Exists(candidate) || Directory.Exists(candidate))
                            continue;

                        try
                        {
                            if (isDirectory)
                                Directory.Move(temp, candidate);
                            else
                                File.Move(temp, candidate);
                            return candidate;
                        }
                        catch (IOException) when (File.Exists(candidate) || Directory.Exists(candidate))
                        {
                            // Another operation took the name before the move.
                        }
                    }
                }
            }
            finally
            {
                if (isDirectory && Directory.Exists(temp))
                    Directory.Delete(temp, true);
                else if (File.Exists(temp))
                    File.Delete(temp);
            }
        }
    }
}
