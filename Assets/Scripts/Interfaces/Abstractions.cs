// Created to hold interface abstractions for player components
// to depend on, to facilitate testing/mocking.
namespace Platformer.Interfaces
{
    // Abstraction for player input
    public interface IPlayerInput
    {
        float MoveX();
        bool JumpPressedThisFrame();
        bool JumpReleasedThisFrame();
    }

    // Abstraction for ground probing
    public interface IGroundProbe
    {
        bool IsGrounded { get; }
    }

    // Abstraction for jump model parameters
    public interface IJumpModel
    {
        float JumpModifier { get; }
        float JumpDeceleration { get; }
    }
}