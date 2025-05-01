Summary Workflow:

Server calls SpawnShip.

SpawnShip loads the Addressable prefab.

SpawnShip calls Runner.Spawn, passing the InitializeShipBeforeSpawn delegate.

Fusion instantiates the prefab on the server.

InitializeShipBeforeSpawn runs (server-only):

Creates the ECS entity (entityManager.CreateEntity).

Sets initial ECS component data.

Gets the ShipNetworkBridge component.

Calls bridge.SetLinkedEntity(entity, entityManager) to establish the link.

Fusion finishes spawning, making the NetworkObject active. Spawned() is called on server and clients.

Server's FixedUpdateNetwork in ShipNetworkBridge: Reads ECS data from _linkedEntity, writes to [Networked] properties.

Fusion syncs [Networked] properties to clients.

Client's Render in ShipNetworkBridge: Reads interpolated [Networked] properties for visual updates.

When the NetworkObject is despawned, Despawned() runs. The server destroys the linked _linkedEntity.

This script-centric approach gives you fine-grained control but requires careful management of the link between the NetworkObject and its corresponding ECS Entity, especially during initialization and destruction.