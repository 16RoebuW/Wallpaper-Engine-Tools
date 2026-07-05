using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dictionary = Godot.Collections.Dictionary;

public partial class UiController : Node
{
    [Export]
    PackedScene TagOptionScene;
    [Export]
    Container ItemTagContainer;
    [Export]
    LineEdit TagSearchBox;
    [Export]
    Label DetailsTitle;
    [Export]
    TextureRect DetailsPreview;
    [Export]
    Theme MainTheme;
    [Export]
    Container WallpaperContainer;
    [Export]
    Control PlaylistCreator;
    [Export]
    Container FilterTagContainer;
    [Export]
    PackedScene FoldableTagContainerScene;
    [Export]
    Texture2D AmbiguousCheckBoxEnabled;
    [Export]
    Texture2D AmbiguousCheckBoxDisabled;

    private List<WallpaperPreview> selectedWallpapers = [];

    private List<TagButton> detailTags = [];
    private List<TagButton> filterTags = [];

    private HashSet<TagButton> filteredDetailTags = [];
    
    private System.Collections.Generic.Dictionary<string, HashSet<string>> filter = [];

    private string oldText = "longer_than_blank";

    private readonly string defaultTagType = "normal";

    private Button fakeFocussedNode;

    public override void _Ready()
    {
        var prefs = Globals.Preferences[Globals.UserName].AsGodotDictionary();
        if (prefs is null)
        {
            GD.PrintErr("Failed to load preferences!!");
            return;
        }

        LoadBuiltinTags(prefs);

        foreach (var kv in Globals.GetBuiltinTags())
        {
            NewDetailsTagNode(kv.Key, true, kv.Value, "Built in");
            NewFilterTagNode(kv.Key, kv.Value, "Built in");
        }

        LoadCustomTags();

        foreach (var kv in Globals.GetCustomTags())
        {
            NewDetailsTagNode(kv.Key, colour: kv.Value, category: "Custom");
            NewFilterTagNode(kv.Key, kv.Value, "Custom");
        }

        LoadSpecialTags(prefs);

        foreach (string tagType in Globals.SupportedSpecialTags)
        {
            foreach (var kv in Globals.GetSpecialTags(tagType))
            {
                NewFilterTagNode(kv.Key, kv.Value, "Meta", tagType);
            }
        }

        TagSearchBox.Editable = false;
    }

    private void FilterWallpaperPreviews()
    {
        for (int i = 0; i < WallpaperContainer.GetChildCount(); i++)
        {
            var wallpaper = WallpaperContainer.GetChildOrNull<WallpaperPreview>(i);
            if (wallpaper is null)
            {
                return;
            }
            wallpaper.Show();
            foreach (var tagType in filter)
            {
                if (!WallpaperMatchesTags(wallpaper.Info, tagType))
                {
                    wallpaper.Hide();
                    break;
                }
            }
        }
    }

    private bool WallpaperMatchesTags(WallpaperInfo wallpaper, KeyValuePair<string, HashSet<string>> tagType)
    {
        if (tagType.Key == defaultTagType)
        {
            foreach (var tag in tagType.Value)
            {
                if (!wallpaper.Tags.Contains(tag))
                {
                    return false;
                }
            }
        }
        else
        {
            foreach (var tag in tagType.Value)
            {
                if (!Globals.HasSpecialTag(wallpaper, tagType.Key, tag))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void OnFilterClearedPressed()
    {
        foreach (var kv in filter)
        {
            kv.Value.Clear();
        }
        foreach (var btn in filterTags)
        {
            btn.CheckBox.SetPressedNoSignal(false);
        }
        FilterWallpaperPreviews();
    }

    private void OnCreatePlaylistPressed()
    {
        PlaylistCreator.Show();
    }

    private void LoadCustomTags()
    {
        Globals.SetCustomTags(ToolsDataManager.LoadCustomTags());
    }

    public override void _Process(double delta)
    {
        if (TagSearchBox.HasFocus() && fakeFocussedNode is not null)
        {
            HandleDetailsTagFilterInput();
        }
    }

    public void OnDetailsTagToggled(TagButton detailsTag, bool tagged)
    {
        if (selectedWallpapers is null)
        {
            return;
        }
        foreach (var wallpaper in selectedWallpapers)
        {
            SetItemTag(wallpaper.Info, detailsTag.Name, tagged);
        }
    }

    public void OnAddTagPressed()
    {
        if (TagSearchBox.Text.Length > 0)
        {
            string formatted = TagSearchBox.Text;
            if (TagSearchBox.Text.ToLower() == TagSearchBox.Text)
            {
                formatted = TagSearchBox.Text[..1].ToUpper() + TagSearchBox.Text[1..];
            }
            var node = AddCustomTag(formatted, ActiveWallpaper().Info.GetSchemeColor());
            foreach (var wallpaper in selectedWallpapers) { 
                SetItemTag(wallpaper.Info, formatted, true);
            }
            filteredDetailTags.Add(node);
        }
    }

    private TagButton AddCustomTag(string tag, Color? colour = null)
    {
        var tagColour = colour ?? new Color(1, 1, 1);
        Globals.AddCustomTag(tag, tagColour);
        ToolsDataManager.SaveCustomTags(Globals.GetCustomTags());
        NewFilterTagNode(tag, tagColour, category: "Custom");
        return NewDetailsTagNode(tag, colour: tagColour, category: "Custom");
    }

    private void RemoveCustomTag(string tag)
    {
        Globals.RemoveCustomTag(tag);
        ToolsDataManager.SaveCustomTags(Globals.GetCustomTags());
        foreach (Node node in WallpaperContainer.GetChildren())
        {
            if (node is WallpaperPreview preview)
            {
                SetItemTag(preview.Info, tag, false);
                ToolsDataManager.SaveCustomData(preview.Info.Identifier, GetCustomWallpaperData(preview.Info));
            }
        }
        RemoveTagFromDetailsPane(tag);
    }

    private Dictionary GetCustomWallpaperData(WallpaperInfo info)
    {
        Dictionary data = [];
        Array<string> tags = [];
        var customTags = Globals.GetCustomTags();
        foreach (var tag in info.Tags)
        {
            if (customTags.ContainsKey(tag))
            {
                tags.Add(tag);
            }
        }
        data.Add("tags", tags);
        return data;
    }

    private void HandleDetailsTagFilterInput()
    {
        // Order matters here because shift+tab (focus prev) means that tab is pressed as well (focus next)
        if (Input.IsActionJustPressed("ui_up") || Input.IsActionJustPressed("ui_focus_prev"))
        {
            var prev = GetPreviousVisibleTag(fakeFocussedNode.GetParent().GetIndex());
            if (prev is not null)
            {
                FakeFocus(prev.CheckBox);
            }
        }
        else if (Input.IsActionJustPressed("ui_down") || Input.IsActionJustPressed("ui_focus_next")) 
        {
            var next = GetNextVisibleTag(fakeFocussedNode.GetParent().GetIndex());
            if (next is not null)
            {
                FakeFocus(next.CheckBox);
            }
        }
        else if (Input.IsActionJustPressed("ui_accept") && fakeFocussedNode.Disabled == false)
        {
            foreach (var wallpaper in selectedWallpapers)
            {
                SetItemTag(wallpaper.Info, fakeFocussedNode.GetParent().Name, !fakeFocussedNode.ButtonPressed);
            }
        }
    }

    private void SetItemTag(WallpaperInfo item, string tag, bool tagged)
    {
        if (tagged)
        {
            item.Tags.Add(tag);
        } 
        else
        {
            item.Tags.Remove(tag);
        }

        ToolsDataManager.SaveCustomData(item.Identifier, GetCustomWallpaperData(item));

        if (ActiveWallpaper() is not null && ActiveWallpaper().Info == item)
        {
            SetDetailsTagsState([tag], tagged);
        }
    }

    private void FakeFocus(Button node)
    {
        fakeFocussedNode?.RemoveThemeStyleboxOverride("disabled");
        fakeFocussedNode = node;
        var focusBox = MainTheme.GetStylebox("CheckBox", "focus");
        fakeFocussedNode.AddThemeStyleboxOverride("normal", focusBox);
        fakeFocussedNode.AddThemeStyleboxOverride("disabled", focusBox);
        fakeFocussedNode.AddThemeStyleboxOverride("pressed", focusBox);
    }

    private void LoadBuiltinTags(Dictionary prefs)
    {
        try
        {
            var current = Globals.LoadFromUntrustworthyDict(prefs, ["general", "browser", "filterinfo", "installed", "tags"]);
            Godot.Collections.Dictionary<string, Color> tags = [];

            foreach (var tag in current.AsGodotDictionary().Keys)
            {
                tags.Add(tag.AsString(), new Color(1,1,1));
            }
            Globals.SetBuiltinTags(tags);
        }
        catch (KeyNotFoundException e)
        {
            GD.PrintErr($"Failed to get tags: {e.Message}");
            return;
        }
    }

    private void LoadSpecialTags(Dictionary prefs)
    {
        // Built in special tags
        foreach (string tagType in Globals.SupportedSpecialTags)
        {
            try
            {
                Godot.Collections.Dictionary<string, Color> tags = [];
                var current = Globals.LoadFromUntrustworthyDict(prefs, ["general", "browser", "filterinfo", "installed", tagType]);
                foreach (var tag in current.AsGodotDictionary().Keys)
                {
                    tags.Add(tag.AsString(), new Color(1, 1, 1));
                }
                Globals.SetSpecialTags(tagType, tags);
            }
            catch (KeyNotFoundException)
            {
                continue;
            }
        }

        // Custom special tags
        Globals.SetSpecialTags("thumbnail", new() { {"Thumbnail", new Color(1,1,1)}, { "No thumbnail", new Color(1, 1, 1) } });

        string[] colours = ["Red", "Orange", "Yellow", "LimeGreen", "DodgerBlue", "Purple", "HotPink", "White", "Black"];
        Godot.Collections.Dictionary<string, Color> colourTags = [];
        foreach (string colour in colours)
        {
            colourTags.Add(colour, new Color(colour));
        }
        Globals.SetSpecialTags("colour", colourTags);
    }

    private TagButton NewDetailsTagNode(string tag, bool disabled=false, Color? colour = null, string category = null)
    {
        TagButton node = TagOptionScene.Instantiate<TagButton>();
        node.CheckBox.Text = tag;
        node.CheckBox.Disabled = disabled;
        node.Name = tag;
        if (colour is Color notNull)
        {
            node.ColorPicker.Color = notNull;
            node.Icon.SelfModulate = notNull;
        }
        node.CheckBox.Toggled += (toggledOn) => OnDetailsTagToggled(node, toggledOn);
        if (category != null)
        {
            if (ItemTagContainer.HasNode(category))
            {
                ItemTagContainer.GetNode(category).GetChild(0).AddChild(node);
            } else {
                var newCategory = FoldableTagContainerScene.Instantiate<FoldableContainer>();
                newCategory.Title = category;
                newCategory.Name = category;
                ItemTagContainer.AddChild(newCategory);
                newCategory.GetChild(0).AddChild(node);
            }
        } else {
            ItemTagContainer.AddChild(node);
        }
        detailTags.Add(node);
        return node;
    }

    private TagButton NewFilterTagNode(string tag, Color? colour = null, string category = null, string specialType = null)
    {
        TagButton node = TagOptionScene.Instantiate<TagButton>();
        node.CheckBox.Text = tag;
        node.Name = tag;
        if (specialType == null)
        {
            node.CheckBox.Toggled += (toggledOn) => OnFilterTagToggled(tag, toggledOn);
        } else {
            node.CheckBox.Toggled += (toggledOn) => OnSpecialFilterTagToggled(specialType, tag, toggledOn);
        }
        if (colour is Color notNull)
        {
            node.ColorPicker.Color = notNull;
            node.Icon.SelfModulate = notNull;
        }
        node.ColorPicker.Hide();
        if (category != null)
        {
            if (FilterTagContainer.HasNode(category))
            {
                FilterTagContainer.GetNode(category).GetChild(0).AddChild(node);
            }
            else
            {
                var newCategory = FoldableTagContainerScene.Instantiate<FoldableContainer>();
                newCategory.Title = category;
                newCategory.Name = category;
                FilterTagContainer.AddChild(newCategory);
                newCategory.GetChild(0).AddChild(node);
            }
        }
        else
        {
            FilterTagContainer.AddChild(node);
        }
        filterTags.Add(node);
        return node;
    }

    private void OnSpecialFilterTagToggled(string specialType, string tag, bool toggledOn)
    {
        if (toggledOn)
        {
            if (!filter.TryGetValue(specialType, out HashSet<string> value))
            {
                filter.Add(specialType, [tag]);
            }
            else
            {
                value.Add(tag);
            }
        }
        else
        {
            filter[specialType].Remove(tag);
        }
        FilterWallpaperPreviews();
    }

    private void OnFilterTagToggled(string tag, bool toggledOn)
    {
        if (toggledOn)
        {
            if (!filter.TryGetValue(defaultTagType, out HashSet<string> value))
            {
                filter.Add(defaultTagType, [tag]);
            } else {
                value.Add(tag);
            }
        } else
        {
            filter[defaultTagType].Remove(tag);
        }
        FilterWallpaperPreviews();
    }

    private void RemoveTagFromDetailsPane(string tag)
    {
        foreach (TagButton node in detailTags)
        {
            if (node.Name == tag)
            {
                node.QueueFree();
                return;
            }
        }
    }

    public void OnWallpaperLoaded(WallpaperPreview wallpaper)
    {
        wallpaper.Toggled += (toggledOn) => OnWallpaperSelected(wallpaper, toggledOn);
    }

    private void OnWallpaperSelected(WallpaperPreview wallpaper, bool toggledOn)
    {
        TagSearchBox.Editable = true;

        if (Input.IsActionPressed("multi_select_modifier")) 
        {
            if (!toggledOn)
            {
                selectedWallpapers.Remove(wallpaper);
                return;
            }
        } 
        else
        {
            while (selectedWallpapers.Count > 0)
            {
                selectedWallpapers[0].SetPressedNoSignal(false);
                selectedWallpapers.Remove(selectedWallpapers[0]);
            }
        }

        selectedWallpapers.Add(wallpaper);

        DetailsTitle.Text = ActiveWallpaper().Info.Title;
        DetailsPreview.Texture = ActiveWallpaper().PreviewImage;

        SetCombinedDetailsTagsState(selectedWallpapers);
    }

    private WallpaperPreview ActiveWallpaper()
    {
        if (selectedWallpapers.Count == 0)
        {
            return null;
        }
        return selectedWallpapers[^1];
    }

    private void SetCombinedDetailsTagsState(List<WallpaperPreview> wallpapers)
    {
        if (wallpapers.Count == 0) { return; }

        Godot.Collections.Dictionary<string, int> tagCounts = [];

        foreach (var wallpaper in wallpapers)
        {
            foreach (var tag in wallpaper.Info.Tags)
            {
                if (tagCounts.TryGetValue(tag, out int val))
                {
                    tagCounts[tag] = val + 1;
                } 
                else
                {
                    tagCounts.Add(tag, 1);
                }
            }
        }


        foreach (var tagButton in detailTags)
        {
            tagButton.CheckBox.RemoveThemeIconOverride("checked");
            tagButton.CheckBox.RemoveThemeIconOverride("checked_disabled");
            if (tagCounts.TryGetValue(tagButton.Name, out int val))
            {
                tagButton.CheckBox.SetPressedNoSignal(true);
                if (val != wallpapers.Count)
                {
                    tagButton.CheckBox.AddThemeIconOverride("checked", AmbiguousCheckBoxEnabled);
                    tagButton.CheckBox.AddThemeIconOverride("checked_disabled", AmbiguousCheckBoxDisabled);
                }
            } 
            else
            {
                tagButton.CheckBox.SetPressedNoSignal(false);
            }
        }
    }

    private void SetDetailsTagsState(HashSet<string> tags, bool pressed)
    {
        foreach (string tag in tags)
        {
            bool found = false;
            foreach (var tagButton in detailTags)
            {
                if (tagButton.Name == tag)
                {
                    found = true;
                    tagButton.CheckBox.SetPressedNoSignal(pressed);
                    break;
                }
            }

            if (!found)
            {
                GD.PrintErr($"Failed to get node for tag {tag}");
                continue;
            }
        }
    }

    public void OnTagSearchTextChanged(string newText)
    {
        if (newText.Length < oldText.Length)
        {
            filteredDetailTags.Clear();
            foreach (TagButton tag in detailTags)
            {
                if (tag.Name.ToString().Contains(newText, StringComparison.InvariantCultureIgnoreCase))
                {
                    filteredDetailTags.Add(tag);
                    tag.Visible = true;
                }
                else
                {
                    tag.Visible = false;
                }
            }
        }
        else
        {
            foreach (TagButton tag in filteredDetailTags)
            {
                if (tag.Name.ToString().Contains(newText, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }
                filteredDetailTags.Remove(tag);
                tag.Visible = false;
            }
        }

        var topTag = GetNextVisibleTag(-1);
        if (topTag is not null)
        {
            FakeFocus(topTag.CheckBox);
        }

        oldText = newText;
    }

    private TagButton GetNextVisibleTag(int from)
    {
        for (int i = from + 1; i < detailTags.Count; i++)
        {
            var tag = detailTags[i];
            if (tag.Visible == true)
            {
                return tag;
            }
        }
        // Loop round
        for (int i = 0; i <= from; i++)
        {
            var tag = detailTags[i];
            if (tag.Visible == true)
            {
                return tag;
            }
        }
        return null;
    }
    
    private TagButton GetPreviousVisibleTag(int from)
    {
        for (int i = from - 1; i >= 0; i--)
        {
            var tag = detailTags[i];
            if (tag.Visible == true)
            {
                return tag;
            }
        }
        // Loop round
        for (int i = detailTags.Count - 1; i >= from; i--)
        {
            var tag = detailTags[i];
            if (tag.Visible == true)
            {
                return tag;
            }
        }
        return null;
    }
}
