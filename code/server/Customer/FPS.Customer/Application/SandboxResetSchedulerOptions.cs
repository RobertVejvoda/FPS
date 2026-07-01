namespace FPS.Customer.Application;

/// <summary>
/// PLAT003B — nightly scheduled sandbox-reset options (bound to the "SandboxReset:Scheduler" section).
/// Default OFF; the demo/evaluation profile opts in. Even when enabled, each configured target is reset
/// only if <see cref="SandboxResetService"/>'s guard confirms it is a resettable sandbox from stored
/// tenant metadata — so a real customer tenant can never be reset even through a misconfigured target list.
/// </summary>
public sealed class SandboxResetSchedulerOptions
{
    public const string SectionName = "SandboxReset:Scheduler";

    /// <summary>Master switch for the nightly job. Off unless the hosting profile opts in.</summary>
    public bool Enabled { get; set; }

    /// <summary>Tenant ids the nightly job attempts to reset. Defaults to the Green Logistics sandbox.</summary>
    public List<string> Targets { get; set; } = ["greenlogistics"];
}
