# P1 — Geometry System Design

Source: `cabinet_architecture_playbook_windows_dotnet_v2.md` (Sections 11, 5, 7.4)

---

## 1. Goals

- Provide the foundational geometry and measurement layer for the entire cabinet design system
- Enforce dimensional correctness at compile time through strongly typed value objects
- Eliminate naked primitives (`double`, `decimal`, `float`) crossing domain boundaries
- Support shop-grade precision with deterministic, reproducible calculations
- Enable both imperial and metric workflows with a single canonical internal representation
- Keep the geometry layer UI-independent, serializable, and fully testable

---

## 2. Namespace & Project Location

```
CabinetDesigner.Domain.Geometry
```

Lives inside the `CabinetDesigner.Domain` project. No dependencies on Application, Presentation, Infrastructure, or Persistence layers.

---

## 3. Core Value Objects

All types are **immutable**, **equatable**, and implement `IEquatable<T>`. All are `readonly record struct` unless otherwise noted.

### 3.1 Length

The fundamental dimensional unit. Wraps a `decimal` value stored canonically in **inches** (internal truth).

```csharp
namespace CabinetDesigner.Domain.Geometry;

public readonly record struct Length : IComparable<Length>
{
    public decimal Inches { get; }

    private Length(decimal inches)
    {
        if (inches < 0)
            throw new ArgumentOutOfRangeException(nameof(inches), "Length cannot be negative.");
        Inches = inches;
    }

    // Factory methods
    public static Length FromInches(decimal inches) => new(inches);
    public static Length FromFeet(decimal feet) => new(feet * 12m);
    public static Length FromMillimeters(decimal mm) => new(mm / 25.4m);
    public static Length FromFractionalInches(int whole, int numerator, int denominator)
        => new(whole + (decimal)numerator / denominator);

    public static readonly Length Zero = new(0m);

    // Conversions (read-only projections)
    public decimal ToMillimeters() => Inches * 25.4m;
    public decimal ToFeet() => Inches / 12m;

    // Arithmetic operators
    public static Length operator +(Length a, Length b) => new(a.Inches + b.Inches);
    // IMPORTANT: Length - Length = Offset (NOT Length)
    public static Offset operator -(Length a, Length b)
        => Offset.FromInches(a.Inches - b.Inches);
    public static Length operator *(Length a, decimal scalar) => new(a.Inches * scalar);
    public static Length operator *(decimal scalar, Length a) => new(a.Inches * scalar);
    public static Length operator /(Length a, decimal scalar) => new(a.Inches / scalar);
    public static decimal operator /(Length a, Length b) => a.Inches / b.Inches;

    // Comparison
    public int CompareTo(Length other) => Inches.CompareTo(other.Inches);
    public static bool operator >(Length a, Length b) => a.Inches > b.Inches;
    public static bool operator <(Length a, Length b) => a.Inches < b.Inches;
    public static bool operator >=(Length a, Length b) => a.Inches >= b.Inches;
    public static bool operator <=(Length a, Length b) => a.Inches <= b.Inches;

    // Utility
    public Length Abs() => new(Math.Abs(Inches));
    public static Length Min(Length a, Length b) => a <= b ? a : b;
    public static Length Max(Length a, Length b) => a >= b ? a : b;

    public override string ToString() => $"{Inches}in";
}
```

**Key decisions:**
- `decimal` for canonical storage — avoids binary floating-point drift on authoritative dimensions
- Canonical unit is **inches** — the dominant shop unit; metric is a display/conversion concern
- Non-negative enforcement — lengths cannot be negative; signed offsets use `Offset` (see below)
- No display formatting on the type itself — formatting is a service concern (see Section 8)

### 3.2 Offset

A signed dimensional quantity for directional differences, deltas, and adjustments.

```csharp
public readonly record struct Offset : IComparable<Offset>
{
    public decimal Inches { get; }

    private Offset(decimal inches) => Inches = inches;

    public static Offset FromInches(decimal inches) => new(inches);
    public static Offset FromMillimeters(decimal mm) => new(mm / 25.4m);
    public static readonly Offset Zero = new(0m);

    public Length Abs() => Length.FromInches(Math.Abs(Inches));

    public static Offset operator +(Offset a, Offset b) => new(a.Inches + b.Inches);
    public static Offset operator -(Offset a, Offset b) => new(a.Inches - b.Inches);
    public static Offset operator -(Offset a) => new(-a.Inches);
    public static Offset operator *(Offset a, decimal s) => new(a.Inches * s);

    // Length +/- Offset → Length (clamped or throws if negative)
    public static Length operator +(Length l, Offset o) => Length.FromInches(l.Inches + o.Inches);
    public static Length operator -(Length l, Offset o) => Length.FromInches(l.Inches - o.Inches);

    // Length - Length → Offset
    public static Offset Between(Length a, Length b) => new(b.Inches - a.Inches);

    public int CompareTo(Offset other) => Inches.CompareTo(other.Inches);
    public override string ToString() => $"{Inches:+0.####;-0.####}in";
}
```

### 3.3 Angle

```csharp
public readonly record struct Angle : IComparable<Angle>
{
    public decimal Degrees { get; }

    private Angle(decimal degrees) => Degrees = NormalizeDegrees(degrees);

    public static Angle FromDegrees(decimal degrees) => new(degrees);
    public static Angle FromRadians(double radians) => new((decimal)(radians * 180.0 / Math.PI));

    public static readonly Angle Zero = new(0m);
    public static readonly Angle Right = new(90m);
    public static readonly Angle Straight = new(180m);
    public static readonly Angle Full = new(360m);

    public double ToRadians() => (double)Degrees * Math.PI / 180.0;

    public static Angle operator +(Angle a, Angle b) => new(a.Degrees + b.Degrees);
    public static Angle operator -(Angle a, Angle b) => new(a.Degrees - b.Degrees);
    public static Angle operator -(Angle a) => new(-a.Degrees);

    public int CompareTo(Angle other) => Degrees.CompareTo(other.Degrees);

    private static decimal NormalizeDegrees(decimal d)
    {
        d %= 360m;
        return d < 0 ? d + 360m : d;
    }

    public override string ToString() => $"{Degrees}°";
}
```

### 3.4 Point2D

A position in 2D space. Coordinates are `decimal` in inches.

```csharp
public readonly record struct Point2D
{
    //Coordinates are stored as decimal inches for performance and simplicity. They are not wrapped in Length to avoid excessive allocation and verbosity.
    public decimal X { get; }
    public decimal Y { get; }

    public Point2D(decimal x, decimal y) { X = x; Y = y; }

    public static readonly Point2D Origin = new(0m, 0m);

    public static Vector2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Point2D operator +(Point2D p, Vector2D v) => new(p.X + v.Dx, p.Y + v.Dy);
    public static Point2D operator -(Point2D p, Vector2D v) => new(p.X - v.Dx, p.Y - v.Dy);

    public Length DistanceTo(Point2D other)
    {
        var dx = (double)(X - other.X);
        var dy = (double)(Y - other.Y);
        return Length.FromInches((decimal)Math.Sqrt(dx * dx + dy * dy));
    }

    public Point2D MidpointTo(Point2D other) => new((X + other.X) / 2m, (Y + other.Y) / 2m);

    public override string ToString() => $"({X}, {Y})";
}
```

**Note:** `DistanceTo` uses `double` for the square root operation only — the result is immediately re-wrapped as a `Length`. This is the sanctioned escape hatch for transcendental math.

### 3.5 Vector2D

A direction and magnitude in 2D space.

```csharp
public readonly record struct Vector2D
{
    public decimal Dx { get; }
    public decimal Dy { get; }

    public Vector2D(decimal dx, decimal dy) { Dx = dx; Dy = dy; }

    public static readonly Vector2D Zero = new(0m, 0m);
    public static readonly Vector2D UnitX = new(1m, 0m);
    public static readonly Vector2D UnitY = new(0m, 1m);

    public Length Magnitude()
    {
        var dx = (double)Dx;
        var dy = (double)Dy;
        return Length.FromInches((decimal)Math.Sqrt(dx * dx + dy * dy));
    }

    public Vector2D Normalized()
    {
        var mag = (double)Magnitude().Inches;
        if (mag == 0.0) return Zero;
        return new((decimal)((double)Dx / mag), (decimal)((double)Dy / mag));
    }

    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.Dx + b.Dx, a.Dy + b.Dy);
    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.Dx - b.Dx, a.Dy - b.Dy);
    public static Vector2D operator -(Vector2D v) => new(-v.Dx, -v.Dy);
    public static Vector2D operator *(Vector2D v, decimal s) => new(v.Dx * s, v.Dy * s);
    public static Vector2D operator *(decimal s, Vector2D v) => new(v.Dx * s, v.Dy * s);

    public decimal Dot(Vector2D other) => Dx * other.Dx + Dy * other.Dy;
    public decimal Cross(Vector2D other) => Dx * other.Dy - Dy * other.Dx;

    public Vector2D Rotate(Angle angle)
    {
        var rad = angle.ToRadians();
        var cos = (decimal)Math.Cos(rad);
        var sin = (decimal)Math.Sin(rad);
        return new(Dx * cos - Dy * sin, Dx * sin + Dy * cos);
    }

    public Vector2D PerpendicularCW() => new(Dy, -Dx);
    public Vector2D PerpendicularCCW() => new(-Dy, Dx);

    public override string ToString() => $"<{Dx}, {Dy}>";
}
```

### 3.6 Rect2D

An axis-aligned rectangle defined by origin + size.

```csharp
public readonly record struct Rect2D
{
    public Point2D Origin { get; }
    public Length Width { get; }
    public Length Height { get; }

    public Rect2D(Point2D origin, Length width, Length height)
    {
        Origin = origin;
        Width = width;
        Height = height;
    }

    public static Rect2D FromCorners(Point2D min, Point2D max)
    {
        var w = Length.FromInches(Math.Abs(max.X - min.X));
        var h = Length.FromInches(Math.Abs(max.Y - min.Y));
        var origin = new Point2D(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y));
        return new Rect2D(origin, w, h);
    }

    public Point2D Min => Origin;
    public Point2D Max => new(Origin.X + Width.Inches, Origin.Y + Height.Inches);
    public Point2D Center => Origin.MidpointTo(Max);

    public decimal Area => Width.Inches * Height.Inches;

    public bool Contains(Point2D p) =>
        p.X >= Origin.X && p.X <= Origin.X + Width.Inches &&
        p.Y >= Origin.Y && p.Y <= Origin.Y + Height.Inches;

    public bool Intersects(Rect2D other) =>
        Origin.X < other.Origin.X + other.Width.Inches &&
        Origin.X + Width.Inches > other.Origin.X &&
        Origin.Y < other.Origin.Y + other.Height.Inches &&
        Origin.Y + Height.Inches > other.Origin.Y;

    public override string ToString() => $"Rect({Origin}, {Width}x{Height})";
}
```

### 3.7 LineSegment2D

```csharp
public readonly record struct LineSegment2D
{
    public Point2D Start { get; }
    public Point2D End { get; }

    public LineSegment2D(Point2D start, Point2D end) { Start = start; End = end; }

    public Length Length() => Start.DistanceTo(End);
    public Point2D Midpoint() => Start.MidpointTo(End);
    public Vector2D Direction() => (End - Start).Normalized();

    public Point2D ClosestPointTo(Point2D p)
    {
        var ab = End - Start;
        var ap = p - Start;
        var lengthSq = ab.Dot(ab);
        if (lengthSq == 0m) return Start;
        var t = Math.Clamp(ap.Dot(ab) / lengthSq, 0m, 1m);
        return Start + ab * t;
    }

    public Length DistanceTo(Point2D p) => ClosestPointTo(p).DistanceTo(p);

    public override string ToString() => $"Seg({Start} → {End})";
}
```

### 3.8 Thickness

A compound value representing nominal vs actual material thickness — a first-class concept per architecture rules.

```csharp
public readonly record struct Thickness
{
    public Length Nominal { get; }
    public Length Actual { get; }

    public Thickness(Length nominal, Length actual)
    {
        Nominal = nominal;
        Actual = actual;
    }

    public static Thickness Exact(Length value) => new(value, value);

    /// <summary>
    /// The difference between nominal and actual. Positive means actual is thinner.
    /// </summary>
    public Offset Variance => Offset.Between(Actual, Nominal);

    public override string ToString() => Nominal == Actual
        ? $"{Nominal}"
        : $"{Nominal} (actual: {Actual})";
}
```

---

## 4. Tolerance & Comparison

Shop work requires tolerance-aware comparisons. Provide a utility for this without polluting the value objects.

```csharp
public static class GeometryTolerance
{
    public static readonly Length DefaultShopTolerance = Length.FromInches(1m / 64m); // 1/64"

    public static bool ApproximatelyEqual(Length a, Length b, Length tolerance)
        => (a - b).Inches <= tolerance.Inches && (b - a).Inches <= tolerance.Inches;

    public static bool ApproximatelyEqual(Point2D a, Point2D b, Length tolerance)
        => a.DistanceTo(b) <= tolerance;

    public static bool IsEffectivelyZero(Length value, Length tolerance)
        => value <= tolerance;
}
```

---

## 5. Measurement System & Display

Display formatting is **not** on the geometry types. It is handled by a dedicated service so the domain stays UI-free.

```csharp
public enum MeasurementSystem { Imperial, Metric }
public enum Axis2D { X, Y }
public enum Direction2D { PositiveX, NegativeX, PositiveY, NegativeY }

public enum ImperialDisplayFormat
{
    FractionalInches,   // 36 1/2"
    DecimalInches,      // 36.5"
    FeetAndInches       // 3' 0 1/2"
}

public record DisplaySettings(
    MeasurementSystem System,
    ImperialDisplayFormat ImperialFormat = ImperialDisplayFormat.FractionalInches,
    int MetricDecimalPlaces = 1,
    int FractionDenominator = 16  // 16ths, 32nds, 64ths
);
```

```csharp
public interface IDimensionFormatter
{
    string Format(Length length);
    string Format(Thickness thickness);
    string FormatCompact(Length length);
}
```

`IDimensionFormatter` implementation lives in `CabinetDesigner.Infrastructure` or `CabinetDesigner.Application` — never in the Domain.

---

## 6. Serialization Contract

Geometry types must serialize cleanly to/from JSON for snapshot persistence. Since they are `readonly record struct`, `System.Text.Json` handles them with minimal configuration. Custom converters are only needed for:

- `Length` — serialize as `{ "inches": 36.5 }` to preserve canonical unit
- `Thickness` — serialize as `{ "nominal": { ... }, "actual": { ... } }`

Converters live in `CabinetDesigner.Persistence`, not in the Domain.

---

## 7. Boundaries — What This System Does NOT Own

- Display formatting logic (owned by Application/Infrastructure)
- Screen-space coordinate transforms (owned by `CabinetDesigner.Rendering`)
- Material definitions (owned by Material Catalog bounded context)
- Snap anchor evaluation (owned by `CabinetDesigner.Editor`)
- 3D geometry (out of scope for MVP; seam left open)

---

## 8. Testing Strategy

### 8.1 Unit tests
- Arithmetic identity: `a + Zero == a`, `a - a == Zero`
- Commutativity / associativity of operators
- Factory method round-trips: `FromMillimeters(25.4m).Inches == 1m`
- Non-negative invariant on `Length` (constructor throws on negative)
- `Rect2D.Contains`, `Rect2D.Intersects` edge cases
- `LineSegment2D.ClosestPointTo` collinear and perpendicular cases
- `Thickness.Variance` correctness
- `Angle` normalization: `FromDegrees(-90) == FromDegrees(270)`

### 8.2 Property-based tests
- `Length(a) + Length(b) == Length(a + b)` for all non-negative a, b
- `point.DistanceTo(point) == Length.Zero`
- `segment.DistanceTo(segment.Start) == Length.Zero`
- Triangle inequality on `Point2D.DistanceTo`
- `Rect2D.Contains(rect.Center) == true` for all non-degenerate rects
- `v.Rotate(Angle.Full) ≈ v` within tolerance

### 8.3 Snapshot tests
- Serialization round-trip: serialize → deserialize → equality check for every type

---

## 9. Future Extension Points

| Extension | Approach |
|---|---|
| `Polygon2D` | Add when room geometry or irregular countertops require it |
| `Transform2D` | Add when rendering or rotation of placed assemblies needs it |
| `BoundingBox` | Derive from `Rect2D` if 3D preview is introduced |
| `Arc2D` / `CurvedSegment` | Only if curved countertops/radius cabinets enter scope |
| Higher-precision decimal | Swap `decimal` internal storage without changing public API |

---

## 10. Risks & Edge Cases

| Risk | Mitigation |
|---|---|
| `decimal` arithmetic is slower than `double` | Acceptable for authoritative values; rendering can project to `double` at the boundary |
| `Math.Sqrt` / trig require `double` | Contained escape hatches in `DistanceTo`, `Normalized`, `Rotate` — results immediately re-wrapped |
| Tolerance comparisons used inconsistently | Central `GeometryTolerance` utility; tests verify tolerance symmetry |- Use exact equality for identity comparisons
- Use tolerance comparisons for spatial/geometry logic
| Serialization drift on snapshot format | Converters in Persistence with explicit version tags |
| Negative lengths from subtraction | `Length` constructor throws; use `Offset` for signed quantities |
| Division by zero in `Normalized` | Returns `Vector2D.Zero` for zero-magnitude vectors |

---

## 11. Summary

This geometry system provides the type-safe dimensional foundation that every other subsystem depends on. By enforcing `Length` over `decimal`, `Thickness` over a pair of numbers, and `Point2D` over tuples, the system makes dimensional bugs a compile-time concern rather than a runtime surprise. Display formatting, serialization, and screen-space mapping are explicitly kept outside the domain boundary.
