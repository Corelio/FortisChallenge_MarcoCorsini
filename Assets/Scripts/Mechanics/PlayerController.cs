// Created to separate player control logic from jump logic for easier testing.
// Also implements interfaces for input, ground probing, and jump model parameters.
// This allows for mocking these dependencies in unit tests.
// Depends on Platformer.Interfaces abstractions.

using UnityEngine;
using Platformer.Gameplay;
using static Platformer.Core.Simulation;
using Platformer.Model;
using Platformer.Core;
using UnityEngine.InputSystem;
using JumpState = Platformer.Gameplay.JumpState;
using Platformer.Interfaces;

namespace Platformer.Mechanics
{
    /// <summary>
    /// This is the main class used to implement control of the player.
    /// It is a superset of the AnimationController class, but is inlined to allow for any kind of customisation.
    /// </summary>
    public class PlayerController : KinematicObject, IGroundProbe, IPlayerInput, IJumpModel
    {
        public AudioClip jumpAudio;
        public AudioClip respawnAudio;
        public AudioClip ouchAudio;

        /// <summary>
        /// Max horizontal speed of the player.
        /// </summary>
        public float maxSpeed = 7;
        /// <summary>
        /// Initial jump velocity at the start of a jump.
        /// </summary>
        public float jumpTakeOffSpeed = 7;

        public JumpState jumpState = JumpState.Grounded;
        /*internal new*/ public Collider2D collider2d;
        /*internal new*/ public AudioSource audioSource;
        public Health health;
        public bool controlEnabled = true;

        Vector2 move;
        SpriteRenderer spriteRenderer;
        internal Animator animator;
        readonly PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        private InputAction m_MoveAction;
        private InputAction m_JumpAction;

        public Bounds Bounds => collider2d.bounds;

        private PlayerJumpLogic jumpLogic = new PlayerJumpLogic();
        private bool applyJumpCutPending;
        private bool doJumpImpulsePending;

        float IJumpModel.JumpModifier => model.jumpModifier;
        float IJumpModel.JumpDeceleration => model.jumpDeceleration;

        public float MoveX() => m_MoveAction.ReadValue<Vector2>().x;
        public bool JumpPressedThisFrame() => m_JumpAction.WasPressedThisFrame();
        public bool JumpReleasedThisFrame() => m_JumpAction.WasReleasedThisFrame();

        void Awake()
        {
            health = GetComponent<Health>();
            audioSource = GetComponent<AudioSource>();
            collider2d = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();

            m_MoveAction = InputSystem.actions.FindAction("Player/Move");
            m_JumpAction = InputSystem.actions.FindAction("Player/Jump");
        }

        // Override Update to handle input and jump logic
        protected override void Update()
        {
            if (controlEnabled)
            {
                move.x = m_MoveAction.ReadValue<Vector2>().x;
                m_MoveAction.Enable();
                m_JumpAction.Enable();
            }
            else
            {
                move.x = 0;
                m_MoveAction.Disable();
                m_JumpAction.Disable();
            }

            // Process jump logic
            // using the separated PlayerJumpLogic class
            var outp = jumpLogic.Tick(
                jumpState,
                this,
                this,
                this,
                maxSpeed,
                jumpTakeOffSpeed,
                velocity.y
                );

            doJumpImpulsePending = outp.DoJumpImpulse;
            applyJumpCutPending = outp.ApplyJumpCut;

            targetVelocity = new Vector2(outp.TargetVelocityX, 0f);

            // Update sprite direction and animator parameters
            if (outp.TargetVelocityX > 0.01f) spriteRenderer.flipX = false;
            else if (outp.TargetVelocityX < -0.01f) spriteRenderer.flipX = true;

            animator.SetBool("grounded", IsGrounded);
            animator.SetFloat("velocityX", Mathf.Abs(velocity.x) / maxSpeed);

            // Fire original Simulation events when state edges happen
            if (jumpState == JumpState.Jumping && jumpLogic.State == JumpState.InFlight && !IsGrounded)
            {
                Schedule<PlayerJumped>().player = this;
            }
            if ((jumpState == JumpState.InFlight || jumpState == JumpState.Jumping) && jumpLogic.State == JumpState.Landed && IsGrounded)
            {
                Schedule<PlayerLanded>().player = this;
            }

            jumpState = jumpLogic.State;

            base.Update();
        }

        // ComputeVelocity overridden to integrate jump logic
        protected override void ComputeVelocity()
        {
            // Update vertical velocity based on jump logic
            velocity.y = jumpLogic.ComputeNewVelY(
                IsGrounded,
                velocity.y,
                doJumpImpulsePending,
                applyJumpCutPending,
                jumpTakeOffSpeed,
                model.jumpModifier,
                model.jumpDeceleration
                );

            doJumpImpulsePending = false;
            applyJumpCutPending = false;

            // Set horizontal target velocity
            targetVelocity = new Vector2(move.x * maxSpeed, velocity.y);
        }
    }
}