# Network Management
Network management is done through entities.<br/>
In most cases you have several network managers, where each of them is responsible for a certain set of connections.

The main network manager is the entity that is instantiated by the `EntityNetwork`<br/>
class itself when a server is started. This entity will target all available network connections.<br/>
For instance you will use the main network manager for authentication and then add authenticated connections to the set of the actual network manager for your game logic as described below.

<br/>



# Example
The following example uses three entities for managing network state.
+ `AuthManager` - Manages authentication.
+ `GameStateManager` - Manages game state & scene synchronization.
+ `GameManager` - Manages the actual game logic.
Note that all entity prefabs must be located in a `Resources` directory.

### AuthManager
At first use the auth manager as main entity for initializing the entity network:
```cs
EntityNetwork.Init(config, "AuthManager");
```

Also create and attach the following component to the `AuthManager` prefab:
```cs
using UnityEngine;
using Unicorn;
using Unicorn.Util;
using Unicorn.Game;
using Unicorn.IO;

public class NetworkAuthManager : EntityComponent<NetworkAuthManager> {
	// Some field to store the server password:
	public String serverPassword = "";

	// Message id for authentication:
	private const byte AUTHENTICATE = 0;

	// Connection set for authenticated clients:
	private SubSet<Connection> _auth;

	protected override void OnEntityActivate() {
		base.OnEntityActivate();
		Object.DontDestroyOnLoad(gameObject);
		if (EntityNetwork.IsServer) {
			// Create a connection set for authenticated clients:
			_auth = new SubSet<Connection>(EntityNetwork.Connections);

			// Create the actual network manager for game state management:
			var gameStateManager = Entity.Create("GameStateManager");
			gameStateManager.Group = _auth;
		} else {
			// As a client, authenticate:
			Send(Authenticate);
		}
	}

	// For simplicity the code below uses non-encrypted password authentication.
	// You should NOT use this in production.

	private void Authenticate(DataWriter msg) {
		Endpoint(msg, AUTHENTICATE);
		// Serialize some password:
		msg.Write(somePassword);
	}

	[Server(AUTHENTICATE)]
	private void Authenticate(Message msg) {
		// Check if the incoming password is correct:
		if (msg.ReadString() == serverPassword) {
			// Add the sender to the set of authenticated clients:
			_auth.Add(msg.Sender);
		}
	}
}
```

### GameStateManager
Create and attach the following component to the `GameStateManager` prefab:
```cs
using UnityEngine;
using UnityEngine.SceneManagement;
using Unicorn;
using Unicorn.Util;
using Unicorn.Game;
using Unicorn.IO;

[RequireComponent(typeof(NetworkSceneManager))]
public class GameStateManager : EntityComponent<GameStateManager> {
	protected override void OnEntityActivate() {
		base.OnEntityActivate();
		Object.DontDestroyOnLoad(gameObject);
		SceneManagement.sceneLoaded += OnSceneLoaded;
		if (EntityNetwork.IsServer) {
			// Load the game scene when the server is started:
			SceneManagement.LoadSceneAsync("Game");
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
		if (scene.Name == "Game") {
			// Create the game manager when the 'Game' scene is loaded:
			var gameManager = Entity.Create("GameManager");

			// Since a 'hacky' client could load the 'Game' scene itself, you should use an intersection set
			// for the game manager to enforce that any connection it communicates with is also authenticated!
			gameManager.Group = new IntersectionSet<Connection>(Entity.Group, EntityNetwork.Scene);
		}
	}
}
```

### GameManager
Create and attach the following component to the `GameManager` prefab:
```cs
using UnityEngine;
using Unicorn;
using Unicorn.Util;
using Unicorn.Game;
using Unicorn.IO;

public class GameManager : EntityComponent<GameManager> {
	// TODO: Actual game logic here...
	// TODO: Spawn players...
}
```

## Conclusion
When a server is started, the `AuthManager` and the `GameStateManager` entities are created and authenticated connections are passed from the auth manager to the game state manager to make clients load the 'Game' scene. When the 'Game' scene is loaded on the server, the `GameManager` entity is created to manage the actual game logic.
