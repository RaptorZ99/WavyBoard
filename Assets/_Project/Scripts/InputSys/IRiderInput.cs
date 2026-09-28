using WavyBoard.Tricks;
using UnityEngine;

namespace WavyBoard.InputSys
{
    /// <summary>
    /// The controls the rider reads each physics step. The player's pad / keyboard and mouse come through the
    /// <see cref="InputRouter"/>; a bot (the autopilot, the ride simulation of the tests) implements it directly, so it
    /// rides with exactly the controls a player has, nothing more.
    ///
    /// Presses are consumed: each one is read once.
    /// </summary>
    public interface IRiderInput
    {
        /// <summary>Left stick: x steers (in the air: spins), y paddles, and on the wave trims — forward drives, back eases off.</summary>
        Vector2 Move { get; }
        /// <summary>Paddling sprint (held).</summary>
        bool SprintHeld { get; }
        /// <summary>Stall (held): sit back and let the curl catch you.</summary>
        bool StallHeld { get; }
        /// <summary>Crouched, loading a pop (board stick pulled down, or the mouse button held).</summary>
        bool Crouched { get; }
        /// <summary>0..1: how loaded the crouch is.</summary>
        float CrouchCharge { get; }
        /// <summary>The board stick is parked out on the rim: in the air, a grab.</summary>
        bool StickHeld { get; }

        bool ConsumePump();
        bool ConsumeDuck();
        bool ConsumeStance();
        bool ConsumeReset();
        /// <summary>The pop button was released: true once, with the charge it was loaded with.</summary>
        bool ConsumeJump(out float charge);
        /// <summary>The gesture the board stick traced (Flick.None when there is none).</summary>
        FlickResult ConsumeFlick();

        void Rumble(float low, float high, float duration);
    }
}
