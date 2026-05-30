using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace BrowseSafe
{
    /// <summary>
    /// Emails a report using the user's chosen client:
    ///   - DefaultMailApp: Simple MAPI compose window (report as body + .txt attachment).
    ///   - Gmail / OutlookWeb: a web compose URL opened in the chosen browser.
    /// Always saves a copy to a temp file and to the clipboard as a fallback.
    /// </summary>
    public static class ReportMailer
    {
        public static void Send(Form owner, string scope, string tabName, AppSettings settings)
        {
            string text;
            try { (text, _) = Reports.Build(scope); }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "Could not build report: " + ex.Message, "Email report",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string file = Path.Combine(Path.GetTempPath(),
                $"browse-safe-{scope}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            string? savedTo = null;
            try { File.WriteAllText(file, text); savedTo = file; } catch { /* non-fatal */ }
            try { Clipboard.SetText(text); } catch { /* clipboard may be busy */ }

            string subject = $"Browse Safe report - {tabName} - {DateTime.Now:yyyy-MM-dd HH:mm}";

            switch (settings.EmailMethod)
            {
                case EmailMethod.Gmail:
                    SendWeb(owner, GmailUrl(subject, text, out bool gt), settings.EmailBrowser, gt, savedTo);
                    break;
                case EmailMethod.OutlookWeb:
                    SendWeb(owner, OutlookUrl(subject, text, out bool ot), settings.EmailBrowser, ot, savedTo);
                    break;
                default:
                    SendViaMapi(owner, subject, text, savedTo);
                    break;
            }
        }

        // ---- Web compose ------------------------------------------------- //
        private static void SendWeb(Form owner, string url, BrowserChoice browser, bool truncated, string? savedTo)
        {
            try { OpenInBrowser(url, browser); }
            catch (Exception ex)
            {
                MessageBox.Show(owner, "Could not open the browser: " + ex.Message, "Email report",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (truncated)
                MessageBox.Show(owner,
                    "This report is long, so the email body was truncated for the web compose window.\n\n" +
                    (savedTo != null ? $"The full report was saved to:\n{savedTo}\n\n" : "") +
                    "It has also been copied to your clipboard - paste to include the full text.",
                    "Email report", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void OpenInBrowser(string url, BrowserChoice browser)
        {
            string? exe = browser switch
            {
                BrowserChoice.Chrome => "chrome.exe",
                BrowserChoice.Firefox => "firefox.exe",
                BrowserChoice.Edge => "msedge.exe",
                _ => null,
            };
            try
            {
                if (exe != null)
                {
                    Process.Start(new ProcessStartInfo(exe) { Arguments = url, UseShellExecute = true });
                    return;
                }
            }
            catch { /* fall through to default browser */ }
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private static string GmailUrl(string subject, string text, out bool truncated)
            => "https://mail.google.com/mail/?view=cm&fs=1" +
               $"&su={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(Body(text, out truncated))}";

        private static string OutlookUrl(string subject, string text, out bool truncated)
            => "https://outlook.office.com/mail/deeplink/compose?" +
               $"subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(Body(text, out truncated))}";

        private static string Body(string text, out bool truncated)
        {
            const int max = 1500; // keep the compose URL within browser/OS limits
            truncated = text.Length > max;
            return truncated
                ? text.Substring(0, max) + "\r\n\r\n...[truncated - full report is saved to a file and on the clipboard]"
                : text;
        }

        // ---- Simple MAPI (default desktop mail client) ------------------- //
        private const int MAPI_LOGON_UI = 0x00000001;
        private const int MAPI_DIALOG = 0x00000008;
        private const int SUCCESS_SUCCESS = 0;
        private const int MAPI_USER_ABORT = 1;

        private static void SendViaMapi(Form owner, string subject, string body, string? attachment)
        {
            // MAPI compose blocks until send/cancel, so run it off the UI thread (STA).
            var t = new Thread(() =>
            {
                int err = MapiCompose(subject, body, attachment);
                if (err != SUCCESS_SUCCESS && err != MAPI_USER_ABORT && owner.IsHandleCreated)
                {
                    owner.BeginInvoke(new Action(() => MessageBox.Show(owner,
                        $"Could not open your default mail client (MAPI code {err}).\n\n" +
                        (attachment != null ? $"The report was saved to:\n{attachment}\n\n" : "") +
                        "It has also been copied to the clipboard.\n\n" +
                        "Tip: use the email dropdown to pick Gmail or Outlook (web) instead.",
                        "Email report", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                }
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private static int MapiCompose(string subject, string body, string? attachment)
        {
            var msg = new MapiMessage { Subject = subject, NoteText = body };
            IntPtr fileBuf = IntPtr.Zero;
            try
            {
                if (attachment != null && File.Exists(attachment))
                {
                    var fd = new MapiFileDesc { Position = -1, Path = attachment, Name = Path.GetFileName(attachment) };
                    fileBuf = Marshal.AllocHGlobal(Marshal.SizeOf<MapiFileDesc>());
                    Marshal.StructureToPtr(fd, fileBuf, false);
                    msg.FileCount = 1;
                    msg.Files = fileBuf;
                }
                return MAPISendMail(IntPtr.Zero, IntPtr.Zero, msg, MAPI_LOGON_UI | MAPI_DIALOG, 0);
            }
            catch { return -1; }
            finally
            {
                if (fileBuf != IntPtr.Zero)
                {
                    Marshal.DestroyStructure<MapiFileDesc>(fileBuf);
                    Marshal.FreeHGlobal(fileBuf);
                }
            }
        }

        [DllImport("MAPI32.DLL", CharSet = CharSet.Ansi)]
        private static extern int MAPISendMail(IntPtr session, IntPtr hwnd, MapiMessage message, int flags, int reserved);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private sealed class MapiMessage
        {
            public int Reserved;
            public string? Subject;
            public string? NoteText;
            public string? MessageType;
            public string? DateReceived;
            public string? ConversationID;
            public int Flags;
            public IntPtr Originator;
            public int RecipCount;
            public IntPtr Recips;
            public int FileCount;
            public IntPtr Files;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct MapiFileDesc
        {
            public int Reserved;
            public int Flags;
            public int Position;
            public string? Path;
            public string? Name;
            public IntPtr Type;
        }
    }
}
