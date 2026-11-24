using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolManagementSystem.Data.Entities
{
    public class Alert : IEntity
    {
        public int Id { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsResolved { get; set; }

        // Nullable so non-Employee roles (e.g., Teachers) can create alerts
        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        // Links the alert directly to the Identity User who created it
        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}
