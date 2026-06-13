using Godot;
using System;
using System.Collections.Generic;

public partial class FilterRule : Container
{
    [Export]
    PackedScene TagToggleScene;
    [Export]
    PackedScene AllButtonScene;
    [Export]
    PackedScene NoneButtonScene;
    [Export]
    PackedScene DeleteButtonScene;
    [Export]
    PackedScene ButtonSpacerScene;

    public PlaylistCreator.RuleSymbol Rule;

    [Signal]
    public delegate void DeletePressedEventHandler();
    [Signal]
    public delegate void TagsStateChangedEventHandler();

    bool[] checkedIndices;
    int checkboxStartIndex = 0;

    public void SetupRuleWithoutCheckboxes(PlaylistCreator.RuleSymbol symbol, VBoxContainer tagContainer, float width)
    {
        var spacer = ButtonSpacerScene.Instantiate<Control>();
        AddChild(spacer);
        var delButton = DeleteButtonScene.Instantiate<BaseButton>();
        delButton.Pressed += () => { EmitSignal(SignalName.DeletePressed); };
        AddChild(delButton);
        checkboxStartIndex = GetChildCount();

        Rule = symbol;
        CustomMinimumSize = new Vector2(width, 0);
    }

    public void SetupRuleWithCheckboxes(PlaylistCreator.RuleSymbol symbol, VBoxContainer tagContainer, float width)
    {
        var allButton = AllButtonScene.Instantiate<BaseButton>();
        allButton.Pressed += OnAllPressed;
        AddChild(allButton);
        var noneButton = NoneButtonScene.Instantiate<BaseButton>();
        noneButton.Pressed += OnNonePressed;
        AddChild(noneButton);
        var delButton = DeleteButtonScene.Instantiate<BaseButton>();
        delButton.Pressed += () => { EmitSignal(SignalName.DeletePressed); };
        AddChild(delButton);
        checkboxStartIndex = GetChildCount();

        const int tagStartIndex = 1;
        for (int i = tagStartIndex; i < tagContainer.GetChildCount(); i++)
        {
            var tagNameContainer = tagContainer.GetChild<Control>(i);

            var boxContainer = TagToggleScene.Instantiate<PanelContainer>();
            boxContainer.SelfModulate = tagNameContainer.SelfModulate;
            tagNameContainer.ItemRectChanged += () => boxContainer.CustomMinimumSize = new Vector2(0, tagNameContainer.Size.Y);
            var box = boxContainer.GetNode<CheckBox>("CheckBox");
            int boxIndex = i - tagStartIndex;
            box.Toggled += (toggledOn) => CheckBoxToggled(boxIndex, toggledOn);
            AddChild(boxContainer);
        }

        checkedIndices = new bool[tagContainer.GetChildCount() - tagStartIndex];

        Rule = symbol;
        CustomMinimumSize = new Vector2(width, 0);
    }

    private void OnAllPressed()
    {
        for (int i = 0; i < checkedIndices.Length; i++)
        {
            checkedIndices[i] = true;
            GetChild(i + checkboxStartIndex).GetNode<CheckBox>("CheckBox").SetPressedNoSignal(true);
        }
        EmitSignal(SignalName.TagsStateChanged);
    }

    private void OnNonePressed()
    {
        for (int i = 0; i < checkedIndices.Length; i++)
        {
            checkedIndices[i] = false;
            GetChild(i + checkboxStartIndex).GetNode<CheckBox>("CheckBox").SetPressedNoSignal(false);
        }
        EmitSignal(SignalName.TagsStateChanged);
    }

    private void CheckBoxToggled(int idx, bool toggledOn)
    {
        checkedIndices[idx] = toggledOn;
        EmitSignal(SignalName.TagsStateChanged);
    }

    public List<int> GetCheckedTagIndices()
    {
        List<int> res = [];
        for (int i = 0; i < checkedIndices.Length; i++)
        {
            if (checkedIndices[i])
            {
                res.Add(i);
            }
        }
        return res;
    }
}
