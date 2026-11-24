
namespace OnlineFoodOrderingSystem.Models
{
    public class CartItem
    {
        public int FoodId { get; set; }

        public string FoodName { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }

        public decimal SubTotal => UnitPrice * Quantity;
    }
}
