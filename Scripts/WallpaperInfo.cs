using Godot;
using Godot.Collections;
using System;

public partial class WallpaperInfo
{
    public Dictionary WEData { get; }
    public string Identifier { get; }
    public string Title { get; }
    public string PreviewPath { get; } = null;
    public System.Collections.Generic.HashSet<string> Tags { get; } = [];

    private Dictionary CustomData = [];

    public static WallpaperInfo From(string json, string folderName)
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

        return new WallpaperInfo(dict, folderName, title, preview);
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

    private WallpaperInfo(Dictionary data, string id, string title, string previewPath)
    {
        WEData = data;
        Identifier = id;
        Title = title;
        PreviewPath = previewPath;

        LoadTags(WEData);
    }
}
