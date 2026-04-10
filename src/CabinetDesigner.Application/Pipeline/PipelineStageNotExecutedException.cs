namespace CabinetDesigner.Application.Pipeline;

/// <summary>
/// Thrown when a <see cref="ResolutionContext"/> stage result is accessed before the stage has run.
/// Carries the stage number, stage name, and – where known – whether the stage was explicitly
/// skipped due to the current pipeline mode.
/// </summary>
public sealed class PipelineStageNotExecutedException : InvalidOperationException
{
    private PipelineStageNotExecutedException(
        string message,
        int stageNumber,
        string stageName,
        bool wasSkipped,
        ResolutionMode? pipelineMode)
        : base(message)
    {
        StageNumber = stageNumber;
        StageName = stageName;
        WasSkipped = wasSkipped;
        PipelineMode = pipelineMode;
    }

    /// <summary>The one-based ordinal of the stage that did not execute.</summary>
    public int StageNumber { get; }

    /// <summary>The human-readable name of the stage that did not execute.</summary>
    public string StageName { get; }

    /// <summary>
    /// <see langword="true"/> when the stage was deliberately skipped because
    /// <see cref="IResolutionStage.ShouldExecute"/> returned <see langword="false"/> for the
    /// current mode; <see langword="false"/> when the pipeline never reached this stage at all
    /// (e.g. an earlier stage failed).
    /// </summary>
    public bool WasSkipped { get; }

    /// <summary>
    /// The pipeline mode that was active when the stage was skipped, or
    /// <see langword="null"/> when the stage was never invoked.
    /// </summary>
    public ResolutionMode? PipelineMode { get; }

    /// <summary>
    /// Creates an exception for a stage that was deliberately skipped because it does not
    /// participate in the given pipeline mode.
    /// </summary>
    public static PipelineStageNotExecutedException Skipped(
        int stageNumber,
        string stageName,
        ResolutionMode mode) =>
        new(
            $"Stage {stageNumber} ({stageName}) was skipped because it does not execute in {mode} mode.",
            stageNumber,
            stageName,
            wasSkipped: true,
            pipelineMode: mode);

    /// <summary>
    /// Creates an exception for a stage that was never invoked – typically because an earlier
    /// stage failed and the pipeline halted before reaching this one.
    /// </summary>
    public static PipelineStageNotExecutedException NeverInvoked(
        int stageNumber,
        string stageName) =>
        new(
            $"Stage {stageNumber} ({stageName}) has not executed. The pipeline may have been interrupted before reaching this stage.",
            stageNumber,
            stageName,
            wasSkipped: false,
            pipelineMode: null);
}
