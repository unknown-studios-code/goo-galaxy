using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Cards.Presenters;
using VContainer;
using VContainer.Unity;

namespace GooGalaxy.Runtime.Core.DI
{
    /// <summary>
    /// Root Dependency Injection Lifetime Scope for the Goo Galaxy game.
    /// Explicitly registers game-wide systems, services, and MonoBehaviour components.
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<GridPresenter>().AsSelf();
            builder.RegisterComponentInHierarchy<UnitPresenter>().AsSelf();
            builder.RegisterComponentInHierarchy<CardPresenter>().AsSelf();
        }
    }
}
