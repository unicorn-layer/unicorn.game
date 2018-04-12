# Entity Components
Entity components are used to implement entity based behaviour.
The `Example` class can be attached to any entity game object:
```cs
public class Example : EntityComponent<Example> {
	// Access the `Entity` component using the 'Entity' property.
}
```

## Attaching to entities
Entity components should only be attached to or detached from entity game objects while the entity component has not been activated. You usually never add or remove entity components at runtime.

## Basic events
Basic events are called on entity activation and deactivation:
```cs
protected override void OnEntityActivate() {
	// Called on server and clients when the entity has been activated (instantiated or loaded from the scene).
}

protected override void OnEntityDeactivate() {
	// Called on server and clients when the entity has been deactivated (destroyed).
}
```

## Group events
Group events are called for changes on the entity group:
```cs
protected override void OnEntityClientConnected(Connection conn) {
	// Called on the server when a connection has been added to the entity group.
}

protected override void OnEntityClientDisconnected(Connection conn) {
	// Called on the server when a connection has been removed from the entity group.
}
```

## Ownership events
Ownership events are called for changes on the ownership state of the entity:
```cs
protected override void OnEntityOwnerAdded(Connection conn) {
	// Called on the server when a connection now owns this entity.
}

protected override void OnEntityOwnerRemoved(Connection conn) {
	// Called on the server when a connection no longer owns this entity.
}

protected override void OnEntityOwnerConnected(Connection conn) {
	// Called on the server when a connection is in the entity group and an owner.
}

protected override void OnEntityOwnerDisconnected(Connection conn) {
	// Called on the server when a connection is not in the entity group or not an owner.
}

protected override void OnEntityOwnershipChanged(bool isMine) {
	// Caled on the client when the ownership state of this client changed.
}
```

## Sending entity messages
The entity component provides a relatively simple way to send messages between remote instances.
```cs
// Define fixed message id's for each message:
private const byte EXAMPLE = 0;

// By adding the 'Server' attribute, 'Example' can be called by clients:
[Server(EXAMPLE)]
// By adding the 'Client' attribute, 'Example' can be called by the server:
[Client(EXAMPLE)]
private void Example(Message msg) {
	// Access the sender connection:
	msg.Sender;

	// Deserialize some data:
	var someData = msg.ReadVector3();
}

// Define a method for calling the 'EXAMPLE' message on remote instances:
private void Example(DataWriter msg) {
	// Define which message is targeted:
	Endpoint(msg, EXAMPLE);

	// Serialize some data:
	msg.Write(transform.position);
}

private void SomeMethod() {
	// Call the example method on connections in the entity group or the server connection:
	Send(Example);
	// Same as above, but not the default (MAIN) channel:
	Send(Channel.UPDATE, Example);

	// Call the example method on a set of connections:
	// (someConnections can also be a single connection as it
	//  implements the IEnumerable<Connection> interface for itself).
	Send(someConnections, Example);
	// Same as above, but another channel:
	Send(someConnections, Channel.UPDATE, Example);
}
```
