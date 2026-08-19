using System.Runtime.CompilerServices;

// DeckPresenter.SetKit and KitDataSO.SetAuthoredCards are authoring seams the test fixtures build a deck
// through, so the two test assemblies are the only holders of this assembly's internals.
[assembly: InternalsVisibleTo("GooGalaxy.Tests.EditMode")]
[assembly: InternalsVisibleTo("GooGalaxy.Tests.PlayMode")]
