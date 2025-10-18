using Platformer.Interfaces;

namespace Platformer.Gameplay
{
    public enum JumpState { Grounded, PrepareToJump, Jumping, InFlight, Landed }

    public struct TickOutput
    {
        public JumpState NewState;
        public bool DoJumpImpulse;     // apply initial jump velocity
        public bool ApplyJumpCut;      // cut jump (decelerate) if released early
        public float TargetVelocityX;  // desired horizontal target velocity
    }

    public class PlayerJumpLogic
    {
        public JumpState State { get; private set; } = JumpState.Grounded;

        public TickOutput Tick(
            JumpState current,
            IPlayerInput input,
            IGroundProbe ground,
            IJumpModel model,
            float maxSpeed,
            float jumpTakeOffSpeed,
            float currentVelY)
        {
            State = current;
            var outp = new TickOutput { NewState = State, TargetVelocityX = input.MoveX() * maxSpeed };

            // Input-driven state changes
            if (State == JumpState.Grounded && input.JumpPressedThisFrame())
                State = JumpState.PrepareToJump;
            else if (input.JumpReleasedThisFrame())
                outp.ApplyJumpCut = true;

            // State machine
            switch (State)
            {
                case JumpState.PrepareToJump:
                    State = JumpState.Jumping;
                    outp.DoJumpImpulse = true; // initial velocity application happens in adapter
                    break;

                case JumpState.Jumping:
                    if (!ground.IsGrounded) State = JumpState.InFlight;
                    break;

                case JumpState.InFlight:
                    if (ground.IsGrounded) State = JumpState.Landed;
                    break;

                case JumpState.Landed:
                    State = JumpState.Grounded;
                    break;
            }

            outp.NewState = State;
            return outp;
        }

        public float ComputeNewVelY(bool isGrounded, float currentVelY, bool doJumpImpulse, bool applyJumpCut,
                                    float jumpTakeOffSpeed, float jumpModifier, float jumpDeceleration)
        {
            float vy = currentVelY;

            if (doJumpImpulse && isGrounded)
            {
                vy = jumpTakeOffSpeed * jumpModifier;
            }
            else if (applyJumpCut && vy > 0f)
            {
                vy = vy * jumpDeceleration;
            }

            return vy;
        }
    }
}
