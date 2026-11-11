using System;
using System.Collections.Generic;
using System.IO;
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
            Func<string, string> destination, Action<string, string> operation,
            Action<string, bool> reportResult = null)
        {
            view.IsEnabled = false;
            var errors = new List<string>();
            var renamed = new List<string>();
            int completed = 0;
            try
            {
                foreach (string file in files)
                {
                    string output = null;
                    bool success = false;
                    try
                    {
                        output = destination(file);
                        string requested = output;
                        output = await Task.Run(() => WriteOutput(requested, temp => operation(file, temp)));
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
                    reportResult?.Invoke(file, success);
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

        public static string WriteOutput(string output, Action<string> write)
        {
            output = Path.GetFullPath(output);
            string directory = Path.GetDirectoryName(output);
            string name = Path.GetFileNameWithoutExtension(output);
            string extension = Path.GetExtension(output);
            if (extension.Equals(".encrypted", StringComparison.OrdinalIgnoreCase))
            {
                // Keep the original extension intact when the encrypted suffix is removed.
                extension = Path.GetExtension(name) + extension;
                name = Path.GetFileNameWithoutExtension(name);
            }
            string temp = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
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
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }
    }
}
