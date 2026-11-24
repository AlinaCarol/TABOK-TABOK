using Godot;
using System;

namespace CrossyRoadBase;

/// <summary>A Philippine Eagle (Pithecophaga jefferyi): dark brown body, cream shaggy-crested head, big beak.</summary>
public partial class Eagle : Node3D
{
    const float Speed = 5.5f;

    public event Action Grabbed;
    public Func<Vector3> Target;
    public bool Swooping, Carrying;
    public bool Active => Swooping || Carrying;

    Node3D wingL, wingR;
    float time;

    public override void _Ready()
    {
        Color brown = new("#4a3322"), cream = new("#f2e6c9"), tan = new("#a07a45"), dark = new("#2b1d14");

        AddChild(Util.Box(new Vector3(0.55f, 0.45f, 1.1f), brown, Vector3.Zero));                    // body
        AddChild(Util.Box(new Vector3(0.45f, 0.3f, 0.55f), cream, new Vector3(0, -0.1f, -0.25f)));   // chest
        AddChild(Util.Box(new Vector3(0.42f, 0.42f, 0.42f), cream, new Vector3(0, 0.2f, -0.7f)));    // head
        AddChild(Util.Box(new Vector3(0.4f, 0.05f, 0.7f), tan, new Vector3(0, 0, 0.85f)));           // tail

        // the famous shaggy crest
        for (int i = -2; i <= 2; i++)
        {
            var feather = Util.Box(new Vector3(0.07f, 0.34f, 0.07f), i % 2 == 0 ? tan : dark,
                new Vector3(i * 0.09f, 0.45f, -0.5f + Math.Abs(i) * 0.08f));
            feather.RotationDegrees = new Vector3(35, 0, i * 12);
            AddChild(feather);
        }

        // big deep beak with a hooked tip
        AddChild(Util.Box(new Vector3(0.16f, 0.28f, 0.4f), new Color("#cfc7a0"), new Vector3(0, 0.12f, -1.0f)));
        AddChild(Util.Box(new Vector3(0.1f, 0.12f, 0.1f), dark, new Vector3(0, -0.06f, -1.17f)));

        // blue eyes
        AddChild(Util.Box(new Vector3(0.07f, 0.07f, 0.04f), new Color("#4aa3df"), new Vector3(-0.16f, 0.28f, -0.9f)));
        AddChild(Util.Box(new Vector3(0.07f, 0.07f, 0.04f), new Color("#4aa3df"), new Vector3(0.16f, 0.28f, -0.9f)));

        // yellow legs for grabbing
        AddChild(Util.Box(new Vector3(0.08f, 0.3f, 0.08f), new Color("#e8c030"), new Vector3(-0.15f, -0.35f, -0.1f)));
        AddChild(Util.Box(new Vector3(0.08f, 0.3f, 0.08f), new Color("#e8c030"), new Vector3(0.15f, -0.35f, -0.1f)));

        // wings on pivots so they can flap
        wingL = new Node3D { Position = new Vector3(-0.25f, 0.1f, 0) };
        wingL.AddChild(Util.Box(new Vector3(1.7f, 0.07f, 0.8f), brown, new Vector3(-0.85f, 0, 0.05f)));
        AddChild(wingL);
        wingR = new Node3D { Position = new Vector3(0.25f, 0.1f, 0) };
        wingR.AddChild(Util.Box(new Vector3(1.7f, 0.07f, 0.8f), brown, new Vector3(0.85f, 0, 0.05f)));
        AddChild(wingR);

        Scale = new Vector3(1.3f, 1.3f, 1.3f);
        Visible = false;
    }

    public void Swoop(Vector3 from, Func<Vector3> target)
    {
        Position = from;
        Target = target;
        Swooping = true;
        Carrying = false;
        Visible = true;
    }

    public void Stop()
    {
        Swooping = false;
        Carrying = false;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!Active) return;
        float dt = (float)delta;
        time += dt;

        float flap = Mathf.Sin(time * 12f) * 35f;
        wingL.RotationDegrees = new Vector3(0, 0, -flap);
        wingR.RotationDegrees = new Vector3(0, 0, flap);

        if (Swooping)
        {
            var d = Target() - Position;
            float dist = d.Length();
            if (dist < 0.8f)
            {
                Swooping = false;
                Carrying = true;
                Grabbed?.Invoke();
            }
            else
            {
                var dir = d / dist;
                Position += dir * Speed * dt;
                Face(dir);
            }
        }
        else if (Carrying)
        {
            Position += new Vector3(0, 5, -5) * dt; // fly away with the chicken
            Face(new Vector3(0, 0.7f, -0.7f));
        }
    }

    void Face(Vector3 dir)
    {
        float h = new Vector2(dir.X, dir.Z).Length();
        Rotation = new Vector3(Mathf.Atan2(dir.Y, h), Mathf.Atan2(-dir.X, -dir.Z), 0);
    }
}
