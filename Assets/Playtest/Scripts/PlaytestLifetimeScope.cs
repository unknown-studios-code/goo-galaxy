using VContainer;
using VContainer.Unity;

namespace GooGalaxy.Playtest
{
    /// <summary>
    /// Child composition root for the playtest harness.
    /// </summary>
    /// <remarks>
    /// The harness types live in this assembly, and <c>GameLifetimeScope</c> sits in <c>Runtime.Core</c>, which
    /// cannot reference it without inverting the dependency direction — so they are registered here instead. As a
    /// child scope this inherits every registration the root made, which is what lets the bootstrap resolve the
    /// board, card and energy systems it drives alongside the HUD registered below.
    /// <para>
    /// The parent comes from the serialized <c>Parent</c> field, which must name <c>GameLifetimeScope</c>.
    /// Nothing about the transform hierarchy participates: this component may sit on the same GameObject as the
    /// root or anywhere else in the scene, but with <c>Parent</c> unset it builds a container of its own, every
    /// inherited resolution fails, and the bootstrap wakes with null references.
    /// </para>
    /// <para>
    /// Both scopes carry <c>[DefaultExecutionOrder(-5000)]</c>, so when they share a GameObject their relative
    /// <c>Awake</c> order is the serialized component order. Waking first is survivable — the root has not built
    /// yet, so <c>GetRuntimeParent</c> throws, and <c>LifetimeScope</c> catches it and re-runs this scope from
    /// its waiting list once the root wakes — but it is a recovery path rather than the direct one.
    /// </para>
    /// </remarks>
    public class PlaytestLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<PlaytestHudView>().AsSelf();
            builder.RegisterComponentInHierarchy<PlaytestBootstrap>().AsSelf();
        }
    }
}
