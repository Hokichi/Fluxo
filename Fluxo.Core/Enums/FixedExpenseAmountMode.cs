namespace Fluxo.Core.Enums;

/// <summary>
/// Whether a fixed expense has a set amount or requires manual input each cycle.
/// </summary>
public enum FixedExpenseAmountMode
{
    Fixed = 0,      // same every cycle (e.g. rent)
    Variable = 1    // must enter each time (e.g. electricity)
}