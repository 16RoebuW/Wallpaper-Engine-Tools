using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public partial class WallpaperInfo
{
    public Dictionary WEData { get; }
    public string Identifier { get; }
    public string Title { get; }
    public string PreviewPath { get; } = null;
    public string FullPath { get; } = null;
    public HashSet<Tag> Tags { get {  return GetTags(); } }
    private HashSet<string> stringTags { get; } = [];

    public static WallpaperInfo From(string json, string fullPath)
    {
        var parser = new Json();
        var err = parser.Parse(json);
        if (err != Error.Ok)
        {
            GD.PrintErr(parser.GetErrorMessage());
            return null;
        }
        var dict = parser.Data.AsGodotDictionary();

        if (!dict.ContainsKey("title"))
        {
            GD.PrintErr("title was missing");
            return null;
        }
        var title = dict["title"].AsString();

        string preview = null;
        if (dict.ContainsKey("preview"))
        {
            preview = dict["preview"].AsString();
        }

        if (dict.ContainsKey("category") && dict["category"].AsString() != "Wallpaper")
        {
            GD.PrintErr("Not a wallpaper"); // There is actually no problem with these, we just can't exclude them from playlists until I add special tag support
            return null;
        }

        return new WallpaperInfo(dict, fullPath.Split("/").Last(), title, fullPath, preview);
    }

    public void LoadCustomData(Dictionary data)
    {
        LoadTags(data);
    }

    private void LoadTags(Dictionary tagDict)
    {
        if (!tagDict.ContainsKey("tags"))
        {
            return;
        }

        foreach (var tag in tagDict["tags"].AsGodotArray())
        {
            stringTags.Add(tag.ToString());
        }
    }

    public Color? GetSchemeColor()
    {
        try
        {
            var colour = Globals.LoadFromUntrustworthyDict(WEData, ["general", "properties", "schemecolor", "value"]);
            // Colour is in string "float float float" form. (or doubles perhaps)
            var floatStrings = colour.AsString().Split(' ');
            if (floatStrings.Length != 3)
            {
                return null;
            }
            float[] parsed = new float[floatStrings.Length];
            for (int i = 0; i < 3; i++)
            {
                parsed[i] = float.Parse(floatStrings[i], CultureInfo.InvariantCulture.NumberFormat);
            }
            return new Color(parsed[0], parsed[1], parsed[2]);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        catch (FormatException e)
        {
            GD.PrintErr($"Failed to parse float: {e.Message}");
            return null;
        }
    }

    private WallpaperInfo(Dictionary data, string id, string title, string fullPath, string previewPath)
    {
        WEData = data;
        Identifier = id;
        Title = title;
        FullPath = fullPath;
        PreviewPath = previewPath;

        LoadTags(WEData);
    }

    public HashSet<string> GetStringTags()
    {
        return stringTags;
    }

    private HashSet<Tag> GetTags()
    {
        // Colour doesn't matter, it is not used for comparison
        HashSet<Tag> tags = [];
        foreach (string tag in stringTags)
        {
            tags.Add(new Tag(tag));
        }
        foreach (var tag in Globals.GetSpecialTags())
        {
            if (Globals.HasSpecialTag(this, tag))
            {
                tags.Add(tag);
            }
        }
        return tags;
    }

    public void AddTag(string tag)
    {
        stringTags.Add(tag);
    }

    public void RemoveTag(string tag)
    {
        stringTags.Remove(tag);
    }
}
