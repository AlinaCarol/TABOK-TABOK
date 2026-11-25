using Godot;
using System;

namespace CrossyRoadBase;

/// <summary>
/// The street vendor: salakot hat, bamboo pole over the shoulder. He sells taho by default, chicharon in
/// Carcar and buko juice in the beach towns. He sprints back and forth along his sidewalk shouting in a
/// speech bubble, is solid (you can't hop into him), and you can talk to him with E.
/// </summary>
public partial class TahoMan : Node3D
{
    const float RunSpeed = 4.8f;
    public const float TalkRange = 2.4f;
    static readonly Random Rng = new();
    static ImageTexture bubbleTexture;

    public int Row;
    public bool Served;                 // he only hands over his item once

    Func<Node3D> getPlayer;
    string vendor;
    float targetX, pause, shoutTimer, shoutShow, time, noticeCooldown;
    Node3D model, legL, legR, armL, armR, load, bubble;
    Label3D shoutLabel;

    // ---------- what he says depends on what he sells ----------

    string[] Cries => vendor switch
    {
        "chicharon" => new[] { "CHICHARON!", "CHICHARONNN!", "CHICHARON! CHICHARON!" },
        "buko" => new[] { "BUKO!", "BUKO JUICE!", "BUKOOO!" },
        _ => new[] { "TAHO!", "TAHOOO!", "TAHO! TAHO!" }
    };

    string OfferLine => vendor switch
    {
        "chicharon" => "Chicharon!\nWant some?",
        "buko" => "Buko juice!\nCold and fresh!",
        _ => "Taho!\nWant a cup?"
    };

    string GiveLine => vendor switch
    {
        "chicharon" => "Here you go!\nSo crunchy!",
        "buko" => "Here you go!\nSalamat!",
        _ => "Here you go!\nFresh taho!"
    };

    const string EmptyLine = "Ubos na!\nSold out!";

    public void Init(int row, Func<Node3D> playerLookup)
    {
        Row = row;
        getPlayer = playerLookup;
        vendor = City.Current.Vendor;
        Position = new Vector3(Rng.Next(-6, 7), 0, -row);
        PickTarget();
        shoutTimer = 0.5f + (float)Rng.NextDouble() * 1.5f;
        BuildModel();
    }

    static Node3D Limb(Node3D root, Vector3 pivotPos, Vector3 size, Color c)
    {
        var pivot = new Node3D { Position = pivotPos };
        pivot.AddChild(Util.Box(size, c, new Vector3(0, -size.Y / 2, 0)));
        root.AddChild(pivot);
        return pivot;
    }

    // ---------- speech bubble texture, drawn once in code ----------

    static bool InRoundRect(float px, float py, float x0, float y0, float w, float h, float r)
    {
        float cx = Mathf.Clamp(px, x0 + r, x0 + w - r);
        float cy = Mathf.Clamp(py, y0 + r, y0 + h - r);
        float dx = px - cx, dy = py - cy;
        return dx * dx + dy * dy <= r * r;
    }

    static ImageTexture GetBubbleTexture()
    {
        if (bubbleTexture != null) return bubbleTexture;

        const int W = 320, H = 150, BodyH = 110, R = 30, B = 6;
        var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                bool outer = InRoundRect(px, py, 0, 0, W, BodyH, R);
                bool inner = InRoundRect(px, py, B, B, W - 2 * B, BodyH - 2 * B, R - B);
                float hw = 24f * (H - py) / (H - (BodyH - 8));                       // tail narrows to a point
                bool tailOuter = py >= BodyH - 8 && Math.Abs(px - W / 2f) <= hw;
                bool tailInner = py >= BodyH - B - 2 && Math.Abs(px - W / 2f) <= hw - 8f;

                Color c = (inner || tailInner) ? Colors.White
                        : (outer || tailOuter) ? Colors.Black
                        : new Color(0, 0, 0, 0);
                img.SetPixel(x, y, c);
            }
        bubbleTexture = ImageTexture.CreateFromImage(img);
        return bubbleTexture;
    }

    void BuildModel()
    {
        Color skin = new("#c68642"), shirt = new("#f5f5f5"), pant = new("#3e2723");

        model = new Node3D();
        AddChild(model);

        legL = Limb(model, new Vector3(-0.1f, 0.35f, 0), new Vector3(0.17f, 0.35f, 0.2f), pant);
        legR = Limb(model, new Vector3(0.1f, 0.35f, 0), new Vector3(0.17f, 0.35f, 0.2f), pant);
        model.AddChild(Util.Box(new Vector3(0.45f, 0.4f, 0.28f), shirt, new Vector3(0, 0.55f, 0)));
        armL = Limb(model, new Vector3(-0.28f, 0.72f, 0), new Vector3(0.12f, 0.34f, 0.12f), skin);
        armR = Limb(model, new Vector3(0.28f, 0.72f, 0), new Vector3(0.12f, 0.34f, 0.12f), skin);
        model.AddChild(Util.Box(new Vector3(0.3f, 0.3f, 0.3f), skin, new Vector3(0, 0.9f, 0)));
        model.AddChild(Util.Box(new Vector3(0.06f, 0.06f, 0.04f), Colors.Black, new Vector3(-0.07f, 0.93f, -0.15f)));
        model.AddChild(Util.Box(new Vector3(0.06f, 0.06f, 0.04f), Colors.Black, new Vector3(0.07f, 0.93f, -0.15f)));

        // salakot (conical straw hat)
        model.AddChild(Util.Cyl(0f, 0.4f, 0.2f, new Color("#d8b45a"), new Vector3(0, 1.2f, 0), 12));

        // pole over the shoulder with something hanging at each end
        load = new Node3D();
        model.AddChild(load);
        load.AddChild(Util.Box(new Vector3(0.04f, 0.04f, 1.7f), new Color("#8d6e63"), new Vector3(0.18f, 0.95f, 0)));
        foreach (int sz in new[] { -1, 1 })
        {
            float z = sz * 0.75f;
            load.AddChild(Util.Box(new Vector3(0.02f, 0.25f, 0.02f), new Color("#5d4037"), new Vector3(0.18f, 0.82f, z)));

            if (vendor == "chicharon")
            {
                load.AddChild(Util.Cyl(0.22f, 0.18f, 0.3f, new Color("#c69c6d"), new Vector3(0.18f, 0.62f, z), 12));   // wicker basket
                for (int i = 0; i < 3; i++)                                                                              // golden chicharon
                    load.AddChild(Util.Box(new Vector3(0.14f, 0.1f, 0.14f), new Color("#d4902a"),
                        new Vector3(0.18f + (i - 1) * 0.1f, 0.82f + (i == 1 ? 0.03f : 0f), z + (i - 1) * 0.04f)));
            }
            else if (vendor == "buko")
            {
                load.AddChild(Util.Cyl(0.22f, 0.18f, 0.3f, new Color("#8d6e63"), new Vector3(0.18f, 0.62f, z), 12));   // basket
                for (int i = 0; i < 3; i++)                                                                              // green coconuts
                    load.AddChild(Util.Cyl(0.1f, 0.1f, 0.16f, new Color("#7cb342"),
                        new Vector3(0.18f + (i - 1) * 0.11f, 0.85f + (i == 1 ? 0.04f : 0f), z + (i - 1) * 0.04f), 10));
            }
            else
            {
                load.AddChild(Util.Cyl(0.18f, 0.16f, 0.42f, new Color("#cfd8dc"), new Vector3(0.18f, 0.55f, z), 12));  // taho containers
                load.AddChild(Util.Cyl(0.19f, 0.19f, 0.04f, new Color("#90a4ae"), new Vector3(0.18f, 0.78f, z), 12));
            }
        }

        // speech bubble: white rounded bubble with a tail, black text on top
        bubble = new Node3D { Position = new Vector3(0, 2.25f, 0), Visible = false };
        bubble.AddChild(new Sprite3D
        {
            Texture = GetBubbleTexture(),
            PixelSize = 0.006f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Shaded = false,
            RenderPriority = 0
        });
        shoutLabel = new Label3D
        {
            Text = "TAHO!",
            FontSize = 40,
            PixelSize = 0.006f,
            OutlineSize = 0,
            Modulate = Colors.Black,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            RenderPriority = 1,
            Position = new Vector3(0, 0.18f, 0)
        };
        bubble.AddChild(shoutLabel);
        AddChild(bubble);
    }

    void PickTarget()
    {
        do { targetX = Rng.Next(-7, 8); }
        while (Math.Abs(targetX - Position.X) < 4f);
    }

    // ---------- talking ----------

    public float DistanceTo(Node3D p) => new Vector2(p.Position.X - Position.X, p.Position.Z - Position.Z).Length();

    void FaceToward(Node3D p)
    {
        float dx = p.Position.X - Position.X, dz = p.Position.Z - Position.Z;
        model.Rotation = new Vector3(0, Mathf.Atan2(-dx, -dz), 0);
    }

    /// <summary>Shows text in the speech bubble. The font shrinks to fit longer lines.</summary>
    void ShowText(string text, float seconds)
    {
        int longest = 1, lines = 1, cur = 0;
        foreach (char ch in text)
        {
            if (ch == '\n') { lines++; cur = 0; }
            else { cur++; if (cur > longest) longest = cur; }
        }
        shoutLabel.Text = text;
        shoutLabel.FontSize = Math.Min(Math.Min(40, 500 / longest), lines > 1 ? 32 : 40);
        bubble.Visible = true;
        shoutShow = seconds;
        shoutTimer = seconds + 1.5f + (float)Rng.NextDouble(); // don't interrupt with a random shout
    }

    /// <summary>The player pressed E next to him. Returns true if he handed over his item (only once).</summary>
    public bool Talk()
    {
        var player = getPlayer?.Invoke();
        if (player != null) FaceToward(player);
        pause = Math.Max(pause, 2.5f);

        if (!Served)
        {
            Served = true;
            ShowText(GiveLine, 2.4f);
            return true;
        }
        ShowText(EmptyLine, 2f);
        return false;
    }

    // ---------- behavior ----------

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        time += dt;
        noticeCooldown -= dt;
        bool running = false;
        var player = getPlayer?.Invoke();

        // a customer walks up: he stops and offers something
        if (player != null && !Served && noticeCooldown <= 0f && DistanceTo(player) < TalkRange)
        {
            pause = Math.Max(pause, 3f);
            noticeCooldown = 10f;
            FaceToward(player);
            ShowText(OfferLine, 2.6f);
        }

        if (pause > 0f)
        {
            pause -= dt;
        }
        else
        {
            float dx = targetX - Position.X;
            float sign = Math.Sign(dx);

            // the player is an obstacle too: stop if they are in the way, then run the other direction
            bool blocked = false;
            if (player != null && Math.Abs(player.Position.Z - Position.Z) < 0.7f)
            {
                float ahead = (player.Position.X - Position.X) * sign;
                if (ahead > -0.3f && ahead < 1.3f)
                {
                    blocked = true;
                    pause = 0.5f;
                    targetX = Mathf.Clamp(Position.X - sign * 5f, -7f, 7f);
                }
            }

            if (!blocked)
            {
                float step = RunSpeed * dt;
                if (Math.Abs(dx) <= step)
                {
                    Position = new Vector3(targetX, 0, Position.Z);
                    pause = 0.3f + (float)Rng.NextDouble() * 0.9f; // catch his breath
                    PickTarget();
                }
                else
                {
                    running = true;
                    var p = Position;
                    p.X += sign * step;
                    p.Y = Math.Abs(Mathf.Sin(time * 14f)) * 0.12f; // bouncy run
                    Position = p;
                    model.Rotation = new Vector3(0, dx > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2, 0);
                }
            }
        }

        if (!running) Position = new Vector3(Position.X, 0, Position.Z);

        // run cycle
        float s = Mathf.Sin(time * 18f);
        float amp = running ? 1f : 0f;
        legL.RotationDegrees = new Vector3(s * 45f * amp, 0, 0);
        legR.RotationDegrees = new Vector3(-s * 45f * amp, 0, 0);
        armL.RotationDegrees = new Vector3(-s * 40f * amp, 0, 0);
        armR.RotationDegrees = new Vector3(s * 40f * amp, 0, 0);
        load.RotationDegrees = new Vector3(Mathf.Sin(time * 14f) * 4f * amp, 0, 0);

        // shouting
        shoutTimer -= dt;
        if (shoutTimer <= 0f && shoutShow <= 0f)
        {
            string[] cries = Cries;
            ShowText(cries[Rng.Next(cries.Length)], 1.2f);
            shoutTimer = 2.5f + (float)Rng.NextDouble() * 1.5f;
        }
        if (shoutShow > 0f)
        {
            shoutShow -= dt;
            bubble.Position = new Vector3(0, 2.25f + Mathf.Sin(time * 10f) * 0.05f, 0); // little bob
            if (shoutShow <= 0f) bubble.Visible = false;
        }
    }
}
