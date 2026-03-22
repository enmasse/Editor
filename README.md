# Editor

A .NET 10 implementation of an `ed`-style line editor.

The repository contains a reusable editor core, a console application that exposes the `ed` command, and a test suite that exercises the editor API, concrete abstractions, and CLI behavior.

## Solution layout

- `Editor.slnx` - solution file for the workspace
- `Ed/` - core editor library
- `Ed.Cli/` - console host that publishes a self-contained `ed` executable
- `Ed.Tests/` - automated tests built with `TUnit` and `FsCheck`
- `LOG.md` - running development log for the project history

## Projects

### `Ed`

The `Ed` project contains the main editing engine:

- `EdEditor` - in-memory buffer management and command execution
- `EdFileSystem` - concrete filesystem abstraction
- `EdShell` - concrete shell abstraction backed by `pwsh.exe`
- supporting models and enums for line ranges, print modes, write modes, search direction, and substitution options

### `Ed.Cli`

The `Ed.Cli` project is the command-line host.

It:

- creates an `EdEditor` instance with the concrete filesystem and shell implementations
- accepts an optional file path argument
- reads commands from standard input
- supports multiline text entry for `a`, `i`, and `c`
- writes normal command output to standard output
- writes `?` style failures to standard error
- toggles prompt and verbose error behavior through the editor commands

By default, the project is configured to publish a self-contained single-file Windows executable. A build and publish alias named `ed.exe` is also created.

### `Ed.Tests`

The test project covers:

- editor API behavior
- command parsing and address handling
- filesystem and shell implementations
- integration between `EdEditor`, `EdFileSystem`, and `EdShell`
- end-to-end CLI execution through the built `ed.exe`

The current suite contains 142 passing tests.

## Implemented command surface

The current implementation supports a substantial subset of classic `ed` behavior, including:

- text input commands: `a`, `i`, `c`
- printing commands: `p`, `n`, empty command, addressed print, `,p`, `,l`
- buffer mutation commands: `d`, `j`, `m`, `t`, `u`, `k`
- file commands: `e`, `E`, `r`, `w`, `W`, `f`
- shell integration: `!`, `r !`, `w !`
- search and substitution: `/.../`, `?...?`, `s/.../.../`, previous-pattern reuse, occurrence flags, global replacement
- global commands: `g/.../d`, `g/.../p`, `g/.../n`, `g/.../s/.../.../`, plus `v` variants
- state and utility commands: `=`, `P`, `H`, `h`, `z`, `q`, `q!`, `Q`

Address parsing includes support for:

- explicit line numbers
- `.` and `$`
- `%`
- marks via `'x`
- relative offsets with `+` and `-`
- search-based addresses
- range separators `,` and `;`

Regex support is implemented with `.NET` regular expressions rather than canonical UNIX `ed` regex semantics.

## Current scope

This repository aims at an `ed`-style editor and currently implements the subset described above. It is not yet documented as a fully conformant UNIX `ed` replacement.

The shell implementation currently assumes `pwsh.exe` is available.

## Build

Build the core library:

```powershell
dotnet build .\Ed\Ed.csproj
```

Build the CLI:

```powershell
dotnet build .\Ed.Cli\Ed.Cli.csproj
```

Build the tests:

```powershell
dotnet build .\Ed.Tests\Ed.Tests.csproj
```

## Run the CLI

Run the editor from the project:

```powershell
dotnet run --project .\Ed.Cli\Ed.Cli.csproj
```

Open a file immediately:

```powershell
dotnet run --project .\Ed.Cli\Ed.Cli.csproj -- .\notes.txt
```

After a debug build, the aliased executable is available at:

```text
Ed.Cli\bin\Debug\net10.0\win-x64\ed.exe
```

## Publish a self-contained `ed` binary

Publish the Windows single-file executable:

```powershell
dotnet publish .\Ed.Cli\Ed.Cli.csproj -c Release -o .\artifacts\ed
```

Published output includes:

```text
artifacts\ed\ed.exe
```

## Test

The repository uses `Microsoft.Testing.Platform` through `TUnit`.

Visual Studio Test Explorer works with the current test project setup.

A plain `dotnet test` invocation on the .NET 10 SDK requires opting into the newer `Microsoft.Testing.Platform` test experience. If you do not have that configured, build the test project and run tests from Visual Studio.

## Example interactive session

```text
a
hello
world
.
1,2p
w notes.txt
q
```

## Notes

- external environment access is abstracted behind `IEdFileSystem` and `IEdShell`
- the CLI host is intentionally thin and delegates editor behavior to `EdEditor`
- the development history is tracked in `LOG.md`
