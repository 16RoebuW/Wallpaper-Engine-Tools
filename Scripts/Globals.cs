using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public partial class Globals : Node
{
    private HashSet<Tag> builtinTags = [];
    private HashSet<Tag> customTags = [];
    private HashSet<Tag> specialTags = [];
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
            if (tagValue == "White")
            {
                return wallCol.S < 0.1f && wallCol.V > 0.75f;
            } else if (tagValue == "Black")
            {
                return wallCol.V < 0.1f;
            }
            Color sentinel = new (0,0,1);
            var tagCol = Color.FromString(tagValue, sentinel);
            if (tagCol == sentinel)
                return false;
            return MathF.Min(MathF.Abs(tagCol.H - wallCol.H), 1 - MathF.Abs(tagCol.H - wallCol.H)) < 0.075f &&
                   wallCol.S > 0.4f && 
                   wallCol.V > 0.1f;
        }}
    };
    public const string DefaultTagType = "normal";
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

    private static float ColourDistanceSq(Color from, Color to)
    {
        return MathF.Pow(from.R - to.R, 2) +
               MathF.Pow(from.G - to.G, 2) +
               MathF.Pow(from.B - to.B, 2);
    }

    public static bool HasSpecialTag(WallpaperInfo wallpaperInfo, Tag tag)
    {
        return specialTagDefinitions[tag.Category](tag.Name, wallpaperInfo);
    }

    public static void SetBuiltinTags(HashSet<Tag> tags)
    {
        instance.builtinTags = tags;
    }

    public static void SetCustomTags(HashSet<Tag> tags)
    {
        instance.customTags = tags;
    }

    public static void SetSpecialTags(HashSet<Tag> tags)
    {
        instance.specialTags = tags;
    }

    public static void RemoveCustomTag(string name)
    {
        instance.customTags.Remove(new Tag(name));
    }

    public static void AddCustomTag(Tag tag)
    {
        instance.customTags.Add(tag);
    }

    public static IEnumerable<Tag> GetTagsEnumerator()
    {
        foreach (var item in instance.builtinTags)
            yield return item;
        
        foreach (var item in instance.customTags)
            yield return item;

        foreach (var item in instance.specialTags)
            yield return item;
    }

    public static HashSet<Tag> GetCustomTags()
    {
        return instance.customTags;
    }

    public static HashSet<Tag> GetBuiltinTags()
    {
        return instance.builtinTags;
    }

    public static HashSet<Tag> GetSpecialTags()
    {
        return instance.specialTags;
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