using System;

namespace PP2ProductionStats.Structs;

public struct UpdateState
{
    public UpdateState(DateTime updatedAt, int identifier, bool isPortrait, bool isPerHour)
    {
        UpdatedAt = updatedAt;
        Identifier = identifier;
        IsPortrait = isPortrait;
        IsPerHour = isPerHour;
    }

    public DateTime UpdatedAt { get; }
    
    public int Identifier { get; }
    
    public bool IsPortrait { get; }
    
    public bool IsPerHour { get; }

    public bool IsDirty(UpdateState prevState)
    {
        return UpdatedAt - prevState.UpdatedAt >= TimeSpan.FromSeconds(3)
               || Identifier != prevState.Identifier
               || IsPortrait != prevState.IsPortrait
               || IsPerHour != prevState.IsPerHour;
    }
}