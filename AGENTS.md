# AGENTS.md

## Purpose

This repository contains FoosVision, an Android-based foosball (table soccer) vision system.
The product focus is low-latency live viewing, 30 fps live ball tracking, shot detection, and 120 fps replay analysis with shot metrics.

FoosVision ships as one Android app package with two role-specific launcher icons:

- Recorder
  - runs on Android smartphones mounted above the table
  - uses the main camera
  - captures 30 fps decoded preview frames for live vision processing
  - captures and streams 120 fps encoded video for live viewing and replay
  - publishes protocol events, live data, and live analysis to connected viewers

- Viewer
  - runs on Android smartphones and tablets
  - connects to the recorder
  - sends recorder commands
  - displays the low-latency 120 fps stream
  - shows tracked-ball visualization and slow-motion replay for shots

## Product Intent

FoosVision is built around a two-device recorder-viewer workflow:

- connect a viewer to a recorder on the local network
- guide installation and table detection before starting a game session
- show a low-latency 120 fps live stream with 30 fps tracked-ball visualization
- detect replay-worthy shot moments during live tracking
- play back 120 fps slow-motion replay and analyze shots for metrics

## Architecture

This repository is organized in a clean-architecture style and should continue in that direction.
Core product behavior belongs in the inward layers; technical implementations, app UI, and runtime wiring stay in outer layers.

Product layers:

- `Product/Core`
  - `FoosVision.Common`
  - `FoosVision.Domain`
  - `FoosVision.UseCases.Dependencies`
  - `FoosVision.UseCases`

- `Product/Adapters`
  - `FoosVision.Adapters.Common`
  - `FoosVision.Adapters.Recorder`
  - `FoosVision.Adapters.Viewer`
  - `FoosVision.Ports.Media`
  - `FoosVision.Ports.Vision`
  - `FoosVision.Protocol`

- `Product/Infrastructure`
  - `FoosVision.Logging`
  - `FoosVision.NetDiscovery`
  - `FoosVision.NetMq`
  - `FoosVision.Settings`
  - `FoosVision.Vision`
  - `Media/*`
  - `NetDiscovery/*`

- `Product/Composition`
  - `FoosVision.Recorder.Composition`
  - `FoosVision.Viewer.Composition`

- `Product/Apps`
  - `FoosVision`
    - single MAUI app package and app entry point
  - `FoosVision.Recorder.App`
    - recorder role UI/runtime module
  - `FoosVision.Viewer.App`
    - viewer role UI/runtime module

Supporting areas:

- `Tests`
- `Documentation`
- `Experiments`
- `Tools`

Keep core dependencies pointing inward.
Composition roots may reference adapters and infrastructure to wire runtime behavior.
App projects may reference composition roots and role-specific UI/runtime modules.
Do not move infrastructure concerns into domain or use-case code.
Do not add new cross-project dependencies without asking for permission.

## Boundaries

When adding or changing behavior, put code in the right place:

### Core

- `FoosVision.Common`
  - shared foundational types and utilities that are safe to reuse across all layers; keep this minimal
- `FoosVision.Domain`
  - entities, value objects, domain services, domain rules
- `FoosVision.UseCases`
  - interactors, input/output ports
- `FoosVision.UseCases.Dependencies`
  - narrow interfaces for infrastructure-facing operations used by interactors

### Adapters

- `FoosVision.Adapters.*`
  - presenters, command handlers, command senders, frame processors, gateway adapters, orchestration across use cases
- `FoosVision.Ports.*`
  - narrow interfaces for infrastructure-facing operations used by adapters
- `FoosVision.Protocol`
  - protocol message definitions, connectivity abstractions, discovery/config contracts

### Infrastructure

- `Product/Infrastructure/*`
  - concrete technical implementations such as settings, logging, NetMQ, discovery, media, and vision
  - implements ports and technical adapters without leaking concrete infrastructure concerns into core or adapter code

### Composition

- `FoosVision.Recorder.Composition`
  - composition root and runtime wiring of the recorder role

- `FoosVision.Viewer.Composition`
  - composition root and runtime wiring of the viewer role

### Apps

App projects own MAUI UI, platform entry points, role launch, role switching, and app-level runtime helpers.

- `FoosVision` is the only launchable FoosVision MAUI app project.
- `FoosVision.Recorder.App` and `FoosVision.Viewer.App` are role-specific UI/runtime modules referenced by the FoosVision app.
- Do not put domain rules, use-case decisions, protocol contracts, or infrastructure implementations in app projects.

If a task can be solved in a more inward layer, do not solve it in composition or infrastructure.

Frame buffers must stay out of the core. Core code represents frames by timestamps,
ids, or similar handles, and delegates frame-buffer operations through narrow ports
such as `FoosVision.UseCases.Game.Ports.IFrameVisionOps` with implementations in
adapters or infrastructure.

## Runtime Model

Runtime dependencies should be introduced through the role app modules and composition roots.
Do not bypass the composition root when adding recorder or viewer runtime dependencies.

### Recorder Role

The recorder role is created by `FoosVision.Recorder.App` and wired through `RecorderHost` / `RecorderCompositionRoot`.

At a high level, the recorder role:

- initializes recorder logging/settings in the app runtime layer
- creates the Android camera feed, video dump writer, and recorder runtime factory in the role app module
- requests camera permission before starting the recorder host
- creates runtime stores, `CameraController`, `VisionSession`, network, installation, and game modules in the composition root
- configures the UDP video stream when a viewer completes the handshake
- routes install and game commands through `RecorderCommandRouter`
- publishes recorder runtime state through `RecorderRuntimeStateController`
- exposes startup, viewer connection notifications, runtime state changes, and active-session shutdown through `RecorderHost`

Recorder startup starts the network surface and makes the recorder discoverable.
Stopping active sessions stops installation/game work and releases the viewer connection.

### Viewer Role

The viewer role is created by `FoosVision.Viewer.App` and wired through `ViewerHost` / `ViewerCompositionRoot`.

At a high level, the viewer role:

- initializes viewer logging/settings in the app runtime layer
- creates recorder discovery, connection, network, vision, and replay-decoder services before connecting
- creates recorder-bound installation, game, runtime-state, live-data, and live-analysis modules only after a successful recorder connection
- exposes connection lifecycle, replay session storage, vision context, ball finding, mask decoding, and replay decoding through `ViewerHost`
- attaches UI state, overlay, playback, live data, live analysis, command handling, and replay coordination through `SessionManager` and `ActiveSession`

A connected viewer session exists only after a successful connection to a recorder.
Recorder-bound state, subscriptions, command routing, live data, live analysis, and media playback must be attached to that connected recorder session rather than treated as application startup state.

## Protocol and Connectivity

The repo already contains protocol and connectivity abstractions for recorder-viewer communication.
Keep protocol contracts separate from concrete transport implementations.

Relevant projects and folders:

- `Product/Adapters/FoosVision.Protocol`
  - protocol message definitions, protocol versioning, default ports, and connectivity abstractions
- `Product/Infrastructure/FoosVision.NetMq`
  - concrete NetMQ transport for handshake, commands, events, live data, and live analysis
- `Product/Infrastructure/FoosVision.NetDiscovery`
  - FoosVision recorder discovery adapter
- `Product/Infrastructure/NetDiscovery`
  - lower-level UDP discovery implementation
- `Experiments/Protocol/RecorderCli`
- `Experiments/Protocol/ViewerCli`

Existing protocol concepts include:

- handshake
- discovery
- commands
- replies
- events
- live data
- live analysis
- default ports
- protocol versioning

Important rule:
A major version increase is treated as a breaking protocol change.

If you change protocol messages, ports, handshake behavior, discovery behavior, transport channels, or version semantics, call that out explicitly.

## Repository Map

### Read these first for broad orientation

1. `README.md`
2. `FoosVision.slnx`
3. `Directory.Build.props`
4. `Directory.Packages.props`
5. `.editorconfig`

### Read these for app and runtime orientation

- `Product/Apps/FoosVision/*`
  - single MAUI app shell, role launch, and role switching
- `Product/Composition/FoosVision.Recorder.Composition/*`
  - recorder runtime wiring
- `Product/Composition/FoosVision.Viewer.Composition/*`
  - viewer runtime wiring

### Important directories

- `Product/Core`
- `Product/Adapters`
- `Product/Infrastructure`
- `Product/Composition`
- `Product/Apps`
- `Tests`
- `Documentation`
- `Tools`
- `Experiments`

## Coding Conventions

### Language and platform

- Language: C#
- The repository default target framework is `net10.0`.
- App and platform-specific projects may use explicit platform target frameworks such as `net10.0-android`.
- Nullable enabled
- Implicit usings enabled

### Formatting and analyzer rules

Follow `.editorconfig` and existing repository conventions.
This repository uses LF line endings and a final newline.
Do not introduce mixed line endings or switch LF files to CRLF to satisfy whitespace tooling.

Important existing code style conventions include:

- use 4-space indentation for C# files
- use 2-space indentation for project, props, targets, solution XML, JSON, YAML, and similar structured files where `.editorconfig` specifies it
- use file-scoped namespaces
- place `using` directives outside the namespace declaration
- prefer explicit types over `var`, unless the touched file's local style or readability clearly favors `var`
- braces are preferred, including for single-line blocks
- write `float` literals with lowercase `f`, for example `1.23f`
- write `double` literals without a suffix, for example `1.23`
- name private fields with a leading underscore and PascalCase
- use `Lock` instead of `object` for private synchronization fields, for example `private readonly Lock _Gate = new();`
- do not use `#region`
- do not make public product classes or records `sealed` by default; use `sealed` only when there is a concrete design reason or when following local private/nested/test-double style
- when modifying existing code, follow the prevailing style of the touched file unless explicitly instructed otherwise

Use the existing file header style for C# files where headers are expected:

- `SPDX-License-Identifier: GPL-3.0-or-later`
- `SPDX-FileCopyrightText: 2026 Robin Kaiser`

Do not introduce a different header format.
`Product/Infrastructure/NetDiscovery/**/*.cs` is third-party/imported-style code and is exempt from the repository file-header rule.

### Logging and runtime metrics

Use `FoosVision.Common.Logging` for product logging. Prefer a static `Source` per class or responsibility
and write structured message templates through `Source` methods.

For high-frequency paths such as frame processing, media streaming, replay analysis, and network traffic,
prefer the existing throttled or aggregated options over per-event log spam:

- use `SourceInterval` when repeated log messages should be rate-limited
- use `IntervalMetric`, `DurationMetric`, or `CounterMetric` from `FoosVision.Common.Metrics`
  with `RuntimeMetricsOptions` when reporting rates, intervals, durations, or counts periodically

### Style guidance

Prefer:

- small focused classes
- explicit interfaces at boundaries
- records / value objects where already idiomatic
- narrow changes in the correct layer
- names that match existing vocabulary in the repo
- short exits / guard clauses over `if`/`else` blocks when one branch is an error,
  validation failure, missing precondition, or early-stop condition

Avoid:

- cross-layer shortcuts
- mixing transport/protocol details into domain code
- mixing infrastructure implementation into use cases
- broad cleanup unrelated to the task
- `else` blocks after a branch that can return, throw, continue, break, or otherwise
  finish the current control flow

## Tests

This repository has test coverage across multiple layers.
Tests are located under `Tests/`, and their structure mirrors the layout under `Product/` where practical.

Implemented product behavior should normally be covered with unit tests.
Place new tests in the matching test project for the touched production layer whenever one exists.
If no matching test project exists, ask before adding a new project.

Unit tests should stay deterministic and in-memory. Use test doubles for time, files, network, OS,
native libraries, codecs, cameras, and other infrastructure concerns. Do not make unit tests depend
on wall-clock timing, real files, sockets, devices, native libraries, or external services.

Do not add integration or validation tests unless the requested change touches a real infrastructure
boundary or explicitly needs real-world validation material.

Test naming conventions:

- UnitTests: tests for product behavior isolated from infrastructure
- IntegrationTests: tests for real component interaction across actual infrastructure boundaries
- ValidationTests: tests against real-world input data, for example images or video sequences

### Test commands

Use from repository root to run all tests:

dotnet test --settings Tests/test.runsettings

Use from repository root to run one test project:

dotnet test Tests/Core/FoosVision.Domain.UnitTests/FoosVision.Domain.UnitTests.csproj --settings Tests/test.runsettings

### Coverage expectations

At minimum:

- preserve existing passing tests in touched areas
- add tests for new code where reasonable, especially for domain/use-case logic

## Build and Run

### Build

Use from repository root to build the solution:

dotnet build FoosVision.slnx

`dotnet build` restores packages as needed.

### Run

Do not run app projects, tool projects, benchmark projects, Android deployment workflows, device-pairing scripts, or solution launch profiles.
The user handles launching and device workflows manually.

### Command execution rule

Run repository commands like build or test one at a time only.
Do not start multiple `dotnet` commands in parallel.

## Change Rules

Before changing code:

1. identify the relevant layer and ownership boundary
2. read the local surrounding module and matching tests
3. preserve existing naming, architecture boundaries, and product vocabulary
4. check whether a matching test project already exists
5. keep the diff focused on the requested behavior
6. avoid introducing new build, analyzer, formatting, or newline churn

Prefer:

- one clean vertical slice at a time
- minimal viable changes that fit existing patterns
- explicit TODOs only when an intentional product or technical decision remains open

Avoid:

- wide repo refactors during feature work
- architectural rewrites for local problems
- duplicate abstractions
- cross-layer shortcuts
- broad cleanup unrelated to the task

### Specialized changes

When modifying algorithmic code in `Product/Infrastructure/FoosVision.Vision`,
read `Documentation/Skills/VisionAlgorithmsSkill.md` first and follow it.

When changing configuration behavior, config schema, config defaults, diagnostics settings,
or recorder/viewer settings propagation, check `Documentation/Configuration.md` and
`Documentation/Diagnostics.md` for accuracy and update them when needed.

## Definition of Done

A task is done when, where applicable:

- the requested behavior is implemented
- the change is in the correct layer and ownership boundary
- related tests are added or updated, or the reason for not adding tests is clear
- relevant documentation is updated when behavior, configuration, diagnostics, protocol, or developer workflows change
- protocol-impacting changes are called out explicitly
- verification was run, or the reason it was not run is stated
- incomplete work is marked clearly with an explicit TODO or called out to the user

## Agent Behavior

When working in this repository:

- orient on the existing structure before proposing or changing code
- prefer repo facts over generic architecture advice
- be concrete and implementation-focused
- keep changes compatible with the existing architecture, naming, and product workflow
- do not speculate about behavior that is not encoded in the repo, documentation, or user request
- for small documentation-only edits, keep verification minimal; use a targeted diff/status check and do not run tests or broad cleanup unless there is a specific risk

When using the CLI or shell:

- follow the Build and Run command-execution rules
- use `rg`/`rg --files` for searching when available
- do not chain independent verification commands with shell parallelization operators or separate sessions

### Git Staging Workflow

The user may stage changes incrementally while continuing to ask questions or request small follow-up refactorings.

Treat the Git index as user-managed state:

- staged changes may be present during normal work
- do not treat staged changes as an instruction to commit
- do not run `git add`, `git restore --staged`, `git reset`, `git commit`, `git commit --amend`, or `git push` unless explicitly asked
- do not overwrite or reorganize staged changes just to make the working tree look clean
- when reviewing or changing files, distinguish between staged and unstaged changes
- keep any follow-up change focused and compatible with already staged work
- if a requested edit would conflict with or substantially rewrite staged changes, call that out before proceeding

### Editing Strategy

- prefer small, file-by-file edits over large multi-file batches
- for `.csproj`, `.props`, `.targets`, `.slnx`, and similar project files, prefer minimal and explicit edits
- do not combine project scaffolding, dependency changes, solution changes, implementation, and tests into one large patch
- after project or solution structure changes, stop and verify before continuing with implementation
- if one patch fails repeatedly, switch to a smaller safer approach instead of increasing patch size
- avoid rewriting entire files unless that is clearly the safest option

### Windows / PowerShell

- when using shell-based file edits on Windows, prefer small independent write steps over one large PowerShell script
- be careful with quoting, here-strings, and escaping when writing XML, JSON, or C# source through PowerShell
- prefer direct file writes only when patch-based editing is unreliable
- if direct file writes are used, verify the resulting file content before continuing

### Context7 Usage

Use Context7 when it is a good fit for the task, especially for:

- library or API documentation
- version-sensitive framework behavior
- .NET MAUI
