﻿using Sandbox;

namespace BonitoBlitz.Activities;

public partial class BoardPawn : AnimatedEntity
{
	[Net] public libblitz.GameMember GameMember { get; private set; }

	public BoardPawn( libblitz.GameMember gameMember )
	{
		GameMember = gameMember;

		if ( gameMember.Client == null )
		{
			return;
		}

		// Load player clothing
		ClothingContainer clothing = new();
		clothing.LoadFromClient( gameMember.Client );

		// Dress player
		clothing.DressEntity( this );
	}

	public BoardPawn() => Host.AssertClient();

	public override void Spawn()
	{
		base.Spawn();

		//
		// Use a watermelon model
		//
		SetModel( "models/citizen/citizen.vmdl" );
		EyePosition = Position + Vector3.Up * 18; // for testing

		EnableDrawing = true;
		EnableHideInFirstPerson = true;
		EnableShadowInFirstPerson = true;

		Transmit = TransmitType.Always;

		// Go to tile position
		var tile = GameMember.CurrentTile;
		if ( tile != null )
		{
			Position = GameMember.CurrentTile.Position;
		}
	}

	/// <summary>
	/// Called every tick, clientside and serverside.
	/// </summary>
	public override void Simulate( Client cl )
	{
		base.Simulate( cl );

		Rotation = Input.Rotation;
		EyeRotation = Rotation;

		// build movement from the input values
		var movement = new Vector3( Input.Forward, Input.Left, 0 ).Normal;

		// rotate it to the direction we're facing
		Velocity = Rotation * movement;

		// apply some speed to it
		Velocity *= Input.Down( InputButton.Run ) ? 1000 : 200;

		// apply it to our position using MoveHelper, which handles collision
		// detection and sliding across surfaces for us
		MoveHelper helper = new MoveHelper( Position, Velocity );
		helper.Trace = helper.Trace.Size( 16 );
		if ( helper.TryMove( Time.Delta ) > 0 )
		{
			Position = helper.Position;
		}

		// If we're running serverside and Attack1 was just pressed, spawn a ragdoll
		if ( IsServer && Input.Pressed( InputButton.PrimaryAttack ) )
		{
			var ragdoll = new ModelEntity();
			ragdoll.SetModel( "models/citizen/citizen.vmdl" );
			ragdoll.Position = EyePosition + EyeRotation.Forward * 40;
			ragdoll.Rotation = Rotation.LookAt( Vector3.Random.Normal );
			ragdoll.SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );
			ragdoll.PhysicsGroup.Velocity = EyeRotation.Forward * 1000;
		}
	}

	/// <summary>
	/// Called every frame on the client
	/// </summary>
	public override void FrameSimulate( Client cl )
	{
		base.FrameSimulate( cl );

		// Update rotation every frame, to keep things smooth
		Rotation = Input.Rotation;
		EyeRotation = Rotation;
	}
}
