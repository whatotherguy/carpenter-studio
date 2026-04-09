namespace CabinetDesigner.Rendering;

public sealed record HitTestResult(
    HitTestTarget Target,
    Guid? EntityId,
    Guid? HandleId);

public enum HitTestTarget
{
    None,
    Cabinet,
    Handle,
    Run,
    Wall
}
