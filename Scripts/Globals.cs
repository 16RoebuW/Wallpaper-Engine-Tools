using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public partial class Globals : Node
{
    private Godot.Collections.Dictionary<string, Color> builtinTags = [];
    private Godot.Collections.Dictionary<string, Color> customTags = [];
    public static List<WallpaperInfo> LoadedWallpapers = [];
    public static Godot.Collections.Dictionary<string, Texture2D> LoadedWallpaperTextures = [];
    public static Dictionary Preferences;
    public static string PreferencesString;
    public static string UserName;

    private static Globals instance;

    public override void _Ready()
    {
        instance = this;
    }

    public static void SetBuiltinTags(Godot.Collections.Dictionary<string, Color> tags)
    {
        instance.builtinTags = tags;
    }

    public static void SetCustomTags(Godot.Collections.Dictionary<string, Color> tags)
    {
        instance.customTags = tags;
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
