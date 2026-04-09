namespace CabinetDesigner.Domain.Commands;

public enum CommandOrigin
{
    User,
    Editor,
    System,
    Template,
    Undo,
    Redo
}
