
using System.Collections.Generic;

namespace OnlineFoodOrderingSystem.Models
{
    public class CustomerDetailsViewModel
    {
        public User Customer { get; set; } = new();

        public List<OrderViewModel> Orders { get; set; } = new();

        public decimal TotalSpent { get; set; }

        public int TotalOrders { get; set; }
    }
}
