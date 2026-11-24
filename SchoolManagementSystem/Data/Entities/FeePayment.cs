using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace SchoolManagementSystem.Data.Entities
{
    public class FeePayment : IEntity
    {
        public int Id { get; set; }

        [Required]
        public int FeeInvoiceId { get; set; }
        public FeeInvoice FeeInvoice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "M-Pesa"; // M-Pesa, Bank Transfer, Cash

        [MaxLength(100)]
        public string? TransactionReference { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    }
}
