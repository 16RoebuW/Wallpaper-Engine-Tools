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
    public System.Collections.Generic.HashSet<string> Tags { get; } = [];

    private Dictionary CustomData = [];

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

        return new WallpaperInfo(dict, fullPath.Split("/").Last(), title, fullPath, preview);
    }

    public void AddCustomData(Dictionary data)
    {
        CustomData = data;
        LoadTags(CustomData);
    }

    private void LoadTags(Dictionary tagDict)
    {
        if (!tagDict.ContainsKey("tags"))
        {
            return;
        }

        foreach (var tag in tagDict["tags"].AsGodotArray())
        {
            Tags.Add(tag.ToString());
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
}
