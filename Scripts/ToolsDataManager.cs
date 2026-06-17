using Godot;
using Godot.Collections;
using System;

public partial class ToolsDataManager : Node
{
    const string SaveDataLocation = "user://";
    const string CustomTagsName = "custom_tags.json";
    const string WallpaperDataSuffix = ".json";
    const string BackupPrefix = "backups/";
    const string PrefsName = "config.json";

    public static ToolsDataManager Instance;

    public override void _Ready()
    {
        Instance = this;
    }

    public static void SaveCustomTags(Dictionary<string, Color> tags)
    {
        Dictionary<string, string> preprocessed = [];
        foreach (var kv in tags)
        {
            preprocessed.Add(kv.Key, kv.Value.ToHtml(false));
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

    public static Dictionary<string, Color> LoadCustomTags()
    {
        if (!FileAccess.FileExists(FullPath(CustomTagsName)))
        {
            return [];
        }

        try
        {
            using var file = FileAccess.Open(FullPath(CustomTagsName), FileAccess.ModeFlags.Read);
            var result = Json.ParseString(file.GetAsText()).AsGodotDictionary();
            Dictionary<string, Color> parsed = [];
            foreach (var kv in result)
            {
                parsed.Add(kv.Key.AsString(), Color.FromHtml(kv.Value.AsString()));
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
