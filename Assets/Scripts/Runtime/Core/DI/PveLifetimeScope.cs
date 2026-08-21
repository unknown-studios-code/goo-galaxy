using GooGalaxy.Runtime.AI.Controllers;
using VContainer;
using VContainer.Unity;

namespace GooGalaxy.Runtime.Core.DI
{
    /// <summary>
    /// Child scope carrying everything a single-player match adds to a match: the machine-driven opponent, and
    /// nothing else. Present only in the PvE scene.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is not a line in <see cref="GameLifetimeScope" />.</b> Almost every entry there is a
    /// <c>RegisterComponentInHierarchy</c>, which <i>finds</i> a component already in the scene — so registering
    /// <see cref="AiController" /> there would make it mandatory in the PvP scene too, and that scene's
    /// <c>Build</c> would throw over an opponent it must not have. A child scope confines the requirement to the
    /// scene that actually carries one, and inherits every match component from the parent unchanged.
    /// </para>
    /// <para>
    /// <b>How it finds its parent.</b> VContainer 1.18 resolves a parent through
    /// <c>LifetimeScope.GetRuntimeParent</c>, which consults, in order, the reference a <c>CreateChild</c> call
    /// planted, the virtual <c>FindParent</c>, the serialized <see cref="LifetimeScope.parentReference" />
    /// <i>type</i>, the global override stack, and finally the project-wide root. <b>Transform nesting is not
    /// among them</b> — putting this object under the parent's GameObject does nothing at all. Of the two
    /// mechanisms a scene can use, only the serialized type survives the parent not having built yet: it throws
    /// <c>VContainerParentTypeReferenceNotFound</c>, which <c>Awake</c> catches to enqueue this scope, and the
    /// parent's own <c>Build</c> ends by awakening every child waiting on its type. Overriding
    /// <c>FindParent</c> has no such retry — it would hand back an unbuilt parent and dereference its null
    /// container — and both scopes sit at <c>[DefaultExecutionOrder(-5000)]</c>, which fixes no order between
    /// them.
    /// </para>
    /// <para>
    /// The type is therefore stamped here rather than left to the Inspector, so a scene cannot be authored with
    /// it unset.
    /// </para>
    /// </remarks>
    public class PveLifetimeScope : LifetimeScope
    {
        protected override void Awake()
        {
            // The retry path assigns the resolved parent onto the same struct before re-entering Awake, and
            // overwriting it unconditionally would throw that away.
            if (parentReference.Type == null)
            {
                parentReference = ParentReference.Create<GameLifetimeScope>();
            }

            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<AiController>().AsSelf();
        }
    }
}
