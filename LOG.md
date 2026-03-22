# Log

1. Prompt
   > *I want to make a clone of UNIX ed, it should support _all_ the canonical feature set. I want to work test driven. Please create a class that is the API for a future CLI implementation. Create the API that you anticipate we need. All calls to that API should result in NotImplementedException at this point.*

   Action
   - Created the initial `EdEditor` API surface for a future `ed` implementation.
   - Added supporting enums and records for ranges, substitution options, print modes, write modes, search direction, global mode, and command results.
   - Left the public editor API unimplemented with `NotImplementedException` placeholders.
   - Removed the original placeholder `Class1` type.

2. Prompt
   > *We want to use dependency injection for anything that affects the external environment. Implement the interfaces*

   Action
   - Added `IEdFileSystem` and `IEdShell` as injected external-environment seams.
   - Updated `EdEditor` to require those dependencies through its constructor.

3. Prompt
   > *Create a test project with tunit and fscheck. Use fscheck to generate test input. Make sure we have good test coverage. All tests should be implemented, but they should at this point fail as we haven't started implementing our editor class. You can mock IEdFileSystem and IEdShell in the test project. You don't need any mocking framework.*

   Action
   - Created the `Ed.Tests` project with `TUnit` and `FsCheck`.
   - Added a project reference to `Ed`.
   - Removed the template sample tests from the generated test project.
   - Added manual test doubles for `IEdFileSystem` and `IEdShell`.
   - Added an initial red-state test suite covering the exposed `EdEditor` API.

4. Prompt
   > *Do we have good test coverage?*

   Action
   - Reviewed the current test scope.
   - Identified that the suite had broad API-surface coverage but still lacked many canonical `ed` semantics and edge cases.

5. Prompt
   > *Fill in the gaps.*

   Action
   - Expanded the test suite to cover more canonical `ed` behavior without implementing production logic.
   - Added shared test support for `FsCheck`-generated inputs.
   - Extended the fake environment dependencies with interaction tracking.
   - Added state, file-command, buffer-command, addressing, and command-parsing coverage tests.
   - Kept the suite intentionally red against the unimplemented `EdEditor`.

6. Prompt
   > *Execute on your plan.*

   Action
   - Executed the coverage expansion work.
   - Built `Ed.Tests` and ran the test host.
   - Confirmed the expanded suite remained fully red against `NotImplementedException` members.

7. Prompt
   > *Add a sequential log of our actions, add my prompts as quoted italic strings. Place the log in a file called LOG.md.*

   Action
   - The request was noted but not executed in that response.

8. Prompt
   > *You didn't do anything.*

   Action
   - Created `LOG.md` at the workspace root.
   - Recorded the sequence of prompts and actions in this file.

9. Prompt
   > *As you can see there's a plan ready for execution. Read LOG.md for context. The existing tests are green. If you need to add something to the API, then do so, but keep the new functions or extensions in the core editor unimplemented before you've added new tests.*

   Action
   - Reviewed `LOG.md` and the pending plan context.
   - Audited the existing `EdEditor` API and test coverage to identify missing canonical `ed` behaviors.
   - Added new conformance tests covering previous-pattern substitution reuse, implicit current-line command parsing, and numeric substitution occurrence parsing.
   - Built `Ed.Tests` successfully.
   - Ran the test executable directly with `dotnet run` because `dotnet test` is blocked by `Microsoft.Testing.Platform` on `.NET 10` in the current configuration.
   - Confirmed two new tests fail as intended against the current implementation: plain `p` defaults to the whole buffer instead of the current line, and previous-pattern substitution reuse does not yet behave as expected.

10. Prompt
    > *Add a sequential log of our actions to LOG.md with my prompts in quoted italic.*

    Action
    - Updated `LOG.md` to append the latest prompts and actions in the requested sequential format.

11. Prompt
    > *What are we missing to implement a UNIX ed conformant CLI editor?*

    Action
    - Reviewed the existing `EdEditor` implementation and coverage to identify the remaining conformance gaps.
    - Summarized the missing CLI host, canonical command parsing, full address syntax, regex search and substitution semantics, global command execution, and output behavior still needed for UNIX `ed` compatibility.

12. Prompt
    > *Add missing tests but don't do anything thing to the core logic yet. We're doing this the TDD way. If needed you can add to the API, but only create stubs that throws NotImplementedException.*

    Action
    - Added new red-state conformance tests for missing canonical command parsing such as `d`, `=`, `f`, `P`, forward search, backward search, and non-delete global commands.
    - Added new red-state address parsing tests for `.`, `$`, relative backward addresses, and mark addresses.
    - Added new red-state file-command parsing tests for `w`, `W`, and `e` command forms.
    - Built `Ed.Tests` successfully to verify the new tests compile without changing the core editor logic.
