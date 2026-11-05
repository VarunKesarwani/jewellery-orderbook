using JewelleryOrderbook.Api.Domain;

namespace JewelleryOrderbook.Api.Infrastructure;

public interface ICustomerRepository
{
    IReadOnlyList<Customer> List(string? search);
    Customer? Get(Guid id);
    Customer Add(Customer customer);
}

public sealed class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly List<Customer> _customers = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<Customer> List(string? search)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return _customers.ToList();
            }

            return _customers
                .Where(c => c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                            || c.Phone.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public Customer? Get(Guid id)
    {
        lock (_gate)
        {
            return _customers.FirstOrDefault(c => c.Id == id);
        }
    }

    public Customer Add(Customer customer)
    {
        lock (_gate)
        {
            _customers.Add(customer);
            return customer;
        }
    }
}
