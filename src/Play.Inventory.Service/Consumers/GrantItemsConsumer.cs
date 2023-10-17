using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Play.Common;
using Play.Inventory.Contracts;
using Play.Inventory.Service.Entities;
using Play.Inventory.Service.Exceptions;

namespace Play.Inventory.Service.Consumers;

public class GrantItemsConsumer : IConsumer<GrantItems>
{
    private readonly IRepository<InventoryItem> _itemsRepository;
    private readonly IRepository<CatalogItem> _catalogItemsRepository;
    private readonly ILogger<GrantItemsConsumer> _logger;


    public GrantItemsConsumer(IRepository<InventoryItem> itemsRepository, IRepository<CatalogItem> catalogItemsRepository, ILogger<GrantItemsConsumer> logger)
    {
        _itemsRepository = itemsRepository;
        _catalogItemsRepository = catalogItemsRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GrantItems> context)
    {
        var message = context.Message;
        _logger.LogInformation("Received grant request of {Quantity} item {ItemId} from user {UserId} with CorrelationId {CorrelationId}"
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

        if (inventoryItem is null)
        {
            inventoryItem = new InventoryItem
            {
                CatalogItemId = message.CatalogItemId,
                UserId = message.UserId,
                Quantity = message.Quantity,
                AcquiredDate = DateTimeOffset.UtcNow
            };

            //we can use CorrelationId in place of context.MessageId
            //https://learn.dotnetacademy.io/courses/take/net-microservices-core/lessons/38614550-implementing-idempotent-consumers/discussions/5813323
            inventoryItem.MessageIds.Add(context.MessageId.Value);

            await _itemsRepository.CreateAsync(inventoryItem);
        }
        else
        {
            if (inventoryItem.MessageIds.Contains(context.MessageId.Value))
            {
                await context.Publish(new InventoryItemsGranted(message.CorrelationId));
                return;
            }

            inventoryItem.Quantity += message.Quantity;
            inventoryItem.MessageIds.Add(context.MessageId.Value);
            await _itemsRepository.UpdateAsync(inventoryItem);
        }

        var itemsGrantedTask = context.Publish(new InventoryItemsGranted(message.CorrelationId));
        var inventoryUpdatedTask = context.Publish(new InventoryItemUpdated(inventoryItem.UserId, inventoryItem.CatalogItemId, inventoryItem.Quantity));

        await Task.WhenAll(itemsGrantedTask, inventoryUpdatedTask);
    }
}