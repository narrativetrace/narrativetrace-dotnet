// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
// Interfaces and implementations placed in namespaces the shipped default
// excludes (Moq, Xunit) or that probe prefix boundaries (Acme.Impl / Acme.Implx).
namespace Moq.Probe
{
    public interface IMocked { string Ping(); }

    public sealed class Mocked : IMocked { public string Ping() => "m"; }

    public interface IMockedToo { string Ping(); }

    public sealed class MockedToo : IMockedToo { public string Ping() => "m2"; }
}

namespace Xunit.Probe
{
    public interface IRunnerHelper { string Ping(); }

    public sealed class RunnerHelper : IRunnerHelper { public string Ping() => "x"; }
}

namespace Acme.Impl
{
    public interface IInner { string Ping(); }

    public sealed class Inner : IInner { public string Ping() => "i"; }
}

namespace Acme.Implx
{
    public interface ISibling { string Ping(); }

    public sealed class Sibling : ISibling { public string Ping() => "s"; }
}

namespace NarrativeTrace.DependencyInjection.Tests.Contracts
{
    // Interface in the included namespace, implementation in an excludable one.
    public interface IBridge { string Ping(); }
}

namespace Acme.Hidden
{
    public sealed class Bridge : NarrativeTrace.DependencyInjection.Tests.Contracts.IBridge
    {
        public string Ping() => "b";
    }
}
