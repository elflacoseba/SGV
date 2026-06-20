# Registro de habilidades — SGV

<!-- Actualizado por sdd-init. Este archivo es un índice; cada `SKILL.md` sigue siendo la fuente de verdad. -->

Última actualización: 2026-06-20

## Fuentes escaneadas

- `/Users/elflacoseba/.config/opencode/skills`
- `/Users/elflacoseba/.copilot/skills`
- `/Users/elflacoseba/.codex/skills`
- `/Users/elflacoseba/.codex/skills/.system`
- `/Users/elflacoseba/Source/SGV/.agents/skills`
- `/Users/elflacoseba/Source/SGV/.codex/skills`

## Contrato

Este registro es un índice operativo para resolver habilidades por disparador y ruta. No reemplaza ni resume las instrucciones completas de cada habilidad.

- Se omiten habilidades `sdd-*`, `_shared` y `skill-registry`.
- Ante duplicados, se prefiere la habilidad de proyecto sobre la global.
- Ante duplicados globales, se conserva la primera fuente según el orden de escaneo.
- Los ejecutores deben leer el `SKILL.md` exacto antes de aplicar una habilidad.

## Convenciones del proyecto

| Archivo | Alcance | Uso |
| --- | --- | --- |
| `/Users/elflacoseba/Source/SGV/AGENTS.md` | Proyecto | Convenciones de estructura, comandos, estilo, pruebas, stack y reglas para agentes. |
| `/Users/elflacoseba/.config/opencode/AGENTS.md` | Usuario | Persona, reglas generales, protocolo Engram y carga contextual de habilidades. |

## Habilidades

| Habilidad | Disparador / descripción | Alcance | Ruta |
| --- | --- | --- | --- |
| `database-designer` | Use when the user asks to design database schemas, plan data migrations, optimize queries, choose between SQL and NoSQL, or model data relationships. | Proyecto | `/Users/elflacoseba/Source/SGV/.agents/skills/database-designer/SKILL.md` |
| `dotnet-best-practices` | Ensure .NET/C# code meets best practices for the solution/project. | Proyecto | `/Users/elflacoseba/Source/SGV/.agents/skills/dotnet-best-practices/SKILL.md` |
| `dotnet-csharp` | Baseline C# skill loaded for every .NET code path. Guides language patterns (records, pattern matching, primary constructors, C# 8-15), coding standards, async/await, DI, LINQ, serialization, domain modeling, concurrency, Roslyn analyzers, globalization, native interop (P/Invoke, LibraryImport, ComWrappers), WASM interop (JSImport/JSExport), and type design. Spans 25 topics. Do not use for ASP.NET endpoint architecture, UI framework patterns, or CI/CD guidance. | Proyecto | `/Users/elflacoseba/Source/SGV/.agents/skills/dotnet-csharp/SKILL.md` |
| `dotnet-xunit` | Writing xUnit tests. v3 Fact/Theory, fixtures, parallelism, IAsyncLifetime, v2 compatibility. | Proyecto | `/Users/elflacoseba/Source/SGV/.agents/skills/dotnet-xunit/SKILL.md` |
| `mysql` | Plan and review MySQL/InnoDB schema, indexing, query tuning, transactions, and operations. Use when creating or modifying MySQL tables, indexes, or queries; diagnosing slow/locking behavior; planning migrations; or troubleshooting replication and connection issues. Load when using a MySQL database. | Proyecto | `/Users/elflacoseba/Source/SGV/.agents/skills/mysql/SKILL.md` |
| `openspec-apply-change` | Implement tasks from an OpenSpec change. Use when the user wants to start implementing, continue implementation, or work through tasks. | Proyecto | `/Users/elflacoseba/Source/SGV/.codex/skills/openspec-apply-change/SKILL.md` |
| `openspec-archive-change` | Archive a completed change in the experimental workflow. Use when the user wants to finalize and archive a change after implementation is complete. | Proyecto | `/Users/elflacoseba/Source/SGV/.codex/skills/openspec-archive-change/SKILL.md` |
| `openspec-explore` | Enter explore mode - a thinking partner for exploring ideas, investigating problems, and clarifying requirements. Use when the user wants to think through something before or during a change. | Proyecto | `/Users/elflacoseba/Source/SGV/.codex/skills/openspec-explore/SKILL.md` |
| `openspec-propose` | Propose a new change with all artifacts generated in one step. Use when the user wants to quickly describe what they want to build and get a complete proposal with design, specs, and tasks ready for implementation. | Proyecto | `/Users/elflacoseba/Source/SGV/.codex/skills/openspec-propose/SKILL.md` |
| `openspec-sync-specs` | Sync delta specs from a change to main specs. Use when the user wants to update main specs with changes from a delta spec, without archiving the change. | Proyecto | `/Users/elflacoseba/Source/SGV/.codex/skills/openspec-sync-specs/SKILL.md` |
| `pr-review-dotnet` | Perform a comprehensive review of a GitHub Pull Request for .NET, ASP.NET Core MVC, Entity Framework Core and enterprise applications. Accepts PR identifiers such as #4, PR 4 or Pull Request 4 and supports output languages spa (Spanish) and eng (English). | Proyecto | `/Users/elflacoseba/Source/SGV/.agents/skills/pr-review-dotnet/SKILL.md` |
| `branch-pr` | Create Gentle AI pull requests with issue-first checks. Trigger: creating, opening, or preparing PRs for review. | Usuario | `/Users/elflacoseba/.config/opencode/skills/branch-pr/SKILL.md` |
| `chained-pr` | Trigger: PRs over 400 lines, stacked PRs, review slices. Split oversized changes into chained PRs that protect review focus. | Usuario | `/Users/elflacoseba/.config/opencode/skills/chained-pr/SKILL.md` |
| `cognitive-doc-design` | Design docs that reduce cognitive load. Trigger: writing guides, READMEs, RFCs, onboarding, architecture, or review-facing docs. | Usuario | `/Users/elflacoseba/.config/opencode/skills/cognitive-doc-design/SKILL.md` |
| `comment-writer` | Write warm, direct collaboration comments. Trigger: PR feedback, issue replies, reviews, Slack messages, or GitHub comments. | Usuario | `/Users/elflacoseba/.config/opencode/skills/comment-writer/SKILL.md` |
| `go-testing` | Trigger: Go tests, go test coverage, Bubbletea teatest, golden files. Apply focused Go testing patterns. | Usuario | `/Users/elflacoseba/.config/opencode/skills/go-testing/SKILL.md` |
| `imagegen` | Generate or edit raster images when the task benefits from AI-created bitmap visuals such as photos, illustrations, textures, sprites, mockups, or transparent-background cutouts. Use when Codex should create a brand-new image, transform an existing image, or derive visual variants from references, and the output should be a bitmap asset rather than repo-native code or vector. Do not use when the task is better handled by editing existing SVG/vector/code-native assets, extending an established icon or logo system, or building the visual directly in HTML/CSS/canvas. | Usuario | `/Users/elflacoseba/.codex/skills/.system/imagegen/SKILL.md` |
| `issue-creation` | Create Gentle AI issues with issue-first checks. Trigger: creating GitHub issues, bug reports, or feature requests. | Usuario | `/Users/elflacoseba/.config/opencode/skills/issue-creation/SKILL.md` |
| `judgment-day` | Trigger: judgment day, dual review, adversarial review, juzgar. Run blind dual review, fix confirmed issues, then re-judge. | Usuario | `/Users/elflacoseba/.config/opencode/skills/judgment-day/SKILL.md` |
| `openai-docs` | Use when the user asks how to build with OpenAI products or APIs, asks about Codex itself or choosing Codex surfaces, needs up-to-date official documentation with citations, help choosing the latest model for a use case, or model upgrade and prompt-upgrade guidance; use OpenAI docs MCP tools for non-Codex docs questions, use the Codex manual helper first for broad Codex self-knowledge, and restrict fallback browsing to official OpenAI domains. | Usuario | `/Users/elflacoseba/.codex/skills/.system/openai-docs/SKILL.md` |
| `plugin-creator` | Create and scaffold plugin directories for Codex with a required `.codex-plugin/plugin.json`, optional plugin folders/files, valid manifest defaults, and personal-marketplace entries by default. Use when Codex needs to create a new personal plugin, add optional plugin structure, generate or update marketplace entries for plugin ordering and availability metadata, or update an existing local plugin during development with the CLI-driven cachebuster and reinstall flow. | Usuario | `/Users/elflacoseba/.codex/skills/.system/plugin-creator/SKILL.md` |
| `skill-creator` | Trigger: new skills, agent instructions, documenting AI usage patterns. Create LLM-first skills with valid frontmatter. | Usuario | `/Users/elflacoseba/.config/opencode/skills/skill-creator/SKILL.md` |
| `skill-improver` | Trigger: improve skills, audit skills, refactor skills, skill quality. Audit and upgrade existing LLM-first skills. | Usuario | `/Users/elflacoseba/.config/opencode/skills/skill-improver/SKILL.md` |
| `skill-installer` | Install Codex skills into $CODEX_HOME/skills from a curated list or a GitHub repo path. Use when a user asks to list installable skills, install a curated skill, or install a skill from another repo (including private repos). | Usuario | `/Users/elflacoseba/.codex/skills/.system/skill-installer/SKILL.md` |
| `translate-technical-markdown` | Translate technical Markdown documents from English to professional Spanish while preserving Markdown structure and technical identifiers. Use when Codex is asked to translate one or more .md files, an OpenSpec change folder, technical docs, specs, design docs, tasks, proposals, ADRs, or README-like documentation from English to Spanish and the user provides or implies a filesystem path containing the documents. | Usuario | `/Users/elflacoseba/.codex/skills/translate-technical-markdown/SKILL.md` |
| `work-unit-commits` | Plan commits as reviewable work units. Trigger: implementation, commit splitting, chained PRs, or keeping tests and docs with code. | Usuario | `/Users/elflacoseba/.config/opencode/skills/work-unit-commits/SKILL.md` |

## Estado del escaneo

- Habilidades indexadas: 26.
- Habilidades omitidas por regla: `_shared`, `skill-registry`, `sdd-*`.
- Duplicados globales resueltos por orden de escaneo: `branch-pr`, `chained-pr`, `cognitive-doc-design`, `comment-writer`, `go-testing`, `issue-creation`, `judgment-day`, `skill-creator`, `skill-improver`, `work-unit-commits`.
- Directorios de proyecto sin habilidades detectadas: `skills/`, `.opencode/skills/`, `.atl/skills/`.

## Protocolo de carga

1. Comparar la tarea y los archivos objetivo contra la columna `Disparador / descripción`.
2. Pasar únicamente las rutas relevantes al ejecutor en `## Skills to load before work`.
3. Indicar al ejecutor que lea esos `SKILL.md` exactos antes de leer, escribir, revisar, probar o crear artefactos.
4. Si no existe una habilidad aplicable, continuar sin inyección de habilidad y reportar `skill_resolution: none`.
