﻿using System;
using System.Linq;
using BonitoBlitz.Entities.CoreBoard;
using libblitz;
using Sandbox;

namespace BonitoBlitz.Activities.CoreBoard;

public class MoveControllerActivity : libblitz.Activity
{
	/* These constructors are required! */
	public MoveControllerActivity( ActivityDescription d ) : base( d ) { }
	public MoveControllerActivity() { }

	/// <summary>
	/// This is for data you want to pass to the next activity.
	/// Create an instance of this and provide it to PushActivity / PopActivity
	/// </summary>
	public class Result : libblitz.ActivityResult
	{
		/// <summary>
		/// Moves left
		/// </summary>
		public int Moves { get; set; }
	}

	/// <summary>
	/// This is for data being provided to this activity.
	/// Other activities can inherit from this Expectation to make passing data to this activity easier.
	/// </summary>
	public class Expectation : libblitz.ActivityResult
	{
		/// <summary>
		/// Moves left
		/// </summary>
		public int Moves { get; set; }
	}

	private GameMember _actor;

	private int _moves;

	private void PerformMoves()
	{
		Host.AssertServer();

		var currentTile = _actor.CurrentTile;

		switch ( currentTile )
		{
			case IStaticTile:
				// Use BatchMoveActivity
				Game.Current.PushActivity( CreateDescription().Transform<BatchMoveActivity>(),
					new Result() { Moves = _moves } );
				break;
			case IActivityTile activityTile:
				// Use tile activity
				Game.Current.PushActivity( CreateDescription().Transform( activityTile.ActivityName ),
					new IActivityTile.Result() { Moves = _moves, Tile = activityTile } );
				break;
			default:
				Log.Info( "unknown or invalid tile" );
				break;
		}
	}

	/// <summary>
	/// Called serverside when the activity is ready to start.
	/// </summary>
	/// <param name="result">Result of previous activity or null</param>
	public override void ActivityStart( ActivityResult result )
	{
		base.ActivityStart( result );

		_actor = Actors.First();

		if ( result is not Expectation expectation )
		{
			throw new InvalidOperationException( "Unknown ActivityResult passed to MoveControllerActivity" );
		}

		_moves = expectation.Moves;

		Log.Info( $"MoveControllerActivity started with Moves {_moves}" );

		foreach ( var member in Members )
		{
			// Make everyone watch this player
			if ( DynamicCamera.CheckExisting( member.Pawn, _actor.Pawn ) )
			{
				continue;
			}

			member.Pawn?.Components.RemoveAny<CameraMode>();
			member.Pawn?.Components.Add(
				new DynamicCamera( _actor.Pawn ) { PrePositionOffset = (Vector3.Up * 150) + (Vector3.Left * 50) } );
		}

		if ( expectation.Moves <= 0 )
		{
			if ( _actor.CurrentTile is IActionTile at )
			{
				at.OnStand( _actor );
			}

			Log.Info( "MoveControllerActivity complete" );
		}
		else
		{
			PerformMoves();
		}
	}
}
