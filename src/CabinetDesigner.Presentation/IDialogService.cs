namespace CabinetDesigner.Presentation;

public interface IDialogService
{
    /// <summary>Returns the chosen file path, or null if the user cancelled.</summary>
    string? ShowOpenFileDialog(string title, string filter);

    /// <summary>Returns the chosen file path, or null if the user cancelled.</summary>
    string? ShowSaveFileDialog(string title, string filter, string defaultFileName);

    /// <summary>Returns true if the user chose Yes.</summary>
    bool ShowYesNoDialog(string title, string message);
}
