using Godot;
using System;
using System.Collections.Generic;

namespace CrossyRoadBase;

// Grass = sidewalk, Road = street, River = canal (estero), Construction = road works.
public enum LaneType { Grass, Road, River, Construction }

// Road variants: normal flowing traffic, a bumper-to-bumper jam, or an accident scene.
public enum RoadMode { Flow, Jam, Accident }

/// <summary>One row of the world. Row N sits at z = -N, so "forward" is -Z.</summary>
public partial class Lane : Node3D
{
	public const int MaxX = 8;        // playable columns: -MaxX..MaxX
	public const float Span = 36f;    // moving objects wrap at +-Span/2

	// Traffic speed range in units per second. Each lane picks its own speed. Tweak to taste.
	const float SpeedMin = 1.3f, SpeedMax = 3.0f;

	static readonly Random Rng = new();

	class Mover { public Node3D Node; public float HalfLen; public float Speed, NormalSpeed, ReverseTimer; public string Kind; }

	class PersonRig
	{
		public Node3D Root, LegL, LegR, ArmL, ArmR;
		public bool Walking, Waving;
		public float Phase;
	}

	class Glow { public StandardMaterial3D Mat; public float Base, Amp, Speed, Phase, Alpha; }
	class Smoke { public MeshInstance3D Node; public StandardMaterial3D Mat; public Vector3 Origin; public float Phase; }
	class Honk { public Label3D Label; public float Timer, Show; }

	// set by Main before Init()
	public RoadMode Mode = RoadMode.Flow;
	public HashSet<int> KeepFree;       // cells that must stay open (the gap of a jam in the previous row)
	public HashSet<int> AvoidCells;     // cells blocked in the previous row (a jam gap must not start there)
	public int GapHint = int.MinValue;  // reuse the previous jam row's gap so the path lines up
	public readonly HashSet<int> JamGaps = new();

	static readonly string[] BeepWords = { "BEEP!", "BEEEP!", "PIN PIN!", "BEEP BEEP!" };

	public LaneType Type;
	public int Row;
	public int CrosswalkX;
	public bool HasPedestrianCrossing { get; private set; }
	public readonly HashSet<int> Blocked = new(); // sidewalk cells with people / signs / bins / huts
	readonly List<Mover> movers = new();
	readonly List<PersonRig> people = new();
	readonly List<Glow> glows = new();
	readonly List<Smoke> smokes = new();
	readonly List<Honk> honks = new();
	float speed, time;
	bool cleared;      // empty sidewalk (taho street)
	bool clearRight;   // landmark zone: no scenery on the +X side

	public void Init(LaneType type, int row, bool clear = false, bool clearRightSide = false)
	{
		Type = type;
		Row = row;
		cleared = clear;
		clearRight = clearRightSide;
		Position = new Vector3(0, 0, -row);

		Color ground = type switch
		{
			LaneType.Grass => row % 2 == 0 ? City.Current.SidewalkA : City.Current.SidewalkB,
			LaneType.Road => City.Current.Road,
			LaneType.Construction => new Color("#8d8577"),                                // gravel work zone
			_ => City.Current.Water
		};
		float top = type == LaneType.River ? -0.2f : 0f;
		AddChild(Util.Box(new Vector3(60, 0.2f, 1), ground, new Vector3(0, top - 0.1f, 0)));
		if (type == LaneType.Grass) BuildConcreteDetails();

		switch (type)
		{
			case LaneType.Grass: BuildSidewalk(); break;
			case LaneType.Road:
				if (Mode == RoadMode.Jam) BuildJam();
				else if (Mode == RoadMode.Accident) BuildAccident();
				else BuildTraffic();
				break;
			case LaneType.River: BuildCanal(); break;
			case LaneType.Construction: BuildConstruction(); break;
		}
	}

	void BuildConcreteDetails()
	{
		Color joint = new("#77736b");
		for (int x = -MaxX; x <= MaxX; x += 4)
			AddChild(Util.Box(new Vector3(0.025f, 0.008f, 1f), joint, new Vector3(x, 0.004f, 0)));
		if (Row % 2 == 0)
			AddChild(Util.Box(new Vector3(MaxX * 2f, 0.008f, 0.025f), joint, new Vector3(0, 0.004f, 0)));
	}

	void BuildCrosswalk()
	{
		for (float x = CrosswalkX - 1.25f; x <= CrosswalkX + 1.25f; x += 0.5f)
			AddChild(Util.Box(new Vector3(0.18f, 0.015f, 0.9f), new Color("#e7e3da"), new Vector3(x, 0.012f, 0)));
	}

	public void MarkPedestrianCrossing()
	{
		if (Type != LaneType.Road || HasPedestrianCrossing) return;
		HasPedestrianCrossing = true;
		BuildCrosswalk();
	}

	public void FaceWavingPeopleAwayFromWater(bool waterAhead)
	{
		float yaw = waterAhead ? Mathf.Pi : 0f;
		foreach (var person in people)
			if (person.Waving) person.Root.Rotation = new Vector3(0, yaw, 0);
	}

	// =====================================================================
	//  SIDEWALK: people waiting for jeepneys, PUJ stop signs, bins, huts
	// =====================================================================

	void BuildSidewalk()
	{
		if (cleared) return; // taho street / plaza: nothing in the way
		bool safe = Row <= 1; // keep the start area clear
		for (int x = -MaxX - 3; x <= MaxX + 3; x++)
		{
			if (clearRight && x > MaxX) continue; // landmark zone
			if (KeepFree != null && KeepFree.Contains(x)) continue; // keep the jam gap open
			bool scenery = Math.Abs(x) > MaxX;
			if (!scenery && Blocked.Contains(x)) continue;
			if (!scenery && (safe || Rng.NextDouble() > 0.18)) continue;
			if (scenery && Rng.NextDouble() > 0.6) continue;
			if (!scenery) Blocked.Add(x);

			Node3D prop;
			switch (Weighted(City.Current.PropWeights))
			{
				case 0: // waiting for a ride, often waving one down
					prop = MakePerson(false, Rng.NextDouble() < 0.6).Root;
					prop.RotationDegrees = new Vector3(0, Rng.Next(2) * 180, 0);
					break;
				case 1: prop = MakeJeepStop(); break;
				case 2: prop = MakeTrashCan(); break;
				case 3: prop = MakeBahayKubo(); break;
				case 4: prop = MakePalm(); break;
				case 5: prop = MakeSariSari(); break;
				case 6: prop = MakeFurniture(); break;
				case 7: prop = MakeChicharonStall(); break;
				case 8: prop = MakeRoadSign("STOP", new Color("#c62828"), Colors.White); break;
				default:
					prop = MakeNoParkingScene();
					if (x < MaxX) Blocked.Add(x + 1);
					break;
			}

			prop.Position = new Vector3(x, 0, 0);
			AddChild(prop);
		}
	}

	static Node3D MakeRoadSign(string text, Color background, Color foreground)
	{
		var sign = new Node3D();
		sign.AddChild(Util.Box(new Vector3(0.08f, 1.3f, 0.08f), new Color("#616161"), new Vector3(0, 0.65f, 0)));
		sign.AddChild(Util.Box(new Vector3(0.78f, 0.72f, 0.08f), background, new Vector3(0, 1.35f, 0)));
		sign.AddChild(new Label3D
		{
			Text = text,
			FontSize = 28,
			PixelSize = 0.006f,
			OutlineSize = 4,
			OutlineModulate = Colors.Black,
			Modulate = foreground,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(0, 1.35f, -0.05f)
		});
		return sign;
	}

	static Node3D MakeNoParkingScene()
	{
		var scene = new Node3D();
		var sign = MakeNoParkingSign();
		sign.Scale = new Vector3(0.78f, 0.78f, 0.78f);
		sign.Position = new Vector3(-0.45f, 0, 0);
		scene.AddChild(sign);

		var parkedTricycle = MakeTricycle(1f);
		parkedTricycle.Position = new Vector3(0.45f, 0, 0.12f);
		scene.AddChild(parkedTricycle);
		return scene;
	}

	static Node3D MakeNoParkingSign()
	{
		var sign = new Node3D();
		sign.AddChild(Util.Box(new Vector3(0.08f, 1.3f, 0.08f), new Color("#616161"), new Vector3(0, 0.65f, 0)));

		sign.AddChild(Util.Box(new Vector3(1.16f, 1.16f, 0.08f), new Color("#181818"), new Vector3(0, 1.4f, 0)));
		sign.AddChild(Util.Box(new Vector3(1.08f, 1.08f, 0.04f), Colors.White, new Vector3(0, 1.4f, 0.055f)));

		var disc = Util.Cyl(0.42f, 0.42f, 0.06f, Colors.White, new Vector3(0, 1.4f, 0.085f));
		disc.RotationDegrees = new Vector3(90, 0, 0);
		sign.AddChild(disc);
		sign.AddChild(new MeshInstance3D
		{
			Mesh = new TorusMesh { InnerRadius = 0.34f, OuterRadius = 0.42f },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#ed1c24") },
			Position = new Vector3(0, 1.4f, 0.12f),
			RotationDegrees = new Vector3(90, 0, 0)
		});

		var ink = new Color("#111111");
		sign.AddChild(Util.Box(new Vector3(0.09f, 0.42f, 0.04f), ink, new Vector3(-0.08f, 1.4f, 0.16f)));
		sign.AddChild(Util.Box(new Vector3(0.2f, 0.07f, 0.04f), ink, new Vector3(0, 1.57f, 0.16f)));
		sign.AddChild(Util.Box(new Vector3(0.08f, 0.16f, 0.04f), ink, new Vector3(0.06f, 1.51f, 0.16f)));
		sign.AddChild(Util.Box(new Vector3(0.18f, 0.07f, 0.04f), ink, new Vector3(0, 1.44f, 0.16f)));

		var slash = Util.Box(new Vector3(0.05f, 0.82f, 0.02f), new Color("#ed1c24"), new Vector3(0, 1.4f, 0.2f));
		slash.RotationDegrees = new Vector3(0, 0, -45);
		sign.AddChild(slash);
		return sign;
	}


	static Node3D Limb(Node3D root, Vector3 pivotPos, Vector3 size, Color c)
	{
		var pivot = new Node3D { Position = pivotPos };
		pivot.AddChild(Util.Box(size, c, new Vector3(0, -size.Y / 2, 0)));
		root.AddChild(pivot);
		return pivot;
	}

	PersonRig MakePerson(bool walking, bool waving)
	{
		Color[] shirts = { new("#e74c3c"), new("#3498db"), new("#f1c40f"), new("#9b59b6"), new("#1abc9c"), new("#e67e22"), new("#ecf0f1") };
		Color[] skins = { new("#f1c27d"), new("#c68642"), new("#8d5524"), new("#e0ac69") };
		Color[] pants = { new("#2c3e50"), new("#34495e"), new("#5d4037"), new("#1b2631") };

		var rig = new PersonRig
		{
			Root = new Node3D(),
			Walking = walking,
			Waving = waving,
			Phase = (float)Rng.NextDouble() * 6.28f
		};
		Color shirt = shirts[Rng.Next(shirts.Length)];
		Color skin = skins[Rng.Next(skins.Length)];
		Color pant = pants[Rng.Next(pants.Length)];

		rig.LegL = Limb(rig.Root, new Vector3(-0.1f, 0.35f, 0), new Vector3(0.17f, 0.35f, 0.2f), pant);
		rig.LegR = Limb(rig.Root, new Vector3(0.1f, 0.35f, 0), new Vector3(0.17f, 0.35f, 0.2f), pant);
		rig.Root.AddChild(Util.Box(new Vector3(0.45f, 0.4f, 0.28f), shirt, new Vector3(0, 0.55f, 0)));
		rig.ArmL = Limb(rig.Root, new Vector3(-0.28f, 0.72f, 0), new Vector3(0.12f, 0.34f, 0.12f), skin);
		rig.ArmR = Limb(rig.Root, new Vector3(0.28f, 0.72f, 0), new Vector3(0.12f, 0.34f, 0.12f), skin);
		rig.Root.AddChild(Util.Box(new Vector3(0.3f, 0.3f, 0.3f), skin, new Vector3(0, 0.9f, 0)));
		rig.Root.AddChild(Util.Box(new Vector3(0.32f, 0.1f, 0.32f), new Color("#2b1d14"), new Vector3(0, 1.07f, 0)));
		rig.Root.AddChild(Util.Box(new Vector3(0.06f, 0.06f, 0.04f), Colors.Black, new Vector3(-0.07f, 0.93f, -0.15f)));
		rig.Root.AddChild(Util.Box(new Vector3(0.06f, 0.06f, 0.04f), Colors.Black, new Vector3(0.07f, 0.93f, -0.15f)));

		// some people shade themselves with an umbrella while waiting
		if (!walking && !waving && Rng.NextDouble() < 0.5)
		{
			Color[] umbrellas = { new("#e91e63"), new("#ff9800"), new("#03a9f4"), new("#4caf50") };
			rig.Root.AddChild(Util.Box(new Vector3(0.03f, 0.45f, 0.03f), new Color("#9e9e9e"), new Vector3(0, 1.28f, 0)));
			rig.Root.AddChild(Util.Cyl(0f, 0.5f, 0.22f, umbrellas[Rng.Next(umbrellas.Length)], new Vector3(0, 1.55f, 0), 10));
		}

		people.Add(rig);
		return rig;
	}

	static Node3D MakeJeepStop()
	{
		var s = new Node3D { RotationDegrees = new Vector3(0, Rng.Next(2) * 90, 0) };
		s.AddChild(Util.Box(new Vector3(0.07f, 1.3f, 0.07f), new Color("#9e9e9e"), new Vector3(0, 0.65f, 0)));
		s.AddChild(Util.Box(new Vector3(0.6f, 0.4f, 0.06f), new Color("#1976d2"), new Vector3(0, 1.2f, 0)));
		s.AddChild(Util.Box(new Vector3(0.42f, 0.12f, 0.07f), Colors.White, new Vector3(0, 1.2f, 0)));
		return s;
	}

	static Node3D MakeTrashCan()
	{
		var t = new Node3D();
		t.AddChild(Util.Cyl(0.26f, 0.22f, 0.7f, new Color("#5d7a6a"), new Vector3(0, 0.35f, 0)));
		t.AddChild(Util.Cyl(0.3f, 0.3f, 0.08f, new Color("#2f3e36"), new Vector3(0, 0.74f, 0)));
		t.AddChild(Util.Box(new Vector3(0.1f, 0.06f, 0.1f), new Color("#2f3e36"), new Vector3(0, 0.83f, 0)));
		return t;
	}

	static Node3D MakeBahayKubo()
	{
		var k = new Node3D();
		Color wood = new("#7a4b22");
		foreach (int sx in new[] { -1, 1 })
			foreach (int sz in new[] { -1, 1 })
				k.AddChild(Util.Box(new Vector3(0.08f, 0.4f, 0.08f), wood, new Vector3(sx * 0.32f, 0.2f, sz * 0.32f)));

		k.AddChild(Util.Box(new Vector3(0.88f, 0.06f, 0.88f), new Color("#b08850"), new Vector3(0, 0.43f, 0)));
		k.AddChild(Util.Box(new Vector3(0.7f, 0.4f, 0.7f), new Color("#d9b779"), new Vector3(0, 0.66f, 0)));
		k.AddChild(Util.Box(new Vector3(0.18f, 0.22f, 0.02f), new Color("#4a3322"), new Vector3(0, 0.6f, 0.36f)));

		var roof = Util.Cyl(0f, 0.74f, 0.65f, new Color("#b8912f"), new Vector3(0, 1.17f, 0), 4);
		roof.RotationDegrees = new Vector3(0, 45, 0);
		k.AddChild(roof);
		return k;
	}

	// =====================================================================
	//  ROAD: jeepneys, motorcycles (habal-habal), taxis, trucks, jaywalkers
	// =====================================================================

	void BuildTraffic()
	{
		float dir = Rng.Next(2) == 0 ? -1 : 1;
		speed = dir * (SpeedMin + Rf() * (SpeedMax - SpeedMin));

		// fewer vehicles per lane, so there are plenty of gaps to cross through
		int n = Math.Max(2, Mathf.RoundToInt(Rng.Next(3, 6) * City.Current.TrafficDensity));
		float spacing = Span / n, offset = Rf() * spacing;

		for (int i = 0; i < n; i++)
		{
			var info = BuildVehicle(PickVehicle(), dir);
			info.Node.Position = new Vector3(-Span / 2 + offset + i * spacing, 0, 0);
			AddChild(info.Node);
			movers.Add(new Mover { Node = info.Node, HalfLen = info.HalfLen, Speed = speed, NormalSpeed = speed, Kind = info.Kind });
		}
	}

	// ---------- vehicle picking (weights come from the current city) ----------

	class VehicleInfo
	{
		public Node3D Node;
		public float HalfLen, RearX, HalfW, Sx = 1f;
		public string Kind;
	}

	static int CellsFor(VKind k) => k == VKind.Jeepney ? 3 : k == VKind.Truck ? 2 : 1;

	static VKind PickVehicle(bool singleTileOnly = false)
	{
		float[] w = City.Current.VehicleWeights;
		float total = 0f;
		for (int i = 0; i < w.Length; i++)
			if (!(singleTileOnly && CellsFor((VKind)i) > 1)) total += w[i];

		float r = Rf() * total;
		for (int i = 0; i < w.Length; i++)
		{
			if (singleTileOnly && CellsFor((VKind)i) > 1) continue;
			r -= w[i];
			if (r <= 0f) return (VKind)i;
		}
		return VKind.Motorcycle;
	}

	static int Weighted(float[] w)
	{
		float total = 0f;
		foreach (float x in w) total += x;
		if (total <= 0f) return 0;
		float r = Rf() * total;
		for (int i = 0; i < w.Length; i++)
		{
			r -= w[i];
			if (r <= 0f) return i;
		}
		return 0;
	}

	static VehicleInfo BuildVehicle(VKind k, float dir)
	{
		switch (k)
		{
			case VKind.Jeepney:
				return new VehicleInfo { Node = MakeJeepney(dir), HalfLen = 1.5f, RearX = 1.5f, HalfW = 0.3f, Kind = "jeepney" };
			case VKind.Truck:
				return new VehicleInfo { Node = MakeTruck(dir), HalfLen = 1.2f, RearX = 1.2f, HalfW = 0.3f, Sx = 0.82f, Kind = "truck" };
			case VKind.Taxi:
				return new VehicleInfo { Node = MakeCar(dir, true), HalfLen = 0.65f, RearX = 0.65f, HalfW = 0.25f, Sx = 0.76f, Kind = "taxi" };
			case VKind.Motorcycle:
				return new VehicleInfo { Node = MakeMotorcycle(dir), HalfLen = 0.45f, RearX = 0.45f, HalfW = 0.05f, Kind = "motorcycle" };
			case VKind.Tricycle:
				return new VehicleInfo { Node = MakeTricycle(dir), HalfLen = 0.6f, RearX = 0.45f, HalfW = 0.05f, Kind = "tricycle" };
			default:
				return new VehicleInfo { Node = MakeCar(dir, false), HalfLen = 0.65f, RearX = 0.65f, HalfW = 0.25f, Sx = 0.76f, Kind = "car" };
		}
	}

	// =====================================================================
	//  WARNING GLOW: pulsing marker on every tile you cannot enter
	// =====================================================================

	void AddWarningGlow(int cell, Color color)
	{
		// pulsing tile on the ground
		AddChild(Util.Glow(new Vector3(0.96f, 0.03f, 0.96f), color, new Vector3(cell, 0.06f, 0), 1f, out var tile, 2f));
		glows.Add(new Glow { Mat = tile, Base = 1.5f, Amp = 2.5f, Speed = 4f });

		// faint see-through light column
		AddChild(Util.Glow(new Vector3(0.9f, 1.5f, 0.9f), color, new Vector3(cell, 0.75f, 0), 0.16f, out var beam, 1f));
		glows.Add(new Glow { Mat = beam, Base = 0.8f, Amp = 1.2f, Speed = 4f, Alpha = 0.16f });
	}

	// =====================================================================
	//  TRAFFIC JAM: bumper to bumper, with a one-tile gap to squeeze through
	// =====================================================================

	void BuildJam()
	{
		float dir = Rng.Next(2) == 0 ? -1 : 1;
		int lo = -MaxX - 4, hi = MaxX + 4;

		// the first gap must be reachable from the previous row
		int g1;
		if (GapHint != int.MinValue) g1 = GapHint; // line up with the previous jam row
		else
		{
			var candidates = new List<int>();
			for (int c = -MaxX + 1; c < MaxX; c++)
				if (AvoidCells == null || !AvoidCells.Contains(c)) candidates.Add(c);
			g1 = candidates.Count > 0 ? candidates[Rng.Next(candidates.Count)] : Rng.Next(-MaxX + 1, MaxX);
		}
		JamGaps.Add(g1);

		// plus more one-tile gaps, spread out along the row (2 to 4 in total)
		int wanted = Rng.Next(2, 5);
		for (int tries = 0; tries < 50 && JamGaps.Count < wanted; tries++)
		{
			int g = Rng.Next(-MaxX + 1, MaxX);
			bool farEnough = true;
			foreach (int e in JamGaps)
				if (Math.Abs(e - g) < 3) farEnough = false;
			if (farEnough && (AvoidCells == null || !AvoidCells.Contains(g))) JamGaps.Add(g);
		}

		var honkSpots = new List<float>();
		int x = lo;
		while (x <= hi)
		{
			if (JamGaps.Contains(x)) { x++; continue; }

			VKind kind = PickVehicle();
			int len = CellsFor(kind);
			for (int j = 1; j < len; j++)
				if (JamGaps.Contains(x + j) || x + j > hi) { len = 1; break; }
			if (len == 1 && CellsFor(kind) > 1) kind = PickVehicle(true); // a long vehicle doesn't fit: use a small one

			var info = BuildVehicle(kind, dir);
			float cx = x + (len - 1) / 2f;
			info.Node.Position = new Vector3(cx, 0, 0);
			info.Node.Scale = new Vector3(info.Sx, 1, 1);
			foreach (int sz in new[] { -1, 1 })                       // glowing brake lights
				info.Node.AddChild(Util.Glow(new Vector3(0.05f, 0.1f, 0.12f), new Color("#ff1a1a"),
					new Vector3(-dir * (info.RearX + 0.01f), 0.4f, sz * info.HalfW), 1f, out _));
			AddChild(info.Node);

			for (int cell = x; cell < x + len; cell++)
				if (Math.Abs(cell) <= MaxX) Blocked.Add(cell);

			if (len == 1 && honkSpots.Count < 2 && Math.Abs(cx) <= MaxX && Rng.NextDouble() < 0.12)
				honkSpots.Add(cx);

			x += len;
		}

		// impatient drivers honking
		foreach (float hx in honkSpots)
		{
			var label = new Label3D
			{
				Text = "BEEP!",
				FontSize = 40,
				PixelSize = 0.008f,
				OutlineSize = 10,
				Modulate = new Color("#ffeb3b"),
				OutlineModulate = Colors.Black,
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				NoDepthTest = true,
				Visible = false,
				Position = new Vector3(hx, 1.6f, 0)
			};
			AddChild(label);
			honks.Add(new Honk { Label = label, Timer = Rf() * 3f });
		}
	}

	// =====================================================================
	//  ACCIDENT: two wrecks, police car or ambulance, cones, smoke, flashing lights
	// =====================================================================

	void BuildAccident()
	{
		float dir = Rng.Next(2) == 0 ? -1 : 1;

		// choose a crash site that doesn't cover tiles that must stay open
		int c = 0;
		for (int attempt = 0; attempt < 30; attempt++)
		{
			c = Rng.Next(-3, 3);
			bool ok = true;
			if (KeepFree != null)
				for (int x = c - 3; x <= c + 2; x++)
					if (KeepFree.Contains(x)) ok = false;
			if (ok) break;
		}

		// skid marks leading into the crash
		foreach (int sz in new[] { -1, 1 })
			AddChild(Util.Box(new Vector3(2.0f, 0.01f, 0.07f), new Color("#25252a"), new Vector3(c - 0.8f, 0.012f, sz * 0.18f)));

		// wreck A: a car, spun around with a scorched hood
		var a = MakeCar(dir, false);
		a.AddChild(Util.Box(new Vector3(0.55f, 0.02f, 0.5f), new Color("#1a1a1a"), new Vector3(dir * 0.35f, 0.49f, 0)));
		a.Scale = new Vector3(0.8f, 1, 1);
		a.Position = new Vector3(c, 0, -0.08f);
		a.RotationDegrees = new Vector3(Rng.Next(-6, 7), 38, 6);
		AddChild(a);
		AddSmoke(new Vector3(c, 0, -0.08f));

		// wreck B: a tricycle, or a motorcycle lying on its side
		bool trike = Rng.NextDouble() < 0.5;
		Node3D b = trike ? MakeTricycle(dir) : MakeMotorcycle(dir);
		b.Position = new Vector3(c + 1, 0, 0.1f);
		b.RotationDegrees = new Vector3(trike ? 10 : (Rng.Next(2) == 0 ? 75 : -75), -42, 0);
		AddChild(b);
		AddSmoke(new Vector3(c + 1, 0, 0.1f));

		// emergency vehicle
		bool amb = Rng.NextDouble() < 0.45;
		Node3D rescue = amb ? MakeAmbulance(dir) : MakePoliceCar(dir);
		rescue.Position = new Vector3(amb ? c - 2.5f : c - 2, 0, 0);
		rescue.Scale = new Vector3(amb ? 0.9f : 0.8f, 1, 1);
		AddChild(rescue);

		// traffic cones
		foreach (int cc in new[] { c - 1, c + 2 })
		{
			var cone = MakeCone();
			cone.Position = new Vector3(cc, 0, 0);
			AddChild(cone);
		}

		// block and mark every tile of the scene
		var cells = new List<int> { c - 1, c, c + 1, c + 2, c - 2 };
		if (amb) cells.Add(c - 3);
		foreach (int cell in cells)
		{
			Blocked.Add(cell);
			AddWarningGlow(cell, new Color("#ff1744"));
		}

		AddDebris(c + 0.5f);
	}

	static Node3D MakeCone()
	{
		var cone = new Node3D();
		cone.AddChild(Util.Box(new Vector3(0.42f, 0.04f, 0.42f), new Color("#212121"), new Vector3(0, 0.02f, 0)));
		cone.AddChild(Util.Cyl(0.04f, 0.2f, 0.6f, new Color("#ff6d00"), new Vector3(0, 0.34f, 0), 10));
		cone.AddChild(Util.Cyl(0.124f, 0.151f, 0.1f, Colors.White, new Vector3(0, 0.33f, 0), 10));
		return cone;
	}

	void AddLightBar(Node3D parent, Vector3 pos)
	{
		parent.AddChild(Util.Glow(new Vector3(0.12f, 0.1f, 0.22f), new Color("#ff1744"), pos + new Vector3(0, 0, -0.15f), 1f, out var red, 1f));
		glows.Add(new Glow { Mat = red, Base = 0.5f, Amp = 6f, Speed = 10f, Phase = 0f });
		parent.AddChild(Util.Glow(new Vector3(0.12f, 0.1f, 0.22f), new Color("#2979ff"), pos + new Vector3(0, 0, 0.15f), 1f, out var blue, 1f));
		glows.Add(new Glow { Mat = blue, Base = 0.5f, Amp = 6f, Speed = 10f, Phase = Mathf.Pi });
	}

	Node3D MakePoliceCar(float f)
	{
		var c = new Node3D();
		c.AddChild(Util.Box(new Vector3(1.3f, 0.4f, 0.7f), new Color("#f5f5f5"), new Vector3(0, 0.28f, 0)));
		c.AddChild(Util.Box(new Vector3(1.36f, 0.08f, 0.76f), new Color("#1565c0"), new Vector3(0, 0.3f, 0)));
		c.AddChild(Util.Box(new Vector3(0.7f, 0.3f, 0.62f), new Color("#2c3e50"), new Vector3(-f * 0.1f, 0.62f, 0)));
		c.AddChild(Util.Box(new Vector3(0.65f, 0.06f, 0.6f), new Color("#f5f5f5"), new Vector3(-f * 0.1f, 0.79f, 0)));
		AddLightBar(c, new Vector3(-f * 0.1f, 0.88f, 0));
		Wheels(c, new[] { f * 0.42f, -f * 0.42f }, 0.36f, 0.16f);
		return c;
	}

	Node3D MakeAmbulance(float f)
	{
		var a = new Node3D();
		a.AddChild(Util.Box(new Vector3(1.6f, 0.8f, 0.8f), new Color("#fafafa"), new Vector3(-f * 0.3f, 0.55f, 0)));      // box body
		a.AddChild(Util.Box(new Vector3(0.6f, 0.55f, 0.8f), new Color("#fafafa"), new Vector3(f * 0.8f, 0.42f, 0)));      // cab
		a.AddChild(Util.Box(new Vector3(0.05f, 0.25f, 0.6f), new Color("#455a64"), new Vector3(f * 1.12f, 0.55f, 0)));    // windshield
		a.AddChild(Util.Box(new Vector3(1.66f, 0.1f, 0.86f), new Color("#d32f2f"), new Vector3(-f * 0.3f, 0.45f, 0)));    // red stripe
		foreach (int sz in new[] { -1, 1 })                                                                                // red cross
		{
			a.AddChild(Util.Box(new Vector3(0.36f, 0.1f, 0.04f), new Color("#d32f2f"), new Vector3(-f * 0.3f, 0.75f, sz * 0.42f)));
			a.AddChild(Util.Box(new Vector3(0.1f, 0.36f, 0.04f), new Color("#d32f2f"), new Vector3(-f * 0.3f, 0.75f, sz * 0.42f)));
		}
		AddLightBar(a, new Vector3(f * 0.8f, 0.75f, 0));
		Wheels(a, new[] { f * 0.8f, -f * 0.7f }, 0.4f, 0.2f);
		return a;
	}

	void AddSmoke(Vector3 origin)
	{
		for (int i = 0; i < 3; i++)
		{
			var mat = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.3f, 0.3f, 0.3f, 0.5f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
			};
			var puff = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.35f, 0.35f) },
				MaterialOverride = mat,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				RotationDegrees = new Vector3(Rng.Next(45), Rng.Next(90), Rng.Next(45))
			};
			AddChild(puff);
			smokes.Add(new Smoke
			{
				Node = puff,
				Mat = mat,
				Origin = origin + new Vector3((Rf() - 0.5f) * 0.3f, 0, (Rf() - 0.5f) * 0.3f),
				Phase = i / 3f + Rf() * 0.1f
			});
		}
	}

	void AddDebris(float cx)
	{
		Color[] cols = { new("#757575"), new("#b71c1c"), new("#212121"), new("#cfd8dc") };
		for (int i = 0; i < 9; i++)
		{
			var d = Util.Box(new Vector3(0.1f + Rf() * 0.15f, 0.05f, 0.1f + Rf() * 0.15f), cols[Rng.Next(cols.Length)],
				new Vector3(cx + (Rf() - 0.5f) * 3.2f, 0.03f + Rf() * 0.02f, (Rf() - 0.5f) * 0.8f));
			d.RotationDegrees = new Vector3(0, Rng.Next(180), 0);
			AddChild(d);
		}
	}

	// =====================================================================
	//  More sidewalk props: palms, sari-sari stores, furniture, chicharon stalls
	// =====================================================================

	static Node3D MakePalm()
	{
		var p = new Node3D { RotationDegrees = new Vector3(0, Rng.Next(360), 0) };
		Color trunk = new("#8d6e63");
		p.AddChild(Util.Box(new Vector3(0.2f, 0.55f, 0.2f), trunk, new Vector3(0, 0.275f, 0)));
		p.AddChild(Util.Box(new Vector3(0.17f, 0.55f, 0.17f), trunk, new Vector3(0.04f, 0.8f, 0)));
		p.AddChild(Util.Box(new Vector3(0.14f, 0.55f, 0.14f), trunk, new Vector3(0.09f, 1.3f, 0)));
		for (int i = 0; i < 4; i++)
		{
			var frond = Util.Box(new Vector3(1.15f, 0.05f, 0.24f), new Color("#2e8b57"), new Vector3(0.09f, 1.62f + i * 0.004f, 0));
			frond.RotationDegrees = new Vector3(0, i * 45, 0);
			p.AddChild(frond);
		}
		p.AddChild(Util.Box(new Vector3(0.12f, 0.12f, 0.12f), new Color("#5d4037"), new Vector3(0.16f, 1.5f, 0.08f)));
		p.AddChild(Util.Box(new Vector3(0.12f, 0.12f, 0.12f), new Color("#5d4037"), new Vector3(0.04f, 1.5f, -0.08f)));
		return p;
	}

	static Node3D MakeSariSari()
	{
		Color[] walls = { new("#ffcc80"), new("#a5d6a7"), new("#90caf9"), new("#f48fb1") };
		Color awning = Rng.NextDouble() < 0.5 ? new Color("#e53935") : new Color("#1e88e5");
		var s = new Node3D { RotationDegrees = new Vector3(0, Rng.Next(2) * 180, 0) };
		s.AddChild(Util.Box(new Vector3(0.9f, 0.7f, 0.8f), walls[Rng.Next(walls.Length)], new Vector3(0, 0.35f, 0.05f)));      // shop
		s.AddChild(Util.Box(new Vector3(0.95f, 0.06f, 0.85f), new Color("#8d6e63"), new Vector3(0, 0.73f, 0.05f)));            // roof
		s.AddChild(Util.Box(new Vector3(0.7f, 0.3f, 0.04f), new Color("#4e342e"), new Vector3(0, 0.45f, -0.36f)));             // counter window
		s.AddChild(Util.Box(new Vector3(1.0f, 0.06f, 0.5f), awning, new Vector3(0, 0.82f, -0.3f)));                             // awning
		s.AddChild(Util.Box(new Vector3(0.12f, 0.12f, 0.06f), new Color("#fdd835"), new Vector3(-0.2f, 0.6f, -0.4f)));          // goods on display
		s.AddChild(Util.Box(new Vector3(0.12f, 0.12f, 0.06f), new Color("#e53935"), new Vector3(0.0f, 0.6f, -0.4f)));
		s.AddChild(Util.Box(new Vector3(0.12f, 0.12f, 0.06f), new Color("#43a047"), new Vector3(0.2f, 0.6f, -0.4f)));
		return s;
	}

	static Node3D MakeFurniture()
	{
		var f = new Node3D { RotationDegrees = new Vector3(0, Rng.Next(4) * 90, 0) };
		if (Rng.NextDouble() < 0.5)
		{
			Color wood = Rng.NextDouble() < 0.5 ? new Color("#a1662f") : new Color("#c68642");
			f.AddChild(Util.Box(new Vector3(0.8f, 0.08f, 0.6f), wood, new Vector3(0, 0.5f, 0)));                                // table
			foreach (int sx in new[] { -1, 1 })
				foreach (int sz in new[] { -1, 1 })
					f.AddChild(Util.Box(new Vector3(0.07f, 0.5f, 0.07f), wood, new Vector3(sx * 0.33f, 0.25f, sz * 0.23f)));
			f.AddChild(Util.Box(new Vector3(0.3f, 0.06f, 0.3f), wood, new Vector3(0, 0.28f, 0)));                              // stool underneath
		}
		else
		{
			Color[] sofas = { new("#5c6bc0"), new("#8d6e63"), new("#26a69a") };
			Color c = sofas[Rng.Next(sofas.Length)];
			f.AddChild(Util.Box(new Vector3(0.9f, 0.3f, 0.5f), c, new Vector3(0, 0.25f, 0)));                                  // seat
			f.AddChild(Util.Box(new Vector3(0.9f, 0.4f, 0.12f), c, new Vector3(0, 0.55f, 0.22f)));                              // back
			f.AddChild(Util.Box(new Vector3(0.12f, 0.35f, 0.5f), c, new Vector3(-0.51f, 0.3f, 0)));                             // arms
			f.AddChild(Util.Box(new Vector3(0.12f, 0.35f, 0.5f), c, new Vector3(0.51f, 0.3f, 0)));
		}
		return f;
	}

	static Node3D MakeChicharonStall()
	{
		var s = new Node3D { RotationDegrees = new Vector3(0, Rng.Next(2) * 180, 0) };
		s.AddChild(Util.Box(new Vector3(0.95f, 0.5f, 0.55f), new Color("#8d6e63"), new Vector3(0, 0.25f, 0)));
		s.AddChild(Util.Box(new Vector3(1.0f, 0.06f, 0.6f), new Color("#a1887f"), new Vector3(0, 0.53f, 0)));
		for (int i = 0; i < 4; i++)                                                                                             // crunchy golden chicharon
			s.AddChild(Util.Box(new Vector3(0.2f, 0.12f + Rf() * 0.06f, 0.2f), new Color("#d4902a"),
				new Vector3(-0.33f + i * 0.22f, 0.62f, (Rf() - 0.5f) * 0.1f)));
		foreach (int sx in new[] { -1, 1 })
			s.AddChild(Util.Box(new Vector3(0.05f, 1.1f, 0.05f), new Color("#6d4c41"), new Vector3(sx * 0.45f, 0.55f, -0.25f)));
		s.AddChild(Util.Box(new Vector3(1.1f, 0.06f, 0.7f), new Color("#e53935"), new Vector3(0, 1.1f, -0.1f)));                // red canopy
		s.AddChild(Util.Box(new Vector3(0.5f, 0.2f, 0.03f), new Color("#fff59d"), new Vector3(0, 0.85f, -0.3f)));               // price sign
		return s;
	}

	static void Wheels(Node3D v, float[] xs, float halfW, float radius)
	{
		foreach (float x in xs)
			foreach (int sz in (halfW > 0.01f ? new[] { -1, 1 } : new[] { 0 }))
			{
				var w = Util.Cyl(radius, radius, 0.1f, new Color("#1c1c1c"), new Vector3(x, radius, sz * halfW), 12);
				w.RotationDegrees = new Vector3(90, 0, 0);
				v.AddChild(w);
			}
	}

	/// <summary>Colorful Cebu jeepney. f = travel direction (+1 / -1), front is on the +f side.</summary>
	static Node3D MakeJeepney(float f)
	{
		Color[] bodies = { new("#d32f2f"), new("#1565c0"), new("#2e7d32"), new("#f9a825"), new("#6a1b9a"), new("#00838f") };
		Color[] accents = { Colors.White, new("#ffd54f"), new("#ff7043"), new("#80deea") };
		Color body = bodies[Rng.Next(bodies.Length)];
		Color accent = accents[Rng.Next(accents.Length)];

		var j = new Node3D();
		j.AddChild(Util.Box(new Vector3(3.0f, 0.4f, 0.85f), body, new Vector3(0, 0.3f, 0)));                            // chassis + hood
		j.AddChild(Util.Box(new Vector3(3.08f, 0.08f, 0.93f), accent, new Vector3(0, 0.38f, 0)));                       // painted stripe
		j.AddChild(Util.Box(new Vector3(2.3f, 0.35f, 0.83f), body, new Vector3(-f * 0.35f, 0.675f, 0)));                // passenger cabin
		j.AddChild(Util.Box(new Vector3(2.36f, 0.2f, 0.9f), new Color("#263238"), new Vector3(-f * 0.35f, 0.72f, 0))); // window band
		j.AddChild(Util.Box(new Vector3(2.4f, 0.08f, 0.9f), new Color("#e0e0e0"), new Vector3(-f * 0.35f, 0.9f, 0)));   // silver roof
		j.AddChild(Util.Box(new Vector3(0.04f, 0.22f, 0.5f), new Color("#cfd8dc"), new Vector3(f * 1.57f, 0.3f, 0)));   // chrome grille
		j.AddChild(Util.Box(new Vector3(0.04f, 0.08f, 0.1f), new Color("#fff59d"), new Vector3(f * 1.57f, 0.4f, -0.28f)));
		j.AddChild(Util.Box(new Vector3(0.04f, 0.08f, 0.1f), new Color("#fff59d"), new Vector3(f * 1.57f, 0.4f, 0.28f)));
		if (Rng.NextDouble() < 0.5)
			j.AddChild(Util.Box(new Vector3(1.0f, 0.15f, 0.5f), new Color("#8d6e63"), new Vector3(-f * 0.5f, 1.0f, 0))); // luggage on the roof
		j.AddChild(Util.Box(new Vector3(0.1f, 0.14f, 0.14f), new Color("#eceff1"), new Vector3(f * 1.3f, 0.57f, 0)));   // hood ornament

		Wheels(j, new[] { f * 1.0f, -f * 0.95f }, 0.46f, 0.2f);
		return j;
	}

	/// <summary>Motorcycle (habal-habal) with a helmeted rider, sometimes a passenger too.</summary>
	static Node3D MakeMotorcycle(float f)
	{
		Color[] bikes = { new("#c62828"), new("#1565c0"), new("#212121"), new("#ef6c00") };
		Color[] shirts = { new("#e74c3c"), new("#3498db"), new("#f1c40f"), new("#2ecc71"), new("#ecf0f1") };
		Color[] helmets = { new("#ffffff"), new("#ffeb3b"), new("#e53935"), new("#212121"), new("#29b6f6") };
		Color bike = bikes[Rng.Next(bikes.Length)];

		var m = new Node3D();
		m.AddChild(Util.Box(new Vector3(0.9f, 0.12f, 0.14f), bike, new Vector3(0, 0.32f, 0)));
		m.AddChild(Util.Box(new Vector3(0.3f, 0.2f, 0.24f), bike, new Vector3(f * 0.15f, 0.45f, 0)));
		m.AddChild(Util.Box(new Vector3(0.5f, 0.08f, 0.22f), new Color("#212121"), new Vector3(-f * 0.2f, 0.42f, 0)));
		m.AddChild(Util.Box(new Vector3(0.06f, 0.06f, 0.45f), new Color("#424242"), new Vector3(f * 0.35f, 0.65f, 0)));
		m.AddChild(Util.Box(new Vector3(0.06f, 0.4f, 0.06f), new Color("#b0bec5"), new Vector3(f * 0.4f, 0.4f, 0)));
		m.AddChild(Util.Box(new Vector3(0.06f, 0.1f, 0.12f), new Color("#fff59d"), new Vector3(f * 0.43f, 0.58f, 0)));

		// rider
		m.AddChild(Util.Box(new Vector3(0.22f, 0.38f, 0.3f), shirts[Rng.Next(shirts.Length)], new Vector3(f * 0.0f, 0.72f, 0)));
		m.AddChild(Util.Box(new Vector3(0.26f, 0.26f, 0.26f), helmets[Rng.Next(helmets.Length)], new Vector3(f * 0.02f, 1.05f, 0)));

		// optional passenger on the back
		if (Rng.NextDouble() < 0.35)
		{
			m.AddChild(Util.Box(new Vector3(0.2f, 0.36f, 0.28f), shirts[Rng.Next(shirts.Length)], new Vector3(-f * 0.25f, 0.7f, 0)));
			m.AddChild(Util.Box(new Vector3(0.24f, 0.24f, 0.24f), helmets[Rng.Next(helmets.Length)], new Vector3(-f * 0.24f, 1.01f, 0)));
		}

		Wheels(m, new[] { f * 0.38f, -f * 0.38f }, 0.0f, 0.2f);
		return m;
	}

	/// <summary>Motorcycle with a colorful enclosed sidecar: the classic Philippine tricycle.</summary>
	static Node3D MakeTricycle(float f)
	{
		Color[] cabs = { new("#43a047"), new("#fb8c00"), new("#e53935"), new("#1e88e5"), new("#fdd835") };
		Color cab = cabs[Rng.Next(cabs.Length)];

		var t = MakeMotorcycle(f);
		t.AddChild(Util.Box(new Vector3(0.9f, 0.08f, 0.42f), new Color("#424242"), new Vector3(-f * 0.1f, 0.2f, 0.36f)));   // sidecar chassis
		t.AddChild(Util.Box(new Vector3(0.8f, 0.4f, 0.4f), cab, new Vector3(-f * 0.1f, 0.44f, 0.36f)));                      // passenger cab
		t.AddChild(Util.Box(new Vector3(0.86f, 0.14f, 0.46f), new Color("#263238"), new Vector3(-f * 0.1f, 0.5f, 0.36f)));   // windows
		t.AddChild(Util.Box(new Vector3(0.95f, 0.06f, 0.5f), cab, new Vector3(-f * 0.1f, 0.72f, 0.36f)));                    // roof

		var wheel = Util.Cyl(0.17f, 0.17f, 0.1f, new Color("#1c1c1c"), new Vector3(-f * 0.1f, 0.17f, 0.58f), 12);
		wheel.RotationDegrees = new Vector3(90, 0, 0);
		t.AddChild(wheel);
		return t;
	}

	static Node3D MakeCar(float f, bool taxi)
	{
		Color[] palette = { new("#ecf0f1"), new("#c0392b"), new("#2980b9"), new("#95a5a6"), new("#34495e") };
		Color body = taxi ? new Color("#f9d71c") : palette[Rng.Next(palette.Length)];

		var c = new Node3D();
		c.AddChild(Util.Box(new Vector3(1.3f, 0.4f, 0.7f), body, new Vector3(0, 0.28f, 0)));
		c.AddChild(Util.Box(new Vector3(0.7f, 0.3f, 0.62f), new Color("#2c3e50"), new Vector3(-f * 0.1f, 0.62f, 0)));
		c.AddChild(Util.Box(new Vector3(0.65f, 0.06f, 0.6f), body, new Vector3(-f * 0.1f, 0.79f, 0)));
		if (taxi) c.AddChild(Util.Box(new Vector3(0.25f, 0.1f, 0.15f), Colors.White, new Vector3(-f * 0.1f, 0.87f, 0)));
		Wheels(c, new[] { f * 0.42f, -f * 0.42f }, 0.36f, 0.16f);
		return c;
	}

	static Node3D MakeTruck(float f)
	{
		Color[] palette = { new("#f57c00"), new("#546e7a"), new("#7cb342"), new("#8d6e63") };
		Color body = palette[Rng.Next(palette.Length)];

		var t = new Node3D();
		t.AddChild(Util.Box(new Vector3(1.7f, 0.55f, 0.78f), body, new Vector3(-f * 0.3f, 0.45f, 0)));            // cargo box
		t.AddChild(Util.Box(new Vector3(0.65f, 0.5f, 0.76f), new Color("#eceff1"), new Vector3(f * 0.85f, 0.4f, 0)));
		t.AddChild(Util.Box(new Vector3(0.05f, 0.2f, 0.6f), new Color("#263238"), new Vector3(f * 1.15f, 0.5f, 0)));
		Wheels(t, new[] { f * 0.85f, -f * 0.3f, -f * 0.85f }, 0.4f, 0.2f);
		return t;
	}

	// =====================================================================
	//  ROAD WORKS: chevron signs, striped bollards, caution tape, gravel, workers
	// =====================================================================

	static float Rf() => (float)Rng.NextDouble();

	void BuildConstruction()
	{
		// freshly dug dirt strip and gravel patches
		AddChild(Util.Box(new Vector3(2 * MaxX + 4, 0.03f, 0.45f), new Color("#6b5640"), new Vector3(0, 0.015f, 0)));
		Color[] gravel = { new("#9e9e9e"), new("#8a8a8a"), new("#a7a193") };
		for (int i = 0; i < 10; i++)
			AddChild(Util.Box(new Vector3(0.5f + Rf() * 0.4f, 0.03f, 0.3f + Rf() * 0.2f),
				gravel[Rng.Next(gravel.Length)],
				new Vector3(-MaxX + Rf() * 2 * MaxX, 0.02f + Rf() * 0.01f, (Rf() - 0.5f) * 0.7f)));

		int gap = Rng.Next(-MaxX + 1, MaxX); // a 3-wide gate that always stays open
		var kinds = new Dictionary<int, int>();
		for (int x = -MaxX - 3; x <= MaxX + 3; x++)
		{
			if (clearRight && x > MaxX) continue; // landmark zone
			if (KeepFree != null && KeepFree.Contains(x)) continue; // keep the jam gap open
			bool scenery = Math.Abs(x) > MaxX;
			if (!scenery && Math.Abs(x - gap) <= 1) continue;
			if (Rng.NextDouble() > (scenery ? 0.5 : 0.6)) continue;
			double r = Rng.NextDouble();
			kinds[x] = r < 0.28 ? 0 : r < 0.56 ? 1 : r < 0.72 ? 2 : r < 0.88 ? 3 : 4;
			if (!scenery) Blocked.Add(x);
		}

		foreach (var kv in kinds)
		{
			Node3D prop = kv.Value switch
			{
				0 => MakeChevronSign(),
				1 => MakeBollard(),
				2 => MakeGravelPile(),
				3 => MakeWorker(),
				_ => MakeCementBags()
			};
			prop.Position = new Vector3(kv.Key, 0, 0);
			AddChild(prop);
			if (Math.Abs(kv.Key) <= MaxX) AddWarningGlow(kv.Key, new Color("#ff6a00")); // glowing "do not enter" marker

			// yellow caution tape between neighboring bollards
			if (kv.Value == 1 && kinds.TryGetValue(kv.Key + 1, out int next) && next == 1)
			{
				AddChild(Util.Box(new Vector3(1f, 0.03f, 0.02f), new Color("#fdd835"), new Vector3(kv.Key + 0.5f, 0.6f, 0)));
				AddChild(Util.Box(new Vector3(1f, 0.03f, 0.02f), new Color("#fdd835"), new Vector3(kv.Key + 0.5f, 0.8f, 0)));
			}
		}
	}

	/// <summary>Yellow board with a black double-headed arrow on a sawhorse stand.</summary>
	static Node3D MakeChevronSign()
	{
		var s = new Node3D { RotationDegrees = new Vector3(0, Rng.Next(2) * 180, 0) };
		Color metal = new("#9e9e9e");
		foreach (int sx in new[] { -1, 1 })
		{
			// legs are thinner than the board so none of their faces line up with it
			s.AddChild(Util.Box(new Vector3(0.05f, 0.62f, 0.03f), metal, new Vector3(sx * 0.36f, 0.31f, 0)));
			var brace = Util.Box(new Vector3(0.04f, 0.5f, 0.04f), metal, new Vector3(sx * 0.36f, 0.25f, 0.18f));
			brace.RotationDegrees = new Vector3(-30, 0, 0);
			s.AddChild(brace);
		}
		s.AddChild(Util.Box(new Vector3(0.9f, 0.42f, 0.05f), new Color("#f7c600"), new Vector3(0, 0.78f, 0)));

		// black arrow on BOTH faces, clear of the board surface
		foreach (int face in new[] { -1, 1 })
		{
			float z = face * 0.04f;
			s.AddChild(Util.Box(new Vector3(0.62f, 0.08f, 0.03f), Colors.Black, new Vector3(0, 0.78f, z)));
			foreach (int sx in new[] { -1, 1 })
				foreach (int sy in new[] { -1, 1 })
				{
					var head = Util.Box(new Vector3(0.2f, 0.07f, 0.03f), Colors.Black, new Vector3(sx * 0.28f, 0.78f + sy * 0.07f, z));
					head.RotationDegrees = new Vector3(0, 0, -45 * sx * sy);
					s.AddChild(head);
				}
		}
		return s;
	}

	static Node3D MakeBollard()
	{
		var b = new Node3D();
		for (int i = 0; i < 3; i++)
			b.AddChild(Util.Cyl(0.08f, 0.08f, 0.3f, i % 2 == 0 ? Colors.White : new Color("#212121"), new Vector3(0, 0.15f + i * 0.3f, 0), 10));
		b.AddChild(Util.Cyl(0f, 0.07f, 0.1f, new Color("#ff6f00"), new Vector3(0, 0.95f, 0), 10));
		return b;
	}

	static Node3D MakeGravelPile()
	{
		var p = new Node3D();
		Color c = Rng.NextDouble() < 0.5 ? new Color("#9e9e9e") : new Color("#7b5e3b");
		p.AddChild(Util.Cyl(0.02f, 0.42f, 0.4f, c, new Vector3(0, 0.2f, 0), 9));
		p.AddChild(Util.Box(new Vector3(0.18f, 0.1f, 0.18f), new Color("#757575"), new Vector3(0.3f, 0.05f, 0.2f)));
		return p;
	}

	static Node3D MakeCementBags()
	{
		var c = new Node3D { RotationDegrees = new Vector3(0, Rng.Next(-15, 15), 0) };
		Color bag = new("#cfd8dc");
		c.AddChild(Util.Box(new Vector3(0.42f, 0.15f, 0.3f), bag, new Vector3(-0.22f, 0.075f, 0)));
		c.AddChild(Util.Box(new Vector3(0.42f, 0.15f, 0.3f), bag, new Vector3(0.22f, 0.075f, 0)));
		c.AddChild(Util.Box(new Vector3(0.42f, 0.15f, 0.3f), new Color("#b0bec5"), new Vector3(0, 0.225f, 0)));
		return c;
	}

	Node3D MakeWorker()
	{
		var w = MakePerson(false, false).Root;
		w.RotationDegrees = new Vector3(0, Rng.Next(4) * 90, 0);
		w.AddChild(Util.Box(new Vector3(0.5f, 0.32f, 0.33f), new Color("#ff6f00"), new Vector3(0, 0.55f, 0)));    // orange safety vest
		w.AddChild(Util.Box(new Vector3(0.36f, 0.12f, 0.36f), new Color("#ffd600"), new Vector3(0, 1.16f, 0)));    // yellow hard hat
		w.AddChild(Util.Box(new Vector3(0.04f, 0.9f, 0.04f), new Color("#6d4c41"), new Vector3(0.38f, 0.45f, -0.15f))); // shovel
		w.AddChild(Util.Box(new Vector3(0.2f, 0.04f, 0.22f), new Color("#90a4ae"), new Vector3(0.38f, 0.02f, -0.22f)));
		return w;
	}

	// =====================================================================
	//  CANAL (ESTERO): gray-blue water, concrete wall, shanties, garbage, rideable rafts
	// =====================================================================

	void BuildCanal()
	{
		bool clean = City.Current.CleanWater;
		float dir = Rng.Next(2) == 0 ? -1 : 1;
		speed = dir * (1f + Rf() * 1.4f);
		int n = 7;
		float spacing = Span / n, offset = Rf() * spacing;

		for (int i = 0; i < n; i++)
		{
			double q = Rng.NextDouble();
			Node3D raft;
			float len;
			if (clean) { len = 2.4f + Rf() * 0.8f; raft = MakeBambooRaft(len); }          // bamboo raft
			else if (q < 0.75) { len = 2f + Rf() * 1.5f; raft = MakeTrashRaft(len); }     // clump of floating garbage
			else { len = 1.8f; raft = MakeScavengerRaft(len); }                            // blue raft with a trash collector

			raft.Position = new Vector3(-Span / 2 + offset + i * spacing, 0, 0);
			AddChild(raft);
			movers.Add(new Mover { Node = raft, HalfLen = len / 2, Speed = speed, Kind = "trash" });
		}

		Color[] litter = { new("#eceff1"), new("#4fa3d9"), new("#d9c9a3"), new("#c0392b"), new("#37474f"), new("#7cb342") };
		Color[] shacks = { new("#a1887f"), new("#4db6ac"), new("#ffb74d"), new("#90a4ae"), new("#e57373"), new("#fff176") };

		// loose litter bobbing in the middle of the water (dirty canals only)
		if (!clean)
			for (int i = 0; i < 10; i++)
			{
				var bit = Util.Box(new Vector3(0.25f + Rf() * 0.25f, 0.04f, 0.2f + Rf() * 0.2f),
					litter[Rng.Next(litter.Length)], new Vector3(-16f + Rf() * 32f, -0.17f + Rf() * 0.02f, (Rf() - 0.5f) * 0.7f));
				bit.RotationDegrees = new Vector3(0, Rng.Next(90), 0);
				AddChild(bit);
			}

		foreach (int s in new[] { -1, 1 })
		{
			if (clean) { BuildBeachBank(s); continue; }

			// dense garbage mat packed against the bank
			for (int i = 0; i < 14; i++)
			{
				var bit = Util.Box(new Vector3(0.25f + Rf() * 0.35f, 0.05f + Rf() * 0.08f, 0.2f + Rf() * 0.3f),
					litter[Rng.Next(litter.Length)], new Vector3(s * (MaxX + 0.15f + Rf() * 1.3f), -0.15f, (Rf() - 0.5f) * 0.8f));
				bit.RotationDegrees = new Vector3(0, Rng.Next(90), 0);
				AddChild(bit);
			}

			// concrete retaining wall, land behind it
			AddChild(Util.Box(new Vector3(1f, 1f, 1f), new Color("#c9c3b0"), new Vector3(s * (MaxX + 2), 0.3f, 0)));
			AddChild(Util.Box(new Vector3(1f, 0.08f, 1f), new Color("#b5ae9a"), new Vector3(s * (MaxX + 2), 0.84f, 0)));
			AddChild(Util.Box(new Vector3(12f, 0.2f, 1f), new Color("#a39a86"), new Vector3(s * (MaxX + 8.5f), -0.1f, 0)));

			// garbage heap on the bank
			for (int i = 0; i < 9; i++)
			{
				float hh = 0.1f + Rf() * 0.25f;
				AddChild(Util.Box(new Vector3(0.2f + Rf() * 0.25f, hh, 0.2f + Rf() * 0.25f),
					litter[Rng.Next(litter.Length)], new Vector3(s * (MaxX + 3f + (Rf() - 0.5f) * 0.9f), hh / 2, (Rf() - 0.5f) * 0.8f)));
			}

			// informal settlers' shanties with tin roofs and blue tarps
			for (int x = MaxX + 4; x <= MaxX + 13; x++)
			{
				if (Rng.NextDouble() > 0.75) continue;
				float h = 0.7f + Rf() * 0.7f;
				var shack = new Node3D { Position = new Vector3(s * x, 0, 0) };
				shack.AddChild(Util.Box(new Vector3(0.9f, h, 0.9f), shacks[Rng.Next(shacks.Length)], new Vector3(0, h / 2, 0)));
				shack.AddChild(Util.Box(new Vector3(1.05f, 0.07f, 1.05f),
					Rng.NextDouble() < 0.3 ? new Color("#29b6f6") : new Color("#78909c"), new Vector3(0, h + 0.035f, 0)));
				AddChild(shack);
			}
		}
	}

	/// <summary>Clean sea/river bank: stone edge, sand, palms and beach huts.</summary>
	void BuildBeachBank(int s)
	{
		AddChild(Util.Box(new Vector3(1f, 0.5f, 1f), new Color("#b0a58c"), new Vector3(s * (MaxX + 2), 0.05f, 0)));
		AddChild(Util.Box(new Vector3(12f, 0.2f, 1f), new Color("#eadba8"), new Vector3(s * (MaxX + 8.5f), -0.1f, 0)));

		for (int x = MaxX + 4; x <= MaxX + 13; x++)
		{
			double r = Rng.NextDouble();
			Node3D prop = r < 0.35 ? MakePalm() : r < 0.5 ? MakeBahayKubo() : null;
			if (prop == null) continue;
			prop.Position = new Vector3(s * x, 0, 0);
			AddChild(prop);
		}
	}

	static Node3D MakeBambooRaft(float len)
	{
		var raft = new Node3D();
		for (int i = 0; i < 5; i++)
		{
			var pole = Util.Cyl(0.08f, 0.08f, len, new Color(i % 2 == 0 ? "#c9b458" : "#b5a043"),
				new Vector3(0, -0.08f, -0.32f + i * 0.16f), 8);
			pole.RotationDegrees = new Vector3(0, 0, 90);
			raft.AddChild(pole);
		}
		foreach (int sx in new[] { -1, 1 })                                  // rope lashings across the poles
			raft.AddChild(Util.Box(new Vector3(0.1f, 0.04f, 0.9f), new Color("#6d4c41"), new Vector3(sx * (len / 2 - 0.4f), 0f, 0)));
		return raft;
	}

	/// <summary>Blue inflatable raft carrying sacks of trash and a man picking through the garbage.</summary>
	Node3D MakeScavengerRaft(float len)
	{
		var raft = new Node3D();
		raft.AddChild(Util.Box(new Vector3(len, 0.12f, 0.8f), new Color("#2f4fc9"), new Vector3(0, -0.06f, 0)));
		raft.AddChild(Util.Box(new Vector3(len + 0.04f, 0.1f, 0.12f), new Color("#233ea8"), new Vector3(0, 0f, -0.4f)));
		raft.AddChild(Util.Box(new Vector3(len + 0.04f, 0.1f, 0.12f), new Color("#233ea8"), new Vector3(0, 0f, 0.4f)));
		for (int i = 0; i < 3; i++)
			raft.AddChild(Util.Box(new Vector3(0.3f, 0.22f, 0.3f), new Color("#d7ccc8"),
				new Vector3(-len / 2 + 0.35f + i * 0.25f, 0.11f, i % 2 == 0 ? -0.12f : 0.12f)));

		var scavenger = MakePerson(false, false).Root;
		scavenger.Position = new Vector3(len / 2 - 0.4f, 0, 0);
		scavenger.RotationDegrees = new Vector3(-35, Rng.Next(2) * 180, 0); // bending over
		raft.AddChild(scavenger);
		return raft;
	}

	/// <summary>A rideable clump of plastic bags, sacks, styrofoam, bottles and tires.</summary>
	static Node3D MakeTrashRaft(float len)
	{
		Color[] pal =
		{
			new("#eceff1"), new("#4fa3d9"), new("#d9c9a3"), new("#f5f5f5"),
			new("#c0392b"), new("#37474f"), new("#e5b94c"), new("#7cb342")
		};
		Color[] bottles = { new("#81d4fa"), new("#a5d6a7"), new("#ffcc80"), new("#e0e0e0") };

		var raft = new Node3D();
		int pieces = Mathf.CeilToInt(len / 0.6f);
		float w = len / pieces;

		for (int i = 0; i < pieces; i++)
		{
			float h = 0.12f + (float)Rng.NextDouble() * 0.1f;
			float x = -len / 2 + w * (i + 0.5f);
			raft.AddChild(Util.Box(new Vector3(w, h, 0.55f + (float)Rng.NextDouble() * 0.3f),
				pal[Rng.Next(pal.Length)], new Vector3(x, -h / 2 - (i % 2) * 0.012f, 0)));

			double q = Rng.NextDouble();
			if (q < 0.35)
				raft.AddChild(Util.Cyl(0.05f, 0.05f, 0.22f, bottles[Rng.Next(bottles.Length)],
					new Vector3(x + (float)(Rng.NextDouble() - 0.5) * 0.3f, 0.1f, (float)(Rng.NextDouble() - 0.5) * 0.4f), 8)); // bottle
			else if (q < 0.55)
				raft.AddChild(Util.Box(new Vector3(0.22f, 0.16f, 0.22f), new Color("#263238"), new Vector3(x, 0.08f, 0)));     // garbage bag
			else if (q < 0.65)
				raft.AddChild(Util.Cyl(0.3f, 0.3f, 0.1f, new Color("#212121"), new Vector3(x, 0.05f, 0), 16));                  // old tire
		}
		return raft;
	}

	// =====================================================================
	//  Per-frame logic
	// =====================================================================

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		time += dt;

		foreach (var m in movers)
		{
			if (m.ReverseTimer > 0f)
			{
				m.ReverseTimer -= dt;
				if (m.ReverseTimer <= 0f) m.Speed = m.NormalSpeed;
			}
			var p = m.Node.Position;
			p.X += m.Speed * dt;
			if (p.X > Span / 2) p.X -= Span;
			else if (p.X < -Span / 2) p.X += Span;
			m.Node.Position = p;
		}

		AnimatePeople();
		AnimateEffects(dt);
	}

	void AnimateEffects(float dt)
	{
		foreach (var g in glows)
		{
			float v = 0.5f + 0.5f * Mathf.Sin(time * g.Speed + g.Phase);
			g.Mat.EmissionEnergyMultiplier = g.Base + g.Amp * v;
			if (g.Alpha > 0f)
			{
				var col = g.Mat.AlbedoColor;
				col.A = g.Alpha * (0.6f + 0.4f * v);
				g.Mat.AlbedoColor = col;
			}
		}

		foreach (var sm in smokes)
		{
			float age = (time * 0.5f + sm.Phase) % 1f;
			sm.Node.Position = sm.Origin + new Vector3(0, 0.6f + age * 1.6f, 0);
			float sc = 0.5f + age * 1.2f;
			sm.Node.Scale = new Vector3(sc, sc, sc);
			var col = sm.Mat.AlbedoColor;
			col.A = 0.55f * (1f - age);
			sm.Mat.AlbedoColor = col;
		}

		foreach (var h in honks)
		{
			if (h.Show > 0f)
			{
				h.Show -= dt;
				if (h.Show <= 0f) h.Label.Visible = false;
			}
			else
			{
				h.Timer -= dt;
				if (h.Timer <= 0f)
				{
					h.Label.Text = BeepWords[Rng.Next(BeepWords.Length)];
					h.Label.Visible = true;
					h.Show = 0.8f;
					h.Timer = 2f + Rf() * 5f;
				}
			}
		}
	}

	void AnimatePeople()
	{
		foreach (var p in people)
		{
			float s = Mathf.Sin(time * 8f + p.Phase);
			if (p.Walking)
			{
				p.LegL.RotationDegrees = new Vector3(s * 35f, 0, 0);
				p.LegR.RotationDegrees = new Vector3(-s * 35f, 0, 0);
				p.ArmL.RotationDegrees = new Vector3(-s * 30f, 0, 0);
				p.ArmR.RotationDegrees = new Vector3(s * 30f, 0, 0);
			}
			if (p.Waving)
				p.ArmR.RotationDegrees = new Vector3(0, 0, 150f + Mathf.Sin(time * 6f + p.Phase) * 18f); // flagging a jeepney
		}
	}

	/// <summary>Road lanes: what is overlapping this x position? Returns its kind, or null if clear.</summary>
	public string HitBy(float px)
	{
		foreach (var m in movers)
		{
			if (Math.Abs(m.Node.Position.X - px) < m.HalfLen + 0.3f) return m.Kind;
		}
		return null;
	}

	public bool HasPersonNear(float px, float distance)
	{
		foreach (var person in people)
			if (Math.Abs(person.Root.Position.X - px) <= distance) return true;
		return false;
	}

	public string NearbyVehicleKind(float px, float distance)
	{
		foreach (var m in movers)
			if (Math.Abs(m.Node.Position.X - px) <= m.HalfLen + distance) return m.Kind;
		return null;
	}

	public bool ReverseTricycle()
	{
		var tricycles = movers.FindAll(m => m.Kind == "tricycle" && m.ReverseTimer <= 0f);
		if (tricycles.Count == 0) return false;

		var tricycle = tricycles[Rng.Next(tricycles.Count)];
		tricycle.Speed = -tricycle.NormalSpeed * 1.35f;
		tricycle.ReverseTimer = 3f;
		return true;
	}

	/// <summary>
	/// Will any vehicle in this lane come near px between t0 and t1 seconds from now?
	/// Pedestrians use this to find a safe gap.
	/// </summary>
	public bool IsClear(float px, float t0, float t1)
	{
		foreach (var m in movers)
		{
			float reach = m.HalfLen + 0.5f;
			for (int k = 0; k <= 6; k++)
			{
				float t = t0 + (t1 - t0) * k / 6f;
				float d = m.Node.Position.X + m.Speed * t - px;
				d = Mathf.PosMod(d + Span / 2, Span) - Span / 2; // account for wrap-around
				if (Math.Abs(d) < reach) return false;
			}
		}
		return true;
	}

	/// <summary>Canal lanes: is there floating trash under this x position? Returns its velocity.</summary>
	public bool TryGetRaftVelocity(float px, out float velocity)
	{
		foreach (var m in movers)
		{
			if (Math.Abs(m.Node.Position.X - px) < m.HalfLen + 0.3f)
			{
				velocity = m.Speed;
				return true;
			}
		}
		velocity = 0;
		return false;
	}
}
