using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
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
    
    private HashSet<Tag> filter = [];

    private string oldText = "longer_than_blank";

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

        foreach (var tag in Globals.GetBuiltinTags())
        {
            NewDetailsTagNode(tag, true, "Built in");
            NewFilterTagNode(tag, "Built in");
        }

        LoadCustomTags();

        foreach (var tag in Globals.GetCustomTags())
        {
            NewDetailsTagNode(tag, category: "Custom");
            NewFilterTagNode(tag, "Custom");
        }

        LoadSpecialTags(prefs);

        foreach (var tag in Globals.GetSpecialTags())
        {
            NewFilterTagNode(tag, "Meta");
        }

        TagSearchBox.Editable = false;

        filter = ToolsDataManager.LoadFilter();
        foreach (var tag in filter)
        {
            foreach (var tagNode in filterTags)
            {
                if (tagNode.Name == tag.Name)
                {
                    tagNode.CheckBox.SetPressedNoSignal(true);
                }
            }
        }
        FilterWallpaperPreviews();
    }

    public override void _ExitTree()
    {
        ToolsDataManager.SaveFilter(filter);
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
            if (!WallpaperMatchesTags(wallpaper.Info, filter))
            {
                wallpaper.Hide();
            }
        }
    }

    private bool WallpaperMatchesTags(WallpaperInfo wallpaper, HashSet<Tag> tags)
    {
        return tags.Intersect(wallpaper.Tags).Count() == tags.Count;
    }

    private void OnFilterClearedPressed()
    {
        filter.Clear();
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

    private TagButton AddCustomTag(string tagName, Color? colour = null)
    {
        var tag = new Tag(tagName, colour: colour);
        Globals.AddCustomTag(tag);
        ToolsDataManager.SaveCustomTags(Globals.GetCustomTags());
        NewFilterTagNode(tag, "Custom");
        return NewDetailsTagNode(tag, category: "Custom");
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
        foreach (var tag in info.GetStringTags())
        {
            if (customTags.Any(c => tag.Equals(c.Name)))
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
            item.AddTag(tag);
        } 
        else
        {
            item.RemoveTag(tag);
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
            HashSet<Tag> tags = [];

            foreach (var tag in current.AsGodotDictionary().Keys)
            {
                tags.Add(new Tag(tag.AsString()));
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
        HashSet<Tag> tags = [];
        foreach (string tagType in Globals.SupportedSpecialTags)
        {
            try
            {
                var current = Globals.LoadFromUntrustworthyDict(prefs, ["general", "browser", "filterinfo", "installed", tagType]);
                foreach (var tag in current.AsGodotDictionary().Keys)
                {
                    tags.Add(new Tag(tag.AsString(), tagType));
                }
            }
            catch (KeyNotFoundException)
            {
                continue;
            }
        }

        // Custom special tags
        tags.UnionWith([new Tag("Thumbnail", "thumbnail"), new Tag("No thumbnail", "thumbnail")]);

        string[] colours = ["Red", "Orange", "Yellow", "LimeGreen", "DodgerBlue", "Purple", "HotPink", "White", "Black"];
        foreach (string colour in colours)
        {
            tags.Add(new Tag(colour, "colour", new Color(colour)));
        }

        Globals.SetSpecialTags(tags);
    }

    private TagButton NewDetailsTagNode(Tag tag, bool disabled=false, string category = null)
    {
        TagButton node = TagOptionScene.Instantiate<TagButton>();
        node.CheckBox.Text = tag.Name;
        node.CheckBox.Disabled = disabled;
        node.Name = tag.Name;
        node.ColorPicker.Color = tag.Colour;
        node.Icon.SelfModulate = tag.Colour;
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

    private TagButton NewFilterTagNode(Tag tag, string category = null)
    {
        TagButton node = TagOptionScene.Instantiate<TagButton>();
        node.CheckBox.Text = tag.Name;
        node.Name = tag.Name;
        node.CheckBox.Toggled += (toggledOn) => OnFilterTagToggled(tag, toggledOn);
        node.ColorPicker.Color = tag.Colour;
        node.Icon.SelfModulate = tag.Colour;
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

    private void OnFilterTagToggled(Tag tag, bool toggledOn)
    {
        if (toggledOn)
        {
            filter.Add(tag);
        } else
        {
            filter.Remove(tag);
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

        System.Collections.Generic.Dictionary<string, int> tagCounts = [];

        foreach (var wallpaper in wallpapers)
        {
            foreach (var tag in wallpaper.Info.GetStringTags())
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
