using Godot;
using System;

namespace CrossyRoadBase;

public enum VKind { Jeepney, Car, Taxi, Motorcycle, Tricycle, Truck }

/// <summary>A place in Cebu. A different one is picked at random every run.</summary>
public class City
{
    public string Name, Tagline;
    public Color Sky, SidewalkA, SidewalkB, Road, Water;
    public bool CleanWater;                                          // sea/river water: bamboo rafts and bangkas instead of trash

    // how likely each lane type is
    public float WGrass = 0.28f, WRoad = 0.46f, WConstruction = 0.10f, WRiver = 0.16f;
    public float JamChance = 0.10f, AccidentChance = 0.10f;          // fractions of road lanes
    public float TrafficDensity = 1f;                                 // multiplies vehicles per lane

    public float[] VehicleWeights = { 20, 15.6f, 8.4f, 24, 24, 8 };  // jeepney, car, taxi, motorcycle, tricycle, truck
    public float[] PropWeights = { 45, 15, 25, 15, 0, 0, 0, 0, 4, 3 }; // person, jeep stop, bin, kubo, palm, sari-sari, furniture, chicharon, traffic sign, no-parking scene
    public string[] Landmarks = Array.Empty<string>();

    public string Vendor = "taho";                                   // what the street vendor sells: taho, chicharon or buko
    public string ItemName => Vendor == "chicharon" ? "Chicharon" : Vendor == "buko" ? "Buko juice" : "Taho";
    public string VendorTitle => Vendor == "chicharon" ? "chicharon vendor" : Vendor == "buko" ? "buko vendor" : "taho man";
    public string GiftText => Vendor == "chicharon" ? "You got some crunchy chicharon!"
                            : Vendor == "buko" ? "You got a cold buko juice!"
                            : "You got a cup of taho!";

    public static City Current;
    static City last;

    public static readonly City[] All =
    {
        new City
        {
            Name = "Cebu City", Tagline = "The Queen City of the South",
            Sky = new Color("#a8dcf0"), SidewalkA = new Color("#999795"), SidewalkB = new Color("#8b8987"),
            Road = new Color("#4a4a52"), Water = new Color("#6a8794"), CleanWater = false,
            Landmarks = new[] { "basilica", "cathedral", "sm", "ayala", "usc" }
        },
        new City
        {
            Name = "Mandaue City", Tagline = "Furniture Capital of the Philippines",
            Sky = new Color("#b7d4e6"), SidewalkA = new Color("#999999"), SidewalkB = new Color("#898989"),
            Road = new Color("#44444c"), Water = new Color("#5d7280"), CleanWater = false,
            WGrass = 0.26f, WRoad = 0.42f, WConstruction = 0.18f, WRiver = 0.14f,
            JamChance = 0.12f, AccidentChance = 0.10f,
            VehicleWeights = new float[] { 14, 14, 6, 18, 14, 22 },
            PropWeights = new float[] { 30, 10, 20, 5, 0, 10, 25, 0, 6, 5 },
            Landmarks = new[] { "furniture", "parkmall" }
        },
        new City
        {
            Name = "Lapu-Lapu City", Tagline = "Home of the Mactan Shrine", Vendor = "buko",
            Sky = new Color("#8fd3f4"), SidewalkA = new Color("#9c9b97"), SidewalkB = new Color("#8c8b87"),
            Road = new Color("#55555c"), Water = new Color("#2bb7c9"), CleanWater = true,
            WGrass = 0.34f, WRoad = 0.38f, WConstruction = 0.08f, WRiver = 0.20f,
            JamChance = 0.06f, AccidentChance = 0.06f, TrafficDensity = 0.9f,
            VehicleWeights = new float[] { 10, 12, 6, 28, 38, 6 },
            PropWeights = new float[] { 25, 8, 10, 12, 30, 15, 0, 0, 4, 3 },
            Landmarks = new[] { "shrine", "airport" }
        },
        new City
        {
            Name = "Carcar City", Tagline = "Heritage town and chicharon capital", Vendor = "chicharon",
            Sky = new Color("#bfe0f0"), SidewalkA = new Color("#9d9990"), SidewalkB = new Color("#8d8980"),
            Road = new Color("#5a5a5f"), Water = new Color("#5aa9b8"), CleanWater = true,
            WGrass = 0.38f, WRoad = 0.40f, WConstruction = 0.08f, WRiver = 0.14f,
            JamChance = 0.05f, AccidentChance = 0.04f, TrafficDensity = 0.8f,
            VehicleWeights = new float[] { 10, 12, 4, 22, 36, 6 },
            PropWeights = new float[] { 30, 10, 10, 5, 5, 12, 0, 28, 4, 3 },
            Landmarks = new[] { "carcar_church", "rotunda" }
        },
        new City
        {
            Name = "Oslob", Tagline = "Swim with the whale sharks", Vendor = "buko",
            Sky = new Color("#7fd0f5"), SidewalkA = new Color("#9c9993"), SidewalkB = new Color("#8c8984"),
            Road = new Color("#58585e"), Water = new Color("#1fb5d0"), CleanWater = true,
            WGrass = 0.40f, WRoad = 0.32f, WConstruction = 0.06f, WRiver = 0.22f,
            JamChance = 0.04f, AccidentChance = 0.06f, TrafficDensity = 0.7f,
            VehicleWeights = new float[] { 6, 8, 2, 32, 44, 8 },
            PropWeights = new float[] { 22, 6, 8, 15, 30, 15, 0, 0, 4, 3 },
            Landmarks = new[] { "whale_shark", "oslob_church" }
        },
        new City
        {
            Name = "Moalboal", Tagline = "Sardine run and island dives", Vendor = "buko",
            Sky = new Color("#86d6f2"), SidewalkA = new Color("#9b9993"), SidewalkB = new Color("#8c8984"),
            Road = new Color("#5c5c62"), Water = new Color("#17b6c8"), CleanWater = true,
            WGrass = 0.42f, WRoad = 0.28f, WConstruction = 0.06f, WRiver = 0.24f,
            JamChance = 0.03f, AccidentChance = 0.05f, TrafficDensity = 0.6f,
            VehicleWeights = new float[] { 4, 6, 2, 36, 46, 6 },
            PropWeights = new float[] { 20, 4, 8, 22, 32, 14, 0, 0, 4, 3 },
            Landmarks = new[] { "dive_resort", "sardine_run" }
        },
        new City
        {
            Name = "Badian", Tagline = "Chase Kawasan Falls", Vendor = "buko",
            Sky = new Color("#8ed8e8"), SidewalkA = new Color("#9c9991"), SidewalkB = new Color("#8c8981"),
            Road = new Color("#5a5a60"), Water = new Color("#26c6da"), CleanWater = true,
            WGrass = 0.42f, WRoad = 0.28f, WConstruction = 0.08f, WRiver = 0.22f,
            JamChance = 0.03f, AccidentChance = 0.05f, TrafficDensity = 0.6f,
            VehicleWeights = new float[] { 6, 6, 2, 34, 44, 8 },
            PropWeights = new float[] { 22, 4, 8, 22, 28, 16, 0, 0, 4, 3 },
            Landmarks = new[] { "kawasan_falls", "badian_resort" }
        }
    };

    /// <summary>A random place, never the same one twice in a row.</summary>
    public static City PickNext(Random rng)
    {
        City next;
        do { next = All[rng.Next(All.Length)]; } while (next == last && All.Length > 1);
        last = next;
        return next;
    }
}
