// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Doctor;

public sealed class DoctorFindingSkillsTests
{
    /// <summary>
    /// The whole table, spelled out. A change of mind about which skill fixes which finding class is
    /// a change to a published contract — an agent reads <c>skill</c> and follows that procedure — so
    /// it should have to edit a row here, not just a line of production code.
    /// </summary>
    [Theory]
    [InlineData("toolchain.package-versions-aligned", "add-narrative-tracing")]
    [InlineData("toolchain.companion-packages", "add-narrative-tracing")]
    [InlineData("config.output-env", "narrativetrace-doctor")]
    [InlineData("config.exclude", "narrativetrace-doctor")]
    [InlineData("config.service-identity", "add-narrative-tracing")]
    [InlineData("config.registration-pair", "add-narrative-tracing")]
    [InlineData("config.appsettings-keys", "narrativetrace-doctor")]
    [InlineData("trap.silent-sink", "narrativetrace-doctor")]
    [InlineData("trap.proxy-interface", "add-narrative-tracing")]
    [InlineData("trap.redaction-proof", "narrativetrace-doctor")]
    [InlineData("trap.approval-traces", "narrativetrace-doctor")]
    [InlineData("config.approval-mode", "narrativetrace-verify")]
    [InlineData("trap.llms-before-you-start", "add-narrative-tracing")]
    public void Names_the_skill_that_fixes_each_finding_class(string id, string skill)
    {
        Assert.Equal(skill, DoctorFindingSkills.ForCheck(id));
    }

    /// <summary>Two ids carry no skill on purpose, and the table still KNOWS them — decided, not forgotten.</summary>
    [Theory]
    [InlineData("toolchain.target-framework")]
    [InlineData("config.skills-installed")]
    public void Names_no_skill_where_none_fixes_it(string id)
    {
        Assert.Null(DoctorFindingSkills.ForCheck(id));
        Assert.True(DoctorFindingSkills.Knows(id));
    }

    /// <summary>
    /// The table decides only for the short form: a finding built with the canonical constructor may
    /// point at any catalogue skill, the clarity skill included.
    /// </summary>
    [Fact]
    public void A_canonical_construction_may_point_at_the_clarity_skill()
    {
        var finding = new DoctorFinding("trap.silent-sink", true, "message", "", "https://x", "add-narrativetrace-clarity");

        Assert.Equal("add-narrativetrace-clarity", finding.Skill);
    }

    [Fact]
    public void An_id_the_table_never_heard_of_is_neither_known_nor_mapped()
    {
        Assert.Null(DoctorFindingSkills.ForCheck("trap.invented-yesterday"));
        Assert.False(DoctorFindingSkills.Knows("trap.invented-yesterday"));
    }

    /// <summary>
    /// A null id can reach this table from <see cref="DoctorFinding"/>'s <c>Pass</c>/<c>Fail</c>
    /// factories, so the lookup answers rather than throwing — a lookup failure here must never mask
    /// whatever a caller does with a null id.
    /// </summary>
    [Fact]
    public void A_null_id_is_answered()
    {
        Assert.Null(DoctorFindingSkills.ForCheck(null));
        Assert.False(DoctorFindingSkills.Knows(null));
    }
}
