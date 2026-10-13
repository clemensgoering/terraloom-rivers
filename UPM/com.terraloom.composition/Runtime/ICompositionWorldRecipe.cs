namespace TerraLoom.Integration
{
    /// <summary>Optional consumer recipe for a complete Core/Rivers/Paths rebuild.
    /// The Workbench invokes this instead of guessing the order of independent modules.</summary>
    public interface ICompositionWorldRecipe
    {
        bool PreparesTerrain { get; }
        bool IncludesDecoration { get; }
        bool RebuildWorld();
        string WorldDiagnostics { get; }
    }
}
