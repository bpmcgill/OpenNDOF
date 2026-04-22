namespace OpenNDOF.Core.Input;

/// <summary>Immutable snapshot of button states for one HID report.</summary>
public sealed class KeyboardState
{
    public static readonly KeyboardState Empty = new([]);

    private readonly IReadOnlySet<int> _pressed;

    public KeyboardState(IEnumerable<int> pressedKeyCodes)
        => _pressed = new HashSet<int>(pressedKeyCodes);

    public bool IsPressed(int keyCode) => _pressed.Contains(keyCode);
    public IReadOnlyCollection<int> PressedKeys => (IReadOnlyCollection<int>)_pressed;
}
