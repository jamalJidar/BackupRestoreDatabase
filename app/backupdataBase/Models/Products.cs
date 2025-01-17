using System.ComponentModel.DataAnnotations;

namespace backupdataBase.Models
{
    public class Products
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public  DateTime DateOfCreated { get; set; }

    }
}
