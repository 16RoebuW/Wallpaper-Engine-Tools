using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;

public partial class ToolsDataManager : Node
{
    // Why did I do it like this D: ... This should just be a dict of string -> variant. Would be so much less code
    private static string _WEWorkshopPath = @"C:\Program Files (x86)\Steam\steamapps\workshop\content\431960";
    private static string _WELocalPath = @"C:\Program Files (x86)\Steam\steamapps\common\wallpaper_engine\projects\myprojects";
    private static string _WEPrefsPath = @"C:\Program Files (x86)\Steam\steamapps\common\wallpaper_engine\config.json";

    [UserSetting("Wallpaper Engine Workshop Path")]
    public static string WEWorkshopPath { 
        get { return _WEWorkshopPath; } 
        set
        {
            if (!DirAccess.DirExistsAbsolute(value))
            {
                throw new ArgumentException("Directory does not exist");
            }
            _WEWorkshopPath = value;
        } 
    }

    [UserSetting("Wallpaper Engine Local Wallpaper Path")]
    public static string WELocalPath
    {
        get { return _WELocalPath; }
        set
        {
            if (!DirAccess.DirExistsAbsolute(value))
            {
                throw new ArgumentException("Directory does not exist");
            }
            _WELocalPath = value;
        }
    }

    [UserSetting("Wallpaper Engine Preferences File")]
    public static string WEPrefsPath
    {
        get { return _WEPrefsPath; }
        set
        {
            if (!FileAccess.FileExists(value))
            {
                throw new ArgumentException("File does not exist");
            }
            _WEPrefsPath = value;
        }
    }

    const string SaveDataLocation = "user://";
    const string CustomTagsName = "custom_tags.json";
    const string WallpaperDataPrefix = "wallpapers/";
    const string WallpaperDataSuffix = ".json";
    const string BackupPrefix = "backups/";
    const string PrefsBackupName = "config.json";
    const string ViewSettingsName = "view_settings.json";
    const string ToolsConfigName = "config.ini";

    public static ToolsDataManager Instance;

    // Fill in a dictionary of the default values we've written above. Sucks because there's no way to store the default value in an attribute.
    private System.Collections.Generic.Dictionary<PropertyInfo, object> defaultValues = [];

    public override void _Ready()
    {
        SetSettingDefaults();
        Instance = this;
        LoadUserSettings();
    }

    private void SetSettingDefaults()
    {
        foreach (var property in typeof(ToolsDataManager).GetProperties())
        {
            var setting = property.GetCustomAttribute<UserSettingAttribute>();
            if (setting == null)
            {
                continue;
            }
            defaultValues.Add(property, property.GetValue(this));
        }
    }

    public static void SaveViewSettings(HashSet<Tag> filters, string sortMode)
    {
        Godot.Collections.Dictionary res = [];
        Godot.Collections.Dictionary<string, Array<string>> filterDict = [];
        foreach (var tag in filters)
        {
            if (filterDict.TryGetValue(tag.Category, out Array<string> value))
            {
                value.Add(tag.Name);
            } 
            else
            {
                filterDict.Add(tag.Category, [tag.Name]);
            }
        }
        res.Add("filters", filterDict);
        res.Add("sort_mode", sortMode);
        string json = Json.Stringify(res);
        using var file = FileAccess.Open(FullPath(ViewSettingsName), FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PrintErr($"Failed to open '{FullPath(ViewSettingsName)}': {FileAccess.GetOpenError()}");
            return;
        }
        file.StoreString(json);
    }

    public static HashSet<Tag> LoadFilter()
    {
        HashSet<Tag> res = [];

        if (!FileAccess.FileExists(FullPath(ViewSettingsName)))
        {
            return [];
        }

        try
        {
            using var file = FileAccess.Open(FullPath(ViewSettingsName), FileAccess.ModeFlags.Read);
            var result = Json.ParseString(file.GetAsText()).AsGodotDictionary().GetValueOrDefault("filters", new Dictionary()).AsGodotDictionary();
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
            GD.PrintErr("Failed to read settings file:" + e.Message);
            return [];
        }
    }

    public static string LoadSortMode()
    {
        try
        {
            using var file = FileAccess.Open(FullPath(ViewSettingsName), FileAccess.ModeFlags.Read);
            var result = Json.ParseString(file.GetAsText()).AsGodotDictionary().GetValueOrDefault("sort_mode", string.Empty).AsString();
            return result;
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to read settings file:" + e.Message);
            return string.Empty;
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
        var path = FullPath(WallpaperDataPrefix + wallpaperID + WallpaperDataSuffix);
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

        var path = FullPath(WallpaperDataPrefix + wallpaperID + WallpaperDataSuffix);

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

        var path = FullPath(BackupPrefix + date + "-" + PrefsBackupName);
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

    public static List<UserSetting> GetUserSettings()
    {
        List<UserSetting> settings = [];

        foreach (var property in typeof(ToolsDataManager).GetProperties())
        {
            var setting = property.GetCustomAttribute<UserSettingAttribute>();
            if (setting == null)
            {
                continue;
            }

            var propertyType = property.PropertyType;
            settings.Add(new UserSetting(setting.Name, Instance.defaultValues[property], propertyType, () => { return property.GetValue(Instance); }, (object val) => {
                if (val.GetType() != propertyType)
                {
                    GD.PrintErr($"Invalid type ({val.GetType()}) for '{setting.Name}', expected {propertyType}");
                    return;
                }
                property.SetValue(Instance, val);
                GD.Print($"Set {property.Name} to {val}");
            }
            ));
        }

        return settings;
    }

    public static void SaveUserSettings()
    {
        var config = new ConfigFile();
        foreach (var property in typeof(ToolsDataManager).GetProperties())
        {
            var setting = property.GetCustomAttribute<UserSettingAttribute>();
            if (setting == null)
            {
                continue;
            }

            var propertyType = property.PropertyType;
            var propertyVal = property.GetValue(Instance);
            if (propertyType != propertyVal.GetType())
            {
                GD.Print($"Invalid type ({propertyVal.GetType()}) for '{setting.Name}', expected {propertyType}");
                continue;
            }

            dynamic d = propertyVal; // Oh fuck

            config.SetValue("Settings", property.Name, Variant.CreateFrom(d));
        }

        config.Save(FullPath(ToolsConfigName));
    }

    public static void LoadUserSettings()
    {
        var config = new ConfigFile();

        Error err = config.Load(FullPath(ToolsConfigName));

        // If the file didn't load, ignore it.
        if (err != Error.Ok)
        {
            return;
        }

        const string section = "Settings";

        if (!config.HasSection(section))
        {
            GD.Print("Config was invalid");
            return;
        }

        foreach (var property in typeof(ToolsDataManager).GetProperties())
        {
            var setting = property.GetCustomAttribute<UserSettingAttribute>();
            if (setting == null)
            {
                continue;
            }

            if (!config.HasSectionKey(section, property.Name))
            {
                continue;
            }

            Variant loadedVal = config.GetValue(section, property.Name);
            var propertyType = property.PropertyType;
            var variantType = loadedVal.Obj.GetType();
            if (variantType != propertyType)
            {
                GD.Print($"Invalid type loaded ({variantType}) for '{setting.Name}', expected {propertyType}");
                continue;
            }

            property.SetValue(Instance, loadedVal.Obj);
        }
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public partial class UserSettingAttribute : Attribute
{
    public string Name { get; }
    public object DefaultValue { get; set; }

    public UserSettingAttribute(string humanReadableName)
    {
        Name = humanReadableName;
    }
}

public partial class UserSetting
{
    private Func<object> getter;
    private Action<object> setter;

    public string HumanReadableName { get; }
    public object Value { get { return getter(); } set { setter(value); } }
    public object DefaultValue { get; }
    public Type PropertyType { get; }

    public UserSetting(string name, object defaultValue, Type propertyType, Func<object> getter, Action<object> setter)
    {
        DefaultValue = defaultValue;
        HumanReadableName = name;
        PropertyType = propertyType;
        this.getter = getter;
        this.setter = setter;
    }
}