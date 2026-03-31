using System;
using System.Diagnostics;
using System.IO;

namespace PlayniteLauncher
{
    class Program
    {
        static void Main()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Playnite", "Playnite.FullscreenApp.exe"),
                @"C:\Games\Playnite\Playnite.FullscreenApp.exe",
                @"C:\Program Files\Playnite\Playnite.FullscreenApp.exe",
                @"C:\Program Files (x86)\Playnite\Playnite.FullscreenApp.exe",
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                    return;
                }
            }
        }
    }
}
