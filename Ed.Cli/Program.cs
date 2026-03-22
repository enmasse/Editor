using Ed;
using Ed.Cli;

var fileSystem = new EdFileSystem();
var shell = new EdShell();
var regexEngine = new DotNetEdRegexEngine();
var editor = new EdEditor(fileSystem, shell, regexEngine);
var application = new EdCommandApplication(
    editor,
    fileSystem,
    Console.In,
    Console.Out,
    Console.Error);

return application.Run(args);
