namespace Ed;

public sealed class EdFileSystem : IEdFileSystem
{
    public bool Exists(string path)
    {
        return File.Exists(path);
    }

    public string GetFullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    public IReadOnlyList<string> ReadAllLines(string path)
    {
        return File.ReadAllLines(path);
    }

    public void WriteAllLines(
        string path,
        IReadOnlyList<string> lines)
    {
        var directoryPath = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllLines(path, lines);
    }

    public void AppendAllLines(
        string path,
        IReadOnlyList<string> lines)
    {
        var directoryPath = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.AppendAllLines(path, lines);
    }
}
