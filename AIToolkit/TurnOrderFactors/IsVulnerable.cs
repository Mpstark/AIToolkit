﻿using BattleTech;

namespace AIToolkit.TurnOrderFactors
{
    public class IsVulnerable : ITurnOrderFactor
    {
        public float EvaluateUnit(AbstractActor unit)
        {
            return unit.IsVulnerableToCalledShots() ? 1f : 0f;
        }
    }
}
