namespace Platformer.Interfaces
{
    public interface IPlayerInput
    {
        float MoveX();
        bool JumpPressedThisFrame();
        bool JumpReleasedThisFrame();
    }

    public interface IGroundProbe
    {
        bool IsGrounded { get; }
    }

    public interface IJumpModel
    {
        float JumpModifier { get; }
        float JumpDeceleration { get; }
    }
}