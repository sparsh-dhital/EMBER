using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

// Bakes EMBER's TextMeshPro fonts into static atlases.
//
// Why this exists: the Inter font assets shipped as Dynamic. A dynamic atlas rasterises
// glyphs on demand at runtime, and when the atlas fills up TMP repacks it in place - which
// invalidates the UVs of glyphs already on screen. That is what produced the scattered,
// smeared text, and on Android it also meant a texture upload (and a GC spike) the first
// time any new character appeared.
//
// Baking every character the game can display into a static atlas up front makes text
// sharp and stable, costs nothing at runtime, and removes that whole class of hitch.
public static class EmberFonts
{
    const string FontDir = "Assets/Art/Fonts";

    // Everything the UI can print. Printable ASCII covers the bulk; the rest are the
    // typographic characters the HUD, menus and story text actually use.
    const string Extras = "—–•·…‘’“”×°→←«»©™";

    [MenuItem("EMBER/Build/6. Bake Font Atlases (fixes text rendering)")]
    public static void BakeAll()
    {
        int baked = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null) continue;
            if (Bake(font, path)) baked++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("EMBER: baked " + baked + " font atlas(es) to static.");
    }

    static bool Bake(TMP_FontAsset font, string path)
    {
        string charset = BuildCharset();

        // Adding glyphs requires a dynamic atlas, so bake in dynamic mode and lock it
        // down afterwards.
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.ClearFontAssetData(true);

        if (!font.TryAddCharacters(charset, out string missing))
        {
            // Not fatal: the face simply has no outline for those code points. Report them
            // so a designer knows why a character shows as a box rather than guessing.
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning("EMBER fonts: '" + System.IO.Path.GetFileName(path) +
                                 "' has no glyph for: " + missing);
        }

        font.atlasPopulationMode = AtlasPopulationMode.Static;
        // A static atlas must never grow at runtime; this makes that explicit.
        font.isMultiAtlasTexturesEnabled = false;

        EditorUtility.SetDirty(font);
        if (font.atlasTextures != null)
            foreach (var tex in font.atlasTextures)
                if (tex) EditorUtility.SetDirty(tex);

        Debug.Log("EMBER fonts: '" + System.IO.Path.GetFileName(path) + "' baked static, " +
                  (font.glyphTable != null ? font.glyphTable.Count : 0) + " glyphs in " +
                  font.atlasWidth + "x" + font.atlasHeight + ".");
        return true;
    }

    static string BuildCharset()
    {
        var sb = new StringBuilder();
        for (char c = ' '; c <= '~'; c++) sb.Append(c);   // printable ASCII
        sb.Append(Extras);
        return sb.ToString();
    }
}
