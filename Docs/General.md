# Unicorn.Game
The unicorn game api provides a thin layer on top of the Unicorn networking api and can be found in the `Unicorn.Game` namespace.

<br/>



# EntityNetwork
The `EntityNetwork` class provides static router api that manages network state and entites.

## Setup
Setting up the entity network should be done at application startup.
```cs
// Define channels:
// The channel with value 0 is the default channel.
public enum Channel : byte {Main, Update, Impact}

// Somewhere initialize the network api:
[RuntimeInitializeOnLoadMethod]
public static void SetupEntityNetwork() {
	var config = new RouterConfig();
	config.AddChannel(Channel.MAIN, QosType.ReliableSequenced);
	config.AddChannel(Channel.UPDATE, QosType.UnreliableSequenced);
	config.AddChannel(Channel.IMPACT, QosType.AllCostDelivery);

	// "NetworkManager" is the resource path of the entity
	// that is used as the network manager as described below:
	EntityNetwork.Init(config, "NetworkManager");
}
```

## [NetworkManager](NetworkManagement.md)
Create an gameobject prefab in `Resources/NetworkManager` and add an `Entity` component.<br/>
Later on, you will add more entity components for managing the network part of your game.

<br/>



# Entities
Entities are used to keep track of objects across server and clients.<br/>
There are two types of entities:
+ **Dynamic** entities are instantiated at game runtime.
+ **Static** entities are placed in a scene.

## Dynamic Instances
Dynamic entity instances are always created by the server and then spawned for a selected set of connections. The argument passed to the `Create` function is the path of the game object prefab located at `*/Resources/Example`. Note that is is required that the prefab has at least an `Entity` component attached.
```cs
var entity = Entity.Create("Example");
```

#### Entity Group
The entity group defines to which connections an entity is visible.<br/>
Entities are automatically spawned and destroyed on clients depending on the connections in the entity's group set.
```cs
// Make the entity visible to all connections:
entity.Group = EntityNetwork.Connections;

// Make the entity visible to all clients in the same scene:
entity.Group = EntityNetwork.Scene;
```

#### Entity Ownership
Unicorn provides a connection based ownership system.<br/>
Server-side changes to the owner set are sent to clients so that
each client knows if an entity is owned by this client or not.

**Server side**
```cs
// Make a connection 'own' the entity (remove using the 'Remove' method):
entity.OwnerSet.Add(someConnection);

// Use your own set of owners for the entity (reverse by setting 'Owners' back to 'OwnerSet'):
entity.Owners = someConnectionSet;

// Check if a connection owns an entity:
if (entity.Owners.Contains(someConnection)) {
	// ...
}
```
**Client side**
```cs
// Check if this client owns the entity:
if (entity.IsMine) {
	// ...
}
```

## [Entity Components](Components.md)
Entity components are used to implement entity based behaviour.
