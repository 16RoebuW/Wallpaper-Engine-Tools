using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PlaylistCreator : Control
{
    [Export]
    Dictionary DefaultPlaylistSettings = new()
    {
        {"delay", 60 },
        {"mode", "timer" },
        {"order", "random" },
        {"transition", "0" },
        {"transitiontime", 1500 },
        {"updateonpause", false },
        {"videosequence", false }
    };

    [Export]
    MenuButton AddRuleButton;
    [Export]
    HBoxContainer WallpaperCarousel;
    [Export]
    SaveAsPopup SaveAsPopup;

    [Export]
    Label TagsLabel;

    [Export]
    VBoxContainer TagNameContainer;
    [Export]
    HBoxContainer QueryContainer;

    [Export]
    TabBar TabBar;

    [Export]
    PackedScene TagLabelScene;
    [Export]
    PackedScene FilterRuleScene;

    private int lastSelectedTab = 0;
    private int queryBoxOffset;
    // Because we need a fixed order. Using the dict will *probably* work, but this is safer
    private List<(string tag, Color colour)> tagsList = [];

    public override void _Ready()
    {
        AddRuleButton.GetPopup().IdPressed += OnAddRulePressed;
        foreach (var kv in Globals.GetTagsEnumerator())
        {
            var node = TagLabelScene.Instantiate<Control>();
            var label = node.GetNode<Label>("Label");
            label.Text = kv.Key;
            node.SelfModulate = kv.Value;
            var brightness = 0.2126 * kv.Value.R + 0.7152 * kv.Value.G + 0.0722 * kv.Value.B;
            if (brightness > 0.5)
            {
                label.AddThemeColorOverride("font_color", new Color(0,0,0));
            }
            TagNameContainer.AddChild(node);
            tagsList.Add((kv.Key, kv.Value));
        }
        TagNameContainer.ItemRectChanged += () =>
        {
            TagsLabel.CustomMinimumSize = new Vector2(TagNameContainer.Size.X, 0);
        };
        queryBoxOffset = QueryContainer.GetChildCount();
        SaveAsPopup.SaveButton.Pressed += OnSaveAsPopupPressed;
    }

    private void OnBackPressed()
    {
        Hide();
    }

    private void OnSaveAsPressed()
    {
        SaveAsPopup.Popup();
    }

    private void OnSaveAsPopupPressed()
    {
        string name = SaveAsPopup.LineEdit.Text;
        if (!ToolsDataManager.IsValidFilename(name))
        {
            GD.PrintErr("Invalid playlist name provided");
            return;
        }

        List<(RuleSymbol, HashSet<string>)> filterList = CollectFilter();

        try
        {
            var operations = ParseFilter(filterList);
            var wallpapers = FilterWallpapers(operations);
            if (wallpapers.Count == 0)
            {
                throw new Exception("Playlist was empty");
            }
            var playlist = GetPlaylistDict(name, wallpapers);
            ToolsDataManager.BackupPreferences();
            Godot.Collections.Array prefsPlaylists = Globals.LoadFromUntrustworthyDict(Globals.Preferences, [Globals.UserName, "general", "playlists"]).AsGodotArray();
            prefsPlaylists.Add(playlist);
            var json = Json.Stringify(Globals.Preferences, indent: "\t");
            using var file = FileAccess.Open(WELoader.GetPrefsPath(), FileAccess.ModeFlags.Write);
            file.StoreString(json);
            GD.Print("Saved to user config");
            SaveAsPopup.Hide();
        }
        catch (Exception ex)
        {
            GD.PrintErr(ex.Message);
            return;
        }
    }

    public Dictionary GetPlaylistDict(string name, List<WallpaperInfo> wallpapers)
    {
        Array<string> files = [];
        foreach (var wallpaper in wallpapers)
        {
            string playlistFile;
            if (wallpaper.WEData["type"].ToString() == "scene" && !FileAccess.FileExists(wallpaper.FullPath + "/scene.json"))
            {
                playlistFile = "scene.pkg"; // Strange edge case for workshop files
            } 
            else
            {
                playlistFile = wallpaper.WEData["file"].ToString();
            }
            string fileName = wallpaper.FullPath + "/" + playlistFile;
            files.Add(fileName);
        }
        return new Dictionary()
        {
            {"items", files},
            {"name", name},
            {"settings", DefaultPlaylistSettings }
        };
    }

    private List<WallpaperInfo> FilterWallpapers(List<Func<Stack<bool>, HashSet<string>, bool>> operations, int max = 0)
    {
        List<WallpaperInfo> filtered = [];
        foreach (var wallpaper in Globals.LoadedWallpapers)
        {
            Stack<bool> results = [];
            foreach (var operation in operations)
            {
                results.Push(operation.Invoke(results, wallpaper.Tags));
            }
            if (results.Count != 1)
            {
                throw new ArgumentException("Filter was invalid");
            }
            if (results.Pop())
            {
                filtered.Add(wallpaper);
            }
            if (max > 0 && filtered.Count == max)
            {
                break;
            }
        }
        return filtered;
    }

    public void OnAddRulePressed(long id)
    {
        switch (id)
        {
            case 0:
                AddRuleWithCheckboxes(RuleSymbol.Any, "ANY"); 
                break;
            case 1:
                AddRuleWithCheckboxes(RuleSymbol.All, "ALL");
                break;
            case 2:
                AddRule(RuleSymbol.Not, "NOT");
                break;
            case 3:
                AddRule(RuleSymbol.And, "AND");
                break;
            case 4:
                AddRule(RuleSymbol.Or, "OR");
                break;
            case 5:
                AddRule(RuleSymbol.OpenBracket, "(");
                break;
            case 6:
                AddRule(RuleSymbol.CloseBracket, ")");
                break;
            default:
                break;
        }

        RefreshPreview();
    }

    private void RefreshPreview()
    {
        List<(RuleSymbol, HashSet<string>)> filterList = CollectFilter();

        try
        {
            var operations = ParseFilter(filterList);
            var filtered = FilterWallpapers(operations, 3);
            foreach (var node in WallpaperCarousel.GetChildren())
            {
                node.QueueFree();
            }
            foreach (var wallpaper in filtered)
            {
                TextureRect preview = new();
                preview.Texture = Globals.LoadedWallpaperTextures[wallpaper.Title];
                preview.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
                preview.CustomMinimumSize = new Vector2(0, 256);
                WallpaperCarousel.AddChild(preview);
            }
        }
        catch (Exception ex)
        {
            GD.Print(ex.Message);
        }
    }

    private List<(RuleSymbol, HashSet<string>)> CollectFilter()
    {
        List<(RuleSymbol, HashSet<string>)> filterList = [];
        for (int i = queryBoxOffset; i < QueryContainer.GetChildCount(); i++)
        {
            FilterRule rule = QueryContainer.GetChild<FilterRule>(i);
            HashSet<string> tags = [];
            if (rule.Rule == RuleSymbol.Any || rule.Rule == RuleSymbol.All)
            {
                var tagIndices = rule.GetCheckedTagIndices();
                foreach (var tagIndex in tagIndices)
                {
                    tags.Add(tagsList[tagIndex].tag);
                }
            }
            filterList.Add((rule.Rule, tags));
        }

        return filterList;
    }

    public enum RuleSymbol
    {
        Any,
        All,
        // In order of precedence
        Not,
        And,
        Or,
        OpenBracket,
        CloseBracket
    }

    private void AddRuleWithCheckboxes(RuleSymbol symbol, string display)
    {
        TabBar.AddTab(display);

        FilterRule rule = FilterRuleScene.Instantiate<FilterRule>();
        rule.SetupRuleWithCheckboxes(symbol, TagNameContainer, TabBar.GetTabRect(TabBar.TabCount - 1).Size.X);
        rule.DeletePressed += () => OnDeletePressed(rule);
        rule.TagsStateChanged += RefreshPreview;
        QueryContainer.AddChild(rule);
    }

    private void AddRule(RuleSymbol symbol, string display)
    {
        TabBar.AddTab(display);

        FilterRule rule = FilterRuleScene.Instantiate<FilterRule>();
        rule.SetupRuleWithoutCheckboxes(symbol, TagNameContainer, TabBar.GetTabRect(TabBar.TabCount - 1).Size.X);
        rule.DeletePressed += () => OnDeletePressed(rule);
        QueryContainer.AddChild(rule);
    }

    private void OnDeletePressed(FilterRule source)
    {
        TabBar.RemoveTab(source.GetIndex() - 1);
        source.QueueFree();
    }

    private void OnTabSelected(int index)
    {
        lastSelectedTab = index;
    }

    private void OnActiveTabRearranged(int to)
    {
        var rule = QueryContainer.GetChild<Control>(lastSelectedTab + 1);
        rule.GetParent().MoveChild(rule, to + 1);
    }

    private List<Func<Stack<bool>, HashSet<string>, bool>> ParseFilter(List<(RuleSymbol, HashSet<string>)> symbols)
    {
        // https://en.wikipedia.org/wiki/Shunting_yard_algorithm#The_algorithm_in_detail

        Stack<(RuleSymbol op, Func<Stack<bool>, HashSet<string>, bool> fn)> operatorStack = [];
        List<Func<Stack<bool>, HashSet<string>, bool>> output = [];
        int resultSize = 0; // To check that the operations actually result in a final answer

        foreach (var (symbol, filter) in symbols)
        {
            // Functions with constant results, so I count them as literals
            if (symbol == RuleSymbol.Any || symbol == RuleSymbol.All)
            {
                var fn = GetFunction(symbol, filter);
                output.Add(fn);
                resultSize++;
            }
            // Operators
            else if (symbol == RuleSymbol.Not || symbol == RuleSymbol.And || symbol == RuleSymbol.Or)
            {
                var fn = GetFunction(symbol, filter);
                while (operatorStack.Count > 0 && operatorStack.Peek().op != RuleSymbol.OpenBracket && operatorStack.Peek().op >= symbol) {
                    output.Add(operatorStack.Pop().fn);
                }
                operatorStack.Push((symbol, fn));
                if (symbol != RuleSymbol.Not)
                {
                    resultSize--;
                }
            } 
            else if (symbol == RuleSymbol.OpenBracket)
            {
                operatorStack.Push((symbol, null));
            }
            else if (symbol == RuleSymbol.CloseBracket) 
            {
                /* If the stack runs out without finding a left parenthesis, then there are mismatched parentheses. */
                while (operatorStack.Peek().op != RuleSymbol.OpenBracket) { output.Add(operatorStack.Pop().fn); }
                operatorStack.Pop();
            }
        }
        while (operatorStack.Count > 0)
        {
            /* If the operator token on the top of the stack is a parenthesis, then there are mismatched parentheses. */
            if (operatorStack.Peek().op == RuleSymbol.CloseBracket || operatorStack.Peek().op == RuleSymbol.OpenBracket)
            {
                throw new ArgumentException("Unbalanced braces");
            }
            output.Add(operatorStack.Pop().fn);
        }
        if (resultSize != 1)
        {
            throw new ArgumentException("Operation does not produce a result");
        }

        return output;
    }

    private static Func<Stack<bool>, HashSet<string>, bool> GetFunction(RuleSymbol symbol, HashSet<string> filter)
    {
        return symbol switch
        {
            RuleSymbol.Any => (Stack<bool> stack, HashSet<string> tags) =>
                {
                    return filter.Intersect(tags).Any();
                }
            ,
            RuleSymbol.All => (Stack<bool> stack, HashSet<string> tags) =>
                {
                    return filter.Intersect(tags).Count() == filter.Count;
                }
            ,
            RuleSymbol.And => (Stack<bool> stack, HashSet<string> tags) =>
                {
                    return stack.Pop() & stack.Pop();
                }
            ,
            RuleSymbol.Or => (Stack<bool> stack, HashSet<string> tags) =>
                {
                    return stack.Pop() | stack.Pop();
                }
            ,
            RuleSymbol.Not => (Stack<bool> stack, HashSet<string> tags) =>
                {
                    return !stack.Pop();
                }
            ,
            _ => throw new ArgumentException("Input symbol was not a function"),
        };
    }
}
