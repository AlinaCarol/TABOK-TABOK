using Godot;

namespace CrossyRoadBase;

public static class Util
{
    /// <summary>Makes a colored box mesh. Most of the game is built from these.</summary>
    public static MeshInstance3D Box(Vector3 size, Color color, Vector3 pos)
    {
        return new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
            Position = pos
        };
    }

    /// <summary>Makes a colored cylinder (or cone/pyramid when topR = 0).</summary>
    public static MeshInstance3D Cyl(float topR, float botR, float height, Color color, Vector3 pos, int segments = 16)
    {
        return new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = topR,
                BottomRadius = botR,
                Height = height,
                RadialSegments = segments,
                Rings = 1
            },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color },
            Position = pos
        };
    }

    /// <summary>
    /// A glowing box (emissive material). alpha &lt; 1 makes it a see-through light column.
    /// The material is returned so callers can pulse its EmissionEnergyMultiplier.
    /// </summary>
    public static MeshInstance3D Glow(Vector3 size, Color color, Vector3 pos, float alpha,
        out StandardMaterial3D mat, float energy = 3f)
    {
        mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(color.R, color.G, color.B, alpha),
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = energy
        };
        if (alpha < 1f) mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

        return new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = mat,
            Position = pos,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
    }
}
