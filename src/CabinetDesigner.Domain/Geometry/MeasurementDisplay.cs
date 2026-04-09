namespace CabinetDesigner.Domain.Geometry;

/// <summary>
/// The measurement system in use for a project or user session.
/// The domain stores everything in inches (decimal); this enum governs display only.
/// </summary>
public enum MeasurementSystem
{
    Imperial,
    Metric
}

/// <summary>Imperial display format variants.</summary>
public enum ImperialDisplayFormat
{
    /// <summary>Example: 36 1/2"</summary>
    FractionalInches,

    /// <summary>Example: 36.5"</summary>
    DecimalInches,

    /// <summary>Example: 3' 0 1/2"</summary>
    FeetAndInches
}

/// <summary>Named axis in 2D space.</summary>
public enum Axis2D { X, Y }

/// <summary>Named cardinal directions in 2D space.</summary>
public enum Direction2D { PositiveX, NegativeX, PositiveY, NegativeY }

/// <summary>
/// User display preferences for dimensions.
/// Passed to <see cref="IDimensionFormatter"/> — never used inside geometry value objects.
/// </summary>
public record DisplaySettings(
    MeasurementSystem System,
    ImperialDisplayFormat ImperialFormat = ImperialDisplayFormat.FractionalInches,
    int MetricDecimalPlaces = 1,
    int FractionDenominator = 16);

/// <summary>
/// Formats geometry value objects for user-visible display.
/// Implementation lives in Application or Infrastructure — never in Domain.
/// </summary>
public interface IDimensionFormatter
{
    string Format(Length length);
    string Format(Thickness thickness);
    string FormatCompact(Length length);
}
