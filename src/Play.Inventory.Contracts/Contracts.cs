using System;

namespace Play.Inventory.Contracts;

/*
* The idea of CorrelationId is that this is going to be what the state machine is going to use to correlate
* different messages to belong to one specific instance of the state machine. Without the correlationId
* the state machine will not know how to map the messages to different instances of the state machine
* that could be happening at any given time. So all the messages that belong to one specific transaction
* should have the same correlationId.
*/

public record GrantItems(Guid UserId, Guid CatalogItemId, int Quantity, Guid CorrelationId);

public record InventoryItemsGranted(Guid CorrelationId);

public record SubtractItems(Guid UserId, Guid CatalogItemId, int Quantity, Guid CorrelationId);

public record InventoryItemsSubtracted(Guid CorrelationId);

public record InventoryItemUpdated(Guid UserId, Guid CatalogItemId, int NewTotalQuantity);