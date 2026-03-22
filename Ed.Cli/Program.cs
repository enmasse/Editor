using Ed;
using Ed.Cli;

var fileSystem = new EdFileSystem();
var shell = new EdShell();
var editor = new EdEditor(fileSystem, shell);
var application = new EdCommandApplication(
    editor,
    fileSystem,
    Console.In,
    Console.Out,
    Console.Error);

return application.Run(args);
