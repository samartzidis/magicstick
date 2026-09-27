namespace MagicStickUI.Backend.Models;

/// <summary>
/// DTO for keymap data returned by GetKeymap.
/// </summary>
public class GetKeymapResult
{
    public string[] Items { get; set; }
}
