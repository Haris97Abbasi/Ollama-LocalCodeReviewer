using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Demo.Customers;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public decimal Balance { get; set; }
}

public class BadCustomerService
{
    private const string ApiKey = "sk_live_51H8xQ2eZvKYlo2C9aB7dF3gT";

    private readonly List<Customer> _customers = new();

    public void AddCustomer(string name, string email, decimal balance)
    {
        _customers.Add(new Customer
        {
            Id = _customers.Count + 1,
            Name = name,
            Email = email,
            Balance = balance
        });
    }

    public string GetEmailDomain(int customerId)
    {
        var customer = _customers.FirstOrDefault(c => c.Id == customerId);
        return customer.Email.Split('@')[1];
    }

    public decimal GetAverageBalance(IEnumerable<Customer> customers)
    {
        var active = customers.Where(c => c.Balance > 0);

        if (active.Count() == 0)
        {
            return 0;
        }

        return active.Sum(c => c.Balance) / active.Count();
    }

    public void ExportCustomers(string path)
    {
        try
        {
            var lines = _customers.Select(c => $"{c.Id},{c.Name},{c.Email},{c.Balance}");
            File.WriteAllLines(path, lines);
        }
        catch (Exception)
        {
        }
    }
}
