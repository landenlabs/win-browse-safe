// Copyright (c) 2026 LanDen Labs - Dennis Lang

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace B4Browse
{
    /// <summary>
    /// A delayed, top-most reminder shown after the app launches Chrome with a <c>chrome://</c>
    /// deep-link. Chrome drops a URL argument when it opens its profile picker (multi-profile
    /// setups), landing on the New Tab page instead of the requested settings panel - so the
    /// caller copies the URL to the clipboard and this floats a hint over Chrome explaining how
    /// to land on the page by hand. The user can suppress it permanently ("Don't show this
    /// again"), persisted as a small file alongside the theme/scale settings.
    /// </summary>
    internal static class ChromeHint
    {
        private static readonly string FlagPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "B4Browse", "chrome-hint-off.txt");

        private static bool? _suppressed;
        private static Form? _open;   // single live instance - a second deep-link replaces the first

        /// <summary>True once the user has ticked "Don't show this again" (persisted across sessions).</summary>
        public static bool Suppressed
        {
            get => _suppressed ??= File.Exists(FlagPath);
            private set
            {
                _suppressed = value;
                try
                {
                    if (value)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(FlagPath)!);
                        File.WriteAllText(FlagPath, "off");
                    }
                    else if (File.Exists(FlagPath)) File.Delete(FlagPath);
                }
                catch { /* non-fatal */ }
            }
        }

        /// <summary>Schedules the hint to appear over Chrome shortly after launch (no-op if the user
        /// suppressed it). Must be called on the UI thread; the delay lets Chrome's window paint so
        /// the top-most hint lands over it rather than behind it.</summary>
        public static void ShowAfterLaunch(string uri)
        {
            if (Suppressed) return;
            var timer = new System.Windows.Forms.Timer { Interval = 1200 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                try { ShowNow(uri); } catch { /* a hint must never crash the app */ }
            };
            timer.Start();
        }

        private static void ShowNow(string uri)
        {
            // Replace any hint still on screen from a previous click.
            if (_open is { IsDisposed: false }) { try { _open.Close(); } catch { /* ignore */ } }

            var dlg = new Form
            {
                Text = "Open in Chrome",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                ShowInTaskbar = false,
                MinimizeBox = false,
                MaximizeBox = false,
                TopMost = true,                       // float above Chrome (the foreground window)
                ClientSize = new Size(480, 284),
                BackColor = Theme.Window,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", 9.5f),
            };

            var surface = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
            dlg.Controls.Add(surface);

            var title = new Label
            {
                Text = "Land on the Chrome page you asked for",
                Left = 18, Top = 16, Width = 444, Height = 26, AutoSize = false,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Theme.Text,
            };

            var intro = new Label
            {
                Text = "Chrome may open its profile picker or New Tab page instead of the page you "
                     + "requested. The address has already been copied to your clipboard.",
                Left = 18, Top = 48, Width = 444, Height = 42, AutoSize = false, ForeColor = Theme.Subtle,
            };

            var steps = new Label
            {
                Text = "To jump straight there:\n"
                     + "     1.   Click Chrome's address bar\n"
                     + "     2.   Press   Ctrl + V\n"
                     + "     3.   Press   Enter",
                Left = 18, Top = 96, Width = 444, Height = 84, AutoSize = false, ForeColor = Theme.Text,
            };

            // The deep-link itself, read-only but selectable (a manual fallback if the paste is lost).
            var url = new TextBox
            {
                Text = uri,
                Left = 18, Top = 182, Width = 444, Height = 24,
                ReadOnly = true, BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Toolbar, ForeColor = Theme.Link,
                Font = new Font("Consolas", 9.5f),
            };

            var dontShow = new CheckBox
            {
                Text = "Don't show this again",
                Left = 18, Top = 224, Width = 220, Height = 24,
                ForeColor = Theme.Subtle, BackColor = Color.Transparent,
            };

            var gotIt = new Button
            {
                Text = "Got it",
                Width = 92, Height = 28, Left = 480 - 18 - 92, Top = 222,
                FlatStyle = FlatStyle.System, Font = new Font("Segoe UI", 9f),
            };
            gotIt.Click += (_, _) => dlg.Close();

            surface.Controls.AddRange(new Control[] { title, intro, steps, url, dontShow, gotIt });
            dlg.AcceptButton = gotIt;
            dlg.CancelButton = gotIt;   // Esc closes

            dlg.FormClosed += (_, _) =>
            {
                if (dontShow.Checked) Suppressed = true;
                if (ReferenceEquals(_open, dlg)) _open = null;
                dlg.Dispose();
            };

            _open = dlg;
            dlg.Show();
            dlg.BringToFront();
            try { dlg.Activate(); } catch { /* foreground lock - TopMost still floats it above */ }
        }
    }
}
