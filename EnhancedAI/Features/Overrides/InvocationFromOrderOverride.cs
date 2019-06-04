﻿using BattleTech;
using EnhancedAI.BehaviorNodes.Orders;

namespace EnhancedAI.Features.Overrides
{
    public static class InvocationFromOrderOverride
    {
        public static InvocationMessage TryCreateInvocation(AbstractActor unit, OrderInfo order)
        {
            if (!(order is IOrderToInvocation modOrder))
                return null;

            return modOrder.GetInvocation(unit);
        }
    }
}