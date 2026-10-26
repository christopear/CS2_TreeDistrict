# Reference sources

Consulted on 2026-10-26. These are design/build references, not dependencies or
vendored code. Verify details against the installed game before using them.

Our selected algorithmic references are **Unreal Procedural Foliage** (primary)
and **EcoBrush, 2017** (environmental model). Their input inventories, source
locators and proposed adaptations are in the
[foliage reference research and design report](foliage-reference-design.md).
The CS2 repositories below are integration references, not extra generation models.

- [Official code-modding overview](https://www.paradoxinteractive.com/games/cities-skylines-ii/modding/dev-diary-3-code-modding):
  official project template, post-processing, ECS systems and publishing.
- [Modding toolchain wiki](https://cs2.paradoxwikis.com/Modding_Toolchain):
  installation reference; version-specific examples can lag the installed game.
- [Community mod-project guidance](https://github.com/CitiesSkylinesModding/agents-plugins/blob/main/plugins/cs2-modding/skills/cs2-mod-project/SKILL.md):
  template ownership, framework constraints, private game references and UI deploy
  ordering. Its stated baseline is game version 1.6.2f1, not a claim about our install.
- [ParkManager](https://github.com/tomkpunkt/ParkManager):
  procedural boundary/preview/build interaction and separation of geometry tests
  from the live game; GPL-3.0. Its project targets net48.
- [Tree Controller](https://github.com/yenyang/Tree_Controller):
  vegetation selection, ages and forest brushes.
- [Advanced Line Tool](https://github.com/algernon-A/LineTool-CS2):
  placement previews and ordinary game-object integration.
- [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core):
  .NET 10 is the current LTS line; SDK choice here applies to portable development,
  not to the game's runtime or official build tools.
- [Official .NET installer](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script):
  repository-local SDK bootstrap.

Read a source project's licence before copying implementation. This scaffold uses
original code and standard .NET templates; no external mod source was copied.
