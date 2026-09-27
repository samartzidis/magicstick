using System.Reflection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace MagicStickUI.Backend.Util;

/// <summary>
/// Blends the embedded PNG icon layers (base battery icon, charge-level indicator, charging
/// overlay, missing overlay) into the composite PNG the frontend hands to Tauri's tray icon API.
/// Not exposed to the frontend directly; reached through <see cref="Services.AppService.BlendIconData"/>.
/// Tray icon manipulation itself (setIcon/setTooltip) is done by the frontend directly through
/// Tauri's own tray API; this class only produces the pixels.
/// </summary>
public class TrayIconRenderer
{
    private readonly Assembly _assembly;
    private readonly string _resourcePrefix;

    public TrayIconRenderer()
    {
        _assembly = Assembly.GetExecutingAssembly();
        _resourcePrefix = _assembly.GetName().Name + ".Assets.Icons.";
    }

    public byte[] BlendIconData(string[] iconNames)
    {
        if (iconNames == null || iconNames.Length == 0)
            return null;

        var layers = new List<Image<Rgba32>>();
        try
        {
            foreach (var name in iconNames)
                layers.Add(LoadEmbeddedPng(name));

            using var blended = BlendImages(layers);
            using var ms = new MemoryStream();
            blended.SaveAsPng(ms);
            return ms.ToArray();
        }
        finally
        {
            foreach (var img in layers)
                img.Dispose();
        }
    }

    private static Image<Rgba32> BlendImages(List<Image<Rgba32>> images)
    {
        var w = images.Max(i => i.Width);
        var h = images.Max(i => i.Height);

        var result = new Image<Rgba32>(w, h); // transparent canvas

        foreach (var img in images)
        {
            var x = (w - img.Width) / 2;
            var y = (h - img.Height) / 2;
            result.Mutate(ctx => ctx.DrawImage(img, new Point(x, y), 1f));
        }

        return result;
    }

    private Image<Rgba32> LoadEmbeddedPng(string fileName)
    {
        var resourceName = _resourcePrefix + fileName;
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new Exception($"Embedded resource {resourceName} not found");

        return Image.Load<Rgba32>(stream);
    }
}
