using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Play.Common;
using Play.Inventory.Contracts;
using Play.Inventory.Service.Entities;
using Play.Inventory.Service.Exceptions;

namespace Play.Inventory.Service.Consumers;

public class SubtractItemsConsumer : IConsumer<SubtractItems>
{
    private readonly IRepository<InventoryItem> _itemsRepository;
    private readonly IRepository<CatalogItem> _catalogItemsRepository;
    private readonly ILogger<SubtractItemsConsumer> _logger;


    public SubtractItemsConsumer(IRepository<InventoryItem> itemsRepository, IRepository<CatalogItem> catalogItemsRepository, ILogger<SubtractItemsConsumer> logger)
    {
        _itemsRepository = itemsRepository;
        _catalogItemsRepository = catalogItemsRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SubtractItems> context)
    {
        var message = context.Message;

        _logger.LogInformation("Received subtract request of {Quantity} item {ItemId} from user {UserId} with CorrelationId {CorrelationId}"
        , message.Quantity
        , message.CatalogItemId
        , message.UserId
        , message.CorrelationId
        );

        var item = await _catalogItemsRepository.GetAsync(message.CatalogItemId);

        if (item is null)
        {
            throw new UnknownItemException(message.CatalogItemId);
        }

        var inventoryItem = await _itemsRepository.GetAsync(
            item => item.UserId == message.UserId && item.CatalogItemId == message.CatalogItemId);

        if (inventoryItem is not null)
        {
            if (inventoryItem.MessageIds.Contains(context.MessageId.Value))
            {
                await context.Publish(new InventoryItemsSubtracted(message.CorrelationId));
                return;
            }

            inventoryItem.Quantity -= message.Quantity;
            inventoryItem.MessageIds.Add(context.MessageId.Value);
            await _itemsRepository.UpdateAsync(inventoryItem);
            await context.Publish(new InventoryItemUpdated(inventoryItem.UserId, inventoryItem.CatalogItemId, inventoryItem.Quantity));
        }

        await context.Publish(new InventoryItemsSubtracted(message.CorrelationId));
    }
}