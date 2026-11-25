using Godot;

namespace CrossyRoadBase;

/// <summary>Character select screen: choose the boy or the girl USC student, then the game starts.</summary>
public partial class Select : Node
{
    int highlighted;                       // 0 = boy, 1 = girl
    readonly StyleBoxFlat[] cardStyles = new StyleBoxFlat[2];

    public override void _Ready()
    {
        highlighted = Player.Gender == PlayerGender.Boy ? 0 : 1;

        var layer = new CanvasLayer();
        AddChild(layer);

        var bg = new ColorRect { Color = new Color("#1b2a38") };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        center.AddChild(column);

        var title = new Label { Text = "CROSSY CEBU", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 60);
        column.AddChild(title);

        var subtitle = new Label { Text = "Choose your student", HorizontalAlignment = HorizontalAlignment.Center };
        subtitle.AddThemeFontSizeOverride("font_size", 28);
        subtitle.AddThemeColorOverride("font_color", new Color("#ffe082"));
        column.AddChild(subtitle);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 40);
        column.AddChild(row);
        row.AddChild(MakeCard(0, "BOY", "res://Assets/Drafts/boy_draft.png"));
        row.AddChild(MakeCard(1, "GIRL", "res://Assets/Drafts/girl_draft.png"));

        var hint = new Label
        {
            Text = "A / D or arrow keys to choose   -   Enter to play   -   press C in game to change",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.AddThemeFontSizeOverride("font_size", 20);
        hint.Modulate = new Color(1, 1, 1, 0.7f);
        column.AddChild(hint);

        Refresh();
    }

    Control MakeCard(int index, string label, string imagePath)
    {
        var style = new StyleBoxFlat { BgColor = new Color(0.1f, 0.14f, 0.2f, 1f) };
        style.SetBorderWidthAll(5);
        style.SetCornerRadiusAll(16);
        style.SetContentMarginAll(16);
        cardStyles[index] = style;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", style);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        card.AddChild(box);

        Texture2D tex = ResourceLoader.Exists(imagePath) ? GD.Load<Texture2D>(imagePath) : null;
        box.AddChild(new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(300, 300)
        });

        var button = new Button { Text = label, CustomMinimumSize = new Vector2(0, 52), FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 28);
        button.Pressed += () => Choose(index);
        button.MouseEntered += () => { highlighted = index; Refresh(); };
        box.AddChild(button);

        return card;
    }

    void Refresh()
    {
        for (int i = 0; i < 2; i++)
            cardStyles[i].BorderColor = i == highlighted ? new Color("#ffd54f") : new Color("#3a4a5c");
    }

    void Choose(int index)
    {
        Player.Gender = index == 0 ? PlayerGender.Boy : PlayerGender.Girl;
        GetTree().ChangeSceneToFile("res://Main.tscn");
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;

        switch (k.Keycode)
        {
            case Key.A: case Key.Left: highlighted = 0; Refresh(); break;
            case Key.D: case Key.Right: highlighted = 1; Refresh(); break;
            case Key.B: case Key.Key1: Choose(0); break;
            case Key.G: case Key.Key2: Choose(1); break;
            case Key.Enter: case Key.Space: Choose(highlighted); break;
        }
    }
}
