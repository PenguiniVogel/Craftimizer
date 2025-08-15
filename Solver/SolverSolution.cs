using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;

namespace Craftimizer.Solver;

public readonly record struct SolverSolution
{
    private readonly List<ActionType> _actions = null!;
    
    public readonly IReadOnlyList<ActionType> Actions { get => _actions; init => ActionEnumerable = value; }
    public readonly IEnumerable<ActionType> ActionEnumerable { init => _actions = value.ToList(); }
    public readonly SimulationState State { get; init; }

    public SolverSolution(IEnumerable<ActionType> actions, in SimulationState state)
    {
        ActionEnumerable = actions;
        State = state;
    }

    public void Deconstruct(out IReadOnlyList<ActionType> actions, out SimulationState state)
    {
        actions = Actions;
        state = State;
    }

    internal static IEnumerable<ActionType> SanitizeCombo(ActionType action)
    {
        if (action.Base() is BaseComboAction combo)
        {
            foreach (var a in SanitizeCombo(combo.ActionTypeA))
                yield return a;
            foreach (var b in SanitizeCombo(combo.ActionTypeB))
                yield return b;
        }
        else
            yield return action;
    }
}
