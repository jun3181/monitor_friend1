using UnityEngine;

namespace Mate.Runtime.Core
{
    public enum MateState
    {
        Initialize,
        Idle,
        Walking,
        Picked,
        Dragged,
        Reacting
    }

    public enum MateReactionType
    {
        Click,
        Stroke,
        DragStart,
        DragEnd
    }

    public static class MateStateExtensions
    {
        public static bool BlocksAutonomousMotion(this MateState state)
        {
            return state == MateState.Picked || state == MateState.Dragged || state == MateState.Reacting;
        }

        public static string ToDebugLabel(this MateState state)
        {
            return state.ToString();
        }
    }
}
