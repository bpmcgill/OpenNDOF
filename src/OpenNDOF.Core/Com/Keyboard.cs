using System.Runtime.InteropServices;

namespace OpenNDOF.Core.Com;

/// <summary>
/// COM-visible Keyboard object. Fires <c>KeyDown</c> / <c>KeyUp</c> dispinterface
/// events as buttons are pressed and released.
/// CLSID: 25BBE090-583A-4903-A61B-D0EC629AC4EC
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("25BBE090-583A-4903-A61B-D0EC629AC4EC")]
[ProgId("TDxInput.Keyboard")]
[ComSourceInterfaces(typeof(_IKeyboardEvents))]
public sealed class Keyboard : IKeyboard, ICustomQueryInterface
{
    private          object?            _device;
    private readonly HashSet<int>       _pressed = [];

    // Own connection-point container for the _IKeyboardEvents outgoing interface.
    private readonly ConnectionPointContainer _events = new(ComIids.KeyboardEvents, (1, "KeyDown"), (2, "KeyUp"));

    // ── IKeyboard ─────────────────────────────────────────────────────────────
    public int    Keys             => _pressed.Count;
    public int    ProgrammableKeys => 0;
    public object Device           => _device!;

    public string GetKeyLabel(int keyCode) => keyCode.ToString();
    public string GetKeyName(int keyCode)  => $"Key{keyCode}";
    public bool   IsKeyDown(int keyCode)   => _pressed.Contains(keyCode);
    public bool   IsKeyUp(int keyCode)     => !_pressed.Contains(keyCode);

    // ── COM events ────────────────────────────────────────────────────────────
#pragma warning disable CS0067
    public event Action<int>? KeyDown;
    public event Action<int>? KeyUp;
#pragma warning restore CS0067

    // ── Internal update called by Device ──────────────────────────────────────
    internal void Update(IReadOnlyCollection<int> nowPressed, object device)
    {
        _device = device;

        // Keys newly pressed
        foreach (int k in nowPressed)
            if (_pressed.Add(k))
                FireKeyDown(k);

        // Keys released
        var released = _pressed.Where(k => !nowPressed.Contains(k)).ToList();
        foreach (int k in released)
        {
            _pressed.Remove(k);
            FireKeyUp(k);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void FireKeyDown(int k)
    {
        KeyDown?.Invoke(k);
        _events.Raise(1, k); // DISPID 1 = KeyDown
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void FireKeyUp(int k)
    {
        KeyUp?.Invoke(k);
        _events.Raise(2, k); // DISPID 2 = KeyUp
    }

    public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
    {
        if (iid == ComIids.IConnectionPointContainer)
        {
            ppv = Marshal.GetComInterfaceForObject(_events, typeof(IConnectionPointContainer));
            return CustomQueryInterfaceResult.Handled;
        }
        ppv = IntPtr.Zero;
        return CustomQueryInterfaceResult.NotHandled;
    }
}
