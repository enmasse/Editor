namespace Ed;

public interface IEdFileSystem
{
    bool Exists(string path);

    string GetFullPath(string path);

    IReadOnlyList<string> ReadAllLines(string path);

    void WriteAllLines(
        string path,
        IReadOnlyList<string> lines);

    void AppendAllLines(
        string path,
        IReadOnlyList<string> lines);
}
