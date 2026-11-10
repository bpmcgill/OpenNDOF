using System;
using System.Globalization;
using System.IO;

namespace OpenNDOF.SolidWorks
{
    /// <summary>
    /// Tuning and axis mapping, read from SpaceMouse.cfg next to the add-in DLL.
    /// Format: whitespace/comma separated "key=value" items, '#' starts a comment line.
    /// </summary>
    internal sealed class AddinSettings
    {
        public const int ChannelPanX = 0, ChannelPanY = 1, ChannelZoom = 2,
                         ChannelTilt = 3, ChannelSpin = 4, ChannelRoll = 5;

        private static readonly string[] ChannelNames = { "panx", "pany", "zoom", "tilt", "spin", "roll" };
        private static readonly string[] AxisNames = { "tx", "ty", "tz", "rx", "ry", "rz" };

        public double RotationDegPerSec { get; private set; } = 360;
        public double ZoomPerSec { get; private set; } = 8.0;
        public double PanPerSec { get; private set; } = 3.0;
        public double Deadzone { get; private set; } = 0.06;
        public double TauSeconds { get; private set; } = 0.05;
        public double CrossTalk { get; private set; } = 0.3;
        public int Hz { get; private set; } = 60;

        /// <summary>Device axis index (tx..rz) feeding each channel.</summary>
        public int[] ChannelAxis { get; } = { 0, 1, 2, 3, 5, 4 };

        /// <summary>Sign applied to each channel.</summary>
        public double[] ChannelSign { get; } = { 1, 1, -1, 1, -1, -1 };

        public static AddinSettings Load(string path, Action<string> log)
        {
            var settings = new AddinSettings();
            if (!File.Exists(path))
            {
                log("no " + Path.GetFileName(path) + ", using defaults");
                return settings;
            }

            foreach (string line in File.ReadAllLines(path))
            {
                if (line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (string item in line.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    settings.Apply(item);
                }
            }

            log(Path.GetFileName(path) + " loaded");
            return settings;
        }

        private void Apply(string item)
        {
            string[] kv = item.Split('=');
            if (kv.Length != 2)
            {
                return;
            }

            string key = kv[0].Trim().ToLowerInvariant();
            string value = kv[1].Trim().ToLowerInvariant();

            int channel = Array.IndexOf(ChannelNames, key);
            if (channel >= 0)
            {
                ApplyMapping(channel, value);
                return;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                ApplyNumber(key, d);
            }
        }

        private void ApplyMapping(int channel, string value)
        {
            int axis = Array.IndexOf(AxisNames, value.TrimStart('+', '-'));
            if (axis < 0)
            {
                return;
            }

            ChannelAxis[channel] = axis;
            ChannelSign[channel] = value.StartsWith("-", StringComparison.Ordinal) ? -1 : 1;
        }

        private void ApplyNumber(string key, double d)
        {
            switch (key)
            {
                case "rot": RotationDegPerSec = d; break;
                case "zoom": ZoomPerSec = Math.Max(1.01, d); break;
                case "pan": PanPerSec = d; break;
                case "dz": Deadzone = Clamp(d, 0, 0.9); break;
                case "tau": TauSeconds = Math.Max(0.001, d); break;
                case "hz": Hz = (int)Clamp(d, 10, 250); break;
                case "xtalk": CrossTalk = Clamp(d, 0, 1); break;
            }
        }

        private static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "hz={0} rot={1} zoom={2} pan={3} dz={4} tau={5} xtalk={6}",
            Hz, RotationDegPerSec, ZoomPerSec, PanPerSec, Deadzone, TauSeconds, CrossTalk);
    }
}
