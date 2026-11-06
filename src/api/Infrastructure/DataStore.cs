using JewelleryOrderbook.Api.Domain;

namespace JewelleryOrderbook.Api.Infrastructure;

/// <summary>
/// One store behind every endpoint. In memory for now; the SQLite implementation
/// will sit behind this same interface so the endpoints do not change.
/// </summary>
public interface IDataStore
{
    IReadOnlyList<Customer> Customers { get; }
    IReadOnlyList<Order> Orders { get; }
    IReadOnlyList<Delivery> Deliveries { get; }
    IReadOnlyList<Payment> Payments { get; }

    Customer? GetCustomer(Guid id);
    Customer AddCustomer(Customer customer);
    bool UpdateCustomer(Customer customer);

    Order? GetOrder(Guid id);
    Order AddOrder(Order order);
    bool UpdateOrder(Order order);
    long NextOrderNumber();

    Delivery AddDelivery(Delivery delivery);
    long NextDeliveryNumber();
    int DeliveredPieces(Guid orderId, Guid? orderLineId = null);

    Payment AddPayment(Payment payment);
    long NextPaymentNumber();
    long PaidPaise(Guid orderId);
}

public sealed class InMemoryDataStore : IDataStore
{
    private readonly List<Customer> _customers = [];
    private readonly List<Order> _orders = [];
    private readonly List<Delivery> _deliveries = [];
    private readonly List<Payment> _payments = [];
    private readonly Lock _gate = new();

    private long _orderSequence = 1000;
    private long _deliverySequence = 100;
    private long _paymentSequence = 5000;

    public IReadOnlyList<Customer> Customers { get { lock (_gate) { return _customers.ToList(); } } }
    public IReadOnlyList<Order> Orders { get { lock (_gate) { return _orders.ToList(); } } }
    public IReadOnlyList<Delivery> Deliveries { get { lock (_gate) { return _deliveries.ToList(); } } }
    public IReadOnlyList<Payment> Payments { get { lock (_gate) { return _payments.ToList(); } } }

    public Customer? GetCustomer(Guid id) { lock (_gate) { return _customers.FirstOrDefault(c => c.Id == id); } }

    public Customer AddCustomer(Customer customer) { lock (_gate) { _customers.Add(customer); return customer; } }

    public bool UpdateCustomer(Customer customer)
    {
        lock (_gate)
        {
            var i = _customers.FindIndex(c => c.Id == customer.Id);
            if (i < 0) return false;
            _customers[i] = customer;
            return true;
        }
    }

    public Order? GetOrder(Guid id) { lock (_gate) { return _orders.FirstOrDefault(o => o.Id == id); } }

    public Order AddOrder(Order order) { lock (_gate) { _orders.Add(order); return order; } }

    public bool UpdateOrder(Order order)
    {
        lock (_gate)
        {
            var i = _orders.FindIndex(o => o.Id == order.Id);
            if (i < 0) return false;
            _orders[i] = order;
            return true;
        }
    }

    public long NextOrderNumber() { lock (_gate) { return ++_orderSequence; } }

    public Delivery AddDelivery(Delivery delivery) { lock (_gate) { _deliveries.Add(delivery); return delivery; } }

    public long NextDeliveryNumber() { lock (_gate) { return ++_deliverySequence; } }

    public int DeliveredPieces(Guid orderId, Guid? orderLineId = null)
    {
        lock (_gate)
        {
            return _deliveries
                .Where(d => d.OrderId == orderId)
                .SelectMany(d => d.Lines)
                .Where(l => orderLineId is null || l.OrderLineId == orderLineId)
                .Sum(l => l.Quantity);
        }
    }

    public Payment AddPayment(Payment payment) { lock (_gate) { _payments.Add(payment); return payment; } }

    public long NextPaymentNumber() { lock (_gate) { return ++_paymentSequence; } }

    /// <summary>Net of refunds, which are stored as negative-amount payment rows.</summary>
    public long PaidPaise(Guid orderId)
    {
        lock (_gate)
        {
            return _payments
                .Where(p => p.OrderId == orderId)
                .Sum(p => p.IsRefund ? -p.AmountPaise : p.AmountPaise);
        }
    }
}
