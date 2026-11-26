using Godot;

namespace CrossyRoadBase;

/// <summary>The high score screen: the top 10 runs with the names people entered.</summary>
public partial class ScoreBoard : Node
{
    static readonly Color Gold = new("#ffd54f"), Silver = new("#cfd8dc"), Bronze = new("#d99058"), Dim = new("#6d7b8a");

    public override void _Ready()
    {
        long highlight = HighScores.LastAddedId;   // the run you just saved lights up
        HighScores.LastAddedId = -1;

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

        var title = new Label { Text = "HIGH SCORES", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 56);
        title.AddThemeColorOverride("font_color", Gold);
        column.AddChild(title);

        // the board
        var style = new StyleBoxFlat { BgColor = new Color(0.1f, 0.14f, 0.2f, 1f), BorderColor = new Color("#3a4a5c") };
        style.SetBorderWidthAll(4);
        style.SetCornerRadiusAll(16);
        style.SetContentMarginAll(22);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", style);
        column.AddChild(panel);

        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 6);
        panel.AddChild(grid);

        grid.AddChild(Cell("#", 50, HorizontalAlignment.Center, Dim, 20));
        grid.AddChild(Cell("NAME", 250, HorizontalAlignment.Left, Dim, 20));
        grid.AddChild(Cell("SCORE", 100, HorizontalAlignment.Right, Dim, 20));
        grid.AddChild(Cell("PLACE", 230, HorizontalAlignment.Left, Dim, 20));
        grid.AddChild(Cell("PLAYED AS", 130, HorizontalAlignment.Left, Dim, 20));

        var top = HighScores.Top();
        for (int i = 0; i < HighScores.ShownCount; i++)
        {
            if (i < top.Count)
            {
                var e = top[i];
                bool mine = e.Id == highlight;
                Color rank = i == 0 ? Gold : i == 1 ? Silver : i == 2 ? Bronze : Colors.White;
                Color text = mine ? Gold : Colors.White;

                grid.AddChild(Cell((i + 1).ToString(), 50, HorizontalAlignment.Center, rank));
                grid.AddChild(Cell(mine ? e.Name + "  <" : e.Name, 250, HorizontalAlignment.Left, text));
                grid.AddChild(Cell(e.Score.ToString(), 100, HorizontalAlignment.Right, text));
                grid.AddChild(Cell(e.Place, 230, HorizontalAlignment.Left, mine ? Gold : new Color("#b0bec5")));
                grid.AddChild(Cell(e.Character, 130, HorizontalAlignment.Left, mine ? Gold : new Color("#b0bec5")));
            }
            else
            {
                grid.AddChild(Cell((i + 1).ToString(), 50, HorizontalAlignment.Center, Dim));
                grid.AddChild(Cell("- - -", 250, HorizontalAlignment.Left, Dim));
                grid.AddChild(Cell("", 100, HorizontalAlignment.Right, Dim));
                grid.AddChild(Cell("", 230, HorizontalAlignment.Left, Dim));
                grid.AddChild(Cell("", 130, HorizontalAlignment.Left, Dim));
            }
        }

        // buttons
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 24);
        column.AddChild(buttons);
        buttons.AddChild(MakeButton("PLAY", () => GetTree().ChangeSceneToFile("res://Main.tscn")));
        buttons.AddChild(MakeButton("MENU", () => GetTree().ChangeSceneToFile("res://Select.tscn")));

        var hint = new Label
        {
            Text = "Enter to play   -   Esc for the menu",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.AddThemeFontSizeOverride("font_size", 18);
        hint.Modulate = new Color(1, 1, 1, 0.6f);
        column.AddChild(hint);
    }

    static Label Cell(string text, int width, HorizontalAlignment align, Color color, int size = 26)
    {
        var label = new Label { Text = text, HorizontalAlignment = align, CustomMinimumSize = new Vector2(width, 0) };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    static Button MakeButton(string text, System.Action onPressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(200, 54), FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 26);
        button.Pressed += onPressed;
        return button;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;

        switch (k.Keycode)
        {
            case Key.Enter: case Key.Space:
                GetTree().ChangeSceneToFile("res://Main.tscn");
                break;
            case Key.Escape: case Key.M: case Key.Backspace:
                GetTree().ChangeSceneToFile("res://Select.tscn");
                break;
        }
    }
}
