using System;
using System.Collections.Generic;
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
            SetOverlay($"[{type}] {Shorten(condition, 220)}\n{Shorten(stackTrace, 400)}");
        }

        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = args.ExceptionObject as Exception;
            string text = e != null ? e.ToString() : args.ExceptionObject?.ToString() ?? "?";
            Append($"[UNHANDLED] {text}");
            SetOverlay("[UNHANDLED] " + Shorten(text, 500));
            TryFlush();
        }

        private static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " | ");
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        public static void Info(string message)
        {
            Append($"[info] {message}");
        }

        // ---------------------------------------------------------------- on-screen mirror
        // A user with no cable and no logcat still has to be able to report WHY the app
        // died. Anything serious is printed over the game view so one screenshot is enough.
        private static string overlay;
        private static float overlayAt;
        private static readonly List<string> overlayLines = new List<string>(12);

        private static void SetOverlay(string line)
        {
            if (line == null) return;
            lock (overlayLines)
            {
                overlayLines.Add(line);
                while (overlayLines.Count > 10) overlayLines.RemoveAt(0);
                overlay = string.Join("\n", overlayLines);
                overlayAt = Time.realtimeSinceStartup;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallOverlay()
        {
            try
            {
                if (overlayHost != null) return;
                GameObject go = new GameObject("~CrashLogOverlay");
                UnityEngine.Object.DontDestroyOnLoad(go);
                overlayHost = go.AddComponent<CrashOverlay>();
            }
            catch (Exception)
            {
            }
        }

        internal static CrashOverlay overlayHost;

        // Nested MonoBehaviours cannot be instantiated reliably by Unity's native side
        // (the type is not registered), so the overlay component lives in its own file
        // and only reads the text from here.
        internal static string OverlayText
        {
            get { lock (overlayLines) return overlay; }
        }

        internal static void ClearOverlay()
        {
            lock (overlayLines) overlayLines.Clear();
        }

        internal static float OverlayAge
        {
            get { return Time.realtimeSinceStartup - overlayAt; }
        }

        public static void Error(string context, Exception e)
        {
            string text = e != null ? e.ToString() : "null";
            Append($"[error] {context}: {text}");
            SetOverlay($"[error] {context}: {Shorten(text, 450)}");
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
