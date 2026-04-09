using System.Threading;
using System.Threading.Tasks;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Editor;

public interface IEditorInteractionService
{
    void BeginPlaceCabinet(string cabinetTypeId, Length nominalWidth, Length nominalDepth, double screenX, double screenY);

    void BeginMoveCabinet(CabinetId cabinetId, double screenX, double screenY);

    void BeginResizeCabinet(CabinetId cabinetId, double screenX, double screenY);

    DragPreviewResult OnDragMoved(double screenX, double screenY);

    Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default);

    void OnDragAborted();
}
