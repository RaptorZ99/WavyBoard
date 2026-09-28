// Test tooling (Tools/playtest.py): editor and development builds only, never in the released game.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using WavyBoard.Rider;
using UnityEngine;

namespace WavyBoard.Debugging
{
    /// <summary>
    /// Hands the rider's controls to a <see cref="RiderBot"/> in Play mode (automated playtests, demo captures). The
    /// bot thinks just before each physics step of the rider; disabling this gives the controls back to the player.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class RiderAutoPilot : MonoBehaviour
    {
        public RiderController rider;
        public RiderBot.Plan plan = RiderBot.Plan.Pocket;
        public bool pumps = true;

        public RiderBot Bot { get; } = new RiderBot();

        void OnEnable()
        {
            if (rider == null) rider = GetComponent<RiderController>();
            if (rider != null) rider.Input = Bot;
        }

        void OnDisable()
        {
            if (rider != null && ReferenceEquals(rider.Input, Bot)) rider.Input = null;
        }

        void FixedUpdate()
        {
            if (rider == null) return;
            Bot.plan = plan;
            Bot.pumps = pumps;
            Bot.Think(rider, Time.fixedTime);
        }
    }
}
#endif
