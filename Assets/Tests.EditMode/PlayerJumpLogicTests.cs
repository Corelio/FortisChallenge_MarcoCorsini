using NUnit.Framework;
using Platformer.Gameplay;
using Platformer.Interfaces;

class FakeInput : IPlayerInput
{
    public float moveX;
    public bool pressed;
    public bool released;

    public float MoveX() => moveX;
    public bool JumpPressedThisFrame() { var v = pressed; pressed = false; return v; }
    public bool JumpReleasedThisFrame() { var v = released; released = false; return v; }
}

class FakeGround : IGroundProbe
{
    public bool IsGrounded { get; set; } = true;
}

class FakeModel : IJumpModel
{
    public float JumpModifier { get; set; } = 1f;
    public float JumpDeceleration { get; set; } = 0.5f;
}

public class PlayerJumpLogicTests
{
    // 1) Grounded -> PrepareToJump/Jumping (impulse applied exactly once)
    [Test]
    public void Grounded_PressJump_TriggersImpulse_AndEntersJump()
    {
        var logic = new PlayerJumpLogic();
        var input = new FakeInput { pressed = true, moveX = 0f };
        var ground = new FakeGround { IsGrounded = true };
        var model  = new FakeModel { JumpModifier = 1f };

        var t = logic.Tick(JumpState.Grounded, input, ground, model,
                           maxSpeed: 7f, jumpTakeOffSpeed: 10f, currentVelY: 0f);

        Assert.AreEqual(JumpState.Jumping, t.NewState);
        Assert.IsTrue(t.DoJumpImpulse);
        Assert.IsFalse(t.ApplyJumpCut);
    }

    // 2) While still grounded, holding jump should NOT retrigger impulse next tick
    [Test]
    public void JumpImpulse_OnlyOnce_OnInitialPress()
    {
        var logic = new PlayerJumpLogic();
        var input = new FakeInput { pressed = true };
        var ground = new FakeGround { IsGrounded = true };
        var model  = new FakeModel();

        // First tick: press → impulse
        var t0 = logic.Tick(JumpState.Grounded, input, ground, model, 7, 10, 0);
        Assert.IsTrue(t0.DoJumpImpulse);

        // Second tick: no new press → no new impulse
        var t1 = logic.Tick(t0.NewState, input, ground, model, 7, 10, 0);
        Assert.IsFalse(t1.DoJumpImpulse);
    }

    // 3) Jumping -> InFlight when we leave the ground
    [Test]
    public void Jumping_LeavesGround_BecomesInFlight()
    {
        var logic = new PlayerJumpLogic();
        var input = new FakeInput { pressed = true };
        var ground = new FakeGround { IsGrounded = true };
        var model  = new FakeModel();

        var t0 = logic.Tick(JumpState.Grounded, input, ground, model, 7, 10, 0); // impulse
        ground.IsGrounded = false;
        var t1 = logic.Tick(t0.NewState, input, ground, model, 7, 10, 8); // going up

        Assert.AreEqual(JumpState.InFlight, t1.NewState);
    }

    // 4) Early release in air applies jump-cut (deceleration) only if vy > 0
    [Test]
    public void ReleaseEarly_InAir_EnablesJumpCut_AndDeceleratesVy()
    {
        var logic = new PlayerJumpLogic();
        var input = new FakeInput();
        var ground = new FakeGround { IsGrounded = false };
        var model  = new FakeModel { JumpDeceleration = 0.4f };

        // We’re in the upward phase
        input.released = true;
        var t = logic.Tick(JumpState.Jumping, input, ground, model, 7, 10, currentVelY: 8f);
        Assert.IsTrue(t.ApplyJumpCut);

        var vy = logic.ComputeNewVelY(
            isGrounded: false,
            currentVelY: 8f,
            doJumpImpulse: false,
            applyJumpCut: true,
            jumpTakeOffSpeed: 10f,
            jumpModifier: 1f,
            jumpDeceleration: model.JumpDeceleration
        );
        Assert.AreEqual(8f * model.JumpDeceleration, vy, 1e-5f);
    }

    // 5) Releasing jump while descending does NOT apply deceleration
    [Test]
    public void ReleaseWhileDescending_DoesNotDecelerate()
    {
        var logic = new PlayerJumpLogic();
        var input = new FakeInput { released = true };
        var ground = new FakeGround { IsGrounded = false };
        var model = new FakeModel { JumpDeceleration = 0.2f };

        // Tick while descending
        var t = logic.Tick(JumpState.InFlight, input, ground, model,
                        maxSpeed: 7f, jumpTakeOffSpeed: 10f, currentVelY: -3f);

        // Regardless of t.ApplyJumpCut being true/false, vy must be unchanged when vy <= 0
        var vy = logic.ComputeNewVelY(
            isGrounded: false,
            currentVelY: -3f,
            doJumpImpulse: false,
            applyJumpCut: t.ApplyJumpCut,
            jumpTakeOffSpeed: 10f,
            jumpModifier: 1f,
            jumpDeceleration: model.JumpDeceleration
        );

        Assert.AreEqual(-3f, vy, 1e-5f);
    }
    
    // 6) InFlight -> Landed when ground is regained
    [Test]
    public void InFlight_To_Landed_WhenTouchingGround()
    {
        var logic = new PlayerJumpLogic();
        var input = new FakeInput();
        var ground = new FakeGround { IsGrounded = false };
        var model  = new FakeModel();

        var t0 = logic.Tick(JumpState.InFlight, input, ground, model, 7, 10, -1);
        ground.IsGrounded = true;
        var t1 = logic.Tick(t0.NewState, input, ground, model, 7, 10, -1);

        Assert.AreEqual(JumpState.Landed, t1.NewState);
    }

    // 7) Landed -> Grounded on the next evaluation
    [Test]
    public void Landed_Returns_To_Grounded()
    {
        var logic = new PlayerJumpLogic();
        var input = new FakeInput();
        var ground = new FakeGround { IsGrounded = true };
        var model  = new FakeModel();

        var t = logic.Tick(JumpState.Landed, input, ground, model, 7, 10, 0);
        Assert.AreEqual(JumpState.Grounded, t.NewState);
    }

    // 8) Horizontal movement: TargetVelocityX follows input * maxSpeed
    [Test]
    public void TargetVelocityX_Follows_MoveX_Times_MaxSpeed()
    {
        var logic = new PlayerJumpLogic();
        var input = new FakeInput { moveX = -0.6f };
        var ground = new FakeGround { IsGrounded = true };
        var model  = new FakeModel();

        var t = logic.Tick(JumpState.Grounded, input, ground, model, maxSpeed: 5f, jumpTakeOffSpeed: 10f, currentVelY: 0);
        Assert.AreEqual(-0.6f * 5f, t.TargetVelocityX, 1e-5f);
    }

    // 9) No mid-air re-jump (pressing jump in-flight should not trigger impulse)
    [Test]
    public void PressingJump_InAir_DoesNotImpulse()
    {
        var logic = new PlayerJumpLogic();
        var input = new FakeInput { pressed = true };
        var ground = new FakeGround { IsGrounded = false };
        var model  = new FakeModel();

        var t = logic.Tick(JumpState.InFlight, input, ground, model, 7, 10, 2f);
        Assert.IsFalse(t.DoJumpImpulse);
        Assert.AreNotEqual(JumpState.Jumping, t.NewState);
    }

    // 10) Impulse size respects JumpModifier
    [Test]
    public void ComputeNewVelY_Impulse_Uses_JumpModifier()
    {
        var logic = new PlayerJumpLogic();
        var vy = logic.ComputeNewVelY(
            isGrounded: true,
            currentVelY: 0f,
            doJumpImpulse: true,
            applyJumpCut: false,
            jumpTakeOffSpeed: 12f,
            jumpModifier: 1.25f,
            jumpDeceleration: 0.4f
        );
        Assert.AreEqual(12f * 1.25f, vy, 1e-5f);
    }

    // 11) No impulse if not grounded (pressing jump off-ground shouldn’t set impulse)
    [Test]
    public void NoImpulse_When_NotGrounded()
    {
        var logic = new PlayerJumpLogic();
        var vy = logic.ComputeNewVelY(
            isGrounded: false,
            currentVelY: 3f,
            doJumpImpulse: true,
            applyJumpCut: false,
            jumpTakeOffSpeed: 10f,
            jumpModifier: 1f,
            jumpDeceleration: 0.5f
        );
        Assert.AreEqual(3f, vy, 1e-5f);
    }

    // 12) Full jump cycle test: Grounded -> Jumping (impulse) -> InFlight -> Landed -> Grounded
    [Test]
    public void Jump_State_Progression_Grounded_To_Landed()
    {
        var logic = new PlayerJumpLogic();
        var input = new FakeInput();
        var ground = new FakeGround();
        var model = new FakeModel();

        var state = JumpState.Grounded;

        // Press jump on ground -> PrepareToJump -> Jumping + impulse
        input.pressed = true;
        var t0 = logic.Tick(state, input, ground, model, maxSpeed:7, jumpTakeOffSpeed:7, currentVelY:0);
        Assert.AreEqual(JumpState.Jumping, t0.NewState);
        Assert.IsTrue(t0.DoJumpImpulse);
        state = t0.NewState;

        // Leave ground -> InFlight
        ground.IsGrounded = false;
        var t1 = logic.Tick(state, input, ground, model, 7, 7, currentVelY:0);
        Assert.AreEqual(JumpState.InFlight, t1.NewState);
        state = t1.NewState;

        // Come back to ground -> Landed
        ground.IsGrounded = true;
        var t2 = logic.Tick(state, input, ground, model, 7, 7, currentVelY:-1);
        Assert.AreEqual(JumpState.Landed, t2.NewState);
        state = t2.NewState;

        // Landed -> Grounded
        var t3 = logic.Tick(state, input, ground, model, 7, 7, currentVelY:0);
        Assert.AreEqual(JumpState.Grounded, t3.NewState);
    }

    // 13) Jump cut applies deceleration when releasing jump early
    [Test]
    public void Jump_Cut_Applies_Deceleration_When_Releasing_Early()
    {
        var logic = new PlayerJumpLogic();
        var input = new FakeInput();
        var ground = new FakeGround();
        var model = new FakeModel { JumpDeceleration = 0.4f, JumpModifier = 1f };

        // Start jump from grounded
        input.pressed = true;
        var t0 = logic.Tick(JumpState.Grounded, input, ground, model, 7, 10, 0);
        Assert.IsTrue(t0.DoJumpImpulse);

        // Simulate we’re in-air with upward velocity
        ground.IsGrounded = false;

        // Release jump early
        input.released = true;
        var t1 = logic.Tick(JumpState.Jumping, input, ground, model, 7, 10, currentVelY: 8);
        Assert.IsTrue(t1.ApplyJumpCut);

        // Compute new velocity Y based on jump cut
        float vy = logic.ComputeNewVelY(
            isGrounded: false,
            currentVelY: 8f,
            doJumpImpulse: false,
            applyJumpCut: true,
            jumpTakeOffSpeed: 10f,
            jumpModifier: model.JumpModifier,
            jumpDeceleration: model.JumpDeceleration
        );

        Assert.AreEqual(8f * model.JumpDeceleration, vy, 1e-5f);
    }
    
    // 14) Grounded with no input should remain grounded and stable
    [Test]
    public void Grounded_NoInput_RemainsGrounded()
    {
        var logic = new PlayerJumpLogic();
        var input = new FakeInput(); // no press, no release
        var ground = new FakeGround { IsGrounded = true };
        var model  = new FakeModel();

        var state = JumpState.Grounded;

        var t = logic.Tick(state, input, ground, model,
                        maxSpeed: 7f, jumpTakeOffSpeed: 10f, currentVelY: 0f);

        // State shouldn't change
        Assert.AreEqual(JumpState.Grounded, t.NewState);

        // Should not trigger jump impulse or cut
        Assert.IsFalse(t.DoJumpImpulse);
        Assert.IsFalse(t.ApplyJumpCut);

        // Horizontal velocity should match input (zero)
        Assert.AreEqual(0f, t.TargetVelocityX, 1e-5f);
    }
}
