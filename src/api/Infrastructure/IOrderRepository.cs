using JewelleryOrderbook.Api.Domain;

namespace JewelleryOrderbook.Api.Infrastructure;

public interface IOrderRepository
{
    IReadOnlyList<Order> List(OrderStatus? status);
    Order? Get(Guid id);
    Order Add(Order order);
    bool TryUpdate(Order order);
}

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly List<Order> _orders = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<Order> List(OrderStatus? status)
    {
        lock (_gate)
        {
            return status is null
                ? _orders.ToList()
                : _orders.Where(o => o.Status == status).ToList();
        }
    }

    public Order? Get(Guid id)
    {
        lock (_gate)
        {
            return _orders.FirstOrDefault(o => o.Id == id);
        }
    }

    public Order Add(Order order)
    {
        lock (_gate)
        {
            _orders.Add(order);
            return order;
        }
    }

    public bool TryUpdate(Order order)
    {
        lock (_gate)
        {
            var index = _orders.FindIndex(o => o.Id == order.Id);
            if (index < 0)
            {
                return false;
            }

            _orders[index] = order;
            return true;
        }
    }
}
