using System.Runtime.InteropServices;

namespace OpenNDOF.Core.Com;

/// <summary>COM-visible Vector3D value object (Translation).</summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("740A7479-C7C1-44DA-8A84-B5DE63C78B32")]
[ProgId("TDxInput.Vector3D")]
public sealed class Vector3D : IVector3D
{
    public double X      { get; set; }
    public double Y      { get; set; }
    public double Z      { get; set; }
    public double Length
    {
        get => Math.Sqrt(X * X + Y * Y + Z * Z);
        set
        {
            double current = Length;
            if (current < 1e-10) return;
            double scale = value / current;
            X *= scale; Y *= scale; Z *= scale;
        }
    }

    internal void Set(double x, double y, double z) { X = x; Y = y; Z = z; }
}

/// <summary>COM-visible AngleAxis value object (Rotation).</summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("512A6C3E-3010-401B-8623-E413E2ACC138")]
[ProgId("TDxInput.AngleAxis")]
public sealed class AngleAxis : IAngleAxis
{
    public double X     { get; set; }
    public double Y     { get; set; }
    public double Z     { get; set; }
    public double Angle { get; set; }

    internal void Set(double x, double y, double z)
    {
        double len = Math.Sqrt(x * x + y * y + z * z);
        Angle = len;
        if (len > 1e-10) { X = x / len; Y = y / len; Z = z / len; }
        else             { X = 0; Y = 1; Z = 0; }
    }
}
