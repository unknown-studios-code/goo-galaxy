using System.IO;
using GooGalaxy.Runtime.Analytics.Controllers;
using GooGalaxy.Runtime.Analytics.Interfaces;
using GooGalaxy.Runtime.Analytics.Services;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Input.Controllers;
using GooGalaxy.Runtime.Input.Interfaces;
using GooGalaxy.Runtime.Input.Presenters;
using GooGalaxy.Runtime.Input.Views;
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Match.Services;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.UI.Presenters;
using GooGalaxy.Runtime.UI.Views;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace GooGalaxy.Runtime.Core.DI
{
    /// <summary>
    /// Root Dependency Injection Lifetime Scope for the Goo Galaxy game.
    /// Explicitly registers game-wide systems, services, and MonoBehaviour components.
    /// </summary>
    /// <remarks>
    /// Almost every entry is a <c>RegisterComponentInHierarchy</c>, which <b>finds</b> a component already in
    /// the scene rather than creating one — so each type registered that way becomes mandatory in any scene
    /// carrying this scope, and <c>Build</c> throws when one is absent. Registering a type is also what makes
    /// the container inject <i>into</i> it, which is why a component appears here even when nothing resolves it.
    /// <para>
    /// <see cref="MatchInitializer"/> is one exception: it is a plain class the container <b>constructs</b>,
    /// resolving the five presenters it needs out of the component registrations in this method — including
    /// <see cref="EnergyPresenter"/>, which is registered after it. Declaration order does not matter, because
    /// the container resolves by type; the line is placed where it reads best. Its lifetime is the scope's, so
    /// it is destroyed with the scene like everything else here.
    /// </para>
    /// <para>
    /// The analytics sink is another constructed entry, built from a factory because its folder comes from
    /// <see cref="Application.persistentDataPath" /> rather than from anything the container holds. Building the sink
    /// touches no file, but the <c>AnalyticsController</c> it feeds closes its session when it is destroyed, so a scope
    /// that is built and torn down still writes a short session file — which is why tests redirect it through
    /// <see cref="AnalyticsDirectoryOverride" />.
    /// </para>
    /// <para>
    /// Components instantiated at runtime — <c>CellView</c> from <see cref="GridView"/>, and the unit visuals
    /// that <see cref="UnitView"/> pools — can never be registered: they do not exist when this runs.
    /// </para>
    /// </remarks>
    public class GameLifetimeScope : LifetimeScope
    {
        /// <remarks>
        /// Test seam: when set before the container is built, analytics sessions are written here instead of under
        /// the persistent data path, so a fixture can point them at a temporary folder and delete it afterwards.
        /// Read once, by the sink factory, when the sink is first resolved.
        /// </remarks>
        internal string AnalyticsDirectoryOverride { get; set; }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<GridPresenter>().AsSelf();
            builder.RegisterComponentInHierarchy<UnitPresenter>().AsSelf();
            builder.RegisterComponentInHierarchy<CardPresenter>().AsSelf();
            builder.RegisterComponentInHierarchy<DeckPresenter>().AsSelf().As<ICardCycle>();
            builder.RegisterComponentInHierarchy<DeployController>().AsSelf();
            builder.RegisterComponentInHierarchy<CardDiscardController>().AsSelf();
            builder.RegisterComponentInHierarchy<MatchController>().AsSelf();
            builder.Register<MatchInitializer>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<EnergyPresenter>().AsSelf().As<IEnergyLedger>().As<IDiscardLedger>();
            builder.RegisterComponentInHierarchy<ConversionController>().AsSelf();
            builder.RegisterComponentInHierarchy<FuseController>().AsSelf();
            builder.RegisterComponentInHierarchy<AbilityController>().AsSelf();
            builder.RegisterComponentInHierarchy<GridView>().AsSelf();
            builder.RegisterComponentInHierarchy<UnitView>().AsSelf();
            builder.RegisterComponentInHierarchy<MatchHudPresenter>().AsSelf();
            builder.RegisterComponentInHierarchy<MatchHudView>().AsSelf().As<IHandGestureSource>();
            builder.RegisterComponentInHierarchy<PointerInputView>().AsSelf().As<IPointerSource>();
            builder.RegisterComponentInHierarchy<TargetHighlightPresenter>().AsSelf();
            builder.RegisterComponentInHierarchy<MatchInputController>().AsSelf();
            builder.Register(_ => CreateAnalyticsSink(), Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<AnalyticsController>().AsSelf();
        }

        private IAnalyticsSink CreateAnalyticsSink()
        {
            string directoryPath = string.IsNullOrEmpty(AnalyticsDirectoryOverride)
                ? Path.Combine(Application.persistentDataPath, JsonlFileSink.DefaultDirectoryName)
                : AnalyticsDirectoryOverride;

            return new JsonlFileSink(directoryPath);
        }
    }
}
