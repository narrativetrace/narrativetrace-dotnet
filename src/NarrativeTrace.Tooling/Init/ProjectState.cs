// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.ObjectModel;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// A read-only snapshot of a consumer project: everything the installer's decisions depend on, read
/// once and never read again.
/// </summary>
/// <remarks>
/// <para>INTENT: makes the planner a pure function. Every "does this file exist", "is this directory
/// ours", "where do traces land" question is answered here, so a plan can be computed, printed as a
/// diff, reviewed, and only then applied — with no chance of the answer changing between the diff and
/// the write.</para>
/// <para>Built two ways: <see cref="ProjectStateReader"/> walks a real directory; a test builds one
/// directly with <see cref="Builder"/>, which is why every planner case is a unit test with no disk
/// at all. Contents are kept byte for byte — line endings, byte-order mark, missing final newline and
/// all. The planner's edits are expressed against exactly these bytes.</para>
/// </remarks>
public sealed class ProjectState
{
    /// <summary>Where rendered traces land when a project says nothing else.</summary>
    public const string DefaultOutputDirectory = "TestResults/narrativetrace";

    private readonly string? _agentsMd;
    private readonly string? _claudeMd;
    private readonly IReadOnlyDictionary<string, string> _pointerFiles;
    private readonly IReadOnlySet<string> _pointersBehindLinks;

    private ProjectState(Builder builder)
    {
        _agentsMd = builder.AgentsMdContent;
        _claudeMd = builder.ClaudeMdContent;
        HasClaudeDirectory = builder.ClaudeDirectory;
        InstalledSkills = new ReadOnlyCollection<InstalledSkill>([.. builder.InstalledSkillsList]);
        LinkedInstallRoots = new ReadOnlyDictionary<SkillFlavour, string>(
            new Dictionary<SkillFlavour, string>(builder.LinkedInstallRootsMap));
        MarkedRuleFiles = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(builder.MarkedRuleFilesMap, StringComparer.Ordinal));
        DetectedPlatforms = new ReadOnlyCollection<string>([.. builder.DetectedPlatformsList]);
        _pointerFiles = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(builder.PointerFilesMap, StringComparer.Ordinal));
        _pointersBehindLinks = new HashSet<string>(builder.PointersBehindLinksSet, StringComparer.Ordinal);
        OutputDirectory = builder.OutputDirectoryValue;
        ResolvedFamilyVersion = builder.ResolvedFamilyVersionValue;
        if (!Invariant())
        {
            throw new ArgumentException("a project state must describe one path once");
        }
    }

    /// <summary>Starts building an untouched-project snapshot.</summary>
    public static Builder NewBuilder() => new();

    /// <summary>The project's <c>AGENTS.md</c>, byte for byte, or <see langword="null"/> when it has none.</summary>
    public string? AgentsMd => _agentsMd;

    /// <summary>The project's <c>CLAUDE.md</c>, byte for byte, or <see langword="null"/> when it has none.</summary>
    public string? ClaudeMd => _claudeMd;

    /// <summary>Whether the vendor directory exists — one half of the vendor-detection rule.</summary>
    public bool HasClaudeDirectory { get; }

    /// <summary>Every skill directory found under either install root, in read order.</summary>
    public IReadOnlyList<InstalledSkill> InstalledSkills { get; }

    /// <summary>
    /// Each flavour whose install root is ITSELF a symbolic link, mapped to what it points at —
    /// nothing may be written into such a flavour at all, because every page of it, present or not,
    /// would land wherever the link goes.
    /// </summary>
    public IReadOnlyDictionary<SkillFlavour, string> LinkedInstallRoots { get; }

    /// <summary>
    /// Vendor rule files that already carry our markers, by project-relative path. The installer
    /// never CREATES one of these; it keeps an existing block up to date.
    /// </summary>
    public IReadOnlyDictionary<string, string> MarkedRuleFiles { get; }

    /// <summary>
    /// The ids of the platform-table rows whose marker the project carries — <c>.cursor/</c>,
    /// <c>replit.md</c>, … — read by exact path, so a near miss such as <c>.cursorx/</c> proves nothing.
    /// </summary>
    public IReadOnlyList<string> DetectedPlatforms { get; }

    /// <summary>
    /// An existing platform pointer file, byte for byte, by project-relative path; <see langword="null"/>
    /// when there is none, or when the path runs through a symbolic link (<see cref="PointerBehindLink"/>).
    /// </summary>
    public string? PointerFile(string path) => _pointerFiles.TryGetValue(path, out var text) ? text : null;

    /// <summary>Whether the pointer file's path, or a directory above it, is a symbolic link.</summary>
    public bool PointerBehindLink(string path) => _pointersBehindLinks.Contains(path);

    /// <summary>Where this project's rendered traces land, detected or <see cref="DefaultOutputDirectory"/>.</summary>
    public string OutputDirectory { get; }

    /// <summary>
    /// The NarrativeTrace family version the project resolves (its <c>PackageReference</c> versions),
    /// or <see langword="null"/> when none can be determined — the input to the version guard (D4).
    /// </summary>
    public string? ResolvedFamilyVersion { get; }

    /// <summary>The skill directory at one flavour's path, or <see langword="null"/> when nothing is there.</summary>
    public InstalledSkill? InstalledSkill(SkillFlavour flavour, string name) =>
        InstalledSkills.FirstOrDefault(
            skill => skill.Flavour == flavour && string.Equals(skill.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// What a flavour's install root points at when the root itself is a symbolic link, or
    /// <see langword="null"/> when it is not — nothing may be written into that flavour at all.
    /// </summary>
    public string? LinkedInstallRoot(SkillFlavour flavour) =>
        LinkedInstallRoots.TryGetValue(flavour, out var target) ? target : null;

    /// <summary>
    /// No path is described twice: one snapshot entry per (flavour, name), and nothing is listed under
    /// a flavour whose whole install root is a link — what was found there was found through it.
    /// </summary>
    internal bool Invariant()
    {
        var distinct = InstalledSkills
            .Select(skill => skill.Flavour + "/" + skill.Name)
            .Distinct(StringComparer.Ordinal)
            .Count();
        return distinct == InstalledSkills.Count
            && !string.IsNullOrWhiteSpace(OutputDirectory)
            && !InstalledSkills.Any(skill => LinkedInstallRoots.ContainsKey(skill.Flavour));
    }

    /// <summary>Assembles a snapshot; every field has the "untouched project" default.</summary>
    public sealed class Builder
    {
        internal string? AgentsMdContent { get; private set; }

        internal string? ClaudeMdContent { get; private set; }

        internal bool ClaudeDirectory { get; private set; }

        internal List<InstalledSkill> InstalledSkillsList { get; } = [];

        internal Dictionary<SkillFlavour, string> LinkedInstallRootsMap { get; } = new();

        internal Dictionary<string, string> MarkedRuleFilesMap { get; } = new(StringComparer.Ordinal);

        internal List<string> DetectedPlatformsList { get; } = [];

        internal Dictionary<string, string> PointerFilesMap { get; } = new(StringComparer.Ordinal);

        internal HashSet<string> PointersBehindLinksSet { get; } = new(StringComparer.Ordinal);

        internal string OutputDirectoryValue { get; private set; } = DefaultOutputDirectory;

        internal string? ResolvedFamilyVersionValue { get; private set; }

        /// <summary>Sets the project's <c>AGENTS.md</c> content.</summary>
        public Builder AgentsMd(string content)
        {
            AgentsMdContent = content;
            return this;
        }

        /// <summary>Sets the project's <c>CLAUDE.md</c> content.</summary>
        public Builder ClaudeMd(string content)
        {
            ClaudeMdContent = content;
            return this;
        }

        /// <summary>Records whether the vendor directory is present.</summary>
        public Builder HasClaudeDirectory(bool present)
        {
            ClaudeDirectory = present;
            return this;
        }

        /// <summary>Adds one installed skill to the snapshot.</summary>
        public Builder AddInstalledSkill(InstalledSkill skill)
        {
            InstalledSkillsList.Add(skill);
            return this;
        }

        /// <summary>Records that a flavour's install root is a symbolic link, and what it points at.</summary>
        public Builder LinkedInstallRoot(SkillFlavour flavour, string target)
        {
            LinkedInstallRootsMap[flavour] = target;
            return this;
        }

        /// <summary>Records a vendor rule file that already carries our markers.</summary>
        public Builder MarkedRuleFile(string relativePath, string content)
        {
            MarkedRuleFilesMap[relativePath] = content;
            return this;
        }

        /// <summary>Records that the project carries the marker of one platform-table row.</summary>
        public Builder DetectedPlatform(string platformId)
        {
            if (!DetectedPlatformsList.Contains(platformId))
            {
                DetectedPlatformsList.Add(platformId);
            }

            return this;
        }

        /// <summary>Records an existing platform pointer file and its text.</summary>
        public Builder PointerFile(string path, string content)
        {
            PointerFilesMap[path] = content;
            return this;
        }

        /// <summary>Records that a pointer file's path runs through a symbolic link.</summary>
        public Builder PointerBehindLink(string path)
        {
            PointersBehindLinksSet.Add(path);
            return this;
        }

        /// <summary>Sets where rendered traces land.</summary>
        public Builder OutputDirectory(string directory)
        {
            OutputDirectoryValue = directory;
            return this;
        }

        /// <summary>Sets the NarrativeTrace family version the project resolves (the version guard's input).</summary>
        public Builder ResolvedFamilyVersion(string? version)
        {
            ResolvedFamilyVersionValue = version;
            return this;
        }

        /// <summary>Builds the immutable snapshot.</summary>
        public ProjectState Build() => new(this);
    }
}
