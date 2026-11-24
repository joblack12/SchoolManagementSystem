using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace SchoolManagementSystem.Data.Entities
{
    public class FeeInvoice : IEntity
    {
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        [Required, MaxLength(100)]
        public string Title { get; set; } = string.Empty; // e.g., "Term 1 Tuition & Lab Fees"

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<FeePayment> Payments { get; set; } = new List<FeePayment>();

        [NotMapped]
        public decimal AmountPaid => Payments.Sum(p => p.AmountPaid);

        [NotMapped]
        public decimal Balance => TotalAmount - AmountPaid;

        [NotMapped]
        public string Status => Balance <= 0 ? "Paid" : (DateTime.Today > DueDate ? "Overdue" : "Pending");
    }
}
