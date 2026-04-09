namespace CabinetDesigner.Domain.ProjectContext;

public enum ApprovalState
{
    Draft,
    UnderReview,
    Approved,
    LockedForManufacture,
    ReleasedToShop,
    ReadyForInstall,
    Installed,
    Superseded
}
