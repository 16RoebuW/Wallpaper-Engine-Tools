using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public partial class ToolsDataManager : Node
{
    const string SaveDataLocation = "user://";
    const string CustomTagsName = "custom_tags.json";
    const string WallpaperDataSuffix = ".json";
    const string BackupPrefix = "backups/";
    const string PrefsName = "config.json";
    const string FilterName = "filters.json";

    public static ToolsDataManager Instance;

    public override void _Ready()
    {
        Instance = this;
    }

    public static void SaveFilter(HashSet<Tag> filters)
    {
        Godot.Collections.Dictionary<string, Array<string>> godotVersion = [];
        foreach (var tag in filters)
        {
            if (godotVersion.ContainsKey(tag.Category))
            {
                godotVersion.Add(tag.Category, [tag.Name]);
            } 
            else
            {
                godotVersion[tag.Category].Add(tag.Name);
            }
        }
        string json = Json.Stringify(godotVersion);
        using var file = FileAccess.Open(FullPath(FilterName), FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PrintErr($"Failed to open '{FullPath(FilterName)}': {FileAccess.GetOpenError()}");
            return;
        }
        file.StoreString(json);
    }

    public static HashSet<Tag> LoadFilter()
    {
        HashSet<Tag> res = [];

        if (!FileAccess.FileExists(FullPath(FilterName)))
        {
            return [];
        }

        try
        {
            using var file = FileAccess.Open(FullPath(FilterName), FileAccess.ModeFlags.Read);
            var result = Json.ParseString(file.GetAsText()).AsGodotDictionary();
            foreach (var kv in result)
            {
                foreach (var tag in kv.Value.AsGodotArray<string>())
                {
                    res.Add(new Tag(tag, kv.Key.AsString()));
                }
            }
            return res;
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to read custom tags file:" + e.Message);
            return [];
        }
    }

    public static void SaveCustomTags(HashSet<Tag> tags)
    {
        Godot.Collections.Dictionary<string, string> preprocessed = [];
        foreach (var tag in tags)
        {
            preprocessed.Add(tag.Name, tag.Colour.ToHtml(false));
        }
        string json = Json.Stringify(preprocessed, "\t");
        using var file = FileAccess.Open(FullPath(CustomTagsName), FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PrintErr($"Failed to open '{FullPath(CustomTagsName)}': {FileAccess.GetOpenError()}");
            return;
        }
        file.StoreString(json);
    }

    public static HashSet<Tag> LoadCustomTags()
    {
        if (!FileAccess.FileExists(FullPath(CustomTagsName)))
        {
            return [];
        }

        try
        {
            using var file = FileAccess.Open(FullPath(CustomTagsName), FileAccess.ModeFlags.Read);
            var result = Json.ParseString(file.GetAsText()).AsGodotDictionary();
            HashSet<Tag> parsed = [];
            foreach (var kv in result)
            {
                parsed.Add(new Tag(kv.Key.AsString(), colour: Color.FromHtml(kv.Value.AsString())));
            }
            return parsed;
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to read custom tags file:" + e.Message);
            return [];
        }
    }

    public static void SaveCustomData(string wallpaperID, Dictionary data)
    {
        // Assume that wallpaper engine already sanitized the ID and made it unique
        if (!IsValidFilename(wallpaperID))
        {
            GD.PrintErr($"Invalid wallpaper ID '{wallpaperID}', data will not be saved!");
            return;
        }
        string json = Json.Stringify(data, "\t");
        var path = FullPath(wallpaperID + WallpaperDataSuffix);
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PrintErr($"Failed to open '{path}': {FileAccess.GetOpenError()}");
            return;
        }
        file.StoreString(json);
    }

    public static Dictionary LoadCustomData(string wallpaperID)
    {
        // Assume that wallpaper engine already sanitized the ID and made it unique
        if (!IsValidFilename(wallpaperID))
        {
            GD.PrintErr($"Invalid wallpaper ID '{wallpaperID}', cannot load");
            return [];
        }

        var path = FullPath(wallpaperID + WallpaperDataSuffix);

        if (!FileAccess.FileExists(path))
        {
            return [];
        }

        try
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            var result = Json.ParseString(file.GetAsText()).AsGodotDictionary();
            return result;
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to read custom data file:" + e.Message);
            return [];
        }
    }

    private static string FullPath(string end)
    {
        return SaveDataLocation + end;
    }

    public static bool IsValidFilename(string anyString)
    {
        if (anyString.Length == 0) {
            return false;
        }
        foreach (var symbol in System.IO.Path.GetInvalidFileNameChars())
        {
            if (anyString.Contains(symbol))
            {
                return false;
            }
        }
        return true;
    }

    public static void BackupPreferences(string json)
    {
        string date = Time.GetDatetimeStringFromSystem().Replace(":", "");
        var error = DirAccess.MakeDirRecursiveAbsolute(FullPath(BackupPrefix));
        if (error != Error.Ok)
        {
            GD.PrintErr("Failed to create backup dir: " + error);
            return;
        }

        var path = FullPath(BackupPrefix + date + "-" + PrefsName);
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PrintErr($"Failed to open '{path}': {FileAccess.GetOpenError()}");
            return;
        }
        file.StoreString(json);
        GD.Print($"Preferences backup saved to {path}");
    }

    public static void BackupPreferences()
    {
        BackupPreferences(Globals.PreferencesString);
    }
}