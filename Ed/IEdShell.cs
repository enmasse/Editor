namespace Ed;

public interface IEdShell
{
    IReadOnlyList<string> ReadCommandOutput(string commandText);

    void WriteToCommand(
        string commandText,
        IReadOnlyList<string> lines);

    void Execute(string commandText);
}
