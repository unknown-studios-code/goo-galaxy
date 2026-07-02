using GooGalaxy.Runtime.Board.Presenters;
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
        // Configures container bindings by registering types and existing components to the container.
        protected override void Configure(IContainerBuilder builder)
        {
            // Register systems and services explicitly here:
            builder.RegisterComponentInHierarchy<GridPresenter>().AsSelf();
        }
    }
}
