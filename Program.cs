// Copyright (c) 2026 LanDen Labs - Dennis Lang

using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrowseSafe
{
    /// <summary>
    /// Browse Safe - confirms the machine is in a safe state before browsing.
    ///
    /// GUI:        BrowseSafe.exe
    /// Headless:   BrowseSafe.exe --run &lt;scope&gt; [--out &lt;file&gt;]
    ///               scope = scan | chrome | services | processes | startup |
    ///                       installed | devices | events | all
    ///             BrowseSafe.exe --report        (alias for --run scan)
    ///             BrowseSafe.exe --inventory     (alias for --run all)
    ///             BrowseSafe.exe --help          (show usage and exit)
    ///
    /// Author: Dennis Lang - LanDen Labs - 2026
    /// </summary>
    static class Program
    {
        [STAThread]
        static void Main()
        {
            var args = Environment.GetCommandLineArgs();

            if (args.Any(a => a is "--help" or "-h" or "/?" or "-?" or "/help"))
            {
                PrintHelp();
                return;
            }

            int run = Array.FindIndex(args, a => a.Equals("--run", StringComparison.OrdinalIgnoreCase));
            if (run >= 0)
            {
                string scope = (run + 1 < args.Length && !args[run + 1].StartsWith("--")) ? args[run + 1] : "all";
                RunHeadless(scope, OutPath(args));
                return;
            }
            if (args.Any(a => a.Equals("--report", StringComparison.OrdinalIgnoreCase)))
            {
                RunHeadless("scan", OutPath(args));
                return;
            }
            if (args.Any(a => a.Equals("--inventory", StringComparison.OrdinalIgnoreCase)))
            {
                RunHeadless("all", OutPath(args));
                return;
            }

            ApplicationConfiguration.Initialize();
            Theme.Load();
            Theme.Apply(Theme.Current); // apply saved light/dark mode before any window is shown
            Application.Run(new MainForm());
        }

        static void PrintHelp()
        {
            // Per-tab scopes come from the report catalog so this stays in sync; "all" is
            // appended by Reports.Scopes and runs every scope.
            string scopes = string.Join(", ", Reports.Scopes);

            Console.WriteLine($@"{AppInfo.Product} {AppInfo.Version} - Chrome safety & system-posture checker
{AppInfo.Copyright}

USAGE:
  BrowseSafe.exe                 Launch the GUI (default; no arguments).
  BrowseSafe.exe --run <scope>   Run checks headless and print a text report.
  BrowseSafe.exe --report        Alias for: --run scan
  BrowseSafe.exe --inventory     Alias for: --run all
  BrowseSafe.exe --help          Show this help and exit.

OPTIONS:
  --run <scope>     Which checks to run. Defaults to 'all' if <scope> is omitted.
  --out <file>      Also write the report text to <file> (headless modes only).
  --help, -h, /?    Show this help and exit.

SCOPES:
  {scopes}

EXAMPLES:
  BrowseSafe.exe --run scan
  BrowseSafe.exe --run events --out events.txt
  BrowseSafe.exe --report");
        }

        static string? OutPath(string[] args)
        {
            int i = Array.FindIndex(args, a => a.Equals("--out", StringComparison.OrdinalIgnoreCase));
            return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null;
        }

        static void RunHeadless(string scope, string? outPath)
        {
            if (!Reports.IsValidScope(scope))
            {
                Console.WriteLine($"Unknown scope '{scope}'. Valid scopes: {string.Join(", ", Reports.Scopes)}");
                return;
            }

            var (text, _) = Reports.Build(scope);
            Console.WriteLine(text);

            if (outPath != null)
            {
                try { File.WriteAllText(outPath, text); Console.WriteLine($"(written to {outPath})"); }
                catch (Exception ex) { Console.WriteLine($"(could not write {outPath}: {ex.Message})"); }
            }
        }
    }
}
