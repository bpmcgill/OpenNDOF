using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace OpenNDOF.SolidWorks
{
    /// <summary>
    /// Per-axis sensitivity and dead zones from the OpenNDOF dashboard's profiles
    /// (%APPDATA%\OpenNDOF\profiles.json, written by its Configuration page).
    /// Uses the profile whose AppNames contains "sldworks", else "default".
    /// <see cref="Refresh"/> reloads it whenever the file changes, so edits made in the
    /// dashboard apply to SOLIDWORKS without restarting it.
    /// </summary>
    internal sealed class AppProfile
    {
        private const string ProcessName = "sldworks";
        private static readonly string[] ScaleKeys = { "ScaleTx", "ScaleTy", "ScaleTz", "ScaleRx", "ScaleRy", "ScaleRz" };

        private readonly string _path;
        private readonly Action<string> _log;
        private DateTime _stamp = DateTime.MinValue;

        public AppProfile(string path, Action<string> log)
        {
            _path = path;
            _log = log;
        }

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenNDOF", "profiles.json");

        /// <summary>Multiplier per device axis (tx, ty, tz, rx, ry, rz); 1 = unchanged, 0 = axis off.</summary>
        public double[] Scale { get; } = { 1, 1, 1, 1, 1, 1 };

        /// <summary>Dead zones (fraction of full scale) for translation and rotation axes.</summary>
        public double DeadzoneTrans { get; private set; }

        public double DeadzoneRot { get; private set; }

        /// <summary>Reloads the profile if profiles.json changed since the last call.</summary>
        public void Refresh()
        {
            DateTime stamp = File.Exists(_path) ? File.GetLastWriteTimeUtc(_path) : DateTime.MinValue;
            if (stamp == _stamp)
            {
                return;
            }
            _stamp = stamp;

            try
            {
                Apply(stamp == DateTime.MinValue ? null : SelectProfile(File.ReadAllText(_path)));
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is InvalidOperationException)
            {
                _log("profiles.json unreadable, keeping previous values: " + e.Message);
                _stamp = DateTime.MinValue;     // retry on the next refresh
            }
        }

        private static Dictionary<string, object> SelectProfile(string json)
        {
            var profiles = (new JavaScriptSerializer().DeserializeObject(json) as object[] ?? new object[0])
                .OfType<Dictionary<string, object>>()
                .ToList();

            return profiles.FirstOrDefault(p => GetAppNames(p).Any(n => n.Equals(ProcessName, StringComparison.OrdinalIgnoreCase)))
                   ?? profiles.FirstOrDefault(p => "default".Equals(p.TryGetValue("Name", out object n) ? n as string : null,
                                                                   StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> GetAppNames(Dictionary<string, object> profile) =>
            profile.TryGetValue("AppNames", out object names) && names is IEnumerable list
                ? list.OfType<string>()
                : Enumerable.Empty<string>();

        private void Apply(Dictionary<string, object> profile)
        {
            for (int a = 0; a < 6; a++)
            {
                Scale[a] = Number(profile, ScaleKeys[a], 1.0);
            }
            DeadzoneTrans = Number(profile, "DeadzoneTrans", 0.0);
            DeadzoneRot = Number(profile, "DeadzoneRot", 0.0);

            string name = profile != null && profile.TryGetValue("Name", out object n) ? n as string : "(none)";
            _log(string.Format(CultureInfo.InvariantCulture,
                "dashboard profile '{0}': scale {1} | deadzone trans {2} rot {3}",
                name, string.Join(" ", Scale.Select(s => s.ToString("0.##", CultureInfo.InvariantCulture))),
                DeadzoneTrans, DeadzoneRot));
        }

        private static double Number(Dictionary<string, object> profile, string key, double fallback)
        {
            if (profile == null || !profile.TryGetValue(key, out object value) || value == null)
            {
                return fallback;
            }

            double d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return double.IsNaN(d) || double.IsInfinity(d) ? fallback : Math.Max(0, d);
        }
    }
}
