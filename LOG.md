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
