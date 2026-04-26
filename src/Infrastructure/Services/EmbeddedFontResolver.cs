using System.Reflection;

using PdfSharpCore.Fonts;

namespace CitizenPortal.Infrastructure.Services;

/// Resolves fonts from .ttf files embedded as resources inside the
/// Infrastructure assembly, so PDF generation does not depend on fonts
/// installed on the host OS or on where the app happens to be launched from.
/// Covers Greek via DejaVu Sans.

/// Register once at startup via <see cref="Register"/>. PdfSharpCore uses a
/// process-wide static resolver (<c>GlobalFontSettings.FontResolver</c>), so
/// this intentionally isn't a DI-scoped service.

public class EmbeddedFontResolver : IFontResolver
{
    /// Logical family name to pass to <c>new XFont(...)</c> in generator code.
    public const string DefaultFamily = "DejaVu Sans";
    public string DefaultFontName => DefaultFamily;

    // Internal face identifiers returned by ResolveTypeface and then keyed by
    // GetFont. Opaque strings — only this class interprets them.
    private const string RegularFace = "DejaVuSans#Regular";
    private const string BoldFace = "DejaVuSans#Bold";

    // Resource names follow: "<DefaultNamespace>.<FolderPath>.<FileName>"
    // with folder separators replaced by dots. For this project the default
    // namespace is CitizenPortal.Infrastructure and the files live in
    // Assets/Fonts/, giving us:
    private const string RegularResource = "CitizenPortal.Infrastructure.Assets.Fonts.DejaVuSans.ttf";
    private const string BoldResource = "CitizenPortal.Infrastructure.Assets.Fonts.DejaVuSans-Bold.ttf";

    private static readonly Assembly ResourceAssembly = typeof(EmbeddedFontResolver).Assembly;

    public byte[] GetFont(string faceName)
    {
        var resourceName = faceName switch
        {
            RegularFace => RegularResource,
            BoldFace => BoldResource,
            _ => throw new InvalidOperationException($"Unknown font face: {faceName}")
        };

        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Embedded font resource '{resourceName}' not found. " +
                $"Check that the .ttf file is marked as <EmbeddedResource> in " +
                $"CitizenPortal.Infrastructure.csproj and that the resource name " +
                $"matches the assembly's default namespace + folder path.");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // Single-family resolver: ignore familyName and always return DejaVu.
        // Italic is not bundled — silently falls back to regular. Add an
        // italic face here later if the design calls for it.
        var face = isBold ? BoldFace : RegularFace;
        return new FontResolverInfo(face);
    }


    /// Register this resolver as PdfSharpCore's global font resolver.
    /// Call once at startup, before any PDF is generated.
    /// Safe to call multiple times — last call wins.

    public static void Register()
    {
        GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
    }
}