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
/// Server-side client with index / history number
/// </summary>
public struct RegisteredClient
{
	public RegisteredClient( Session session, uint index, Sandbox.Client? activeClient = null )
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
	private uint? lastAcknowledgedEvent = null;
	public uint? LastAcknowledgedEvent => lastAcknowledgedEvent;

	private bool waitingForResponse = false;
	public bool WaitingForResponse => waitingForResponse;

	public uint HandleEventAcknowledged( SessionIncomingMessage message )
	{
		if ( message.Client != ActiveClient )
			return 2;

		uint eventIndex = message.Event.Var1;
		uint statusCode = message.Event.Var2;

		if ( statusCode != 0 )
			Log.Warning( $"Received non-zero status code from client for event {eventIndex}, code {statusCode}" );

		for ( int i = Queue.Count - 1; i >= 0; i-- )
		{
			if ( Queue[i].Index == eventIndex )
			{
				lastAcknowledgedEvent = eventIndex;
				waitingForResponse = false;
				Queue.RemoveAt( i );
				SendNextInQueue();
				return 0;
			}
		}
		Log.Error( $"Event {eventIndex} acknowledged by client not found in queue!" );
		return 1;
	}

	public void SendNextInQueue()
	{
		if ( waitingForResponse )
			return;

		if ( Queue.Count == 0 )
			return;

		var @event = Queue.Last();
		if ( @event.Index == lastAcknowledgedEvent )
		{
			Log.Error( $"Attempted to send already sent event {@event.Index}" );
			return;
		}

		waitingForResponse = true;
		CoreNetworking.SendToClient( ActiveClient, @event );
	}
}

public partial class Session
{
	private readonly List<RegisteredGameEvent> eventRegistry = new();
	private readonly List<RegisteredClient> clientRegistry = new();
	protected uint GetNextEventIndex() => (uint)eventRegistry.Count;
	protected uint GetNextClientIndex() => (uint)clientRegistry.Count;

	protected RegisteredGameEvent RegisterEvent( GameEvent @event )
	{
		var registeredEvent = new RegisteredGameEvent(
			GetNextEventIndex(),
			@event
		);

		eventRegistry.Add( registeredEvent );
		return registeredEvent;
	}

	protected RegisteredClient RegisterPlayer( Sandbox.Client client )
	{
		var registeredClient = new RegisteredClient(
			this,
			GetNextClientIndex(),
			client
		);

		clientRegistry.Add( registeredClient );
		return registeredClient;
	}

	/// <summary>
	/// Get registered player from client (or register it if it doesn't exist)
	/// </summary>
	/// <param name="client">Client</param>
	/// <returns>RegisteredPlayer</returns>
	public RegisteredClient GetRegisteredPlayer( Sandbox.Client client )
	{
		RegisteredClient? player = null;
		for ( int i = clientRegistry.Count - 1; i >= 0; i-- )
		{
			RegisteredClient v = clientRegistry[i];
			if ( v.ActiveClient == client )
			{
				player = v;
				break;
			}
		}

		if ( player != null )
			return player.Value;

		player = RegisterPlayer( client );
		clientRegistry.Add( player.Value );
		return player.Value;
	}

	public void SendToPlayer( RegisteredClient client, RegisteredGameEvent @event )
	{
		if ( !Sandbox.Host.IsServer )
			return;

		client.Queue.Add( @event );
		client.SendNextInQueue();
	}
	public void SendToPlayer( RegisteredClient client, GameEvent @event ) => SendToPlayer( client, RegisterEvent( @event ) );
	public void SendToAllPlayers( RegisteredGameEvent @event )
	{
		foreach ( var player in clientRegistry )
		{
			SendToPlayer( player, @event );
		}
	}
	public void SendToAllPlayers( GameEvent @event ) => SendToAllPlayers( RegisterEvent( @event ) );
}