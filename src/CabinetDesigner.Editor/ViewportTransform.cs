using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Editor;

public sealed record ViewportTransform(
    decimal ScalePixelsPerInch,
    decimal OffsetXPixels,
    decimal OffsetYPixels)
{
    public static readonly ViewportTransform Default = new(10m, 0m, 0m);

    public double PixelsPerDip { get; init; } = 1.0;

    public Point2D ToWorld(double screenX, double screenY) =>
        new(((decimal)screenX - OffsetXPixels) / ScalePixelsPerInch,
            ((decimal)screenY - OffsetYPixels) / ScalePixelsPerInch);

    public (double X, double Y) ToScreen(Point2D world) =>
        ((double)(world.X * ScalePixelsPerInch + OffsetXPixels),
         (double)(world.Y * ScalePixelsPerInch + OffsetYPixels));

    public ViewportTransform Panned(decimal deltaXPixels, decimal deltaYPixels) =>
        this with
        {
            OffsetXPixels = OffsetXPixels + deltaXPixels,
            OffsetYPixels = OffsetYPixels + deltaYPixels
        };
}
