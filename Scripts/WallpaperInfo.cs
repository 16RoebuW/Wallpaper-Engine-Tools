using Godot;
using Godot.Collections;
using System;

public partial class WallpaperInfo
{
    public Dictionary WEData { get; }
    public string Identifier { get; }
    public string Title { get; }
    public string PreviewPath { get; } = null;

    public Dictionary CustomData { get; set; }

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

        //if (!dict.ContainsKey("workshopid")) 
        //{
        //    GD.PrintErr("workshopid was missing");
        //    return null;
        //}
        //var id = dict["workshopid"].AsInt64();

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

    private WallpaperInfo(Dictionary data, string id, string title, string previewPath)
    {
        WEData = data;
        Identifier = id;
        Title = title;
        PreviewPath = previewPath;
    }
}
