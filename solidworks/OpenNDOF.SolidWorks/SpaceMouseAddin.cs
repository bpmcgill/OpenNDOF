using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;

namespace OpenNDOF.SolidWorks
{
    /// <summary>
    /// EXPERIMENTAL SOLIDWORKS add-in: drives the active view's camera from a 3Dconnexion
    /// device read over raw HID.
    ///
    /// SOLIDWORKS never loads the TDxInput COM server — its 3D-mouse support comes from the
    /// 3DxWare driver's own add-in. This add-in plays that role: a pump thread posts one tick
    /// at a time to SOLIDWORKS' UI thread, and each tick applies a low-pass-filtered,
    /// dead-zoned rotation / zoom / pan through the IModelView API (angles in radians).
    /// Only the foreground SOLIDWORKS window is driven, like 3DxWare.
    /// Settings: SpaceMouse.cfg next to the DLL. Log: OpenNDOF.SolidWorks.log next to the DLL.
    /// </summary>
    [ComVisible(true)]
    [Guid(AddinGuid)]
    [ProgId(AddinProgId)]
    public sealed class SpaceMouseAddin : ISwAddin
    {
        public const string AddinGuid = "41726e6e-bf05-4139-a6bf-cacd88efff1b";
        public const string AddinProgId = "OpenNDOF.SolidWorksAddin";
        public const string AddinTitle = "OpenNDOF SpaceMouse";
        public const string AddinDescription = "3Dconnexion device support via OpenNDOF (experimental)";

        private const double FullScale = 350.0;     // raw device units at full deflection
        private const int SwIsometricView = 7;      // swStandardViews_e.swIsometricView

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly uint _processId = (uint)Process.GetCurrentProcess().Id;
        private readonly double[] _filtered = new double[6];
        private readonly double[] _center = new double[3];

        private ISldWorks _sw;
        private AddinSettings _settings;
        private AppProfile _profile;
        private HidReader _hid;
        private Control _ui;                        // hidden control owned by the SW UI thread
        private Thread _pump;
        private volatile bool _run;
        private int _tickPending;
        private StreamWriter _log;

        private const double PanViewWidthsPerUnit = 0.25;  // pan=1 -> a quarter view width per second
        private const double DefaultViewWidth = 0.3;       // TranslateBy units, if the view size is unknown

        private readonly double[] _peakAxis = new double[6];
        private double _viewWidth = DefaultViewWidth;
        private double _lastTick, _lastStats, _lastDocFetch = -10, _lastProfileCheck = -10;
        private IModelDoc2 _doc;
        private object _lastDoc;
        private uint _previousButtons;
        private int _statTicks, _moveTicks;
        private double _sumNavMs, _maxNavMs;

        private DateTime _settingsStamp;

        private static string AddinDirectory => Path.GetDirectoryName(typeof(SpaceMouseAddin).Assembly.Location);

        private static string SettingsPath => Path.Combine(AddinDirectory, "SpaceMouse.cfg");

        private static DateTime SettingsStamp() =>
            File.Exists(SettingsPath) ? File.GetLastWriteTimeUtc(SettingsPath) : DateTime.MinValue;

        /// <summary>Reloads SpaceMouse.cfg when it has been saved since the last load.</summary>
        private void RefreshSettings()
        {
            DateTime stamp = SettingsStamp();
            if (stamp == _settingsStamp)
            {
                return;
            }

            _settingsStamp = stamp;
            try
            {
                _settings = AddinSettings.Load(SettingsPath, Log);
                Log("settings reloaded: " + _settings);
            }
            catch (IOException e)
            {
                Log("SpaceMouse.cfg unreadable, keeping previous settings: " + e.Message);
                _settingsStamp = DateTime.MinValue;
            }
        }

        // ── ISwAddin ─────────────────────────────────────────────────────────
        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                _log = new StreamWriter(Path.Combine(AddinDirectory, "OpenNDOF.SolidWorks.log"), append: true) { AutoFlush = true };
                _sw = (ISldWorks)ThisSW;
                _settings = AddinSettings.Load(SettingsPath, Log);
                _settingsStamp = SettingsStamp();
                Log("=== add-in loaded (SOLIDWORKS " + _sw.RevisionNumber() + ") " + _settings);
                _profile = new AppProfile(AppProfile.DefaultPath, Log);
                _profile.Refresh();

                _ui = new Control();
                _ui.CreateControl();
                _ = _ui.Handle;                     // force handle creation on the UI thread

                _hid = new HidReader(Log);
                _hid.Start();
                timeBeginPeriod(1);

                _run = true;
                _pump = new Thread(Pump) { IsBackground = true, Name = "openndof-sw-pump" };
                _pump.Start();
                return true;
            }
            catch (Exception e)
            {
                Log("ConnectToSW failed: " + e);
                return false;
            }
        }

        public bool DisconnectFromSW()
        {
            _run = false;
            _pump?.Join(500);
            _hid?.Dispose();
            timeEndPeriod(1);
            _ui?.Dispose();
            Log("add-in unloaded");
            _log?.Dispose();

            _doc = null;
            _lastDoc = null;
            if (_sw != null)
            {
                Marshal.ReleaseComObject(_sw);
                _sw = null;
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return true;
        }

        private void Log(string message)
        {
            try
            {
                _log?.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + message);
            }
            catch (IOException)
            {
            }
        }

        // ── Timing: never more than one tick queued on the UI thread ─────────
        private void Pump()
        {
            double next = _clock.Elapsed.TotalSeconds;
            while (_run)
            {
                double period = 1.0 / _settings.Hz;     // settings may be reloaded live
                if (Interlocked.CompareExchange(ref _tickPending, 1, 0) == 0)
                {
                    try
                    {
                        _ui.BeginInvoke((MethodInvoker)Tick);
                    }
                    catch (InvalidOperationException)
                    {
                        Interlocked.Exchange(ref _tickPending, 0);
                    }
                }

                next += period;
                double wait = next - _clock.Elapsed.TotalSeconds;
                if (wait < -0.05)
                {
                    next = _clock.Elapsed.TotalSeconds;  // fell behind: resynchronise
                }
                else if (wait > 0)
                {
                    Thread.Sleep(Math.Max(1, (int)(wait * 1000)));
                }
            }
        }

        private void Tick()
        {
            try
            {
                TickCore();
            }
            catch (Exception e)
            {
                Log("tick: " + e.Message);
                _doc = null;
            }
            finally
            {
                Interlocked.Exchange(ref _tickPending, 0);
            }
        }

        private void TickCore()
        {
            double now = _clock.Elapsed.TotalSeconds;
            double dt = now - _lastTick;
            _lastTick = now;
            if (dt <= 0.0005 || dt > 0.25)
            {
                dt = 1.0 / _settings.Hz;
            }
            _statTicks++;

            double[] raw = _hid.Read(out uint buttons);
            double alpha = 1.0 - Math.Exp(-dt / _settings.TauSeconds);
            for (int a = 0; a < 6; a++)
            {
                _filtered[a] += (raw[a] / FullScale - _filtered[a]) * alpha;
                _peakAxis[a] = Math.Max(_peakAxis[a], Math.Abs(raw[a]) / FullScale);
            }

            if (now - _lastProfileCheck > 1.0)
            {
                _lastProfileCheck = now;
                _profile.Refresh();             // picks up dashboard edits live
                RefreshSettings();              // and SpaceMouse.cfg edits
            }

            IModelView view = SolidWorksHasFocus() ? GetActiveView(now) : null;
            if (view == null)
            {
                _previousButtons = buttons;
                return;
            }

            Navigate(view, dt);
            HandleButtons(view, buttons);
            LogStats(now);
        }

        private bool SolidWorksHasFocus()
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
            return pid == _processId;
        }

        private IModelView GetActiveView(double now)
        {
            if (_doc == null || now - _lastDocFetch > 0.5)
            {
                _doc = _sw.ActiveDoc as IModelDoc2;
                _lastDocFetch = now;
                if (_doc != null && !ReferenceEquals(_doc, _lastDoc))
                {
                    _lastDoc = _doc;
                    UpdateCenter(_doc);
                    Log("active document: " + _doc.GetTitle());
                }
            }
            var view = _doc?.ActiveView as IModelView;
            if (view != null && _lastDocFetch == now)
            {
                UpdateViewWidth(view);
            }
            return view;
        }

        /// <summary>
        /// Width of the graphics area in IModelView.TranslateBy units. TranslateBy moves the
        /// view by a fixed on-screen distance whatever the zoom (measured: 1 unit = Transform
        /// scale / Scale2 pixels), so pan speed must be expressed in screen terms, not model size.
        /// </summary>
        private void UpdateViewWidth(IModelView view)
        {
            try
            {
                var box = view.GetVisibleBox() as int[];
                double pixelsPerUnit = ((IMathTransform)view.Transform).ArrayData is double[] t && t.Length > 12 && view.Scale2 > 0
                    ? t[12] / view.Scale2
                    : 0;
                if (box != null && box.Length >= 4 && pixelsPerUnit > 0)
                {
                    _viewWidth = Math.Max(1, Math.Abs(box[2] - box[0])) / pixelsPerUnit;
                }
            }
            catch (COMException)
            {
                _viewWidth = DefaultViewWidth;
            }
        }

        private void Navigate(IModelView view, double dt)
        {
            var channel = new double[6];
            double peak = 0;
            for (int c = 0; c < 6; c++)
            {
                int axis = _settings.ChannelAxis[c];
                double dz = Math.Max(_settings.Deadzone, axis < 3 ? _profile.DeadzoneTrans : _profile.DeadzoneRot);
                channel[c] = SoftDeadzone(_filtered[axis], dz) * _profile.Scale[axis] * _settings.ChannelSign[c];
                peak = Math.Max(peak, Math.Abs(channel[c]));
            }
            if (peak <= 0.002)
            {
                return;
            }

            // Cross-talk filter: fade out channels much weaker than the dominant one.
            // The fade is continuous (0 below CrossTalk/2 of the peak, full weight from
            // CrossTalk up): a hard cut-off makes combined motions jerk as a secondary
            // axis toggles on and off around the threshold.
            double full = _settings.CrossTalk * peak;
            if (full > 0)
            {
                for (int c = 0; c < 6; c++)
                {
                    double t = (Math.Abs(channel[c]) - full / 2) / (full / 2);
                    t = Math.Max(0, Math.Min(1, t));
                    channel[c] *= t * t * (3 - 2 * t);   // smoothstep
                }
            }

            _moveTicks++;
            long t0 = Stopwatch.GetTimestamp();
            ApplyCamera(view, channel, dt);
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            _sumNavMs += ms;
            _maxNavMs = Math.Max(_maxNavMs, ms);
        }

        private void ApplyCamera(IModelView view, double[] channel, double dt)
        {
            double k = _settings.RotationDegPerSec * dt * Math.PI / 180.0;  // camera API is in radians
            double tilt = channel[AddinSettings.ChannelTilt], spin = channel[AddinSettings.ChannelSpin];
            double roll = channel[AddinSettings.ChannelRoll], zoom = channel[AddinSettings.ChannelZoom];
            double panX = channel[AddinSettings.ChannelPanX], panY = channel[AddinSettings.ChannelPanY];

            if (tilt != 0 || spin != 0 || roll != 0)
            {
                Rotate(view, tilt * k, spin * k, -roll * k);
            }
            if (zoom != 0)
            {
                double factor = Math.Pow(_settings.ZoomPerSec, -zoom * dt);
                view.ZoomByFactor(Math.Max(0.05, Math.Min(1.95, factor)));
            }
            if (panX != 0 || panY != 0)
            {
                // Screen-relative: same on-screen speed at any zoom level and part size.
                double s = _settings.PanPerSec * PanViewWidthsPerUnit * _viewWidth * dt;
                view.TranslateBy(panX * s, panY * s);
            }
        }

        private void HandleButtons(IModelView view, uint buttons)
        {
            if (buttons == _previousButtons)
            {
                return;
            }

            uint pressed = buttons & ~_previousButtons;
            _previousButtons = buttons;
            if ((pressed & 1u) != 0)
            {
                _doc.ViewZoomtofit2();
                Log("button 0: zoom to fit");
            }
            if ((pressed & 2u) != 0)
            {
                if (_doc.GetStandardViewRotation(SwIsometricView) is MathTransform isometric)
                {
                    view.Orientation3 = isometric;
                }
                _doc.ViewZoomtofit2();
                Log("button 1: isometric view");
            }
        }

        private void LogStats(double now)
        {
            if (now - _lastStats < 5.0)
            {
                return;
            }

            if (_moveTicks > 0)
            {
                Log(string.Format(CultureInfo.InvariantCulture,
                    "stats {0:F1} ticks/s, {1} moving | camera avg {2:F2} max {3:F2} ms | peak tx {4:F2} ty {5:F2} tz {6:F2} rx {7:F2} ry {8:F2} rz {9:F2}",
                    _statTicks / (now - _lastStats), _moveTicks, _sumNavMs / _moveTicks, _maxNavMs,
                    _peakAxis[0], _peakAxis[1], _peakAxis[2], _peakAxis[3], _peakAxis[4], _peakAxis[5]));
            }
            Array.Clear(_peakAxis, 0, 6);
            _lastStats = now;
            _statTicks = _moveTicks = 0;
            _sumNavMs = _maxNavMs = 0;
        }

        private static double SoftDeadzone(double v, double dz)
        {
            dz = Math.Min(dz, 0.9);
            double m = Math.Abs(v) - dz;
            return m <= 0 ? 0 : Math.Sign(v) * Math.Min(1.0, m / (1.0 - dz));
        }

        /// <summary>
        /// One combined rotation (radians) about the screen X, screen Y and view axes,
        /// through the model centre — a single API call per tick keeps it smooth.
        /// </summary>
        private void Rotate(IModelView view, double ax, double ay, double az)
        {
            var m = ((IMathTransform)view.Orientation3).ArrayData as double[];
            if (m == null || m.Length < 9)
            {
                return;
            }

            double x = ax * m[0] + ay * m[1] + az * m[2];
            double y = ax * m[3] + ay * m[4] + az * m[5];
            double z = ax * m[6] + ay * m[7] + az * m[8];
            double angle = Math.Sqrt(x * x + y * y + z * z);
            if (angle < 1e-9)
            {
                return;
            }
            view.RotateAboutAxis(angle, _center[0], _center[1], _center[2], x / angle, y / angle, z / angle);
        }

        private void UpdateCenter(IModelDoc2 doc)
        {
            Array.Clear(_center, 0, 3);
            try
            {
                double[] box = (doc as IPartDoc)?.GetPartBox(true) as double[]
                               ?? (doc as IAssemblyDoc)?.GetBox(0) as double[];
                if (box == null || box.Length < 6)
                {
                    return;
                }

                for (int i = 0; i < 3; i++)
                {
                    _center[i] = (box[i] + box[i + 3]) / 2;
                }
            }
            catch (COMException e)
            {
                Log("bounding box unavailable: " + e.Message);
            }
        }

        // ── regasm /codebase registration (admin). Install-SolidWorksAddin.ps1 is the
        //    usual route: per-user COM registration, one elevated HKLM key. ──────
        [ComRegisterFunction]
        public static void Register(Type t)
        {
            using (RegistryKey k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\SolidWorks\Addins\{" + AddinGuid + "}"))
            {
                k.SetValue(null, 1);
                k.SetValue("Title", AddinTitle);
                k.SetValue("Description", AddinDescription);
            }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\SolidWorks\AddInsStartup\{" + AddinGuid + "}"))
            {
                k.SetValue(null, 1);
            }
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            Registry.LocalMachine.DeleteSubKey(@"SOFTWARE\SolidWorks\Addins\{" + AddinGuid + "}", false);
            Registry.CurrentUser.DeleteSubKey(@"Software\SolidWorks\AddInsStartup\{" + AddinGuid + "}", false);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint milliseconds);
    }
}
