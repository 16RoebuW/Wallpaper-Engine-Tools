using Godot;
using System;

public partial class DebouncedLineEdit : LineEdit
{
    [Export]
    float debounceTime = 0.3f;

    [Signal]
    public delegate void DebouncedTextChangedEventHandler(string newText);

    private Timer debouncer;

    public override void _Ready()
    {
        debouncer = new Timer
        {
            OneShot = true,
            WaitTime = debounceTime
        };
        debouncer.Timeout += TimerTimeout;
        AddChild(debouncer);
    }

    private void LineEditTextChanged(string newText)
    {
        debouncer.Start();
    }

    private void LineEditTextSubmitted(string newText)
    {
        EmitSignal(SignalName.DebouncedTextChanged, newText);
    }

    private void TimerTimeout()
    {
        EmitSignal(SignalName.DebouncedTextChanged, Text);
    }
}
