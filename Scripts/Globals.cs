using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Globals : Node
{
    private Godot.Collections.Dictionary<string, Color> builtinTags = [];
    private Godot.Collections.Dictionary<string, Color> customTags = [];
    private static readonly System.Collections.Generic.Dictionary<string, Func<string, WallpaperInfo, bool>> specialTagDefinitions = new() {
        // Built-in special tags
        {"sourcetags", (string tagValue, WallpaperInfo wallpaper) =>
        {
            return tagValue switch
            {
                "Local" => !wallpaper.WEData.ContainsKey("workshopid"), "Official" => false, "Workshop" => wallpaper.WEData.ContainsKey("workshopid"), _ => false,
            }; }
        },
        {"categorytags", (string tagValue, WallpaperInfo wallpaper) =>
        {
            return tagValue switch
            {
                "Preset" => false, "Wallpaper" => !wallpaper.WEData.ContainsKey("category") || wallpaper.WEData["category"].AsString() == "Wallpaper", _ => false
            }; }
        },
        {"typetags", (string tagValue, WallpaperInfo wallpaper) =>
        {
            if (!wallpaper.WEData.ContainsKey("type"))
                return false;
            return tagValue.Equals(wallpaper.WEData["type"].AsString(), StringComparison.CurrentCultureIgnoreCase);
        }},

        // Our special tags
        {"thumbnail", (string tagValue, WallpaperInfo wallpaper) =>
        {
            return tagValue == "Thumbnail" ^ (wallpaper.PreviewPath is null);
        }},
        {"colour", (string tagValue, WallpaperInfo wallpaper) =>
        {
            var wallpaperCol = wallpaper.GetSchemeColor();
            if (wallpaperCol is null)
                return false;
            var wallCol = wallpaperCol.Value;
            Color sentinel = new (0,0,1);
            var tagCol = Color.FromString(tagValue, sentinel);
            if (tagCol == sentinel)
                return false;
            return MathF.Pow(wallCol.R - tagCol.R, 2) +
                   MathF.Pow(wallCol.G - tagCol.G, 2) +
                   MathF.Pow(wallCol.B - tagCol.B, 2) < 0.25f;
        }}
    };
    private Godot.Collections.Dictionary<string, Godot.Collections.Dictionary<string, Color>> specialTags = [];
    public static List<WallpaperInfo> LoadedWallpapers = [];
    public static Godot.Collections.Dictionary<string, Texture2D> LoadedWallpaperTextures = [];
    public static Dictionary Preferences;
    public static string PreferencesString;
    public static string UserName;
    public static HashSet<string> SupportedSpecialTags
    {
        get { return [.. specialTagDefinitions.Keys]; }
    }

    private static Globals instance;

    public override void _Ready()
    {
        instance = this;
    }

    public static bool HasSpecialTag(WallpaperInfo wallpaperInfo, string tagType, string tagValue)
    {
        return specialTagDefinitions[tagType](tagValue, wallpaperInfo);
    }

    public static void SetBuiltinTags(Godot.Collections.Dictionary<string, Color> tags)
    {
        instance.builtinTags = tags;
    }

    public static void SetCustomTags(Godot.Collections.Dictionary<string, Color> tags)
    {
        instance.customTags = tags;
    }

    public static void SetSpecialTags(string tag, Godot.Collections.Dictionary<string, Color> tags)
    {
        if (!instance.specialTags.TryAdd(tag, tags))
        {
            instance.specialTags[tag] = tags;
        }
    }

    public static void RemoveCustomTag(string name)
    {
        instance.customTags.Remove(name);
    }

    public static void AddCustomTag(string name, Color colour)
    {
        instance.customTags.Add(name, colour);
    }

    public static IEnumerable<KeyValuePair<string, Color>> GetTagsEnumerator()
    {
        foreach (var item in instance.builtinTags)
            yield return item;
        
        foreach (var item in instance.customTags)
            yield return item;
    }

    public static Godot.Collections.Dictionary<string, Color> GetCustomTags()
    {
        return instance.customTags;
    }

    public static Godot.Collections.Dictionary<string, Color> GetBuiltinTags()
    {
        return instance.builtinTags;
    }

    public static Godot.Collections.Dictionary<string, Color> GetSpecialTags(string tag)
    {
        return instance.specialTags.GetValueOrDefault(tag, []);
    }

    public static Variant LoadFromUntrustworthyDict(Dictionary dictionary, string[] path)
    {
        Variant current = dictionary;
        foreach (var part in path)
        {
            if (!current.AsGodotDictionary().ContainsKey(part))
            {
                throw new KeyNotFoundException($"key '{part}' not found");
            }
            current = current.AsGodotDictionary()[part];
        };
        return current;
    }
}
