namespace MagicStickUI.Backend.Models;

/// <summary>
/// DTO for keymap apply result returned by SetKeymap.
/// </summary>
public class SetKeymapResult
{
    public bool Success { get; set; }
    public string Error { get; set; }
}
