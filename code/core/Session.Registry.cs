/*
 * part of the gm0 (w.i.p name) gamemode
 * - lotuspar, 2022 (github.com/lotuspar)
 */
#nullable enable // ?
namespace gm0;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Server-side GameEvent with index / history number
/// </summary>
public struct RegisteredGameEvent
{
	public RegisteredGameEvent( uint index, GameEvent @event )
	{
		Index = index;
		Event = @event;
	}
	public readonly uint Index;
	public readonly GameEvent Event;
}

/// <summary>
/// Server-side player data with index / history number
/// </summary>
public struct RegisteredPlayer
{
	public RegisteredPlayer( Session session, uint index, Sandbox.Client? activeClient = null )
	{
		Index = index;
		ActiveClient = activeClient;

		// Set up acknowledgement handler for this player
		session.AddForeverHandler( new SessionEventHandler<SessionIncomingMessage>(
			GameEventAction.INVALID, "_Session.Registry_ACKFromClient_" + Index,
			HandleEventAcknowledged
		) );
	}
	public readonly uint Index;
	public readonly Sandbox.Client? ActiveClient;
	public readonly List<RegisteredGameEvent> Queue = new();
	private uint? _LastAcknowledgedEvent = null;
	public uint? LastAcknowledgedEvent => _LastAcknowledgedEvent;

	private bool _WaitingForResponse = false;
	public bool WaitingForResponse => _WaitingForResponse;

	public uint HandleEventAcknowledged( SessionIncomingMessage message )
	{
		if ( message.Client != ActiveClient )
			return 2;

		for ( int i = Queue.Count - 1; i >= 0; i-- )
		{
			if ( Queue[i].Index == message.RegistryIndex )
			{
				_LastAcknowledgedEvent = message.RegistryIndex;
				_WaitingForResponse = false;
				Queue.RemoveAt( i );
				SendNextInQueue();
				return 0;
			}
		}
		Log.Error( $"Event {message.RegistryIndex} acknowledged by client not found in queue!" );
		return 1;
	}

	public void SendNextInQueue()
	{
		if ( _WaitingForResponse )
			return;

		if ( Queue.Count == 0 )
			return;

		var @event = Queue.Last();
		if ( @event.Index == _LastAcknowledgedEvent )
		{
			Log.Error( $"Attempted to send already sent event {@event.Index}" );
			return;
		}

		_WaitingForResponse = true;
		SessionNetworking.SendToClient( ActiveClient, @event );
	}
}

public partial class Session
{
	private readonly List<RegisteredGameEvent> eventRegistry = new();
	private readonly List<RegisteredPlayer> playerRegistry = new();
	protected uint GetNextEventIndex() => (uint)eventRegistry.Count;
	protected uint GetNextPlayerIndex() => (uint)playerRegistry.Count;

	protected RegisteredGameEvent RegisterEvent( GameEvent @event )
	{
		var registeredEvent = new RegisteredGameEvent(
			GetNextEventIndex(),
			@event
		);

		eventRegistry.Add( registeredEvent );
		return registeredEvent;
	}

	protected RegisteredPlayer RegisterPlayer( Sandbox.Client client )
	{
		var registeredClient = new RegisteredPlayer(
			this,
			GetNextPlayerIndex(),
			client
		);

		playerRegistry.Add( registeredClient );
		return registeredClient;
	}

	/// <summary>
	/// Get registered player from client (or register it if it doesn't exist)
	/// </summary>
	/// <param name="client">Client</param>
	/// <returns>RegisteredPlayer</returns>
	public RegisteredPlayer GetRegisteredPlayer( Sandbox.Client client )
	{
		RegisteredPlayer? player = null;
		for ( int i = playerRegistry.Count - 1; i >= 0; i-- )
		{
			RegisteredPlayer v = playerRegistry[i];
			if ( v.ActiveClient == client )
			{
				player = v;
				break;
			}
		}

		if ( player != null )
			return player.Value;

		player = RegisterPlayer( client );
		playerRegistry.Add( player.Value );
		return player.Value;
	}

	public void SendToPlayer( RegisteredPlayer client, RegisteredGameEvent @event )
	{
		if ( !Sandbox.Host.IsServer )
			return;

		client.Queue.Add( @event );
		client.SendNextInQueue();
	}
	public void SendToPlayer( RegisteredPlayer client, GameEvent @event ) => SendToPlayer( client, RegisterEvent( @event ) );
	public void SendToAllPlayers( RegisteredGameEvent @event )
	{
		foreach ( var player in playerRegistry )
		{
			SendToPlayer( player, @event );
		}
	}
	public void SendToAllPlayers( GameEvent @event ) => SendToAllPlayers( RegisterEvent( @event ) );
}