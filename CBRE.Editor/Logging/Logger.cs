using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CBRE.Editor.Logging {
    public static class Logger {
        public static void ShowException(Exception ex, string message = "") {
            var info = new ExceptionInfo(ex, message);
            var window = new ExceptionWindow(info);
            if (Editor.Instance == null || Editor.Instance.IsDisposed) window.Show();
            else window.Show(Editor.Instance);
        }
    }

    public class ExceptionInfo {
        public Exception Exception { get; set; }
        public string RuntimeVersion { get; set; }
        public string OperatingSystem { get; set; }
        public string ApplicationVersion { get; set; }
        public DateTime Date { get; set; }
        public string InformationMessage { get; set; }
        public string UserEnteredInformation { get; set; }

        public string Source {
            get { return Exception.Source; }
        }

        public string Message {
            get {
                var msg = String.IsNullOrWhiteSpace(InformationMessage) ? Exception.Message : InformationMessage;
                return msg.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => !String.IsNullOrWhiteSpace(x));
            }
        }

        public string StackTrace {
            get { return Exception.StackTrace; }
        }

        public string FullStackTrace { get; set; }

        public string FriendlyOSName() {
            Version version = System.Environment.OSVersion.Version;
            string os;

            switch (version.Major) {
                case 6:
                    switch (version.Minor) {
                        case 1: os = "Windows 7"; break;
                        case 2: os = "Windows 8"; break;
                        case 3: os = "Windows 8.1"; break;
                        default: os = "Unknown"; break;
                    }
                    break;
                case 10:
                    switch (version.Minor) {
                        case 0:
                            if (version.Build >= 22000) os = "Windows 11";
                            else os = "Windows 10";
                            break;
                        default: os = "Unknown"; break;
                    }
                    break;
                default:
                    os = "Unknown";
                    break;
            }
            os += $" (NT {version.Major}.{version.Minor}, Build {version.Build})";
            return os;
        }

        public ExceptionInfo(Exception exception, string info) {
            Exception = exception;
            RuntimeVersion = System.Environment.Version.ToString();
            Date = DateTime.Now;
            InformationMessage = info;
            ApplicationVersion = FileVersionInfo.GetVersionInfo(typeof(Logger).Assembly.Location).FileVersion;
            OperatingSystem = FriendlyOSName();

            var list = new List<Exception>();
            do {
                list.Add(exception);
                exception = exception.InnerException;
            } while (exception != null);

            FullStackTrace = (info + "\r\n").Trim();
            foreach (var ex in Enumerable.Reverse(list)) {
                FullStackTrace += "\r\n" + ex.Message + " (" + ex.GetType().FullName + ")\r\n" + ex.StackTrace;
            }
            FullStackTrace = FullStackTrace.Trim();
        }
    }
}
