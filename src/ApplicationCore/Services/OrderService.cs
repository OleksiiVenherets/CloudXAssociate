using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Newtonsoft.Json;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IHttpClientFactory _clientFactory;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        IHttpClientFactory clientFactory)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
        _clientFactory = clientFactory;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.GetBySpecAsync(basketSpec);

        Guard.Against.NullBasket(basketId, basket);
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);

        await _orderRepository.AddAsync(order);


        var orders = await _orderRepository.ListAsync();
        var lastOrder = orders.First(x => x.OrderDate == order.OrderDate);

        double finalPrice = 0;
        foreach (var item in lastOrder.OrderItems)
        {
            finalPrice += item.Units * Decimal.ToDouble(item.UnitPrice);
        }
        var request = new
        {
            ShippingAddress = lastOrder.ShipToAddress,
            Items = lastOrder.OrderItems,
            FinalPrice = finalPrice
        };

        var json = JsonConvert.SerializeObject(request);

        var connectionString = "";
        var topicName = "orderitemsrecerver";

        await using var client = new ServiceBusClient(connectionString);

        await using ServiceBusSender sender = client.CreateSender(topicName);
        try
        {
            var message = new ServiceBusMessage(json);
            await sender.SendMessageAsync(message);
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            await sender.DisposeAsync();
            await client.DisposeAsync();
        }

        var httpClient = _clientFactory.CreateClient();

        var data = new StringContent(json, Encoding.UTF8, "application/json");
        var response = httpClient.PostAsync("https://delivery-order-processor-func.azurewebsites.net/api/DeliveryOrderProcessor", data).Result;
    }
}
