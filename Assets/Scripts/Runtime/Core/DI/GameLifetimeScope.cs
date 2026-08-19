using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Controllers;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Shared.Interfaces;
using VContainer;
using VContainer.Unity;

namespace GooGalaxy.Runtime.Core.DI
{
    /// <summary>
    /// Root Dependency Injection Lifetime Scope for the Goo Galaxy game.
    /// Explicitly registers game-wide systems, services, and MonoBehaviour components.
    /// </summary>
    /// <remarks>
    /// Every entry is a <c>RegisterComponentInHierarchy</c>, which <b>finds</b> a component already in the scene
    /// rather than creating one — so each type listed here becomes mandatory in any scene carrying this scope,
    /// and <c>Build</c> throws when one is absent. Registering a type is also what makes the container inject
    /// <i>into</i> it, which is why a component appears here even when nothing resolves it.
    /// <para>
    /// Components instantiated at runtime — <c>CellView</c> from <see cref="GridView"/>, and the unit visuals
    /// that <see cref="UnitView"/> pools — can never be registered: they do not exist when this runs.
    /// </para>
    /// </remarks>
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<GridPresenter>().AsSelf();
            builder.RegisterComponentInHierarchy<UnitPresenter>().AsSelf();
            builder.RegisterComponentInHierarchy<CardPresenter>().AsSelf();
            builder.RegisterComponentInHierarchy<DeckPresenter>().AsSelf();
            builder.RegisterComponentInHierarchy<DeployController>().AsSelf();
            builder.RegisterComponentInHierarchy<CardDiscardController>().AsSelf();
            builder.RegisterComponentInHierarchy<EnergyPresenter>().AsSelf().As<IEnergyLedger>().As<IDiscardLedger>();
            builder.RegisterComponentInHierarchy<ConversionController>().AsSelf();
            builder.RegisterComponentInHierarchy<FuseController>().AsSelf();
            builder.RegisterComponentInHierarchy<AbilityController>().AsSelf();
            builder.RegisterComponentInHierarchy<GridView>().AsSelf();
            builder.RegisterComponentInHierarchy<UnitView>().AsSelf();
        }
    }
}
