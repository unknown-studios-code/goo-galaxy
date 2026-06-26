using VContainer;
using VContainer.Unity;

namespace GooGalaxy.Runtime.Shared.Services
{
    /// <summary>
    /// Root Dependency Injection Lifetime Scope for the Goo Galaxy game.
    /// Explicitly registers game-wide systems, services, and MonoBehaviour components.
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        /// <summary>
        /// Configures container bindings by registering types and existing components to the container.
        /// </summary>
        /// <param name="builder">The VContainer container builder.</param>
        protected override void Configure(IContainerBuilder builder)
        {
            // Register systems and services explicitly here, for example:
            // builder.RegisterComponent(_myService).As<IMyService>();
            // builder.Register<InventorySystem>(Lifetime.Singleton);
        }
    }
}
