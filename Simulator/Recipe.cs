using System.Diagnostics;

namespace Craftimizer.Simulator;

[DebuggerDisplay("IsExpert: {IsExpert} | ClassJobLevel: {ClassJobLevel} | ConditionsFlag: {ConditionsFlag} | MaxDurability: {MaxDurability} | MaxQuality: {MaxQuality} | MaxProgress: {MaxProgress} | QualityModifier: {QualityModifier} | QualityDivider: {QualityDivider} | ProgressModifier: {ProgressModifier} | ProgressDivider: {ProgressDivider}")]
public sealed record RecipeInfo
{
    public bool IsExpert { get; init; }
    public int ClassJobLevel { get; init; }
    public ushort ConditionsFlag { get; init; }
    public int MaxDurability { get; init; }
    public int MaxQuality { get; init; }
    public int MaxProgress { get; init; }
    public int QualityModifier { get; init; }
    public int QualityDivider { get; init; }
    public int ProgressModifier { get; init; }
    public int ProgressDivider { get; init; }
}
