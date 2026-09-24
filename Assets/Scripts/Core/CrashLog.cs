using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Field diagnostics: captures every Unity error/exception plus unhandled managed
    /// exceptions into a rolling text file inside persistentDataPath.
    /// Without this, a release build on a phone is a black box - which is exactly why
    /// the first device crash had to be guessed at.
    /// </summary>
    public static class CrashLog
    {
        private const string FileName = "starbeak_log.txt";
        private const int MaxBytes = 256 * 1024;

        private static readonly object Gate = new object();
        private static bool installed;

        public static string FilePath =>
            Path.Combine(Application.persistentDataPath, FileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (installed) return;
            installed = true;

            try
            {
                Application.logMessageReceivedThreaded += OnLog;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
                Info($"boot v{Application.version} unity={Application.unityVersion} device={SystemInfo.deviceModel} gfx={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType}");
            }
            catch (Exception)
            {
                // Diagnostics must never be the reason a build dies.
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            Append($"[{type}] {condition}\n{stackTrace}");
        }

        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = args.ExceptionObject as Exception;
            Append($"[UNHANDLED] {(e != null ? e.ToString() : args.ExceptionObject?.ToString() ?? "?")}");
            TryFlush();
        }

        public static void Info(string message)
        {
            Append($"[info] {message}");
        }

        public static void Error(string context, Exception e)
        {
            Append($"[error] {context}: {(e != null ? e.ToString() : "null")}");
        }

        public static void Warn(string message)
        {
            Append($"[warn] {message}");
        }

        private static void Append(string line)
        {
            try
            {
                string stamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                lock (Gate)
                {
                    string path = FilePath;
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    Roll(path);
                    File.AppendAllText(path, $"{stamp} {line}{Environment.NewLine}", Encoding.UTF8);
                }
            }
            catch (Exception)
            {
                // Never throw from logging.
            }
        }

        private static void Roll(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                if (new FileInfo(path).Length < MaxBytes) return;

                string old = path + ".1";
                if (File.Exists(old)) File.Delete(old);
                File.Move(path, old);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>Force the buffered stream to disk (used on fatal paths).</summary>
        public static void TryFlush()
        {
            try
            {
                lock (Gate)
                {
                    using (FileStream fs = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        fs.Flush(true);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
